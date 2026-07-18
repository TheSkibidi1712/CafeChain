namespace CafeChain.Application.Constants;

public static class PurchaseOrderBatchStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string PdfGenerated = "PDF_GENERATED";
    public const string SentToSupplier = "SENT_TO_SUPPLIER";
    public const string PartiallyReceived = "PARTIALLY_RECEIVED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlySet<string> ApprovedOrLater = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Approved, PdfGenerated, SentToSupplier, PartiallyReceived, Completed
    };
}

public static class PurchaseOrderBatchErrorCodes
{
    public const string NotFound = "PURCHASE_ORDER_BATCH_NOT_FOUND";
    public const string Forbidden = "PURCHASE_ORDER_BATCH_FORBIDDEN";
    public const string Invalid = "PURCHASE_ORDER_BATCH_INVALID";
    public const string StaleVersion = "PURCHASE_ORDER_BATCH_STALE_VERSION";
    public const string Conflict = "PURCHASE_ORDER_BATCH_CONFLICT";
    public const string DocumentNotFound = "PURCHASE_ORDER_BATCH_DOCUMENT_NOT_FOUND";
    public const string DocumentStorageFailure = "PURCHASE_ORDER_BATCH_DOCUMENT_STORAGE_FAILURE";
}

public static class PurchaseOrderBatchDocumentStatuses
{
    public const string Generated = "GENERATED";
    public const string Sent = "SENT";
    public const string Superseded = "SUPERSEDED";
}

public static class PurchaseOrderBatchDocumentChannels
{
    public const string ZaloManual = "ZALO_MANUAL";
    public const string EmailManual = "EMAIL_MANUAL";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ZaloManual,
        EmailManual
    };
}
