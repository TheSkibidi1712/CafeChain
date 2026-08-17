using System.Net.Http;
using System.Text.Json;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Options;
using CafeChain.Application.Services.AI;
using CafeChain.Application.Services.AIImport;
using CafeChain.Infrastructure.Configurations;
using CafeChain.Models.AIImport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CafeChain.Tests;

[Trait("Category", "AIImportRuntimeSmoke")]
public sealed class AIImportRuntimeFixtureTests
{
    public static IEnumerable<object[]> FixtureCases() => Manifest().Cases.Select(testCase => new object[] { testCase });

    [Fact]
    public void Manifest_covers_every_committed_fixture_exactly_once()
    {
        var root = FixtureRoot();
        var supportedExtensions = new HashSet<string>([".xlsx", ".xls", ".docx", ".doc", ".docm", ".pdf"],
            StringComparer.OrdinalIgnoreCase);
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => supportedExtensions.Contains(Path.GetExtension(path)))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var declared = Manifest().Cases.Select(testCase => testCase.File.Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(126, files.Length);
        Assert.Equal(files, declared, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(declared.Length, declared.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [MemberData(nameof(FixtureCases))]
    public async Task Pipeline_fixture_matches_business_manifest(AIImportRuntimeFixtureCase testCase)
    {
        var path = Path.Combine(FixtureRoot(), testCase.File.Replace('/', Path.DirectorySeparatorChar));
        var pipeline = CreatePipeline(new OfflineOllamaClient(), ocrProvider: null);
        var result = await pipeline.AnalyzeAsync(
            new AIImportSourceFile(Path.GetFileName(path), await File.ReadAllBytesAsync(path), ContentType(path)),
            ParseHint(testCase.EntityHint), default);

        AssertFixture(testCase.ExpectedOutcome, testCase.ExpectedCodes, testCase.MinCandidates, result);
    }

    [Fact]
    public async Task Native_tesseract_processes_every_scan_fixture_using_the_manifest()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CAFECHAIN_RUN_AIIMPORT_RUNTIME_SMOKE"), "1",
                StringComparison.Ordinal)) return;

        var executable = RequiredEnvironment("CAFECHAIN_TESSERACT_PATH");
        var tessdata = RequiredEnvironment("CAFECHAIN_TESSDATA_PATH");
        var root = RepositoryRoot();
        var options = Options.Create(DefaultOptions(executable, tessdata));
        var provider = new TesseractLocalOcrProvider(new PdfiumAIImportPdfPageRenderer(),
            new TesseractProcessRunner(), options,
            new TestWebHostEnvironment { ContentRootPath = Path.Combine(root, "CafeChain") },
            NullLogger<TesseractLocalOcrProvider>.Instance);
        var health = await provider.CheckHealthAsync(default);
        Assert.True(health.Ready, health.Message);
        var ollama = CreateRuntimeOllama();
        var ollamaHealth = await ollama.CheckHealthAsync(default);
        Assert.True(ollamaHealth.ServerAvailable && ollamaHealth.ModelAvailable, ollamaHealth.Message);
        var pipeline = CreatePipeline(ollama, provider, options.Value, enableAiExtractor: false);
        var failures = new List<string>();

        foreach (var testCase in Manifest().Cases.Where(testCase => testCase.File.StartsWith("04_PDF_SCAN/", StringComparison.Ordinal)))
        {
            var path = Path.Combine(FixtureRoot(), testCase.File.Replace('/', Path.DirectorySeparatorChar));
            var result = await pipeline.AnalyzeAsync(
                new AIImportSourceFile(Path.GetFileName(path), await File.ReadAllBytesAsync(path), "application/pdf", true),
                ParseHint(testCase.EntityHint), default);

            try
            {
                AssertFixture(testCase.NativeExpectedOutcome ?? "OCR_DOCUMENT", testCase.NativeExpectedCodes,
                    testCase.NativeMinCandidates, result);
            }
            catch (Exception exception)
            {
                failures.Add($"{testCase.File}: {exception.Message}");
                continue;
            }
            if (testCase.NativeExpectedOutcome is "OCR_DOCUMENT" or "OCR_ATTEMPT")
            {
                Assert.True(result.OcrUsed, $"{testCase.File} không ghi nhận OCR.");
                Assert.True(result.OcrPageCount > 0, $"{testCase.File} không ghi nhận trang OCR.");
                Assert.All(result.OcrPages, page => Assert.All(page.Words, word =>
                {
                    Assert.InRange(word.Confidence, 0m, 1m);
                    Assert.True(word.BoundingBox.Width > 0 && word.BoundingBox.Height > 0);
                }));
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Runtime_ollama_processes_the_narrative_fallback_fixture()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CAFECHAIN_RUN_AIIMPORT_RUNTIME_SMOKE"), "1",
                StringComparison.Ordinal)) return;

        var runtime = CreateRuntimeOllama();
        var health = await runtime.CheckHealthAsync(default);
        Assert.True(health.ServerAvailable && health.ModelAvailable, health.Message);
        var ollama = new RecordingOllamaClient(runtime);
        var path = Path.Combine(FixtureRoot(), "02_DOCX", "D23_narrative_ai_fallback.docx");
        var result = await CreatePipeline(ollama, null).AnalyzeAsync(
            new AIImportSourceFile(Path.GetFileName(path), await File.ReadAllBytesAsync(path), ContentType(path)),
            AIImportEntityType.Drink, default);

        Assert.True(result.Errors.Count == 0,
            $"{SafeSummary(result)}; aiFailureType={result.Metadata.GetValueOrDefault("aiFailureType")}; {ollama.SafeSummary()}");
        Assert.True(result.UsedAI);
        Assert.Contains(result.Groups, group => group.Candidates.Count > 0);
    }

    private static void AssertFixture(
        string expectedOutcome,
        IReadOnlyCollection<string>? expectedCodes,
        int minCandidates,
        AIImportSourceDocument result)
    {
        var codes = AllCodes(result).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var code in expectedCodes ?? [])
            Assert.True(codes.Contains(code), $"Thiếu mã {code}. {SafeSummary(result)}");
        var candidates = result.Groups.Sum(group => group.Candidates.Count);

        switch (expectedOutcome.ToUpperInvariant())
        {
            case "CANDIDATES":
                Assert.True(result.Errors.Count == 0, SafeSummary(result));
                Assert.True(candidates >= Math.Max(1, minCandidates),
                    $"Cần ít nhất {Math.Max(1, minCandidates)} candidate nhưng nhận {candidates}. Codes={string.Join(',', codes)}");
                break;
            case "DOCUMENT":
                Assert.True(result.Errors.Count == 0, SafeSummary(result));
                Assert.False(string.IsNullOrWhiteSpace(result.SourceFormat));
                break;
            case "ISSUE":
                Assert.NotEmpty(expectedCodes ?? []);
                Assert.True(candidates >= minCandidates,
                    $"Cần ít nhất {minCandidates} candidate nhưng nhận {candidates}. Codes={string.Join(',', codes)}");
                break;
            case "ERROR":
            case "NEEDS_OCR":
                Assert.NotEmpty(result.Errors);
                break;
            case "OCR_DOCUMENT":
                Assert.True(result.Errors.Count == 0, SafeSummary(result));
                break;
            case "OCR_ATTEMPT":
                Assert.True(result.OcrUsed, SafeSummary(result));
                Assert.True(result.OcrPageCount > 0, SafeSummary(result));
                break;
            default:
                throw new InvalidOperationException($"ExpectedOutcome không hỗ trợ: {expectedOutcome}");
        }
    }

    private static string SafeSummary(AIImportSourceDocument result) =>
        $"errors={string.Join(',', result.Errors.Select(error => error.Code))}; " +
        $"groups={result.Groups.Count}; candidates={result.Groups.Sum(group => group.Candidates.Count)}; " +
        $"tableBlocks={result.Blocks.Count(block => block.Kind == "TABLE_ROW")}; pageBlocks={result.Blocks.Count(block => block.Kind == "PAGE_BLOCK")}; " +
        $"ocrPages={result.OcrPages.Count}; ocrWords={result.OcrPages.Sum(page => page.Words.Count)}";

    private static IEnumerable<string> AllCodes(AIImportSourceDocument result)
    {
        foreach (var issue in result.Errors.Concat(result.Warnings)) yield return issue.Code;
        foreach (var group in result.Groups)
        {
            foreach (var issue in group.Issues) yield return issue.Code;
            foreach (var candidate in group.Candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate.AIErrorCode)) yield return candidate.AIErrorCode;
                foreach (var issue in candidate.Issues) yield return issue.Code;
            }
        }
    }

    private static AIImportDocumentPipeline CreatePipeline(
        IOllamaClient ollama,
        IAIImportOcrProvider? ocrProvider,
        AIImportOptions? configuredOptions = null,
        bool enableAiExtractor = true)
    {
        var options = Options.Create(configuredOptions ?? DefaultOptions());
        var schemas = new AIImportSchemaRegistry();
        var analyzer = new AIImportRegionAnalyzer(schemas, ollama, options);
        var parsers = new IAIImportSourceParser[]
        {
            new AIImportExcelSourceParser(new AIImportExcelParser(options), analyzer, schemas),
            new AIImportDocxSourceParser(options, schemas),
            new AIImportPdfSourceParser(options, schemas, ocrProvider)
        };
        return new AIImportDocumentPipeline(parsers,
            enableAiExtractor ? new AIImportDocumentAiExtractor(ollama, schemas, options) : null);
    }

    private static OllamaClient CreateRuntimeOllama()
    {
        var settings = new OllamaOptions
        {
            BaseUrl = "http://127.0.0.1:11434/",
            Model = "qwen3:4b",
            TimeoutSeconds = 120,
            Think = false
        };
        return new OllamaClient(new HttpClient
        {
            BaseAddress = new Uri(settings.BaseUrl),
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
        }, Options.Create(settings), NullLogger<OllamaClient>.Instance);
    }

    private static AIImportOptions DefaultOptions(string executable = "tesseract", string tessdata = "Resources/OCR/tessdata") => new()
    {
        OcrExecutablePath = executable,
        OcrTessdataPath = tessdata,
        OcrLanguages = "vie+eng"
    };

    private static AIImportEntityType? ParseHint(string? value) =>
        Enum.TryParse<AIImportEntityType>(value, true, out var parsed) ? parsed : null;

    private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };

    private static AIImportRuntimeFixtureManifest Manifest()
    {
        var json = File.ReadAllText(Path.Combine(FixtureRoot(), "runtime-smoke-manifest.json"));
        return JsonSerializer.Deserialize<AIImportRuntimeFixtureManifest>(json,
                   new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidDataException("Manifest runtime smoke không hợp lệ.");
    }

    private static string FixtureRoot() => Path.Combine(RepositoryRoot(), "CafeChain.Tests", "Fixtures", "AIImport");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }

    private static string RequiredEnvironment(string key) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Thiếu biến môi trường {key} cho native smoke test.");

    public sealed class AIImportRuntimeFixtureManifest
    {
        public int SchemaVersion { get; init; }
        public List<AIImportRuntimeFixtureCase> Cases { get; init; } = [];
    }

    public sealed class AIImportRuntimeFixtureCase
    {
        public string File { get; init; } = string.Empty;
        public string? EntityHint { get; init; }
        public string ExpectedOutcome { get; init; } = "CANDIDATES";
        public List<string> ExpectedCodes { get; init; } = [];
        public int MinCandidates { get; init; } = 1;
        public string? NativeExpectedOutcome { get; init; }
        public List<string> NativeExpectedCodes { get; init; } = [];
        public int NativeMinCandidates { get; init; }

        public override string ToString() => File;
    }

    private sealed class OfflineOllamaClient : IOllamaClient
    {
        private static readonly OllamaResultDTO Offline = new()
        {
            Success = false,
            ErrorCode = "OLLAMA_OFFLINE",
            ErrorMessage = "Runtime smoke deterministic đang kiểm tra nhánh Ollama offline."
        };

        public Task<OllamaResultDTO> ChatAsync(string systemPrompt, string userPayload, CancellationToken cancellationToken = default) =>
            Task.FromResult(Offline);

        public Task<OllamaResultDTO> ChatAsync(string systemPrompt, string userPayload, string featureName,
            CancellationToken cancellationToken = default) => Task.FromResult(Offline);

        public Task<OllamaResultDTO> ChatStructuredAsync(string systemPrompt, string userPayload, object jsonSchema,
            string featureName, CancellationToken cancellationToken = default) => Task.FromResult(Offline);

        public Task<OllamaHealthDTO> CheckHealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new OllamaHealthDTO { Message = "OFFLINE", Model = "runtime-smoke-offline" });
    }

    private sealed class RecordingOllamaClient(IOllamaClient inner) : IOllamaClient
    {
        private OllamaResultDTO? _lastStructured;
        private string? _lastUserPayload;

        public Task<OllamaResultDTO> ChatAsync(string systemPrompt, string userPayload,
            CancellationToken cancellationToken = default) => inner.ChatAsync(systemPrompt, userPayload, cancellationToken);

        public Task<OllamaResultDTO> ChatAsync(string systemPrompt, string userPayload, string featureName,
            CancellationToken cancellationToken = default) =>
            inner.ChatAsync(systemPrompt, userPayload, featureName, cancellationToken);

        public async Task<OllamaResultDTO> ChatStructuredAsync(string systemPrompt, string userPayload,
            object jsonSchema, string featureName, CancellationToken cancellationToken = default)
        {
            _lastUserPayload = userPayload;
            _lastStructured = await inner.ChatStructuredAsync(
                systemPrompt, userPayload, jsonSchema, featureName, cancellationToken);
            return _lastStructured;
        }

        public Task<OllamaHealthDTO> CheckHealthAsync(CancellationToken cancellationToken = default) =>
            inner.CheckHealthAsync(cancellationToken);

        public string SafeSummary()
        {
            if (_lastStructured == null) return "ollamaCalls=0";
            if (!_lastStructured.Success)
                return $"ollamaSuccess=false; errorCode={_lastStructured.ErrorCode}";
            try
            {
                using var json = JsonDocument.Parse(_lastStructured.Content ?? string.Empty);
                var records = json.RootElement.TryGetProperty("records", out var node)
                              && node.ValueKind == JsonValueKind.Array
                    ? node.GetArrayLength()
                    : -1;
                var fieldCount = records > 0
                                 && node[0].TryGetProperty("fields", out var fields)
                                 && fields.ValueKind == JsonValueKind.Object
                    ? fields.EnumerateObject().Count()
                    : 0;
                var entity = records > 0 && node[0].TryGetProperty("entity", out var entityNode)
                    ? entityNode.GetString()
                    : null;
                var confidenceValid = records > 0
                                      && node[0].TryGetProperty("confidence", out var confidenceNode)
                                      && confidenceNode.TryGetDecimal(out var confidence)
                                      && confidence is >= 0 and <= 1;
                var fieldNames = records > 0
                                 && node[0].TryGetProperty("fields", out var namesNode)
                                 && namesNode.ValueKind == JsonValueKind.Object
                    ? string.Join(',', namesNode.EnumerateObject().Select(field => field.Name))
                    : string.Empty;
                var fieldTypesValid = records > 0
                                      && node[0].TryGetProperty("fields", out var typesNode)
                                      && typesNode.ValueKind == JsonValueKind.Object
                                      && typesNode.EnumerateObject().All(field =>
                                          field.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null);
                var evidenceExact = false;
                var valuesInsideEvidence = false;
                if (records > 0
                    && node[0].TryGetProperty("evidence", out var evidenceNode)
                    && evidenceNode.ValueKind == JsonValueKind.String
                    && JsonDocument.Parse(_lastUserPayload ?? "{}").RootElement.TryGetProperty("text", out var textNode))
                {
                    var evidence = evidenceNode.GetString() ?? string.Empty;
                    evidenceExact = (textNode.GetString() ?? string.Empty).Contains(evidence, StringComparison.Ordinal);
                    valuesInsideEvidence = node[0].TryGetProperty("fields", out var fieldNode)
                                           && fieldNode.ValueKind == JsonValueKind.Object
                                           && fieldNode.EnumerateObject().Where(field => field.Value.ValueKind == JsonValueKind.String)
                                               .All(field => evidence.Contains(field.Value.GetString() ?? string.Empty,
                                                   StringComparison.OrdinalIgnoreCase));
                }
                return $"ollamaSuccess=true; json=true; records={records}; entity={entity}; confidenceValid={confidenceValid}; " +
                       $"fields={fieldCount}[{fieldNames}]; fieldTypesValid={fieldTypesValid}; " +
                       $"evidenceExact={evidenceExact}; valuesInsideEvidence={valuesInsideEvidence}";
            }
            catch (JsonException)
            {
                return "ollamaSuccess=true; json=false";
            }
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CafeChain.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "RuntimeSmoke";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
