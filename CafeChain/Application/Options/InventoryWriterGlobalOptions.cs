namespace CafeChain.Application.Options
{
    /// <summary>
    /// Issue #124 — deployment-controlled global kill switch for legacy Recipe BTP writers.
    /// Bind from environment / config provider; do not commit secrets or force-enable via appsettings in repo.
    /// Env: InventoryWriter__LegacyBtpWritesDisabled=true
    /// </summary>
    public sealed class InventoryWriterGlobalOptions
    {
        public const string SectionName = "InventoryWriter";

        /// <summary>
        /// When true, every legacy Recipe-based BTP mutation fails with LEGACY_BTP_WRITES_GLOBALLY_DISABLED,
        /// even if Store WriterMode is still LegacyRecipe. Ingredient paths are unaffected.
        /// </summary>
        public bool LegacyBtpWritesDisabled { get; set; }
    }
}
