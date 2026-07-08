using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace CCCBL
{
    public class ModEntry : Mod
    {
        // ─── Constants ──────────────────────────────────────────────────────────

        /// <summary>Sentinel value meaning "use vanilla / no custom pack".</summary>
        private const string DefaultVariantKey = "Default";

        // No bundle-sprite constants needed — each pack's icons are loaded at a
        // custom content path (Mods/{packId}/BundleIcons) using the pack's own sprite sheet.

        // ─── State ──────────────────────────────────────────────────────────────

        private ModConfig Config = null!;

        /// <summary>All loaded content packs: UniqueID → (data, pack reference).</summary>
        private readonly Dictionary<string, (ContentPackData Data, IContentPack Pack)> LoadedPacks = new();

        /// <summary>Built-in Completionist bundle data (replaces CCCC when Default + Completionist).</summary>
        private Dictionary<string, string>? CompletionistBundles;

        /// <summary>Allowed values for the GMCM dropdown — "Default" plus every loaded pack UniqueID.</summary>
        private string[] GmcmAllowedValues = Array.Empty<string>();



        /// <summary>Snapshot of the full BundleData before we modify it each day, used for reversion.</summary>
        private Dictionary<string, string> OriginalBundleData = new();

        private bool BundlesApplied = false;

        // ─── Entry Point ─────────────────────────────────────────────────────────

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            // 1. Load built-in Completionist data (CCCC equivalent)
            this.LoadCompletionistData();

            // 2. Load all content packs declared for this mod
            foreach (IContentPack pack in helper.ContentPacks.GetOwned())
                this.TryLoadContentPack(pack);

            // 3. Build the GMCM dropdown list (Default first, then packs in load order)
            var values = new List<string> { DefaultVariantKey };
            values.AddRange(this.LoadedPacks.Keys);
            this.GmcmAllowedValues = values.ToArray();

            // 4. Validate config — reset to Default if the saved variant no longer exists
            if (this.Config.BundleVariant != DefaultVariantKey &&
                !this.LoadedPacks.ContainsKey(this.Config.BundleVariant))
            {
                this.Monitor.Log(
                    $"Configured bundle variant '{this.Config.BundleVariant}' is not loaded. Resetting to Default.",
                    LogLevel.Warn);
                this.Config.BundleVariant = DefaultVariantKey;
                helper.WriteConfig(this.Config);
            }

            // 5. If the active pack forces Completionist Mode, make sure config reflects that
            if (this.ActivePackRequiresCompletionist() && !this.Config.CompletionistMode)
            {
                this.Config.CompletionistMode = true;
                helper.WriteConfig(this.Config);
            }

            this.Monitor.Log(
                $"CCCBL ready — Variant: '{this.Config.BundleVariant}', CompletionistMode: {this.Config.CompletionistMode}",
                LogLevel.Info);

            // 6. Register events
            helper.Events.GameLoop.GameLaunched  += this.OnGameLaunched;
            helper.Events.GameLoop.DayStarted    += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding     += this.OnDayEnding;
            helper.Events.GameLoop.Saving        += this.OnSaving;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
        }

        // ─── Built-in Data ───────────────────────────────────────────────────────

        private void LoadCompletionistData()
        {
            try
            {
                this.CompletionistBundles = this.Helper.ModContent
                    .Load<Dictionary<string, string>>("assets/CompletionistBundles.json");
                this.Monitor.Log("Loaded built-in Completionist bundle data.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                this.Monitor.Log(
                    $"Could not load assets/CompletionistBundles.json — Completionist Mode (Default variant) will be unavailable. ({ex.Message})",
                    LogLevel.Warn);
            }
        }

        // ─── Content Pack Loading ─────────────────────────────────────────────────

        private void TryLoadContentPack(IContentPack pack)
        {
            try
            {
                var data = pack.ReadJsonFile<ContentPackData>("content.json");
                if (data is null)
                {
                    this.Monitor.Log($"'{pack.Manifest.Name}' — content.json is missing or invalid. Skipping.", LogLevel.Warn);
                    return;
                }

                if (data.Bundles is null || data.Bundles.Count == 0)
                {
                    this.Monitor.Log($"'{pack.Manifest.Name}' — content.json has no bundle entries. Skipping.", LogLevel.Warn);
                    return;
                }

                this.LoadedPacks[pack.Manifest.UniqueID] = (data, pack);

                this.Monitor.Log(
                    $"Loaded bundle pack '{pack.Manifest.Name}' ({pack.Manifest.UniqueID}) — " +
                    $"{data.Bundles.Count} bundle(s), RequireCompletionistMode: {data.RequireCompletionistMode}",
                    LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed to load content pack '{pack.Manifest.Name}': {ex.Message}", LogLevel.Error);
            }
        }



        // ─── Logic Helpers ────────────────────────────────────────────────────────

        /// <summary>Whether the currently selected pack forces Completionist Mode on.</summary>
        private bool ActivePackRequiresCompletionist()
        {
            if (this.Config.BundleVariant == DefaultVariantKey) return false;
            return this.LoadedPacks.TryGetValue(this.Config.BundleVariant, out var entry)
                   && entry.Data.RequireCompletionistMode;
        }

        /// <summary>Whether Completionist Mode is effectively active (user toggle OR pack forces it).</summary>
        private bool IsCompletionistActive()
            => this.Config.CompletionistMode || this.ActivePackRequiresCompletionist();

        /// <summary>
        /// Returns an ordered list of bundle data layers to apply, from lowest to highest priority.
        /// An empty list means nothing should change (vanilla mode).
        ///
        /// Order:
        ///   1. Completionist base — creates all extra bundle slots when Completionist is active.
        ///   2. Custom pack data   — overrides specific bundles on top of those slots.
        ///
        /// Applying in layers is critical for packs with RequireCompletionistMode = true:
        /// they use bundle-name keys like "Garden" or "Rare Crops" that only exist after
        /// the Completionist base data has first created those slots.
        /// </summary>
        private List<Dictionary<string, string>> GetActiveBundleDataLayers()
        {
            var layers = new List<Dictionary<string, string>>();

            // Layer 1: Completionist base (creates extra slots when active)
            if (this.IsCompletionistActive() && this.CompletionistBundles is not null)
                layers.Add(this.CompletionistBundles);

            // Layer 2: Custom pack overrides (applied on top, wins on key conflicts)
            if (this.Config.BundleVariant != DefaultVariantKey &&
                this.LoadedPacks.TryGetValue(this.Config.BundleVariant, out var entry) &&
                entry.Data.Bundles is not null &&
                entry.Data.Bundles.Count > 0)
                layers.Add(entry.Data.Bundles);

            return layers;
        }

        /// <summary>
        /// Applies bundle data entries from <paramref name="source"/> onto <paramref name="target"/>.
        /// Supports both Room/ID keys (direct set) and bundle-name keys (matched by name field).
        /// New Room/ID entries not present in target are added (required for Completionist extra slots).
        /// </summary>
        private void ApplyBundleDataTo(IDictionary<string, string> target, Dictionary<string, string> source)
        {
            foreach (var (key, value) in source)
            {
                // Sanitize sprite reference (strips formats the game can't parse)
                string sanitized  = this.SanitizeBundleDataSprite(value);
                // If no sprite is specified, fall back to CCCBL's bundleicon_default
                string finalValue = this.InjectDefaultSpriteIfNeeded(sanitized);

                if (key.Contains('/'))
                {
                    // Room/ID key — set directly (adds new slots or replaces existing ones)
                    target[key] = finalValue;
                }
                else
                {
                    // Bundle-name key — find the matching slot by the name field (field 0)
                    bool found = false;
                    foreach (string existingKey in target.Keys.ToList())
                    {
                        string existingName = target[existingKey].Split('/')[0];
                        if (string.Equals(existingName, key, StringComparison.OrdinalIgnoreCase))
                        {
                            target[existingKey] = finalValue;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        this.Monitor.Log($"Bundle '{key}' not found in game data — entry skipped.", LogLevel.Trace);
                }
            }
        }

        /// <summary>
        /// Ensures the sprite reference in field 5 of a bundle data string is in a format
        /// the game can actually parse. Strips references using unrecognized formats
        /// (e.g. "Mods\SomeMod\bundlesprite:0" from packs designed for other loaders)
        /// so the game doesn't crash trying to load them as content paths.
        /// Valid formats: empty, a plain integer, or "LooseSprites\BundleSprites:N".
        /// </summary>
        private string SanitizeBundleDataSprite(string bundleData)
        {
            string[] parts = bundleData.Split('/');
            if (parts.Length < 6) return bundleData;

            string sprite = parts[5].Trim();
            bool isValid = string.IsNullOrEmpty(sprite)
                        || int.TryParse(sprite, out _)
                        || sprite.StartsWith("LooseSprites\\", StringComparison.OrdinalIgnoreCase)
                        || sprite.StartsWith("Mods\\", StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                this.Monitor.Log(
                    $"Stripping unrecognized sprite reference '{sprite}' — " +
                    "this pack may have been designed for a different bundle loader.",
                    LogLevel.Debug);
                parts[5] = "";
                return string.Join("/", parts);
            }

            return bundleData;
        }

        /// <summary>
        /// If the bundle data string has no sprite reference in field 5, injects a reference
        /// to CCCBL's bundleicon_default sprite so all custom bundles have a visible icon
        /// even when the pack author hasn't specified one.
        /// Bundles that already have a valid sprite reference are left unchanged.
        /// </summary>
        private string InjectDefaultSpriteIfNeeded(string bundleData)
        {
            string[] parts       = bundleData.Split('/');
            bool     hasSprite   = parts.Length >= 6 && !string.IsNullOrWhiteSpace(parts[5]);
            if (hasSprite) return bundleData;

            while (parts.Length < 6)
                parts = parts.Concat(new[] { "" }).ToArray();

            parts[5] = $"Mods\\{this.ModManifest.UniqueID}\\bundleicon_default:0";
            return string.Join("/", parts);
        }

        // ─── GMCM ─────────────────────────────────────────────────────────────────

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null) return;

            gmcm.Register(
                mod:             this.ModManifest,
                reset:           () =>
                {
                    this.Config.BundleVariant    = DefaultVariantKey;
                    this.Config.CompletionistMode = false;
                    this.OnConfigChanged();
                },
                save:            () => this.Helper.WriteConfig(this.Config),
                titleScreenOnly: false
            );

            gmcm.AddSectionTitle(
                mod:  this.ModManifest,
                text: () => this.Helper.Translation.Get("config.section.title")
            );

            // ── Bundle pack dropdown ──────────────────────────────────────────────
            gmcm.AddTextOption(
                mod:           this.ModManifest,
                getValue:      () => this.Config.BundleVariant,
                setValue:      value =>
                {
                    this.Config.BundleVariant = value;
                    if (this.ActivePackRequiresCompletionist())
                        this.Config.CompletionistMode = true;
                    this.OnConfigChanged();
                },
                name:          () => this.Helper.Translation.Get("config.bundle-variant.name"),
                tooltip:       () => this.Helper.Translation.Get("config.bundle-variant.tooltip"),
                allowedValues: this.GmcmAllowedValues,
                formatAllowedValue: id =>
                {
                    if (id == DefaultVariantKey)
                        return this.Helper.Translation.Get("config.bundle-variant.default");
                    if (this.LoadedPacks.TryGetValue(id, out var entry))
                        return entry.Pack.Manifest.Name;
                    return id;
                }
            );

            // ── Completionist Mode toggle ─────────────────────────────────────────
            // GMCM does not support disabled/locked controls, so we keep a single
            // static toggle. When the active pack requires Completionist Mode:
            //   - getValue always returns true so it appears checked.
            //   - setValue ignores changes so it can't be turned off.
            //   - The tooltip explains why.
            // This is evaluated at render time via lambdas, so it reflects whichever
            // pack is currently selected without needing to re-register.
            gmcm.AddBoolOption(
                mod:      this.ModManifest,
                getValue: () => this.IsCompletionistActive(),
                setValue: value =>
                {
                    if (!this.ActivePackRequiresCompletionist())
                    {
                        this.Config.CompletionistMode = value;
                        this.OnConfigChanged();
                    }
                },
                name:    () => this.Helper.Translation.Get("config.completionist.name"),
                tooltip: () => this.ActivePackRequiresCompletionist()
                    ? this.Helper.Translation.Get("config.completionist.required-tooltip")
                    : this.Helper.Translation.Get("config.completionist.tooltip")
            );
        }

        /// <summary>Called whenever a config value changes — invalidates relevant cached assets.</summary>
        private void OnConfigChanged()
        {
            this.BundlesApplied = false;
            this.Helper.GameContent.InvalidateCache("Data/Bundles");
            this.Helper.GameContent.InvalidateCache("Data/RandomBundles");
            this.Helper.GameContent.InvalidateCache("LooseSprites/JunimoNote");
            // Sprite textures are served on demand; no specific cache invalidation needed for them.
        }

        // ─── Asset Editing ────────────────────────────────────────────────────────

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            // ── Data/Bundles ──────────────────────────────────────────────────────
            // Applied at asset load time, which covers new game creation.
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Bundles"))
            {
                var layers = this.GetActiveBundleDataLayers();
                if (layers.Count > 0)
                {
                    e.Edit(asset =>
                    {
                        var data = asset.AsDictionary<string, string>().Data;
                        foreach (var layer in layers)
                            this.ApplyBundleDataTo(data, layer);
                    }, AssetEditPriority.Default);
                }
            }

            // ── LooseSprites/JunimoNote ───────────────────────────────────────────
            // Overlay the extended note sprite when Completionist Mode is active.
            // The active pack may supply its own note_override.png; otherwise we use
            // our built-in note_default.png.
            if (e.NameWithoutLocale.IsEquivalentTo("LooseSprites/JunimoNote") && this.IsCompletionistActive())
            {
                string? packOverride = this.GetPackNoteOverridePath();
                bool    useDefault   = this.Helper.ModContent
                    .GetInternalAssetName("assets/LooseSprites/note_default.png") is not null
                    && this.HasModAsset("assets/LooseSprites/note_default.png");

                if (packOverride is not null || useDefault)
                {
                    e.Edit(asset =>
                    {
                        try
                        {
                            Texture2D overlay;
                            if (packOverride is not null)
                            {
                                var pack = this.LoadedPacks[this.Config.BundleVariant].Pack;
                                overlay = pack.ModContent.Load<Texture2D>(packOverride);
                            }
                            else
                            {
                                overlay = this.Helper.ModContent.Load<Texture2D>("assets/LooseSprites/note_default.png");
                            }
                            // Overlay at the area CCCC uses for its extra bundle slot indicators.
                            // Replace this Rectangle if your note_default.png covers a different area.
                            asset.AsImage().PatchImage(overlay, targetArea: new Rectangle(484, 110, 135, 51));
                        }
                        catch (Exception ex)
                        {
                            this.Monitor.Log($"Failed to apply JunimoNote overlay: {ex.Message}", LogLevel.Warn);
                        }
                    }, AssetEditPriority.Default);
                }
            }

            // ── Mods/{CCCBL}/bundleicon_default ──────────────────────────────────
            // Serves the default bundle icon for any bundle that doesn't specify its own.
            // Pack authors override this by including a sprite reference in their bundle
            // data strings (Mods\{theirPackId}\{spriteFile}:frameIndex).
            if (e.NameWithoutLocale.IsEquivalentTo($"Mods/{this.ModManifest.UniqueID}/bundleicon_default") &&
                this.HasModAsset("assets/LooseSprites/bundleicon_default.png"))
            {
                e.LoadFromModFile<Texture2D>("assets/LooseSprites/bundleicon_default.png", AssetLoadPriority.Low);
            }

            // ── Mods/{activePackId}/{anything} ────────────────────────────────────
            // Pack authors can reference custom textures in their bundle data sprite field:
            //   Mods\{PackUniqueId}\{filename}:frameIndex
            // CCCBL intercepts that request and serves assets/{filename}.png from the pack.
            // This matches Kind's and Vegas's approach exactly. No separate Content Patcher
            // mod is needed — everything lives in one CCCBL content pack.
            if (this.Config.BundleVariant != DefaultVariantKey &&
                this.LoadedPacks.TryGetValue(this.Config.BundleVariant, out var spritePack))
            {
                string modPrefix = $"Mods/{this.Config.BundleVariant}/";
                string assetPath = e.NameWithoutLocale.ToString()!;
                if (assetPath.StartsWith(modPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string assetName = assetPath[modPrefix.Length..];
                    string pngPath   = $"assets/{assetName}.png";

                    if (spritePack.Pack.HasFile(pngPath))
                    {
                        var captured = spritePack.Pack;
                        e.LoadFrom(
                            () => captured.ModContent.Load<Texture2D>(pngPath),
                            AssetLoadPriority.Medium);
                        this.Monitor.Log(
                            $"Serving '{pngPath}' from '{spritePack.Pack.Manifest.Name}' " +
                            $"for asset request '{e.NameWithoutLocale}'.",
                            LogLevel.Debug);
                    }
                }
            }

            // ── Data/RandomBundles ────────────────────────────────────────────────
            // Replaces the Remixed Bundles pool when Completionist Mode is active.
            //
            // This is currently disabled because the exact public C# type for the
            // Data/RandomBundles asset varies by SDV/SMAPI version and isn't easily
            // determined without inspecting the game's assembly at runtime.
            //
            // To enable it, find the correct type by checking which class SMAPI reports
            // in a type-mismatch error, then uncomment and adjust the block below:
            //
            // if (e.NameWithoutLocale.IsEquivalentTo("Data/RandomBundles") && this.IsCompletionistActive())
            // {
            //     if (this.HasModAsset("assets/CompletionistRandomBundles.json"))
            //     {
            //         e.LoadFromModFile<List<THE_CORRECT_TYPE_HERE>>(
            //             "assets/CompletionistRandomBundles.json",
            //             AssetLoadPriority.Medium);
            //     }
            // }
            //
            // Without this, Completionist Mode still applies all bundles correctly.
            // The only missing piece is the Remixed Bundles pool at new-game creation.
        }

        // ─── Runtime Bundle Application (existing saves) ──────────────────────────
        // Data/Bundles only applies when a new game or save is loaded from disk.
        // For existing saves already in memory we also patch BundleData directly,
        // then revert it before saving so the save file stays vanilla-compatible.

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            var layers = this.GetActiveBundleDataLayers();
            if (layers.Count == 0) return;

            var current = Game1.netWorldState.Value.BundleData;

            // Snapshot the full current BundleData for reversion
            this.OriginalBundleData = new Dictionary<string, string>(current);

            // Apply each layer in order on top of a copy
            var modified = new Dictionary<string, string>(current);
            foreach (var layer in layers)
                this.ApplyBundleDataTo(modified, layer);

            Game1.netWorldState.Value.SetBundleData(modified);
            this.BundlesApplied = true;

            this.Monitor.Log(
                $"Applied '{this.Config.BundleVariant}' bundle data to in-memory BundleData.",
                LogLevel.Debug);


        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e) => this.RevertBundleData();
        private void OnSaving   (object? sender, SavingEventArgs e)    => this.RevertBundleData();

        private void RevertBundleData()
        {
            if (!this.BundlesApplied || this.OriginalBundleData.Count == 0) return;
            Game1.netWorldState.Value.SetBundleData(this.OriginalBundleData);
            this.BundlesApplied = false;
            this.Monitor.Log("Reverted BundleData to original for saving.", LogLevel.Debug);
        }

        // ─── Sprite Utilities ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns the relative path to note_override.png inside the active content pack,
        /// or null if the active pack doesn't provide one.
        /// </summary>
        private string? GetPackNoteOverridePath()
        {
            if (this.Config.BundleVariant == DefaultVariantKey) return null;
            if (!this.LoadedPacks.TryGetValue(this.Config.BundleVariant, out var entry)) return null;

            const string path = "assets/LooseSprites/note_override.png";
            return entry.Pack.HasFile(path) ? path : null;
        }



        /// <summary>
        /// Returns true if a file at the given path exists inside this mod's own folder.
        /// Uses HasFile-equivalent logic via IContentPack not available for the host mod,
        /// so we fall back to a direct disk check (safe — we are only checking our own directory).
        /// </summary>
        private bool HasModAsset(string relativePath)
        {
            string fullPath = Path.Combine(
                this.Helper.DirectoryPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(fullPath);
        }
    }
}
