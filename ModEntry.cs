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
        // ─── Constants ───────────────────────────────────────────────────────────

        private const string DefaultVariantKey  = "Default";
        private const string CompletionistKey   = "Completionist";

        // Default JunimoNote overlay area (matches CCCC / Completionist Mode layout)
        private static readonly Rectangle DefaultNoteOverlayArea = new(484, 110, 135, 51);

        // ─── State ───────────────────────────────────────────────────────────────

        private ModConfig Config = null!;

        /// <summary>All loaded content packs: UniqueID → (data, pack).</summary>
        private readonly Dictionary<string, (ContentPackData Data, IContentPack Pack)> LoadedPacks = new();

        /// <summary>Allowed values for the GMCM dropdown.</summary>
        private string[] GmcmAllowedValues = Array.Empty<string>();

        /// <summary>Snapshot of BundleData before our changes, for reversion on save.</summary>
        private Dictionary<string, string> OriginalBundleData = new();

        private bool BundlesApplied = false;

        // ─── Entry ───────────────────────────────────────────────────────────────

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            // Load built-in Completionist data (CCCC equivalent)
            this.LoadCompletionistData();

            // Load all CCCBL content packs
            foreach (IContentPack pack in helper.ContentPacks.GetOwned())
                this.TryLoadContentPack(pack);

            // Build GMCM dropdown list
            var values = new List<string> { DefaultVariantKey };
            values.AddRange(this.LoadedPacks.Keys);
            this.GmcmAllowedValues = values.ToArray();

            // Validate config
            if (this.Config.BundleVariant != DefaultVariantKey &&
                !this.LoadedPacks.ContainsKey(this.Config.BundleVariant))
            {
                this.Monitor.Log($"Bundle variant '{this.Config.BundleVariant}' not found. Resetting to Default.", LogLevel.Warn);
                this.Config.BundleVariant = DefaultVariantKey;
                helper.WriteConfig(this.Config);
            }

            // Auto-enable Completionist if active pack requires it
            if (this.ActivePackRequiresCompletionist() && !this.Config.CompletionistMode)
            {
                this.Config.CompletionistMode = true;
                helper.WriteConfig(this.Config);
            }

            this.Monitor.Log($"CCCBL ready — Variant: '{this.Config.BundleVariant}', CompletionistMode: {this.Config.CompletionistMode}", LogLevel.Info);

            helper.Events.GameLoop.GameLaunched  += this.OnGameLaunched;
            helper.Events.GameLoop.DayStarted    += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding     += this.OnDayEnding;
            helper.Events.GameLoop.Saving        += this.OnSaving;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
        }

        // ─── Built-in Completionist Data ─────────────────────────────────────────

        private void LoadCompletionistData()
        {
            try
            {
                // Register as a virtual pack so it appears in the dropdown
                // and goes through the same layer system as real packs.
                // We handle it specially in GetActiveBundleDataLayers.
                this.Monitor.Log("Loaded built-in Completionist bundle data.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Could not load CompletionistBundles.json: {ex.Message}", LogLevel.Warn);
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
                    this.Monitor.Log($"'{pack.Manifest.Name}' — content.json missing or invalid. Skipping.", LogLevel.Warn);
                    return;
                }
                if (data.Bundles is null || data.Bundles.Count == 0)
                {
                    this.Monitor.Log($"'{pack.Manifest.Name}' — no bundle entries found. Skipping.", LogLevel.Warn);
                    return;
                }

                this.LoadedPacks[pack.Manifest.UniqueID] = (data, pack);
                this.Monitor.Log(
                    $"Loaded pack '{pack.Manifest.Name}' ({pack.Manifest.UniqueID}) — " +
                    $"{data.Bundles.Count} bundle(s), RequireCompletionistMode: {data.RequireCompletionistMode}",
                    LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed to load '{pack.Manifest.Name}': {ex.Message}", LogLevel.Error);
            }
        }

        // ─── Logic Helpers ────────────────────────────────────────────────────────

        private bool ActivePackRequiresCompletionist()
        {
            if (this.Config.BundleVariant == DefaultVariantKey) return false;
            return this.LoadedPacks.TryGetValue(this.Config.BundleVariant, out var e) && e.Data.RequireCompletionistMode;
        }

        private bool IsCompletionistActive()
            => this.Config.CompletionistMode || this.ActivePackRequiresCompletionist();

        /// <summary>
        /// Returns ordered layers to apply, lowest to highest priority.
        /// Layer 1 (optional): Completionist base — adds extra slots.
        /// Layer 2 (optional): Active pack data — overrides specific bundles.
        /// Packs that use their own extended IDs (not Completionist IDs) just use layer 2.
        /// </summary>
        private List<Dictionary<string, string>> GetActiveBundleDataLayers()
        {
            var layers = new List<Dictionary<string, string>>();

            // Layer 1: Completionist base (creates CCCC-style extra slots when active)
            if (this.IsCompletionistActive())
            {
                try
                {
                    var completionist = this.Helper.ModContent.Load<Dictionary<string, string>>("assets/CompletionistBundles.json");
                    if (completionist is not null)
                        layers.Add(completionist);
                }
                catch { /* already logged at startup */ }
            }

            // Layer 2: Custom pack overrides
            if (this.Config.BundleVariant != DefaultVariantKey &&
                this.LoadedPacks.TryGetValue(this.Config.BundleVariant, out var entry) &&
                entry.Data.Bundles is not null &&
                entry.Data.Bundles.Count > 0)
            {
                layers.Add(entry.Data.Bundles);
            }

            return layers;
        }

        /// <summary>
        /// Applies bundle data entries from source onto target.
        /// Supports Room/ID keys (direct set) and bundle-name keys (matched by name field).
        /// All keys are passed through as-is — no ID validation is performed.
        /// </summary>
        private void ApplyBundleDataTo(IDictionary<string, string> target, Dictionary<string, string> source)
        {
            foreach (var (key, value) in source)
            {
                string sanitized  = this.SanitizeBundleDataSprite(value);
                string finalValue = this.InjectDefaultSpriteIfNeeded(sanitized);

                if (key.Contains('/'))
                {
                    target[key] = finalValue;
                }
                else
                {
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
                        this.Monitor.Log($"Bundle '{key}' not found in game data — skipped.", LogLevel.Trace);
                }
            }
        }

        /// <summary>
        /// Strips sprite references the game can't parse (e.g. custom loader paths
        /// from mods designed for different frameworks).
        /// Valid formats: empty, plain integer, LooseSprites\..., Mods\...
        /// </summary>
        private string SanitizeBundleDataSprite(string bundleData)
        {
            string[] parts  = bundleData.Split('/');
            if (parts.Length < 6) return bundleData;

            string sprite   = parts[5].Trim();
            bool   isValid  = string.IsNullOrEmpty(sprite)
                           || int.TryParse(sprite, out _)
                           || sprite.StartsWith("LooseSprites\\", StringComparison.OrdinalIgnoreCase)
                           || sprite.StartsWith("Mods\\",         StringComparison.OrdinalIgnoreCase);

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
        /// If a bundle data string has no sprite in field 5, injects a reference to
        /// CCCBL's bundleicon_default so all custom bundles have a visible icon.
        /// </summary>
        private string InjectDefaultSpriteIfNeeded(string bundleData)
        {
            string[] parts     = bundleData.Split('/');
            bool     hasSprite = parts.Length >= 6 && !string.IsNullOrWhiteSpace(parts[5]);
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

            gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("config.section.title"));

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
                    if (id == DefaultVariantKey) return this.Helper.Translation.Get("config.bundle-variant.default");
                    if (this.LoadedPacks.TryGetValue(id, out var entry)) return entry.Pack.Manifest.Name;
                    return id;
                }
            );

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

        private void OnConfigChanged()
        {
            this.BundlesApplied = false;
            this.Helper.GameContent.InvalidateCache("Data/Bundles");
            this.Helper.GameContent.InvalidateCache("Data/RandomBundles");
            this.Helper.GameContent.InvalidateCache("LooseSprites/JunimoNote");
        }

        // ─── Asset Editing ────────────────────────────────────────────────────────

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            // ── Data/Bundles ──────────────────────────────────────────────────────
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
            // Two independent patches may be applied in a single edit call:
            //
            // (a) CCCBL's note_default.png — always applied when any pack is active.
            //     Extends the item display background so bundles with many items have
            //     enough visual space. Positioned at the default CCCC area (484, 110).
            //
            // (b) Pack's note_override.png — additionally applied when the active pack
            //     provides one. Handles the extra bundle slot backgrounds for packs
            //     that use their own JunimoNote layout (e.g. BBB at 256, 292).
            //
            // Both patches to different areas of the same texture so they stack correctly.
            if (e.NameWithoutLocale.IsEquivalentTo("LooseSprites/JunimoNote"))
            {
                bool    anyPackActive    = this.Config.BundleVariant != DefaultVariantKey || this.IsCompletionistActive();
                bool    hasDefault       = this.HasModAsset("assets/LooseSprites/note_default.png");
                string? packOverridePath = this.GetPackNoteOverridePath();
                bool    hasPackOverride  = packOverridePath is not null;

                if (anyPackActive && (hasDefault || hasPackOverride))
                {
                    e.Edit(asset =>
                    {
                        try
                        {
                            var editor = asset.AsImage();

                            // (a) Apply CCCBL's note_default whenever any pack is active
                            if (hasDefault)
                            {
                                var defaultOverlay = this.Helper.ModContent.Load<Texture2D>("assets/LooseSprites/note_default.png");
                                editor.ExtendImage(
                                    minWidth:  DefaultNoteOverlayArea.X + DefaultNoteOverlayArea.Width,
                                    minHeight: DefaultNoteOverlayArea.Y + DefaultNoteOverlayArea.Height);
                                editor.PatchImage(defaultOverlay, targetArea: DefaultNoteOverlayArea);
                            }

                            // (b) Additionally apply the pack's own overlay if it has one
                            if (hasPackOverride && this.LoadedPacks.TryGetValue(this.Config.BundleVariant, out var packEntry))
                            {
                                var     packOverlay = packEntry.Pack.ModContent.Load<Texture2D>(packOverridePath!);
                                var     noteArea    = packEntry.Data.NoteOverlay;
                                var     packArea    = noteArea is not null
                                    ? new Rectangle(noteArea.X, noteArea.Y, noteArea.Width, noteArea.Height)
                                    : DefaultNoteOverlayArea;
                                editor.ExtendImage(
                                    minWidth:  packArea.X + packArea.Width,
                                    minHeight: packArea.Y + packArea.Height);
                                editor.PatchImage(packOverlay, targetArea: packArea);
                            }
                        }
                        catch (Exception ex)
                        {
                            this.Monitor.Log($"Failed to apply JunimoNote overlay: {ex.Message}", LogLevel.Warn);
                        }
                    }, AssetEditPriority.Default);
                }
            }

            // ── Mods/{CCCBL}/bundleicon_default ──────────────────────────────────
            if (e.NameWithoutLocale.IsEquivalentTo($"Mods/{this.ModManifest.UniqueID}/bundleicon_default") &&
                this.HasModAsset("assets/LooseSprites/bundleicon_default.png"))
            {
                e.LoadFromModFile<Texture2D>("assets/LooseSprites/bundleicon_default.png", AssetLoadPriority.Low);
            }

            // ── LooseSprites/BundleSprites ───────────────────────────────────────
            // If the active pack declares a BundleSpritesOverlay, patch its custom sprites
            // into the BundleSprites sheet at the specified position. This is needed for
            // packs that add new bundle portrait icons referenced as LooseSprites\BundleSprites:N.
            if (e.NameWithoutLocale.IsEquivalentTo("LooseSprites/BundleSprites") &&
                this.Config.BundleVariant != DefaultVariantKey &&
                this.LoadedPacks.TryGetValue(this.Config.BundleVariant, out var spriteSheetPack) &&
                spriteSheetPack.Data.BundleSpritesOverlay is not null)
            {
                var overlay = spriteSheetPack.Data.BundleSpritesOverlay;
                string pngPath = $"assets/{overlay.File}.png";

                if (spriteSheetPack.Pack.HasFile(pngPath))
                {
                    var captured    = spriteSheetPack.Pack;
                    int targetX     = overlay.X;
                    int targetY     = overlay.Y;

                    e.Edit(asset =>
                    {
                        try
                        {
                            var patch  = captured.ModContent.Load<Texture2D>(pngPath);
                            var editor = asset.AsImage();
                            editor.ExtendImage(
                                minWidth:  targetX + patch.Width,
                                minHeight: targetY + patch.Height);
                            editor.PatchImage(patch, targetArea: new Rectangle(targetX, targetY, patch.Width, patch.Height));
                            this.Monitor.Log(
                                $"Applied BundleSprites patch '{pngPath}' at ({targetX},{targetY}) " +
                                $"from '{captured.Manifest.Name}'.",
                                LogLevel.Debug);
                        }
                        catch (Exception ex)
                        {
                            this.Monitor.Log($"Failed to apply BundleSprites overlay: {ex.Message}", LogLevel.Warn);
                        }
                    }, AssetEditPriority.Default);
                }
            }

            // ── Mods/{activePackId}/{anything} ────────────────────────────────────
            // Serves any PNG from the active pack's assets/ folder when the game requests
            // it at Mods/{PackUniqueId}/{filename}. Pack authors reference these in bundle
            // data sprite fields: Mods\{PackUniqueId}\{filename}:frameIndex
            if (this.Config.BundleVariant != DefaultVariantKey &&
                this.LoadedPacks.TryGetValue(this.Config.BundleVariant, out var spritePack))
            {
                string modPrefix  = $"Mods/{this.Config.BundleVariant}/";
                string assetPath  = e.NameWithoutLocale.ToString()!;

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
                            $"Serving '{pngPath}' from '{spritePack.Pack.Manifest.Name}' for '{e.NameWithoutLocale}'.",
                            LogLevel.Debug);
                    }
                }
            }
        }

        // ─── Runtime BundleData (existing saves) ──────────────────────────────────

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            var layers = this.GetActiveBundleDataLayers();
            if (layers.Count == 0) return;

            var current = Game1.netWorldState.Value.BundleData;
            this.OriginalBundleData = new Dictionary<string, string>(current);

            var modified = new Dictionary<string, string>(current);
            foreach (var layer in layers)
                this.ApplyBundleDataTo(modified, layer);

            Game1.netWorldState.Value.SetBundleData(modified);
            this.BundlesApplied = true;
            this.Monitor.Log($"Applied '{this.Config.BundleVariant}' bundle data to in-memory BundleData.", LogLevel.Debug);
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

        // ─── Utilities ────────────────────────────────────────────────────────────

        private string? GetPackNoteOverridePath()
        {
            if (this.Config.BundleVariant == DefaultVariantKey) return null;
            if (!this.LoadedPacks.TryGetValue(this.Config.BundleVariant, out var entry)) return null;
            const string path = "assets/note_override.png";
            return entry.Pack.HasFile(path) ? path : null;
        }

        private bool HasModAsset(string relativePath)
        {
            string fullPath = Path.Combine(
                this.Helper.DirectoryPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(fullPath);
        }
    }
}
