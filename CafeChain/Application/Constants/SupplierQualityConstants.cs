namespace CafeChain.Application.Constants;

public static class SupplierReceiptIssueTypes
{
    public const string LateDelivery = "LATE_DELIVERY";
    public const string ShortDelivery = "SHORT_DELIVERY";
    public const string WrongItem = "WRONG_ITEM";
    public const string Damaged = "DAMAGED";
    public const string Expired = "EXPIRED";
    public const string QualityFailure = "QUALITY_FAILURE";
    public const string PackagingFailure = "PACKAGING_FAILURE";
    public const string DocumentMismatch = "DOCUMENT_MISMATCH";
    public const string Other = "OTHER";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        LateDelivery, ShortDelivery, WrongItem, Damaged, Expired,
        QualityFailure, PackagingFailure, DocumentMismatch, Other
    };
}

public static class SupplierReceiptIssueStatuses
{
    public const string Open = "OPEN";
    public const string UnderReview = "UNDER_REVIEW";
    public const string Resolved = "RESOLVED";
    public const string Dismissed = "DISMISSED";
    public const string Closed = "CLOSED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Open, UnderReview, Resolved, Dismissed, Closed
    };
}

public static class SupplierPerformanceStatuses
{
    public const string InsufficientData = "INSUFFICIENT_DATA";
    public const string Good = "GOOD";
    public const string Watch = "WATCH";
    public const string Risk = "RISK";
}
