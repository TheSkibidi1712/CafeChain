namespace CafeChain.Application.Constants
{
    /// <summary>Issue #128 — BranchReceipt status codes.</summary>
    public static class BranchReceiptStatuses
    {
        public const string Draft = "DRAFT";
        public const string Confirmed = "CONFIRMED";
    }

    /// <summary>Issue #128 — RestockRequestFulfillment source types (no transfer dual-post).</summary>
    public static class RestockFulfillmentSourceTypes
    {
        public const string Supplier = "SUPPLIER";
        public const string Manual = "MANUAL";
    }

    /// <summary>Issue #128 — RestockRequestFulfillment status.</summary>
    public static class RestockFulfillmentStatuses
    {
        public const string Planned = "PLANNED";
        public const string Linked = "LINKED";
        public const string Received = "RECEIVED";
        public const string Cancelled = "CANCELLED";
    }

    /// <summary>Issue #128 — structured error codes for restock receipt workflow.</summary>
    public static class BranchReceiptErrorCodes
    {
        public const string RestockOverReceiptNotAllowed = "RESTOCK_OVER_RECEIPT_NOT_ALLOWED";
        public const string ReceiptCostIncomplete = "RECEIPT_COST_INCOMPLETE";
        public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
        public const string ReceiptNotFound = "RECEIPT_NOT_FOUND";
        public const string ReceiptNotDraft = "RECEIPT_NOT_DRAFT";
        public const string ReceiptImmutable = "RECEIPT_IMMUTABLE";
        public const string RequestNotFound = "REQUEST_NOT_FOUND";
        public const string RequestStateInvalid = "REQUEST_STATE_INVALID";
        public const string IdentityMismatch = "IDENTITY_MISMATCH";
        public const string ConversionFailed = "CONVERSION_FAILED";
        public const string QuantityInvalid = "QUANTITY_INVALID";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string StoreMismatch = "STORE_MISMATCH";
        public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
        public const string ConfirmFailed = "CONFIRM_FAILED";
        public const string TransitionInvalid = "TRANSITION_INVALID";
        public const string ReceiptKeyRequired = "RECEIPT_KEY_REQUIRED";
        public const string DuplicateReceiptKey = "DUPLICATE_RECEIPT_KEY";
        public const string SupplierNotAssigned = "SUPPLIER_NOT_ASSIGNED";
        public const string OfferNotAvailable = "OFFER_NOT_AVAILABLE";
        public const string MinimumOrderNotMet = "MINIMUM_ORDER_NOT_MET";
        public const string ActualReceivedRequired = "ACTUAL_RECEIVED_REQUIRED";
        public const string ActualReceivedNotPositive = "ACTUAL_RECEIVED_NOT_POSITIVE";
        public const string RejectedQuantityNegative = "REJECTED_QUANTITY_NEGATIVE";
        public const string RejectedExceedsActualReceived = "REJECTED_EXCEEDS_ACTUAL_RECEIVED";
        public const string RejectionReasonRequired = "REJECTION_REASON_REQUIRED";
        public const string ReceiptExceedsRemaining = "RECEIPT_EXCEEDS_REMAINING";
        public const string PoLineScopeMismatch = "PO_LINE_SCOPE_MISMATCH";
        public const string ReceiptStaleVersion = "RECEIPT_STALE_VERSION";
        public const string ValidationRowVersionRequired = "VALIDATION_ROW_VERSION_REQUIRED";
        public const string ResourceChanged = "RESOURCE_CHANGED_BY_ANOTHER_USER";
    }
}
