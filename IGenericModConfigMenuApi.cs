using System;
using StardewModdingAPI;

namespace CCCBL
{
    /// Minimal interface for Generic Mod Config Menu.
    /// Only the methods used by CCCBL are declared here.
    /// See https://github.com/spacechase0/StardewValleyMods/tree/develop/GenericModConfigMenu
    /// for the full API.
    public interface IGenericModConfigMenuApi
    {
        /// Register a mod with GMCM.
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        /// Add a section title to the config page.
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

        /// Add a paragraph of descriptive text.
        void AddParagraph(IManifest mod, Func<string> text);

        /// Add a true/false toggle option.
        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null);

        /// Add a text option, optionally constrained to a fixed list (renders as a dropdown).
        void AddTextOption(
            IManifest mod,
            Func<string> getValue,
            Action<string> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string[]? allowedValues = null,
            Func<string, string>? formatAllowedValue = null,
            string? fieldId = null);
    }
}
