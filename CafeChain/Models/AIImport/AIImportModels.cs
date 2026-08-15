using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.AIImport;

public enum AIImportEntityType
{
    Unknown = 0,
    Category = 1,
    Drink = 2,
    Size = 3,
    Ingredient = 4,
    Supplier = 5
}

public static class AIImportSessionStatuses
{
    public const string Uploaded = "UPLOADED";
    public const string Analyzing = "ANALYZING";
    public const string Validating = "VALIDATING";
    public const string ReadyToPreview = "READY_TO_PREVIEW";
    public const string Importing = "IMPORTING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";
}

public static class AIImportItemStatuses
{
    public const string Valid = "VALID";
    public const string Warning = "WARNING";
    public const string Error = "ERROR";
    public const string ReviewRequired = "REVIEW_REQUIRED";
    public const string Skipped = "SKIPPED";
    public const string Imported = "IMPORTED";
}

public static class AIImportActions
{
    public const string Create = "CREATE";
    public const string Skip = "SKIP";
}

public static class AIImportSourceDocumentStatuses
{
    public const string Processing = "PROCESSING";
    public const string Ready = "READY";
    public const string Failed = "FAILED";
    public const string Removed = "REMOVED";
}

public class ImportSession
{
    public virtual int ImportSessionId { get; set; }
    public virtual string FileName { get; set; } = string.Empty;
    public virtual string FileHash { get; set; } = string.Empty;
    public virtual long FileSize { get; set; }
    public virtual string SourceFormat { get; set; } = "XLSX";
    public virtual string SourceMetadataJson { get; set; } = "{}";
    public virtual string? SourceSnapshotJson { get; set; }
    public virtual int UploadedByStaffId { get; set; }
    public virtual int UploadedByAccountId { get; set; }
    public virtual int StoreId { get; set; }
    public virtual string Status { get; set; } = AIImportSessionStatuses.Uploaded;
    public virtual int AnalysisVersion { get; set; } = 1;
    public virtual int PreviewVersion { get; set; }
    public virtual string? ModelName { get; set; }
    public virtual string PromptVersion { get; set; } = "ai-import-v1";
    public virtual string SchemaVersion { get; set; } = "master-data-v1";
    public virtual string ExtractionVersion { get; set; } = "ai-import-extraction-v2";
    public virtual int TotalGroups { get; set; }
    public virtual int TotalRows { get; set; }
    public virtual int ValidRows { get; set; }
    public virtual int WarningRows { get; set; }
    public virtual int ErrorRows { get; set; }
    public virtual int ReviewRows { get; set; }
    public virtual int SkippedRows { get; set; }
    public virtual DateTime CreatedAtUtc { get; set; }
    public virtual DateTime ExpiresAtUtc { get; set; }
    public virtual DateTime? ConfirmedAtUtc { get; set; }
    public virtual DateTime? CompletedAtUtc { get; set; }
    public virtual string? FailureCode { get; set; }
    public virtual string? FailureMessage { get; set; }
    public virtual string AnalysisWarningsJson { get; set; } = "[]";
    public virtual string? ResultJson { get; set; }
    public virtual bool RequestedOcr { get; set; }
    public virtual bool EffectiveOcr { get; set; }
    public virtual string OcrConfigVersion { get; set; } = string.Empty;

    [Timestamp]
    public virtual byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<ImportGroup> Groups { get; set; } = new List<ImportGroup>();
    public virtual ICollection<ImportSourceDocument> SourceDocuments { get; set; } = new List<ImportSourceDocument>();
    public virtual ICollection<ImportAudit> Audits { get; set; } = new List<ImportAudit>();
}

public class ImportSourceDocument
{
    public virtual int ImportSourceDocumentId { get; set; }
    public virtual int ImportSessionId { get; set; }
    public virtual string OriginalFileName { get; set; } = string.Empty;
    public virtual string FileHash { get; set; } = string.Empty;
    public virtual long FileSize { get; set; }
    public virtual string SourceFormat { get; set; } = string.Empty;
    public virtual int SortOrder { get; set; }
    public virtual string SourceMetadataJson { get; set; } = "{}";
    public virtual string? SourceSnapshotJson { get; set; }
    public virtual string Status { get; set; } = AIImportSourceDocumentStatuses.Processing;
    public virtual string? ErrorCode { get; set; }
    public virtual string? ErrorMessage { get; set; }
    public virtual DateTime CreatedAtUtc { get; set; }

    public virtual ImportSession Session { get; set; } = null!;
    public virtual ICollection<ImportGroup> Groups { get; set; } = new List<ImportGroup>();
}

public class ImportGroup
{
    public virtual int ImportGroupId { get; set; }
    public virtual int ImportSessionId { get; set; }
    public virtual int? ImportSourceDocumentId { get; set; }
    public virtual string SheetName { get; set; } = string.Empty;
    public virtual string RegionAddress { get; set; } = string.Empty;
    public virtual string SourceLabel { get; set; } = string.Empty;
    public virtual string SourceLocatorJson { get; set; } = "{}";
    public virtual string ExtractionMode { get; set; } = "XLSX_DETERMINISTIC";
    public virtual int HeaderRow { get; set; }
    public virtual AIImportEntityType EntityType { get; set; }
    public virtual string MappingJson { get; set; } = "{}";
    public virtual string SourceHeadersJson { get; set; } = "[]";
    public virtual string SourceColumnsJson { get; set; } = "[]";
    public virtual string IssuesJson { get; set; } = "[]";
    public virtual int DependencyOrder { get; set; }
    public virtual decimal Confidence { get; set; }
    public virtual decimal? LayoutConfidence { get; set; }
    public virtual string Status { get; set; } = AIImportItemStatuses.ReviewRequired;

    public virtual ImportSession Session { get; set; } = null!;
    public virtual ImportSourceDocument? SourceDocument { get; set; }
    public virtual ICollection<ImportItem> Items { get; set; } = new List<ImportItem>();
}

public class ImportItem
{
    public virtual int ImportItemId { get; set; }
    public virtual int ImportGroupId { get; set; }
    public virtual int SourceRow { get; set; }
    public virtual string RawDataJson { get; set; } = "{}";
    public virtual string NormalizedDataJson { get; set; } = "{}";
    public virtual string SourceTraceJson { get; set; } = "{}";
    public virtual string SourceLocatorJson { get; set; } = "{}";
    public virtual string? EvidenceSnippet { get; set; }
    public virtual string Status { get; set; } = AIImportItemStatuses.ReviewRequired;
    public virtual string Action { get; set; } = AIImportActions.Create;
    public virtual string ErrorsJson { get; set; } = "[]";
    public virtual string WarningsJson { get; set; } = "[]";
    public virtual string SourceIssuesJson { get; set; } = "[]";
    public virtual decimal Confidence { get; set; }
    public virtual decimal? AiConfidence { get; set; }
    public virtual decimal? OcrConfidence { get; set; }
    public virtual decimal? LayoutConfidence { get; set; }
    public virtual string FieldEvidenceJson { get; set; } = "{}";
    public virtual bool WarningsAcknowledged { get; set; }
    public virtual bool ManualReviewConfirmed { get; set; }
    public virtual DateTime? ManualReviewConfirmedAtUtc { get; set; }
    public virtual int? ManualReviewConfirmedByAccountId { get; set; }
    public virtual string? ManualReviewPayloadHash { get; set; }
    public virtual Guid? SupplierDuplicateWarningId { get; set; }
    public virtual string? DuplicateOverrideReason { get; set; }
    public virtual int? ImportedEntityId { get; set; }

    public virtual ImportGroup Group { get; set; } = null!;
}

public class ImportAudit
{
    public virtual int ImportAuditId { get; set; }
    public virtual int ImportSessionId { get; set; }
    public virtual int StaffId { get; set; }
    public virtual int AccountId { get; set; }
    public virtual string Action { get; set; } = string.Empty;
    public virtual string? StatusBefore { get; set; }
    public virtual string? StatusAfter { get; set; }
    public virtual AIImportEntityType? EntityType { get; set; }
    public virtual string? ModelName { get; set; }
    public virtual string PromptVersion { get; set; } = string.Empty;
    public virtual string SchemaVersion { get; set; } = string.Empty;
    public virtual string ExtractionVersion { get; set; } = string.Empty;
    public virtual int PreviewVersion { get; set; }
    public virtual string? IdempotencyKeyHash { get; set; }
    public virtual string? ResultSummaryJson { get; set; }
    public virtual string? ErrorCode { get; set; }
    public virtual string SourceFormat { get; set; } = "XLSX";
    public virtual string? ExtractionMode { get; set; }
    public virtual bool OcrUsed { get; set; }
    public virtual int OcrPageCount { get; set; }
    public virtual string? OcrProvider { get; set; }
    public virtual string? OcrProviderVersion { get; set; }
    public virtual string? OcrExtractionVersion { get; set; }
    public virtual string? OcrConfidenceSummaryJson { get; set; }
    public virtual int AiChunkCount { get; set; }
    public virtual DateTime CreatedAtUtc { get; set; }
    public virtual DateTime? CompletedAtUtc { get; set; }

    public virtual ImportSession Session { get; set; } = null!;
}
