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
            Draft,
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

    public static class RestockRequestErrorCodes
    {
        public const string ActiveRequestExists = "ACTIVE_RESTOCK_REQUEST_EXISTS";
        public const string DemandAdjustmentInvalid = "DEMAND_ADJUSTMENT_INVALID";
        public const string DemandAdjustmentNotAllowed = "DEMAND_ADJUSTMENT_NOT_ALLOWED";
        public const string ProcurementUnitMismatch = "PROCUREMENT_UNIT_MISMATCH";
        public const string ResourceChanged = "RESTOCK_REQUEST_RESOURCE_CHANGED";
        public const string Unauthorized = "RESTOCK_REQUEST_UNAUTHORIZED";
    }

    public static class RestockRequestAuditKeys
    {
        public const string DemandAdjustmentPrefix = "DEMAND_ADJUSTMENT:";
    }

    public static class RestockFulfillmentDocumentTypes
    {
        public const string BranchReceipt = "BRANCH_RECEIPT";
        public const string InventoryTransfer = "INVENTORY_TRANSFER";
    }
}
