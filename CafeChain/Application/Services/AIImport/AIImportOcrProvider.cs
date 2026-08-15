namespace CafeChain.Application.Services.AIImport;

public sealed record AIImportOcrRequest(
    byte[] DocumentContent,
    IReadOnlyList<int> PageNumbers,
    string ContentType = "application/pdf",
    string? Languages = null,
    int? RenderDpi = null,
    int? MaxConcurrentPages = null,
    int? PageTimeoutSeconds = null,
    int? TotalTimeoutSeconds = null);

public sealed class AIImportOcrWord
{
    public string Text { get; init; } = string.Empty;
    public int Offset { get; init; }
    public int Length { get; init; }
    public decimal Confidence { get; init; }
    public AIImportBoundingBox BoundingBox { get; init; } = new();
}

public sealed class AIImportOcrPage
{
    public int PageNumber { get; init; }
    public string Text { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
    public int Rotation { get; init; }
    public string Unit { get; init; } = "pixel";
    public List<AIImportOcrWord> Words { get; init; } = [];
}

public sealed class AIImportOcrResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string? ProviderVersion { get; init; }
    public List<AIImportOcrPage> Pages { get; init; } = [];

    public static AIImportOcrResult Failure(string code, string message, string provider = "TesseractLocal") =>
        new() { ErrorCode = code, ErrorMessage = message, Provider = provider };
}

public sealed record AIImportOcrHealthResult(
    bool Ready,
    string Status,
    string Message,
    string? ProviderVersion = null,
    string? ConfigurationFingerprint = null,
    bool ExecutableAvailable = false,
    bool ModelDataReady = false);

public sealed record AIImportOcrHealthRequest(string? Languages = null);

public interface IAIImportOcrProvider
{
    Task<AIImportOcrResult> RecognizeAsync(AIImportOcrRequest request, CancellationToken cancellationToken);

    Task<AIImportOcrHealthResult> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new AIImportOcrHealthResult(
            false, "NOT_SUPPORTED", "Provider chưa hỗ trợ kiểm tra trạng thái."));

    Task<AIImportOcrHealthResult> CheckHealthAsync(
        AIImportOcrHealthRequest request,
        CancellationToken cancellationToken) => CheckHealthAsync(cancellationToken);
}
