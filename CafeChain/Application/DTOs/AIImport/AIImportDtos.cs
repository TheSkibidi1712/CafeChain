using CafeChain.Models.AIImport;

namespace CafeChain.Application.DTOs.AIImport;

public sealed class AIImportAnalyzeRequest
{
    public IFormFile? File { get; set; }
    public AIImportEntityType? EntityHint { get; set; }
}

public sealed class AIImportConfirmRequest
{
    public int ExpectedPreviewVersion { get; set; }
}

public sealed class AIImportCancelRequest
{
    public int ExpectedPreviewVersion { get; set; }
}

public sealed class AIImportGroupPatchRequest
{
    public int ExpectedPreviewVersion { get; set; }
    public AIImportEntityType EntityType { get; set; }
    public Dictionary<string, string?> Mapping { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AIImportItemPatchRequest
{
    public int ExpectedPreviewVersion { get; set; }
    public string Action { get; set; } = AIImportActions.Create;
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool WarningsAcknowledged { get; set; }
    public string? DuplicateOverrideReason { get; set; }
}

public sealed class AIImportPositionDto
{
    public string? SourceFormat { get; set; }
    public string? Sheet { get; set; }
    public string? Region { get; set; }
    public int? Row { get; set; }
    public string? Column { get; set; }
    public int? Section { get; set; }
    public int? Paragraph { get; set; }
    public int? Table { get; set; }
    public int? TableRow { get; set; }
    public int? TableColumn { get; set; }
    public int? Page { get; set; }
    public int? Block { get; set; }
    public int? TextStart { get; set; }
    public int? TextEnd { get; set; }
    public AIImportBoundingBoxDto? BoundingBox { get; set; }
}

public sealed class AIImportBoundingBoxDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public sealed class AIImportErrorDto
{
    public int? ItemId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AIImportPositionDto? Position { get; set; }
    public string? Field { get; set; }
}

public sealed class AIImportEditorOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool FromCurrentSession { get; set; }
}

public sealed class AIImportEditorOptionsDto
{
    public int SessionId { get; set; }
    public int PreviewVersion { get; set; }
    public List<AIImportEditorOptionDto> Categories { get; set; } = new();
    public List<AIImportEditorOptionDto> ProductTypes { get; set; } = new();
    public List<AIImportEditorOptionDto> Units { get; set; } = new();
}

public sealed class AIImportSummaryDto
{
    public int TotalGroups { get; set; }
    public int TotalRows { get; set; }
    public int Valid { get; set; }
    public int Warnings { get; set; }
    public int Errors { get; set; }
    public int ReviewRequired { get; set; }
    public int Skipped { get; set; }
    public int Imported { get; set; }
}

public sealed class AIImportSessionDto
{
    public int SessionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string SourceFormat { get; set; } = "XLSX";
    public Dictionary<string, object?> SourceMetadata { get; set; } = new();
    public List<string> ExtractionModes { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public int AnalysisVersion { get; set; }
    public int PreviewVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public List<AIImportErrorDto> AnalysisWarnings { get; set; } = new();
    public AIImportSummaryDto Summary { get; set; } = new();
    public List<AIImportGroupDto> Groups { get; set; } = new();
    public AIImportPageDto Page { get; set; } = new();
}

public sealed class AIImportGroupDto
{
    public int GroupId { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public string RegionAddress { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public AIImportPositionDto? SourceLocator { get; set; }
    public string ExtractionMode { get; set; } = string.Empty;
    public int HeaderRow { get; set; }
    public AIImportEntityType EntityType { get; set; }
    public Dictionary<string, string?> Mapping { get; set; } = new();
    public List<string> SourceHeaders { get; set; } = new();
    public int DependencyOrder { get; set; }
    public decimal Confidence { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<AIImportItemDto> Items { get; set; } = new();
}

public sealed class AIImportItemDto
{
    public int ItemId { get; set; }
    public int SourceRow { get; set; }
    public Dictionary<string, string?> RawData { get; set; } = new();
    public Dictionary<string, string?> NormalizedData { get; set; } = new();
    public Dictionary<string, string?> SourceTrace { get; set; } = new();
    public AIImportPositionDto? SourceLocator { get; set; }
    public string? EvidenceSnippet { get; set; }
    public decimal? AiConfidence { get; set; }
    public decimal? OcrConfidence { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public List<AIImportErrorDto> Errors { get; set; } = new();
    public List<AIImportErrorDto> Warnings { get; set; } = new();
    public bool WarningsAcknowledged { get; set; }
    public string? DuplicateOverrideReason { get; set; }
    public int? ImportedEntityId { get; set; }
}

public sealed class AIImportPageDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; } = 1;
}

public sealed class AIImportHistoryItemDto
{
    public int SessionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string SourceFormat { get; set; } = "XLSX";
    public List<string> ExtractionModes { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public int PreviewVersion { get; set; }
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class AIImportHistoryDto
{
    public List<AIImportHistoryItemDto> Items { get; set; } = new();
    public AIImportPageDto Page { get; set; } = new();
}

public sealed class AIImportConfirmResultDto
{
    public int SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public Dictionary<string, int> ImportedByEntity { get; set; } = new();
}

public sealed class AIImportOperationResult<T>
{
    public bool Success { get; init; }
    public int StatusCode { get; init; } = StatusCodes.Status200OK;
    public string? ErrorCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public List<AIImportErrorDto> Details { get; init; } = new();

    public static AIImportOperationResult<T> Ok(T data, string message = "Thành công.") =>
        new() { Success = true, Data = data, Message = message };

    public static AIImportOperationResult<T> Fail(int statusCode, string code, string message,
        IEnumerable<AIImportErrorDto>? details = null) => new()
        {
            StatusCode = statusCode,
            ErrorCode = code,
            Message = message,
            Details = details?.ToList() ?? new List<AIImportErrorDto>()
        };
}
