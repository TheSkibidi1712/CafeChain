namespace CafeChain.Models.Enums.Inventory
{
    /// <summary>Issue #132 — production actual-cost valuation lifecycle (fail-closed Complete).</summary>
    public enum ProductionValuationStatus
    {
        /// <summary>Confirmed intent; stock/cost not applied.</summary>
        Pending = 0,

        /// <summary>Completed with full FIFO evidence snapshotted.</summary>
        Complete = 1
    }
}
