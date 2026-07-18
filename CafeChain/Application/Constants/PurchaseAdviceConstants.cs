namespace CafeChain.Application.Constants
{
    public static class PurchaseAdviceStatuses
    {
        public const string Draft = "DRAFT";
        public const string Submitted = "SUBMITTED";
        public const string UnderReview = "UNDER_REVIEW";
        public const string Rejected = "REJECTED";
        public const string Cancelled = "CANCELLED";

        public static readonly IReadOnlySet<string> ActiveReservationStatuses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Draft, Submitted, UnderReview
            };
    }

    public static class PurchaseAdvicePriorities
    {
        public const string Normal = "NORMAL";
        public const string High = "HIGH";
        public const string Urgent = "URGENT";

        public static readonly IReadOnlySet<string> All =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Normal, High, Urgent
            };
    }

    public static class PurchaseAdviceErrorCodes
    {
        public const string NotFound = "PURCHASE_ADVICE_NOT_FOUND";
        public const string NotEditable = "PURCHASE_ADVICE_NOT_EDITABLE";
        public const string Empty = "PURCHASE_ADVICE_EMPTY";
        public const string QuantityInvalid = "PURCHASE_ADVICE_QUANTITY_INVALID";
        public const string ExceedsRestockRemaining = "PURCHASE_ADVICE_EXCEEDS_RESTOCK_REMAINING";
        public const string AlreadyExists = "PURCHASE_ADVICE_ALREADY_EXISTS";
        public const string SourceInvalid = "PURCHASE_ADVICE_SOURCE_INVALID";
        public const string StoreScopeMismatch = "PURCHASE_ADVICE_STORE_SCOPE_MISMATCH";
        public const string StaleVersion = "PURCHASE_ADVICE_STALE_VERSION";
        public const string Forbidden = "PURCHASE_ADVICE_FORBIDDEN";
        public const string RejectionReasonRequired = "PURCHASE_ADVICE_REJECTION_REASON_REQUIRED";
    }
}
