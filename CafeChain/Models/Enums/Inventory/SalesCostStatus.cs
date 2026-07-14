namespace CafeChain.Models.Enums.Inventory
{
    /// <summary>Issue #133 — actual sales COGS lifecycle (payment independent of completeness).</summary>
    public enum SalesCostStatus
    {
        /// <summary>Order paid; deduction/cost not applied yet.</summary>
        Pending = 0,

        /// <summary>Full FIFO evidence for all deducted requirements.</summary>
        Complete = 1,

        /// <summary>Quantity deducted; one or more cost gaps remain.</summary>
        Incomplete = 2
    }
}
