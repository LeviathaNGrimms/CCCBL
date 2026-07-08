using System;
using StardewModdingAPI;

namespace CCCBL
{
    /// <summary>
    /// Minimal interface for Generic Mod Config Menu.
    /// Only the methods used by CCCBL are declared here.
    /// See https://github.com/spacechase0/StardewValleyMods/tree/develop/GenericModConfigMenu
    /// for the full API.
    /// </summary>
    public interface IGenericModConfigMenuApi
    {
        /// <summary>Register a mod with GMCM.</summary>
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        /// <summary>Add a section title to the config page.</summary>
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

        /// <summary>Add a paragraph of descriptive text.</summary>
        void AddParagraph(IManifest mod, Func<string> text);

        /// <summary>Add a true/false toggle option.</summary>
        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null);

        /// <summary>Add a text option, optionally constrained to a fixed list (renders as a dropdown).</summary>
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
