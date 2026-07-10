namespace CafeChain.Application.Constants
{
    /// <summary>
    /// Audit-only issue codes for purchase/unit remediation (Issue #113 Checkpoint A).
    /// Does not change #117 costing semantics — use CostIssueCodes for EstimatedBomCost findings.
    /// </summary>
    public static class PurchaseUnitAuditIssueCodes
    {
        public const string PriceHistoryMissingCurrent = "PRICE_HISTORY_MISSING_CURRENT";
        public const string PriceHistoryMultipleCurrent = "PRICE_HISTORY_MULTIPLE_CURRENT";
        public const string PriceHistorySnapshotMismatch = "PRICE_HISTORY_SNAPSHOT_MISMATCH";
        public const string PriceHistoryIncompleteSnapshot = "PRICE_HISTORY_INCOMPLETE_SNAPSHOT";
        public const string PriceHistoryInvalidPrice = "PRICE_HISTORY_INVALID_PRICE";
        public const string PriceHistoryInactivePackageUnit = "PRICE_HISTORY_INACTIVE_PACKAGE_UNIT";
        public const string SoleCompleteOfferNotPrimary = "SOLE_COMPLETE_OFFER_NOT_PRIMARY";
        public const string NoActiveOffer = "NO_ACTIVE_OFFER";
    }
}
