namespace CafeChain.Models.Enums.Inventory
{
    /// <summary>Issue #134 — full-order cash refund lifecycle.</summary>
    public enum OrderRefundStatus
    {
        Requested = 1,
        Processing = 2,
        Completed = 3,
        Failed = 4
    }

    public enum RefundInventoryReversalStatus
    {
        Pending = 0,
        Completed = 1,
        NotApplicable = 2,
        NoOriginalDeduction = 3
    }
}
