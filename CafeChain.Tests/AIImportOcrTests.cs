using System.Diagnostics;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Options;
using CafeChain.Application.Services.AIImport;
using CafeChain.Data;
using CafeChain.Models.AIImport;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace CafeChain.Tests;

public sealed class AIImportOcrTests
{
    [Fact]
    public async Task Saving_runtime_settings_immediately_refreshes_ready_health_state()
    {
        using var workspace = new OcrWorkspace();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(dbOptions);
        await CreateRuntimeSettingsTablesAsync(db);
        var options = Options.Create(new AIImportOptions
        {
            OcrProvider = "TesseractLocal",
            OcrExecutablePath = "tesseract-test",
            OcrTessdataPath = workspace.Root,
            OcrLanguages = "vie+eng"
        });
        var provider = workspace.Provider(new FakeRenderer([]), new FakeProcessRunner(Tsv()));
        IAIImportOcrRuntimeSettings settings = new AIImportOcrRuntimeSettings(
            db, options, provider, new TestWebHostEnvironment { ContentRootPath = workspace.Root });

        var state = await settings.UpdateAsync(new AIImportOcrRuntimeUpdate(
                "vie+eng", 0.85m, 200, 50, 20_000_000, 200_000_000, 45, 180, 1),
            new AdminActorContext { AccountId = 42, StaffId = 7 }, default);

        Assert.True(state.InfrastructureConfigured);
        Assert.True(state.ProviderReady);
        Assert.True(state.EffectiveEnabled);
        Assert.Equal("READY", state.HealthStatus);
        Assert.Equal("tesseract 5.5.0", state.ProviderVersion);
        Assert.True(state.ExecutableAvailable);
        Assert.True(state.ModelDataReady);
        Assert.NotNull(state.LastHealthCheckedAtUtc);
    }

    [Fact]
    public async Task Runtime_settings_reject_languages_outside_the_supported_select_options()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new AppDbContext(dbOptions);
        IAIImportOcrRuntimeSettings settings = new AIImportOcrRuntimeSettings(
            db,
            Options.Create(new AIImportOptions()),
            new FakeOcrProvider(_ => throw new InvalidOperationException("Provider must not be called.")),
            new TestWebHostEnvironment { ContentRootPath = AppContext.BaseDirectory });

        await Assert.ThrowsAsync<ArgumentException>(() => settings.UpdateAsync(
            new AIImportOcrRuntimeUpdate(
                "fra", 0.85m, 200, 50, 20_000_000, 200_000_000, 45, 180, 1),
            new AdminActorContext { AccountId = 42 }, default));
    }

    [Fact]
    public async Task Saving_unhealthy_runtime_settings_persists_values_but_keeps_effective_ocr_disabled()
    {
        using var workspace = new OcrWorkspace();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(dbOptions);
        await CreateRuntimeSettingsTablesAsync(db);
        var options = Options.Create(new AIImportOptions
        {
            OcrProvider = "TesseractLocal",
            OcrExecutablePath = "tesseract-test",
            OcrTessdataPath = workspace.Root,
            OcrLanguages = "vie+eng"
        });
        var fingerprint = TesseractLocalOcrProvider.ConfigurationFingerprint(
            "tesseract-test", workspace.Root, "vie");
        var provider = new UnhealthyOcrProvider(new AIImportOcrHealthResult(
            false, "UNAVAILABLE", "Tesseract process failed.", "tesseract 5.5.0", fingerprint,
            ExecutableAvailable: true, ModelDataReady: true));
        IAIImportOcrRuntimeSettings settings = new AIImportOcrRuntimeSettings(
            db, options, provider, new TestWebHostEnvironment { ContentRootPath = workspace.Root });

        var saved = await settings.UpdateAsync(new AIImportOcrRuntimeUpdate(
                "vie", 0.77m, 240, 40, 18_000_000, 180_000_000, 35, 150, 2),
            new AdminActorContext { AccountId = 42 }, default);
        var reloaded = await settings.GetAsync(default);

        Assert.Equal("vie", reloaded.Languages);
        Assert.Equal(0.77m, reloaded.ReviewConfidenceThreshold);
        Assert.Equal("UNAVAILABLE", saved.HealthStatus);
        Assert.False(saved.ProviderReady);
        Assert.False(saved.EffectiveEnabled);
        Assert.NotNull(saved.LastHealthCheckedAtUtc);
    }

    [Fact]
    public async Task Ocr_disabled_preserves_pdf_needs_ocr_and_never_calls_provider()
    {
        var provider = new FakeOcrProvider(_ => throw new InvalidOperationException("Provider must not be called."));
        var parser = new AIImportPdfSourceParser(Options.Create(new AIImportOptions()),
            new AIImportSchemaRegistry(), provider);

        var result = await parser.ParseAsync(new AIImportSourceFile("scan.pdf", ImageOnlyPdf()), null, default);

        Assert.Contains(result.Errors, error => error.Code == "PDF_CẦN_OCR");
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task Text_pdf_never_renders_or_calls_ocr()
    {
        var provider = new FakeOcrProvider(_ => throw new InvalidOperationException("Provider must not be called."));
        var parser = new AIImportPdfSourceParser(Options.Create(new AIImportOptions()),
            new AIImportSchemaRegistry(), provider);

        var result = await parser.ParseAsync(new AIImportSourceFile("text.pdf", TextPdf()), null, default);

        Assert.DoesNotContain(result.Errors, error => error.Code == "PDF_CẦN_OCR");
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task Image_pdf_uses_existing_pipeline_and_exposes_low_confidence_field_provenance()
    {
        var provider = new FakeOcrProvider(request => Task.FromResult(new AIImportOcrResult
        {
            Success = true,
            Provider = "FakeOCR",
            ProviderVersion = "test-v1",
            Pages = [OcrPage(request.PageNumbers.Single())]
        }));
        var parser = new AIImportPdfSourceParser(Options.Create(new AIImportOptions()),
            new AIImportSchemaRegistry(), provider);

        var result = await parser.ParseAsync(
            new AIImportSourceFile("scan.pdf", ImageOnlyPdf(), "application/pdf", UseOcr: true),
            AIImportEntityType.Category,
            default);

        Assert.Empty(result.Errors);
        Assert.True(result.OcrUsed);
        var request = Assert.Single(provider.Requests);
        Assert.Equal([1], request.PageNumbers);
        Assert.Equal("vie+eng", request.Languages);
        Assert.Equal(200, request.RenderDpi);
        Assert.Equal(1, request.MaxConcurrentPages);
        var group = Assert.Single(result.Groups);
        Assert.Equal(AIImportExtractionModes.PdfOcrDeterministic, group.ExtractionMode);
        var candidate = Assert.Single(group.Candidates);
        Assert.Equal("CAT_OCR", candidate.MappedData["CategoryCode"]);
        var evidence = candidate.FieldEvidence["CategoryCode"];
        Assert.Equal(AIImportSourceKinds.Ocr, evidence.SourceKind);
        Assert.Equal(0.72m, evidence.OcrConfidence);
        Assert.NotEmpty(evidence.Locator.BoundingBox!.Polygon);
        Assert.Contains(candidate.Issues, issue => issue.Code == "OCR_CONFIDENCE_THẤP");
    }

    [Fact]
    public async Task Ocr_tsv_text_offsets_preserve_lines_when_diacritics_shift_word_geometry()
    {
        var provider = new FakeOcrProvider(request => Task.FromResult(new AIImportOcrResult
        {
            Success = true,
            Provider = "FakeOCR",
            Pages = [OcrPageWithOffsets(request.PageNumbers.Single())]
        }));
        var parser = new AIImportPdfSourceParser(Options.Create(new AIImportOptions()),
            new AIImportSchemaRegistry(), provider);

        var result = await parser.ParseAsync(
            new AIImportSourceFile("scan.pdf", ImageOnlyPdf(), "application/pdf", UseOcr: true),
            AIImportEntityType.Category,
            default);

        Assert.Empty(result.Errors);
        var candidate = Assert.Single(Assert.Single(result.Groups).Candidates);
        Assert.Equal("CAT_UTF8", candidate.MappedData["CategoryCode"]);
        Assert.Equal("Danh mục tiếng Việt", candidate.MappedData["Name"]);
    }

    [Fact]
    public async Task Ocr_provider_failure_is_mapped_without_creating_second_import_path()
    {
        var provider = new FakeOcrProvider(_ => Task.FromResult(
            AIImportOcrResult.Failure("PDF_OCR_QUÁ_THỜI_GIAN", "timeout")));
        var parser = new AIImportPdfSourceParser(Options.Create(new AIImportOptions()),
            new AIImportSchemaRegistry(), provider);

        var result = await parser.ParseAsync(
            new AIImportSourceFile("scan.pdf", ImageOnlyPdf(), UseOcr: true), null, default);

        Assert.Contains(result.Errors, error => error.Code == "PDF_OCR_QUÁ_THỜI_GIAN");
        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task Ocr_resource_limit_fails_before_provider_call_without_silent_truncation()
    {
        var provider = new FakeOcrProvider(_ => throw new InvalidOperationException("Provider must not be called."));
        var parser = new AIImportPdfSourceParser(Options.Create(new AIImportOptions
            {
                OcrMaxRenderedPixelsPerPage = 1,
                OcrMaxTotalRenderedPixels = 1
            }),
            new AIImportSchemaRegistry(), provider);

        var result = await parser.ParseAsync(
            new AIImportSourceFile("scan.pdf", ImageOnlyPdf(), UseOcr: true), null, default);

        Assert.Contains(result.Errors, error => error.Code == "PDF_OCR_VƯỢT_GIỚI_HẠN");
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task Cancellation_is_propagated_through_pdf_pipeline_to_ocr_provider()
    {
        var provider = new BlockingOcrProvider();
        var parser = new AIImportPdfSourceParser(Options.Create(new AIImportOptions()),
            new AIImportSchemaRegistry(), provider);
        using var cancellation = new CancellationTokenSource();

        var parse = parser.ParseAsync(
            new AIImportSourceFile("scan.pdf", ImageOnlyPdf(), UseOcr: true), null, cancellation.Token);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => parse);
    }

    [Fact]
    public async Task Local_provider_parses_word_tsv_with_offsets_confidence_and_pixel_box()
    {
        using var workspace = new OcrWorkspace();
        var renderer = new FakeRenderer([
            new AIImportRenderedPdfPage(2, [1, 2, 3], 1200, 1600)
        ]);
        var runner = new FakeProcessRunner(Tsv(
            "5\t1\t1\t1\t1\t1\t10\t20\t80\t25\t72.5\tXin",
            "5\t1\t1\t1\t1\t2\t95\t20\t100\t25\t98\tchào",
            "5\t1\t1\t1\t2\t1\t10\t60\t70\t25\t88\tViệt"));
        var provider = workspace.Provider(renderer, runner);

        var result = await provider.RecognizeAsync(new AIImportOcrRequest(
            [1, 2, 3], [2], Languages: "vie+eng", RenderDpi: 240, MaxConcurrentPages: 2), default);

        Assert.True(result.Success);
        var page = Assert.Single(result.Pages);
        Assert.Equal("Xin chào\nViệt", page.Text);
        Assert.Equal(1200, page.Width);
        var words = page.Words;
        Assert.Equal(3, words.Count);
        Assert.Equal(0, words[0].Offset);
        Assert.Equal(4, words[1].Offset);
        Assert.Equal(72.5m / 100m, words[0].Confidence);
        Assert.Equal([10d, 20d, 90d, 20d, 90d, 45d, 10d, 45d], words[0].BoundingBox.Polygon);
        Assert.Equal(240, renderer.RenderDpi);
        Assert.Equal([2], renderer.PageNumbers);
        Assert.Equal("vie+eng", Assert.Single(runner.OcrRequests).Languages);
    }

    [Fact]
    public async Task Local_provider_renders_only_requested_pages()
    {
        var renderer = new PdfiumAIImportPdfPageRenderer();

        var pages = await renderer.RenderAsync(TwoPageImagePdf(), [2], 100, default);

        var page = Assert.Single(pages);
        Assert.Equal(2, page.PageNumber);
        Assert.NotEmpty(page.PngContent);
        Assert.True(page.Width > 0);
        Assert.True(page.Height > 0);
    }

    [Fact]
    public async Task Missing_language_model_returns_typed_not_configured_error()
    {
        using var workspace = new OcrWorkspace(createModels: false);
        var provider = workspace.Provider(new FakeRenderer([]), new FakeProcessRunner(Tsv()));

        var result = await provider.RecognizeAsync(new AIImportOcrRequest([1], [1]), default);
        var health = await provider.CheckHealthAsync(default);

        Assert.Equal("OCR_CHƯA_ĐƯỢC_CẤU_HÌNH", result.ErrorCode);
        Assert.False(health.Ready);
        Assert.False(health.ModelDataReady);
        Assert.DoesNotContain(workspace.Root, health.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Process_crash_and_empty_tsv_are_mapped_to_typed_errors()
    {
        using var workspace = new OcrWorkspace();
        var page = new AIImportRenderedPdfPage(1, [1], 100, 100);
        var crashed = workspace.Provider(new FakeRenderer([page]), new FakeProcessRunner(string.Empty, exitCode: 1));
        var empty = workspace.Provider(new FakeRenderer([page]), new FakeProcessRunner(Tsv()));

        var crashResult = await crashed.RecognizeAsync(new AIImportOcrRequest([1], [1]), default);
        var emptyResult = await empty.RecognizeAsync(new AIImportOcrRequest([1], [1]), default);

        Assert.Equal("PDF_OCR_KHÔNG_KHẢ_DỤNG", crashResult.ErrorCode);
        Assert.Equal("OCR_OUTPUT_KHÔNG_HỢP_LỆ", emptyResult.ErrorCode);
    }

    [Fact]
    public async Task Health_is_ready_only_when_executable_and_both_models_are_available()
    {
        using var workspace = new OcrWorkspace();
        var runner = new FakeProcessRunner(Tsv());
        var provider = workspace.Provider(new FakeRenderer([]), runner);

        var health = await provider.CheckHealthAsync(default);

        Assert.True(health.Ready);
        Assert.Equal("READY", health.Status);
        Assert.True(health.ExecutableAvailable);
        Assert.True(health.ModelDataReady);
        Assert.Equal("tesseract 5.5.0", health.ProviderVersion);
        Assert.False(string.IsNullOrWhiteSpace(health.ConfigurationFingerprint));
        Assert.DoesNotContain(workspace.Root, health.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ocr_contract_contains_no_cloud_provider_or_credential_configuration()
    {
        var optionNames = typeof(AIImportOptions).GetProperties().Select(property => property.Name).ToHashSet();
        var credentialProperty = string.Concat("Ocr", "Api", "Key");
        var endpointProperty = string.Concat("Ocr", "End", "point");
        var systemSwitchProperty = string.Concat("Ocr", "Enabled");
        var cloudProvider = string.Concat("Azure", "Document", "Intelligence");
        var repositoryRoot = FindRepositoryRoot();
        var appsettings = File.ReadAllText(Path.Combine(repositoryRoot, "CafeChain", "appsettings.json"));
        var view = File.ReadAllText(Path.Combine(repositoryRoot, "CafeChain", "Areas", "Admin", "Views",
            "AdminSetting", "Partials", "_OcrSettings.cshtml"));

        Assert.DoesNotContain(credentialProperty, optionNames);
        Assert.DoesNotContain(endpointProperty, optionNames);
        Assert.DoesNotContain(systemSwitchProperty, optionNames);
        Assert.DoesNotContain(cloudProvider, appsettings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(credentialProperty, appsettings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(systemSwitchProperty, appsettings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(string.Concat("API", " key"), view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bật OCR toàn hệ thống", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Tesseract_native_integration_does_not_require_external_tsv_config()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CAFECHAIN_RUN_TESSERACT_INTEGRATION"), "1",
                StringComparison.Ordinal)) return;

        var repositoryRoot = FindRepositoryRoot();
        var contentRoot = Path.Combine(repositoryRoot, "CafeChain");
        var sourceTessdata = Environment.GetEnvironmentVariable("CAFECHAIN_TESSDATA_PATH")
                             ?? Path.Combine(contentRoot, "Resources", "OCR", "tessdata");
        using var workspace = new OcrWorkspace(createModels: false);
        File.Copy(Path.Combine(sourceTessdata, "vie.traineddata"), Path.Combine(workspace.Root, "vie.traineddata"));
        File.Copy(Path.Combine(sourceTessdata, "eng.traineddata"), Path.Combine(workspace.Root, "eng.traineddata"));
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "configs")));
        var options = Options.Create(new AIImportOptions
        {
            OcrExecutablePath = Environment.GetEnvironmentVariable("CAFECHAIN_TESSERACT_PATH") ?? "tesseract",
            OcrTessdataPath = workspace.Root,
            OcrLanguages = "vie+eng"
        });
        var provider = new TesseractLocalOcrProvider(new PdfiumAIImportPdfPageRenderer(),
            new TesseractProcessRunner(), options,
            new TestWebHostEnvironment { ContentRootPath = contentRoot },
            NullLogger<TesseractLocalOcrProvider>.Instance);

        var health = await provider.CheckHealthAsync(default);
        var result = await provider.RecognizeAsync(new AIImportOcrRequest(TextPdf(), [1]), default);

        Assert.True(health.Ready, health.Message);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotEmpty(Assert.Single(result.Pages).Words);
    }

    private static string Tsv(params string[] rows) =>
        "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n"
        + string.Join('\n', rows);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }

    private static Task CreateRuntimeSettingsTablesAsync(AppDbContext db) =>
        db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE SystemSettings (
                SettingId INTEGER PRIMARY KEY AUTOINCREMENT,
                SettingKey TEXT NOT NULL UNIQUE,
                SettingValue TEXT NOT NULL,
                Description TEXT NULL
            );
            CREATE TABLE AuditLogs (
                AuditLogId INTEGER PRIMARY KEY AUTOINCREMENT,
                TableName TEXT NOT NULL,
                RecordId INTEGER NOT NULL,
                Action TEXT NOT NULL,
                OldData TEXT NULL,
                NewData TEXT NULL,
                UserId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            """);

    private static byte[] ImageOnlyPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container => container.Page(page =>
            page.Content().Height(300).Background("#111111"))).GeneratePdf();
    }

    private static byte[] TwoPageImagePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page => page.Content().Height(200).Background("#111111"));
            container.Page(page => page.Content().Height(300).Background("#222222"));
        }).GeneratePdf();
    }

    private static byte[] TextPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container => container.Page(page =>
            page.Content().Text("Mã danh mục: CAT_TEXT\nTên danh mục: Danh mục chữ"))).GeneratePdf();
    }

    private static AIImportOcrPage OcrPage(int pageNumber) => new()
    {
        PageNumber = pageNumber,
        Text = "Mã danh mục: CAT_OCR Tên danh mục: Danh mục OCR",
        Width = 600,
        Height = 800,
        Unit = "pixel",
        Confidence = 0.72m,
        Words =
        [
            Word("Mã danh mục:", 20, 20, 100, 12), Word("CAT_OCR", 125, 20, 70, 12),
            Word("Tên danh mục:", 20, 50, 110, 12), Word("Danh mục OCR", 135, 50, 100, 12)
        ]
    };

    private static AIImportOcrPage OcrPageWithOffsets(int pageNumber)
    {
        const string text = "Mã danh mục: CAT_UTF8\nTên danh mục: Danh mục tiếng Việt";
        var tokens = new[] { "Mã", "danh", "mục:", "CAT_UTF8", "Tên", "danh", "mục:", "Danh", "mục", "tiếng", "Việt" };
        var words = new List<AIImportOcrWord>();
        var searchFrom = 0;
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            var offset = text.IndexOf(token, searchFrom, StringComparison.Ordinal);
            var secondLine = offset > text.IndexOf('\n');
            words.Add(new AIImportOcrWord
            {
                Text = token,
                Offset = offset,
                Length = token.Length,
                Confidence = 0.95m,
                BoundingBox = new AIImportBoundingBox
                {
                    X = 20 + (secondLine ? index - 4 : index) * 55,
                    Y = (secondLine ? 80 : 20) + (index % 2 == 0 ? 7 : 0),
                    Width = 48,
                    Height = 12,
                    PageWidth = 600,
                    PageHeight = 800,
                    Unit = "PIXEL",
                    Polygon = [0, 0, 1, 0, 1, 1, 0, 1]
                }
            });
            searchFrom = offset + token.Length;
        }
        return new AIImportOcrPage
        {
            PageNumber = pageNumber,
            Text = text,
            Width = 600,
            Height = 800,
            Unit = "pixel",
            Confidence = 0.95m,
            Words = words
        };
    }

    private static AIImportOcrWord Word(string text, double x, double y, double width, double height) => new()
    {
        Text = text,
        Confidence = 0.72m,
        BoundingBox = new AIImportBoundingBox
        {
            X = x, Y = y, Width = width, Height = height, PageWidth = 600, PageHeight = 800,
            Unit = "PIXEL", Polygon = [x, y, x + width, y, x + width, y + height, x, y + height]
        }
    };

    private sealed class FakeOcrProvider(
        Func<AIImportOcrRequest, Task<AIImportOcrResult>> recognize) : IAIImportOcrProvider
    {
        public int CallCount { get; private set; }
        public List<AIImportOcrRequest> Requests { get; } = [];

        public Task<AIImportOcrResult> RecognizeAsync(
            AIImportOcrRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Requests.Add(request);
            cancellationToken.ThrowIfCancellationRequested();
            return recognize(request);
        }
    }

    private sealed class BlockingOcrProvider : IAIImportOcrProvider
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<AIImportOcrResult> RecognizeAsync(
            AIImportOcrRequest request,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        }
    }

    private sealed class UnhealthyOcrProvider(AIImportOcrHealthResult health) : IAIImportOcrProvider
    {
        public Task<AIImportOcrResult> RecognizeAsync(
            AIImportOcrRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Provider recognition must not be called during health check.");

        public Task<AIImportOcrHealthResult> CheckHealthAsync(
            AIImportOcrHealthRequest request,
            CancellationToken cancellationToken) => Task.FromResult(health);
    }

    private sealed class FakeRenderer(IReadOnlyList<AIImportRenderedPdfPage> pages) : IAIImportPdfPageRenderer
    {
        public IReadOnlyList<int> PageNumbers { get; private set; } = [];
        public int RenderDpi { get; private set; }

        public Task<IReadOnlyList<AIImportRenderedPdfPage>> RenderAsync(
            byte[] pdfContent,
            IReadOnlyList<int> pageNumbers,
            int renderDpi,
            CancellationToken cancellationToken)
        {
            PageNumbers = pageNumbers;
            RenderDpi = renderDpi;
            return Task.FromResult(pages);
        }
    }

    private sealed class FakeProcessRunner(string tsv, int exitCode = 0) : ITesseractProcessRunner
    {
        public List<TesseractProcessRequest> OcrRequests { get; } = [];

        public Task<TesseractProcessResult> RecognizeAsync(
            TesseractProcessRequest request,
            CancellationToken cancellationToken)
        {
            OcrRequests.Add(request);
            return Task.FromResult(new TesseractProcessResult(exitCode, tsv, exitCode == 0 ? string.Empty : "failed"));
        }

        public Task<TesseractProcessResult> GetVersionAsync(
            string executablePath,
            int timeoutSeconds,
            CancellationToken cancellationToken) =>
            Task.FromResult(new TesseractProcessResult(0, "tesseract 5.5.0\n leptonica", string.Empty));

        public Task<TesseractProcessResult> GetLanguagesAsync(
            string executablePath,
            string tessdataPath,
            int timeoutSeconds,
            CancellationToken cancellationToken) =>
            Task.FromResult(new TesseractProcessResult(0, "List of available languages (2):\neng\nvie", string.Empty));
    }

    private sealed class OcrWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "CafeChainTests", Guid.NewGuid().ToString("N"));

        public OcrWorkspace(bool createModels = true)
        {
            Directory.CreateDirectory(Root);
            if (!createModels) return;
            File.WriteAllBytes(Path.Combine(Root, "vie.traineddata"), [1]);
            File.WriteAllBytes(Path.Combine(Root, "eng.traineddata"), [1]);
        }

        public TesseractLocalOcrProvider Provider(IAIImportPdfPageRenderer renderer, ITesseractProcessRunner runner)
        {
            var options = Options.Create(new AIImportOptions
            {
                OcrExecutablePath = "tesseract-test",
                OcrTessdataPath = Root,
                OcrLanguages = "vie+eng"
            });
            return new TesseractLocalOcrProvider(renderer, runner, options,
                new TestWebHostEnvironment { ContentRootPath = Root },
                NullLogger<TesseractLocalOcrProvider>.Instance);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CafeChain.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
