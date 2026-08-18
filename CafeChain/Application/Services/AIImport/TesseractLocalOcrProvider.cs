using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CafeChain.Application.Options;
using Microsoft.Extensions.Options;
using PDFtoImage;

namespace CafeChain.Application.Services.AIImport;

public sealed record AIImportRenderedPdfPage(int PageNumber, byte[] PngContent, int Width, int Height);

public interface IAIImportPdfPageRenderer
{
    Task<IReadOnlyList<AIImportRenderedPdfPage>> RenderAsync(
        byte[] pdfContent,
        IReadOnlyList<int> pageNumbers,
        int renderDpi,
        CancellationToken cancellationToken);
}

public sealed class PdfiumAIImportPdfPageRenderer : IAIImportPdfPageRenderer
{
    private static readonly SemaphoreSlim PdfiumLock = new(1, 1);

    public async Task<IReadOnlyList<AIImportRenderedPdfPage>> RenderAsync(
        byte[] pdfContent,
        IReadOnlyList<int> pageNumbers,
        int renderDpi,
        CancellationToken cancellationToken)
    {
        var rendered = new List<AIImportRenderedPdfPage>(pageNumbers.Count);
        await PdfiumLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var pageNumber in pageNumbers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var output = new MemoryStream();
#pragma warning disable CA1416 // PDFtoImage supports the Windows deployment target used by CafeChain.
                Conversion.SavePng(output, pdfContent, new Index(pageNumber - 1), null,
                    new RenderOptions(Dpi: renderDpi));
#pragma warning restore CA1416
                var bytes = output.ToArray();
                var info = SixLabors.ImageSharp.Image.Identify(bytes)
                           ?? throw new InvalidDataException("Không đọc được kích thước ảnh PDF đã render.");
                rendered.Add(new AIImportRenderedPdfPage(pageNumber, bytes, info.Width, info.Height));
            }
        }
        finally
        {
            PdfiumLock.Release();
        }
        return rendered;
    }
}

public sealed record TesseractProcessRequest(
    string ExecutablePath,
    string ImagePath,
    string TessdataPath,
    string Languages,
    int TimeoutSeconds);

public sealed record TesseractProcessResult(int ExitCode, string StandardOutput, string StandardError);

public interface ITesseractProcessRunner
{
    Task<TesseractProcessResult> RecognizeAsync(
        TesseractProcessRequest request,
        CancellationToken cancellationToken);

    Task<TesseractProcessResult> GetVersionAsync(
        string executablePath,
        int timeoutSeconds,
        CancellationToken cancellationToken);

    Task<TesseractProcessResult> GetLanguagesAsync(
        string executablePath,
        string tessdataPath,
        int timeoutSeconds,
        CancellationToken cancellationToken);
}

public sealed class TesseractProcessRunner : ITesseractProcessRunner
{
    public Task<TesseractProcessResult> RecognizeAsync(
        TesseractProcessRequest request,
        CancellationToken cancellationToken) => RunAsync(
            request.ExecutablePath,
            // Enable TSV directly: a custom --tessdata-dir may contain models only and no configs/tsv file.
            [request.ImagePath, "stdout", "--tessdata-dir", request.TessdataPath, "-l", request.Languages,
                "--oem", "1", "--psm", "3", "-c", "tessedit_create_tsv=1"],
            request.TimeoutSeconds,
            cancellationToken);

    public Task<TesseractProcessResult> GetVersionAsync(
        string executablePath,
        int timeoutSeconds,
        CancellationToken cancellationToken) =>
        RunAsync(executablePath, ["--version"], timeoutSeconds, cancellationToken);

    public Task<TesseractProcessResult> GetLanguagesAsync(
        string executablePath,
        string tessdataPath,
        int timeoutSeconds,
        CancellationToken cancellationToken) =>
        RunAsync(executablePath, ["--tessdata-dir", tessdataPath, "--list-langs"], timeoutSeconds, cancellationToken);

    private static async Task<TesseractProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start()) throw new Win32Exception("Không thể khởi chạy Tesseract.");

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new TesseractProcessResult(process.ExitCode, await standardOutput, await standardError);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            throw;
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }
}

public sealed class TesseractLocalOcrProvider(
    IAIImportPdfPageRenderer renderer,
    ITesseractProcessRunner processRunner,
    IOptions<AIImportOptions> options,
    IWebHostEnvironment environment,
    ILogger<TesseractLocalOcrProvider> logger) : IAIImportOcrProvider
{
    private const string ProviderName = "TesseractLocal";
    private readonly AIImportOptions _options = options.Value;

    public async Task<AIImportOcrResult> RecognizeAsync(
        AIImportOcrRequest request,
        CancellationToken cancellationToken)
    {
        var pages = request.PageNumbers.Distinct().OrderBy(page => page).ToArray();
        if (pages.Length == 0 || pages.Any(page => page <= 0) || pages.Length > _options.OcrMaxPages)
            return Failure("PDF_OCR_VƯỢT_GIỚI_HẠN", "Danh sách trang OCR không hợp lệ hoặc vượt giới hạn cấu hình.");

        var languages = NormalizeLanguages(request.Languages ?? _options.OcrLanguages);
        var renderDpi = request.RenderDpi ?? _options.OcrRenderDpi;
        var concurrency = request.MaxConcurrentPages ?? _options.OcrMaxConcurrentPages;
        if (string.IsNullOrWhiteSpace(languages) || renderDpi is < 72 or > 600 || concurrency is < 1 or > 16)
            return Failure("OCR_CHƯA_ĐƯỢC_CẤU_HÌNH", "Cấu hình Tesseract local không hợp lệ.");

        var executable = _options.OcrExecutablePath.Trim();
        var tessdata = ResolveTessdataPath();
        if (string.IsNullOrWhiteSpace(executable) || !RequiredModelsExist(tessdata, languages))
            return Failure("OCR_CHƯA_ĐƯỢC_CẤU_HÌNH", "Thiếu Tesseract executable hoặc model ngôn ngữ OCR.");

        var pageTimeout = request.PageTimeoutSeconds ?? _options.OcrPageTimeoutSeconds;
        var totalTimeout = request.TotalTimeoutSeconds ?? _options.OcrTotalTimeoutSeconds;
        using var total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        total.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, totalTimeout)));
        var stopwatch = Stopwatch.StartNew();
        var tempRoot = Path.Combine(Path.GetTempPath(), "CafeChain", "AIImportOcr");
        var requestDirectory = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(requestDirectory);
            IReadOnlyList<AIImportRenderedPdfPage> rendered;
            try
            {
                rendered = await renderer.RenderAsync(request.DocumentContent, pages, renderDpi, total.Token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                logger.LogWarning("AI Import PDF render failed. ErrorType={ErrorType} PageCount={PageCount}",
                    exception.GetType().Name, pages.Length);
                return Failure("PDF_OCR_KHÔNG_KHẢ_DỤNG", "Không thể rasterize các trang PDF cần OCR.");
            }

            if (rendered.Count != pages.Length || !pages.SequenceEqual(rendered.Select(page => page.PageNumber)))
                return Failure("OCR_OUTPUT_KHÔNG_HỢP_LỆ", "PDF renderer trả về danh sách trang không hợp lệ.");

            using var gate = new SemaphoreSlim(concurrency, concurrency);
            var tasks = rendered.Select(async page =>
            {
                await gate.WaitAsync(total.Token);
                try
                {
                    var imagePath = Path.Combine(requestDirectory, $"page-{page.PageNumber:D5}.png");
                    await File.WriteAllBytesAsync(imagePath, page.PngContent, total.Token);
                    var process = await processRunner.RecognizeAsync(
                        new TesseractProcessRequest(executable, imagePath, tessdata, languages, pageTimeout), total.Token);
                    if (process.ExitCode != 0)
                        throw new TesseractUnavailableException();
                    return ParseTsv(page, process.StandardOutput);
                }
                finally
                {
                    gate.Release();
                }
            }).ToArray();

            var recognized = await Task.WhenAll(tasks);
            if (recognized.Any(page => page.Words.Count == 0))
                return Failure("OCR_OUTPUT_KHÔNG_HỢP_LỆ", "Tesseract không trả về word evidence hợp lệ.");

            logger.LogInformation(
                "AI Import local OCR completed. Provider={Provider} PageCount={PageCount} ElapsedMs={ElapsedMs}",
                ProviderName, pages.Length, stopwatch.ElapsedMilliseconds);
            return new AIImportOcrResult
            {
                Success = true,
                Provider = ProviderName,
                ProviderVersion = await TryReadVersionAsync(executable, cancellationToken),
                Pages = recognized.OrderBy(page => page.PageNumber).ToList()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("AI Import local OCR timed out. PageCount={PageCount} ElapsedMs={ElapsedMs}",
                pages.Length, stopwatch.ElapsedMilliseconds);
            return Failure("PDF_OCR_QUÁ_THỜI_GIAN", "OCR vượt thời gian xử lý cho phép.");
        }
        catch (TesseractUnavailableException)
        {
            return Failure("PDF_OCR_KHÔNG_KHẢ_DỤNG", "Tesseract không thể xử lý trang PDF.");
        }
        catch (Win32Exception)
        {
            return Failure("OCR_CHƯA_ĐƯỢC_CẤU_HÌNH", "Không tìm thấy Tesseract executable.");
        }
        catch (InvalidDataException)
        {
            return Failure("OCR_OUTPUT_KHÔNG_HỢP_LỆ", "Tesseract trả về TSV không hợp lệ.");
        }
        finally
        {
            DeleteRequestDirectory(requestDirectory, tempRoot);
        }
    }

    public Task<AIImportOcrHealthResult> CheckHealthAsync(CancellationToken cancellationToken) =>
        CheckHealthAsync(new AIImportOcrHealthRequest(), cancellationToken);

    public async Task<AIImportOcrHealthResult> CheckHealthAsync(
        AIImportOcrHealthRequest request,
        CancellationToken cancellationToken)
    {
        var executable = _options.OcrExecutablePath.Trim();
        var languages = NormalizeLanguages(request.Languages ?? _options.OcrLanguages);
        var tessdata = ResolveTessdataPath();
        var fingerprint = ConfigurationFingerprint(executable, tessdata, languages);
        var modelsReady = RequiredModelsExist(tessdata, languages);
        if (string.IsNullOrWhiteSpace(executable))
            return new(false, "NOT_CONFIGURED", "Chưa cấu hình Tesseract executable.",
                ConfigurationFingerprint: fingerprint, ModelDataReady: modelsReady);
        if (!modelsReady)
            return new(false, "NOT_CONFIGURED", "Thiếu model Tesseract cho một hoặc nhiều ngôn ngữ đã chọn.",
                ConfigurationFingerprint: fingerprint, ModelDataReady: false);

        try
        {
            var result = await processRunner.GetVersionAsync(executable,
                Math.Clamp(_options.OcrPageTimeoutSeconds, 1, 30), cancellationToken);
            if (result.ExitCode != 0)
                return new(false, "UNAVAILABLE", "Tesseract executable không phản hồi hợp lệ.",
                    ConfigurationFingerprint: fingerprint, ExecutableAvailable: true, ModelDataReady: true);
            var languageResult = await processRunner.GetLanguagesAsync(executable, tessdata,
                Math.Clamp(_options.OcrPageTimeoutSeconds, 1, 30), cancellationToken);
            var availableLanguages = languageResult.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var requiredLanguages = languages.Split('+',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (languageResult.ExitCode != 0 || requiredLanguages.Any(language => !availableLanguages.Contains(language)))
                return new(false, "NOT_CONFIGURED", "Tesseract không tải được đủ model ngôn ngữ đã chọn.",
                    ConfigurationFingerprint: fingerprint, ExecutableAvailable: true, ModelDataReady: false);
            var version = FirstSafeLine(result.StandardOutput, result.StandardError);
            if (string.IsNullOrWhiteSpace(version))
                return new(false, "UNAVAILABLE", "Không xác định được phiên bản Tesseract.",
                    ConfigurationFingerprint: fingerprint, ExecutableAvailable: true, ModelDataReady: true);
            return new(true, "READY", "Tesseract local và các model ngôn ngữ đã sẵn sàng.", version,
                fingerprint, true, true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "UNAVAILABLE", "Kiểm tra Tesseract vượt thời gian cho phép.",
                ConfigurationFingerprint: fingerprint, ModelDataReady: true);
        }
        catch (Win32Exception)
        {
            return new(false, "NOT_CONFIGURED", "Không tìm thấy Tesseract executable.",
                ConfigurationFingerprint: fingerprint, ModelDataReady: true);
        }
    }

    public static string NormalizeLanguages(string? languages) => languages?.Trim().ToLowerInvariant() switch
    {
        "vi" => "vie+eng",
        "en" => "eng",
        var value => value ?? string.Empty
    };

    private async Task<string?> TryReadVersionAsync(string executable, CancellationToken cancellationToken)
    {
        try
        {
            var result = await processRunner.GetVersionAsync(executable, 5, cancellationToken);
            return result.ExitCode == 0 ? FirstSafeLine(result.StandardOutput, result.StandardError) : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { return null; }
    }

    public string ResolveTessdataPath() => Path.GetFullPath(Path.IsPathRooted(_options.OcrTessdataPath)
        ? _options.OcrTessdataPath
        : Path.Combine(environment.ContentRootPath, _options.OcrTessdataPath));

    public static bool RequiredModelsExist(string tessdataPath, string languages) =>
        Directory.Exists(tessdataPath)
        && languages.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is { Length: > 0 } required
        && required.All(language => File.Exists(Path.Combine(tessdataPath, $"{language}.traineddata")));

    public static string ConfigurationFingerprint(string executable, string tessdata, string languages)
    {
        var value = $"{ProviderName}|{executable}|{tessdata}|{languages}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string? FirstSafeLine(params string[] outputs) => outputs
        .SelectMany(output => output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        .Select(line => line.Trim())
        .FirstOrDefault(line => line.StartsWith("tesseract ", StringComparison.OrdinalIgnoreCase))?
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2)
        .Aggregate((left, right) => $"{left} {right}");

    private static AIImportOcrPage ParseTsv(AIImportRenderedPdfPage page, string tsv)
    {
        using var reader = new StringReader(tsv ?? string.Empty);
        var header = reader.ReadLine();
        if (header == null || !header.StartsWith("level\tpage_num\t", StringComparison.Ordinal))
            throw new InvalidDataException("TSV header is invalid.");

        var rows = new List<TsvWord>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var columns = line.Split('\t');
            if (columns.Length < 12 || columns[0] != "5") continue;
            var text = string.Join('\t', columns.Skip(11)).Trim();
            if (text.Length == 0
                || !int.TryParse(columns[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var block)
                || !int.TryParse(columns[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var paragraph)
                || !int.TryParse(columns[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var lineNumber)
                || !int.TryParse(columns[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var left)
                || !int.TryParse(columns[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var top)
                || !int.TryParse(columns[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
                || !int.TryParse(columns[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
                || !decimal.TryParse(columns[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var confidence)
                || left < 0 || top < 0 || width <= 0 || height <= 0 || confidence < 0) continue;
            rows.Add(new TsvWord(block, paragraph, lineNumber, left, top, width, height,
                Math.Clamp(confidence / 100m, 0m, 1m), text));
        }

        var textBuilder = new StringBuilder();
        var words = new List<AIImportOcrWord>(rows.Count);
        (int Block, int Paragraph, int Line)? previousLine = null;
        foreach (var row in rows)
        {
            var currentLine = (row.Block, row.Paragraph, row.Line);
            if (textBuilder.Length > 0) textBuilder.Append(previousLine == currentLine ? ' ' : '\n');
            var offset = textBuilder.Length;
            textBuilder.Append(row.Text);
            words.Add(new AIImportOcrWord
            {
                Text = row.Text,
                Offset = offset,
                Length = row.Text.Length,
                Confidence = row.Confidence,
                BoundingBox = new AIImportBoundingBox
                {
                    X = row.Left,
                    Y = row.Top,
                    Width = row.Width,
                    Height = row.Height,
                    PageWidth = page.Width,
                    PageHeight = page.Height,
                    Unit = "PIXEL",
                    Polygon = [row.Left, row.Top, row.Left + row.Width, row.Top,
                        row.Left + row.Width, row.Top + row.Height, row.Left, row.Top + row.Height]
                }
            });
            previousLine = currentLine;
        }
        return new AIImportOcrPage
        {
            PageNumber = page.PageNumber,
            Text = textBuilder.ToString(),
            Confidence = words.Count == 0 ? 0m : words.Average(word => word.Confidence),
            Width = page.Width,
            Height = page.Height,
            Unit = "pixel",
            Words = words
        };
    }

    private static void DeleteRequestDirectory(string requestDirectory, string tempRoot)
    {
        try
        {
            var root = Path.GetFullPath(tempRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(requestDirectory);
            if (target.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target))
                Directory.Delete(target, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static AIImportOcrResult Failure(string code, string message) =>
        AIImportOcrResult.Failure(code, message, ProviderName);

    private sealed record TsvWord(
        int Block, int Paragraph, int Line, int Left, int Top, int Width, int Height,
        decimal Confidence, string Text);

    private sealed class TesseractUnavailableException : Exception;
}
