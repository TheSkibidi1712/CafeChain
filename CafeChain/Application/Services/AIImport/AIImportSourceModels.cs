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
    public const string PdfOcrDeterministic = "PDF_OCR_DETERMINISTIC";
    public const string PdfOcrAiExtraction = "PDF_OCR_AI_EXTRACTION";
    public const string PdfMixedTextOcr = "PDF_MIXED_TEXT_OCR";
}

public static class AIImportPdfPageClassifications
{
    public const string TextBased = "TEXT_BASED";
    public const string ImageBased = "IMAGE_BASED";
    public const string Mixed = "MIXED";
}

public static class AIImportSourceKinds
{
    public const string TextLayer = "TEXT_LAYER";
    public const string Ocr = "OCR";
    public const string AiAfterText = "AI_AFTER_TEXT";
    public const string AiAfterOcr = "AI_AFTER_OCR";
}

public sealed record AIImportSourceFile(
    string FileName,
    byte[] Content,
    string? ContentType = null,
    bool UseOcr = false,
    AIImportOcrRuntimeState? OcrRuntime = null);

public sealed class AIImportSourceSnapshot
{
    public string SourceFormat { get; init; } = string.Empty;
    public string ExtractedText { get; init; } = string.Empty;
    public Dictionary<string, object?> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<AIImportSemanticBlock> Blocks { get; init; } = [];
    public List<AIImportOcrPageSnapshot> OcrPages { get; init; } = [];
}

public sealed class AIImportBoundingBox
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double? PageWidth { get; init; }
    public double? PageHeight { get; init; }
    public int Rotation { get; init; }
    public string Unit { get; init; } = "POINT";
    public List<double> Polygon { get; init; } = [];
}

public sealed class AIImportFieldEvidence
{
    public string SourceKind { get; init; } = AIImportSourceKinds.TextLayer;
    public AIImportSourceLocator Locator { get; init; } = new();
    public string RawText { get; init; } = string.Empty;
    public string? NormalizedValue { get; init; }
    public decimal? OcrConfidence { get; init; }
    public decimal? AiConfidence { get; init; }
}

public sealed class AIImportSemanticBlock
{
    public int Ordinal { get; init; }
    public string Kind { get; init; } = "PARAGRAPH_GROUP";
    public string Text { get; init; } = string.Empty;
    public AIImportSourceLocator Locator { get; init; } = new();
}

public sealed class AIImportOcrPageSnapshot
{
    public int PageNumber { get; init; }
    public string Text { get; init; } = string.Empty;
    public List<AIImportOcrWord> Words { get; init; } = [];
    public string Provider { get; init; } = string.Empty;
    public string? ProviderVersion { get; init; }
    public string ExtractionVersion { get; init; } = string.Empty;
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
    public decimal? LayoutConfidence { get; init; }
    public decimal? OcrConfidence { get; init; }
    public Dictionary<string, AIImportFieldEvidence> FieldEvidence { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? AIErrorCode { get; init; }
    public List<AIImportErrorDto> Issues { get; init; } = [];
}

public sealed class AIImportSourceGroup
{
    public string SourceLabel { get; init; } = string.Empty;
    public AIImportSourceLocator SourceLocator { get; init; } = new();
    public string ExtractionMode { get; set; } = string.Empty;
    public string SourceRegionId { get; init; } = string.Empty;
    public string? BoundingRange { get; init; }
    public string? HeaderRange { get; init; }
    public string? DataRange { get; init; }
    public int HeaderOrdinal { get; init; }
    public AIImportEntityType EntityType { get; init; }
    public Dictionary<string, string?> Mapping { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> SourceHeaders { get; init; } = [];
    public List<AIImportSourceColumn> SourceColumns { get; init; } = [];
    public List<AIImportErrorDto> Issues { get; init; } = [];
    public decimal Confidence { get; init; }
    public decimal? LayoutConfidence { get; init; }
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
    public bool OcrUsed { get; set; }
    public int OcrPageCount { get; set; }
    public string? OcrProvider { get; set; }
    public string? OcrProviderVersion { get; set; }
    public string ExtractionVersion { get; set; } = "ai-import-extraction-v2";
    public List<AIImportSemanticBlock> Blocks { get; init; } = [];
    public List<AIImportOcrPageSnapshot> OcrPages { get; init; } = [];
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
    Task<AIImportSourceDocument> PreflightAsync(
        AIImportSourceFile source,
        AIImportEntityType? entityHint,
        CancellationToken cancellationToken);

    Task<AIImportSourceDocument> AnalyzePreflightedAsync(
        AIImportSourceDocument document,
        AIImportEntityType? entityHint,
        CancellationToken cancellationToken);

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
        var document = await PreflightAsync(source, entityHint, cancellationToken);
        return await AnalyzePreflightedAsync(document, entityHint, cancellationToken);
    }

    public async Task<AIImportSourceDocument> PreflightAsync(
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

        return await parser.ParseAsync(source, entityHint, cancellationToken);
    }

    public async Task<AIImportSourceDocument> AnalyzePreflightedAsync(
        AIImportSourceDocument document,
        AIImportEntityType? entityHint,
        CancellationToken cancellationToken)
    {
        var needsAi = document.SourceFormat is AIImportSourceFormats.Docx or AIImportSourceFormats.Pdf
                      && !string.IsNullOrWhiteSpace(document.ExtractedText)
                      && (document.Groups.Count == 0 || document.Groups.Any(group => group.EntityType == AIImportEntityType.Unknown));
        if (needsAi && _aiExtractor != null)
            await _aiExtractor.EnrichAsync(document, entityHint, cancellationToken);
        return document;
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
            Metadata = snapshot.Metadata,
            Blocks = snapshot.Blocks,
            OcrPages = snapshot.OcrPages,
            OcrUsed = snapshot.OcrPages.Count > 0,
            OcrPageCount = snapshot.OcrPages.Count,
            OcrProvider = snapshot.OcrPages.FirstOrDefault()?.Provider,
            OcrProviderVersion = snapshot.OcrPages.FirstOrDefault()?.ProviderVersion,
            ExtractionVersion = snapshot.Metadata.TryGetValue("extractionVersion", out var version)
                ? Convert.ToString(version) ?? "ai-import-extraction-v2"
                : "ai-import-extraction-v2"
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
