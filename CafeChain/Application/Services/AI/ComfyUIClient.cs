using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AI;

public sealed class ComfyUIClient : IComfyUIClient
{
    private static readonly HashSet<string> AllowedImageTypes = ["image/png", "image/jpeg", "image/webp"];
    private readonly HttpClient _httpClient;
    private readonly ComfyUIOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ComfyUIClient> _logger;

    public ComfyUIClient(HttpClient httpClient, IOptions<ComfyUIOptions> options,
        IWebHostEnvironment environment, ILogger<ComfyUIClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<ComfyUIImageResultDTO> GenerateImageAsync(
        ComfyUIImageRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return Failure("ComfyUI đang bị tắt trong cấu hình.");
        if (string.IsNullOrWhiteSpace(_options.CheckpointName)) return Failure("ComfyUI chưa được cấu hình checkpoint.");
        if (request.GenerationMode == ComfyUIGenerationMode.ReferenceImage && request.ReferenceImageBytes.Length == 0)
            return Failure("Ảnh tham chiếu Pexels không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.PositivePrompt)) return Failure("Positive prompt không hợp lệ.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 10, 600)));
        try
        {
            string? uploadedName = null;
            if (request.GenerationMode == ComfyUIGenerationMode.ReferenceImage)
            {
                uploadedName = await UploadReferenceAsync(request, timeout.Token);
                if (uploadedName == null) return Failure("ComfyUI không nhận được ảnh tham chiếu.");
            }

            var workflow = await LoadWorkflowAsync(request.GenerationMode, timeout.Token);
            ConfigureWorkflow(workflow, request, uploadedName);
            using var promptResponse = await _httpClient.PostAsJsonAsync("/prompt", new { prompt = workflow }, timeout.Token);
            if (!promptResponse.IsSuccessStatusCode)
                return Failure($"ComfyUI trả về HTTP {(int)promptResponse.StatusCode} khi nhận workflow.");
            var promptJson = await promptResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: timeout.Token);
            var promptId = promptJson?["prompt_id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(promptId)) return Failure("ComfyUI không trả về prompt_id.");

            var outputs = await WaitForOutputsAsync(promptId, timeout.Token);
            if (outputs.Count == 0) return Failure("ComfyUI hoàn tất nhưng không có ảnh đầu ra.");
            var images = new List<ComfyUIImageOutputDTO>();
            foreach (var output in outputs.Take(Math.Clamp(request.OutputCount, 2, 4)))
            {
                var downloaded = await DownloadOutputAsync(output, timeout.Token);
                if (downloaded != null) images.Add(downloaded);
            }
            if (images.Count == 0) return Failure("Không tải được output hợp lệ từ ComfyUI.");

            _logger.LogInformation("ComfyUI generation completed. Mode={Mode} PromptId={PromptId} OutputCount={OutputCount}",
                request.GenerationMode, promptId, images.Count);
            return new ComfyUIImageResultDTO { Success = true, PromptId = promptId, Images = images };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("ComfyUI phản hồi quá thời gian cho phép.");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("ComfyUI connection failed. ErrorType={ErrorType}", ex.GetType().Name);
            return Failure("Không thể kết nối ComfyUI.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FileNotFoundException)
        {
            _logger.LogError(ex, "ComfyUI workflow failed.");
            return Failure("Workflow hoặc phản hồi ComfyUI không hợp lệ.");
        }
    }

    private async Task<string?> UploadReferenceAsync(ComfyUIImageRequestDTO request, CancellationToken cancellationToken)
    {
        var extension = request.ReferenceContentType.ToLowerInvariant() switch
        {
            "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg"
        };
        var name = $"cafechain-reference-{Guid.NewGuid():N}{extension}";
        using var content = new MultipartFormDataContent();
        using var image = new ByteArrayContent(request.ReferenceImageBytes);
        image.Headers.ContentType = new(request.ReferenceContentType);
        content.Add(image, "image", name);
        content.Add(new StringContent("input"), "type");
        content.Add(new StringContent("false"), "overwrite");
        using var response = await _httpClient.PostAsync("/upload/image", content, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var payload = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
        return payload?["name"]?.GetValue<string>() ?? name;
    }

    private async Task<JsonObject> LoadWorkflowAsync(ComfyUIGenerationMode mode, CancellationToken cancellationToken)
    {
        var configuredPath = mode == ComfyUIGenerationMode.TextToImage
            ? _options.TextToImageWorkflowPath
            : _options.WorkflowPath;
        var path = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, configuredPath));
        if (!path.StartsWith(Path.GetFullPath(_environment.ContentRootPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Workflow path nằm ngoài ContentRootPath.");
        if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy workflow ComfyUI.", path);
        return JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken))?.AsObject()
            ?? throw new JsonException("Workflow ComfyUI rỗng.");
    }

    private void ConfigureWorkflow(JsonObject workflow, ComfyUIImageRequestDTO request, string? uploadedName)
    {
        Inputs(workflow, _options.CheckpointNodeId)["ckpt_name"] = _options.CheckpointName.Trim();
        Inputs(workflow, _options.PositivePromptNodeId)["text"] = request.PositivePrompt.Trim();
        Inputs(workflow, _options.NegativePromptNodeId)["text"] = string.Join(", ",
            new[] { _options.NegativePrompt, request.NegativePrompt }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var sampler = Inputs(workflow, _options.SamplerNodeId);
        sampler["seed"] = request.Seed is > 0 ? request.Seed.Value : Random.Shared.NextInt64(1, long.MaxValue);
        sampler["steps"] = Math.Clamp(request.Steps ?? _options.Steps, 1, 100);
        sampler["cfg"] = Math.Clamp(request.Cfg ?? _options.Cfg, 1.0, 30.0);
        sampler["sampler_name"] = SanitizeSamplerValue(request.SamplerName ?? _options.SamplerName, "euler");
        sampler["scheduler"] = SanitizeSamplerValue(request.Scheduler ?? _options.Scheduler, "normal");
        if (request.GenerationMode == ComfyUIGenerationMode.ReferenceImage)
        {
            Inputs(workflow, _options.ReferenceImageNodeId)["image"] = uploadedName
                ?? throw new InvalidOperationException("Reference image is required for img2img.");
            Inputs(workflow, _options.ImageScaleNodeId)["width"] = NormalizeDimension(request.Width);
            Inputs(workflow, _options.ImageScaleNodeId)["height"] = NormalizeDimension(request.Height);
            Inputs(workflow, _options.SamplerNodeId)["denoise"] = Math.Clamp(request.Denoise, 0.40, 0.65);
            Inputs(workflow, _options.BatchNodeId)["amount"] = Math.Clamp(request.OutputCount, 2, 4);
        }
        else
        {
            Inputs(workflow, _options.TextLatentNodeId)["width"] = NormalizeDimension(request.Width);
            Inputs(workflow, _options.TextLatentNodeId)["height"] = NormalizeDimension(request.Height);
            Inputs(workflow, _options.TextLatentNodeId)["batch_size"] = Math.Clamp(request.OutputCount, 2, 4);
            Inputs(workflow, _options.SamplerNodeId)["denoise"] = 1.0;
        }
        Inputs(workflow, _options.OutputImageNodeId)["filename_prefix"] = SanitizePrefix(request.FileNamePrefix);
    }

    private static string SanitizeSamplerValue(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var clean = value.Trim().ToLowerInvariant();
        return clean.All(x => char.IsLetterOrDigit(x) || x is '_' or '-') ? clean : fallback;
    }

    private async Task<List<(string FileName, string Subfolder, string Type)>> WaitForOutputsAsync(
        string promptId, CancellationToken cancellationToken)
    {
        while (true)
        {
            using var response = await _httpClient.GetAsync($"/history/{Uri.EscapeDataString(promptId)}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var root = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
                var images = root?[promptId]?["outputs"]?[_options.OutputImageNodeId]?["images"]?.AsArray();
                if (images is { Count: > 0 })
                {
                    return images.Select(x => x?.AsObject()).Where(x => x != null)
                        .Select(x => (
                            x!["filename"]?.GetValue<string>() ?? string.Empty,
                            x["subfolder"]?.GetValue<string>() ?? string.Empty,
                            x["type"]?.GetValue<string>() ?? "output"))
                        .Where(x => !string.IsNullOrWhiteSpace(x.Item1)).ToList();
                }
            }
            await Task.Delay(Math.Clamp(_options.PollIntervalMilliseconds, 250, 5000), cancellationToken);
        }
    }

    private async Task<ComfyUIImageOutputDTO?> DownloadOutputAsync(
        (string FileName, string Subfolder, string Type) output,
        CancellationToken cancellationToken)
    {
        var url = $"/view?filename={Uri.EscapeDataString(output.FileName)}" +
                  $"&subfolder={Uri.EscapeDataString(output.Subfolder)}&type={Uri.EscapeDataString(output.Type)}";
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
        if (contentType == null || !AllowedImageTypes.Contains(contentType)) return null;
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0 || bytes.Length > Math.Clamp(_options.MaxImageBytes, 1024, 10 * 1024 * 1024)) return null;
        return new ComfyUIImageOutputDTO { Bytes = bytes, ContentType = contentType, FileName = output.FileName };
    }

    private static JsonObject Inputs(JsonObject workflow, string nodeId) => workflow[nodeId]?["inputs"]?.AsObject()
        ?? throw new JsonException($"Workflow thiếu node {nodeId} hoặc inputs.");
    private static int NormalizeDimension(int value) => Math.Clamp(value / 8 * 8, 256, 2048);
    private static string SanitizePrefix(string value)
    {
        var clean = new string((value ?? string.Empty).Where(x => char.IsLetterOrDigit(x) || x is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "cafechain_ai" : clean[..Math.Min(clean.Length, 80)];
    }
    private static ComfyUIImageResultDTO Failure(string message) => new() { ErrorMessage = message };
}
