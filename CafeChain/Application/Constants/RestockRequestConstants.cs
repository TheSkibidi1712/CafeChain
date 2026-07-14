namespace CafeChain.Application.Constants
{
    /// <summary>Issue #100 / #128 — RestockRequest status codes (intent-only until BranchReceipt confirm).</summary>
    public static class RestockRequestStatuses
    {
        public const string Draft = "DRAFT";
        public const string Submitted = "SUBMITTED";
        public const string Processing = "PROCESSING";
        /// <summary>Issue #128 — some confirmed receipts, remaining &gt; 0.</summary>
        public const string PartiallyReceived = "PARTIALLY_RECEIVED";
        public const string Completed = "COMPLETED";
        public const string Rejected = "REJECTED";
        public const string Cancelled = "CANCELLED";

        public static readonly string[] ActiveValues =
        {
            Submitted,
            Processing,
            PartiallyReceived
        };
    }

    /// <summary>Issue #100 — RestockRequest priority codes.</summary>
    public static class RestockRequestPriorities
    {
        public const string Normal = "NORMAL";
        public const string High = "HIGH";
        public const string Urgent = "URGENT";
    }

    public static class RestockFulfillmentDocumentTypes
    {
        public const string BranchReceipt = "BRANCH_RECEIPT";
        public const string InventoryTransfer = "INVENTORY_TRANSFER";
    }
}
