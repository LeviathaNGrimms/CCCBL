using System.Collections.Generic;

namespace CCCBL
{
    /// <summary>
    /// Represents the data read from a CCCBL content pack's content.json.
    /// </summary>
    class ContentPackData
    {
        /// <summary>
        /// When true, Completionist Mode is automatically enabled and cannot be disabled
        /// while this pack is selected. Use this if your pack defines extra bundle slots
        /// (indices 37-53 range) that only exist when Completionist Mode is on.
        /// </summary>
        public bool RequireCompletionistMode { get; set; } = false;

        /// <summary>
        /// Bundle entries to apply. Keys can be either:
        ///   - "Room/ID" format (e.g. "Pantry/0") — directly sets or adds that bundle slot.
        ///   - Bundle name (e.g. "Spring Crops")  — finds the matching slot by name and replaces it.
        ///
        /// Value format: "Name/Reward/Items/Color"
        ///           or: "Name/Reward/Items/Color/RequiredCount"
        ///           or: "Name/Reward/Items/Color/RequiredCount/Mods\{PackUniqueId}\{SpriteName}:N"
        ///
        /// To use a custom bundle icon, include a sprite reference as the last field using
        /// the format above, where {SpriteName} matches a PNG file in your assets/ folder.
        /// For example: "Mods\author.MyPack\bundleicons:0" loads frame 0 from assets/bundleicons.png.
        /// CCCBL automatically serves any file under assets/ when the game requests it at
        /// Mods/{PackUniqueId}/{filename}.
        ///
        /// Each item in the Items field is: ItemId Quantity Quality
        /// Quality: 0=Normal, 1=Silver, 2=Gold, 4=Iridium
        /// </summary>
        public Dictionary<string, string> Bundles { get; set; } = new();
    }
}
