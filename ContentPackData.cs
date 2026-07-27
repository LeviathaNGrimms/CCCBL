using System.Collections.Generic;

namespace CCCBL
{
    class ContentPackData
    {
        /// <summary>
        /// When true, Completionist Mode is automatically enabled and cannot be disabled
        /// while this pack is selected. Use this for packs that use CCCC-style extra slot IDs
        /// (Pantry/37-39, Crafts Room/43-44-52, Fish Tank/12-48-49, Boiler Room/45-46-50-51,
        /// Bulletin Board/40-42-53).
        ///
        /// Packs with their own self-contained extended IDs (like BBB-style) should set this
        /// to false — those IDs are supported directly by the game.
        /// </summary>
        public bool RequireCompletionistMode { get; set; } = false;

        /// <summary>
        /// Bundle entries to apply. Keys can be either:
        ///   - "Room/ID" format (e.g. "Pantry/0", "Boiler Room/28") — sets that bundle slot.
        ///   - Bundle name (e.g. "Spring Crops") — finds the matching slot by name and replaces it.
        ///
        /// All IDs the game supports are valid — CCCBL passes them through without restriction.
        ///
        /// Value format: "Name/Reward/Items/Color"
        ///           or: "Name/Reward/Items/Color/RequiredCount"
        ///           or: "Name/Reward/Items/Color/RequiredCount/SpriteRef"
        ///
        /// SpriteRef can be a plain integer (frame index in LooseSprites/BundleSprites)
        /// or "LooseSprites\\BundleSprites:N" or "Mods\\{PackUniqueId}\\{filename}:N".
        /// Quality: 0=Normal, 1=Silver, 2=Gold, 4=Iridium.
        /// Each item in Items is: ItemId Quantity Quality
        /// </summary>
        public Dictionary<string, string> Bundles { get; set; } = new();

        /// <summary>
        /// Optional. If your pack provides assets/note_override.png, specifies where it is
        /// overlaid on LooseSprites/JunimoNote (pixel coordinates, zero-based).
        /// Applied whenever your pack is active, regardless of Completionist Mode.
        ///
        /// If omitted, CCCBL's own note_default.png is used when Completionist Mode is on.
        ///
        /// Example matching BBB's layout:
        ///   "NoteOverlay": { "X": 256, "Y": 292, "Width": 256, "Height": 48 }
        /// </summary>
        public NoteOverlayArea? NoteOverlay { get; set; } = null;

        /// <summary>
        /// Optional. If your pack provides a custom PNG to patch into LooseSprites/BundleSprites,
        /// specifies the file name (without extension, relative to your assets/ folder) and the
        /// pixel coordinates where it should be placed in the BundleSprites sheet.
        ///
        /// Example matching BBB's BundleSpritesNew.png:
        ///   "BundleSpritesOverlay": { "File": "BundleSpritesNew", "X": 160, "Y": 64 }
        ///
        /// The image dimensions determine how much of the sheet is replaced — the patch is
        /// applied at the given X/Y origin and covers exactly the size of the source image.
        /// </summary>
        public BundleSpritesOverlayData? BundleSpritesOverlay { get; set; } = null;
    }

    class NoteOverlayArea
    {
        public int X      { get; set; } = 484;
        public int Y      { get; set; } = 110;
        public int Width  { get; set; } = 135;
        public int Height { get; set; } = 51;
    }

    class BundleSpritesOverlayData
    {
        /// <summary>File name without extension, relative to the pack's assets/ folder.</summary>
        public string File { get; set; } = "bundlesprites";
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
    }
}
