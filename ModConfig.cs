namespace CCCBL
{
    class ModConfig
    {
        /// <summary>
        /// UniqueID of the active bundle pack, or "Default" to use no custom pack.
        /// </summary>
        public string BundleVariant { get; set; } = "Default";

        /// <summary>
        /// When true, all Community Center bundles (including Remixed variants) are active.
        /// Forced on when the selected pack declares RequireCompletionistMode = true.
        /// </summary>
        public bool CompletionistMode { get; set; } = false;
    }
}
