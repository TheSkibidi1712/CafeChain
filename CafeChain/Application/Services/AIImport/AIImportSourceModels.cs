using CafeChain.Application.DTOs.AIImport;
using CafeChain.Models.AIImport;

namespace CafeChain.Application.Services.AIImport;

public static class AIImportSourceFormats
{
    public const string Xlsx = "XLSX";
    public const string Docx = "DOCX";
    public const string Pdf = "PDF";

    public static string? FromFileName(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".xlsx" => Xlsx,
        ".docx" => Docx,
        ".pdf" => Pdf,
        _ => null
    };
}

public static class AIImportExtractionModes
{
    public const string XlsxDeterministic = "XLSX_DETERMINISTIC";
    public const string XlsxAiMapping = "XLSX_AI_MAPPING";
    public const string DocxTableDeterministic = "DOCX_TABLE_DETERMINISTIC";
    public const string DocxTextDeterministic = "DOCX_TEXT_DETERMINISTIC";
    public const string DocxAiExtraction = "DOCX_AI_EXTRACTION";
    public const string PdfTextDeterministic = "PDF_TEXT_DETERMINISTIC";
    public const string PdfTextAiExtraction = "PDF_TEXT_AI_EXTRACTION";
}

public sealed record AIImportSourceFile(string FileName, byte[] Content, string? ContentType = null);

public sealed class AIImportSourceSnapshot
{
    public string SourceFormat { get; init; } = string.Empty;
    public string ExtractedText { get; init; } = string.Empty;
    public Dictionary<string, object?> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AIImportBoundingBox
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}

public sealed class AIImportSourceLocator
{
    public string SourceFormat { get; init; } = string.Empty;
    public string? Sheet { get; init; }
    public string? Region { get; init; }
    public int? Row { get; init; }
    public string? Column { get; init; }
    public int? Section { get; init; }
    public int? Paragraph { get; init; }
    public int? Table { get; init; }
    public int? TableRow { get; init; }
    public int? TableColumn { get; init; }
    public int? Page { get; init; }
    public int? Block { get; init; }
    public int? TextStart { get; init; }
    public int? TextEnd { get; init; }
    public AIImportBoundingBox? BoundingBox { get; init; }
}

public sealed class AIImportSourceCandidate
{
    public int SortOrder { get; init; }
    public Dictionary<string, string?> RawData { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string?> MappedData { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string?> SourceTrace { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public AIImportSourceLocator SourceLocator { get; init; } = new();
    public string EvidenceSnippet { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public decimal? AiConfidence { get; init; }
    public string? AIErrorCode { get; init; }
    public List<AIImportErrorDto> Issues { get; init; } = [];
}

public sealed class AIImportSourceGroup
{
    public string SourceLabel { get; init; } = string.Empty;
    public AIImportSourceLocator SourceLocator { get; init; } = new();
    public string ExtractionMode { get; init; } = string.Empty;
    public int HeaderOrdinal { get; init; }
    public AIImportEntityType EntityType { get; init; }
    public Dictionary<string, string?> Mapping { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> SourceHeaders { get; init; } = [];
    public List<AIImportSourceColumn> SourceColumns { get; init; } = [];
    public List<AIImportErrorDto> Issues { get; init; } = [];
    public decimal Confidence { get; init; }
    public List<AIImportSourceCandidate> Candidates { get; init; } = [];
}

public sealed class AIImportSourceDocument
{
    public string SourceFormat { get; init; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public Dictionary<string, object?> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<AIImportSourceGroup> Groups { get; init; } = [];
    public List<AIImportErrorDto> Warnings { get; init; } = [];
    public List<AIImportErrorDto> Errors { get; init; } = [];
    public int AiChunkCount { get; set; }
    public bool UsedAI { get; set; }
}

public interface IAIImportSourceParser
{
    string SourceFormat { get; }
    Task<AIImportSourceDocument> ParseAsync(
        AIImportSourceFile source,
        AIImportEntityType? entityHint,
        CancellationToken cancellationToken);
}

public interface IAIImportDocumentPipeline
{
    Task<AIImportSourceDocument> AnalyzeAsync(
        AIImportSourceFile source,
        AIImportEntityType? entityHint,
        CancellationToken cancellationToken);

    Task<AIImportSourceDocument> ReanalyzeAsync(
        AIImportSourceSnapshot snapshot,
        AIImportEntityType? entityHint,
        CancellationToken cancellationToken);
}

public sealed class AIImportDocumentPipeline : IAIImportDocumentPipeline
{
    private readonly IReadOnlyDictionary<string, IAIImportSourceParser> _parsers;
    private readonly IAIImportDocumentAiExtractor? _aiExtractor;

    public AIImportDocumentPipeline(IEnumerable<IAIImportSourceParser> parsers)
        : this(parsers, null)
    {
    }

    public AIImportDocumentPipeline(
        IEnumerable<IAIImportSourceParser> parsers,
        IAIImportDocumentAiExtractor? aiExtractor)
    {
        _parsers = parsers.ToDictionary(parser => parser.SourceFormat, StringComparer.OrdinalIgnoreCase);
        _aiExtractor = aiExtractor;
    }

    public async Task<AIImportSourceDocument> AnalyzeAsync(
        AIImportSourceFile source,
        AIImportEntityType? entityHint,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(source.FileName).ToLowerInvariant();
        if (extension == ".doc") return Failure("ĐỊNH_DẠNG_DOC_CŨ_KHÔNG_HỖ_TRỢ", "Tệp .doc không được hỗ trợ; vui lòng chuyển sang .docx.");
        if (extension == ".docm") return Failure("ĐỊNH_DẠNG_KHÔNG_HỖ_TRỢ", "Tệp Word có macro không được hỗ trợ.");

        var format = AIImportSourceFormats.FromFileName(source.FileName);
        if (format == null || !_parsers.TryGetValue(format, out var parser))
            return Failure("ĐỊNH_DẠNG_KHÔNG_HỖ_TRỢ", "Chỉ hỗ trợ tệp .xlsx, .docx hoặc .pdf.");
        if (!ContentTypeMatches(format, source.ContentType))
            return Failure("ĐỊNH_DẠNG_KHÔNG_KHỚP_NỘI_DUNG", "Content-Type của tệp không khớp phần mở rộng đã chọn.");

        var result = await parser.ParseAsync(source, entityHint, cancellationToken);
        var needsAi = format is AIImportSourceFormats.Docx or AIImportSourceFormats.Pdf
                      && !string.IsNullOrWhiteSpace(result.ExtractedText)
                      && (result.Groups.Count == 0 || result.Groups.Any(group => group.EntityType == AIImportEntityType.Unknown));
        if (needsAi && _aiExtractor != null)
            await _aiExtractor.EnrichAsync(result, entityHint, cancellationToken);
        return result;
    }

    public async Task<AIImportSourceDocument> ReanalyzeAsync(
        AIImportSourceSnapshot snapshot,
        AIImportEntityType? entityHint,
        CancellationToken cancellationToken)
    {
        var result = new AIImportSourceDocument
        {
            SourceFormat = snapshot.SourceFormat,
            ExtractedText = snapshot.ExtractedText,
            Metadata = snapshot.Metadata
        };
        if (_aiExtractor == null || string.IsNullOrWhiteSpace(snapshot.ExtractedText))
        {
            result.Errors.Add(new AIImportErrorDto { Code = "KHÔNG_CÓ_SNAPSHOT_NGUỒN", Message = "Không còn dữ liệu nguồn để phân tích lại." });
            return result;
        }
        await _aiExtractor.EnrichAsync(result, entityHint, cancellationToken);
        return result;
    }

    private static AIImportSourceDocument Failure(string code, string message) => new()
    {
        Errors = [new AIImportErrorDto { Code = code, Message = message }]
    };

    private static bool ContentTypeMatches(string format, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)
            || contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)) return true;
        return format switch
        {
            AIImportSourceFormats.Xlsx => contentType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase),
            AIImportSourceFormats.Docx => contentType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase),
            AIImportSourceFormats.Pdf => contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

}
