using System.Collections.Generic;

namespace CCCBL
{
    /// Represents the data read from a CCCBL content pack's content.json.
    class ContentPackData
    {
        /// When true, Completionist Mode is automatically enabled and cannot be disabled
        /// while this pack is selected. Use this if your pack defines extra bundle slots
        /// (indices 37-53 range) that only exist when Completionist Mode is on.
        public bool RequireCompletionistMode { get; set; } = false;

        public Dictionary<string, string> Bundles { get; set; } = new();
    }
}
