namespace CCCBL
{
    class ModConfig
    {
        /// UniqueID of the active bundle pack, or "Default" to use no custom pack.
        public string BundleVariant { get; set; } = "Default";

        /// When true, all Community Center bundles (including Remixed variants) are active.
        /// Forced on when the selected pack declares RequireCompletionistMode = true.
        public bool CompletionistMode { get; set; } = false;
    }
}
