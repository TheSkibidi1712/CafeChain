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
    private static readonly HashSet<string> AllowedImageTypes =
        ["image/png", "image/jpeg", "image/webp"];

    private readonly HttpClient _httpClient;
    private readonly ComfyUIOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ComfyUIClient> _logger;

    public ComfyUIClient(
        HttpClient httpClient,
        IOptions<ComfyUIOptions> options,
        IWebHostEnvironment environment,
        ILogger<ComfyUIClient> logger)
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
        if (!_options.Enabled)
            return Failure("ComfyUI đang bị tắt trong cấu hình.");
        if (string.IsNullOrWhiteSpace(_options.CheckpointName))
            return Failure("ComfyUI chưa được cấu hình checkpoint.");
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return Failure("Prompt tạo ảnh không hợp lệ.");

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 10, 600)));
        var token = timeoutSource.Token;

        try
        {
            var workflow = await LoadWorkflowAsync(token);
            ConfigureWorkflow(workflow, request);

            using var promptResponse = await _httpClient.PostAsJsonAsync(
                "/prompt", new { prompt = workflow }, token);
            if (!promptResponse.IsSuccessStatusCode)
                return Failure($"ComfyUI trả về HTTP {(int)promptResponse.StatusCode} khi nhận workflow.");

            var promptJson = await promptResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: token);
            var promptId = promptJson?["prompt_id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(promptId))
                return Failure("ComfyUI không trả về prompt_id.");

            var output = await WaitForOutputAsync(promptId, token);
            if (output == null)
                return Failure("ComfyUI hoàn tất nhưng không có ảnh đầu ra.");

            var viewUrl = $"/view?filename={Uri.EscapeDataString(output.Value.FileName)}" +
                          $"&subfolder={Uri.EscapeDataString(output.Value.Subfolder)}" +
                          $"&type={Uri.EscapeDataString(output.Value.Type)}";
            using var imageResponse = await _httpClient.GetAsync(viewUrl, HttpCompletionOption.ResponseHeadersRead, token);
            if (!imageResponse.IsSuccessStatusCode)
                return Failure($"Không thể tải ảnh từ ComfyUI (HTTP {(int)imageResponse.StatusCode}).");

            var contentType = imageResponse.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (contentType == null || !AllowedImageTypes.Contains(contentType))
                return Failure("ComfyUI trả về định dạng ảnh không được hỗ trợ.");
            var bytes = await imageResponse.Content.ReadAsByteArrayAsync(token);
            if (bytes.Length == 0 || bytes.Length > Math.Clamp(_options.MaxImageBytes, 1024, 10 * 1024 * 1024))
                return Failure("Ảnh ComfyUI rỗng hoặc vượt quá kích thước cho phép.");

            _logger.LogInformation("ComfyUI image generated. Bytes={Bytes} ContentType={ContentType}", bytes.Length, contentType);
            return new ComfyUIImageResultDTO
            {
                Success = true,
                Bytes = bytes,
                ContentType = contentType,
                FileName = output.Value.FileName
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("ComfyUI phản hồi quá thời gian cho phép.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("ComfyUI connection failed. ErrorType={ErrorType}", ex.GetType().Name);
            return Failure("Không thể kết nối ComfyUI.");
        }
        catch (JsonException)
        {
            return Failure("Workflow hoặc phản hồi ComfyUI không đúng JSON.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected ComfyUI error.");
            return Failure("ComfyUI hiện không khả dụng.");
        }
    }

    private async Task<JsonObject> LoadWorkflowAsync(CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _options.WorkflowPath));
        if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy workflow ComfyUI.", path);
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonNode.Parse(json)?.AsObject() ?? throw new JsonException("Workflow ComfyUI rỗng.");
    }

    private void ConfigureWorkflow(JsonObject workflow, ComfyUIImageRequestDTO request)
    {
        Inputs(workflow, _options.CheckpointNodeId)["ckpt_name"] = _options.CheckpointName.Trim();
        Inputs(workflow, _options.PositivePromptNodeId)["text"] = BuildPositivePrompt(request.Prompt);
        Inputs(workflow, _options.NegativePromptNodeId)["text"] = _options.NegativePrompt;
        Inputs(workflow, _options.SamplerNodeId)["seed"] = Random.Shared.NextInt64(1, long.MaxValue);
        Inputs(workflow, _options.LatentNodeId)["width"] = NormalizeDimension(_options.Width);
        Inputs(workflow, _options.LatentNodeId)["height"] = NormalizeDimension(_options.Height);
    }

    private async Task<(string FileName, string Subfolder, string Type)?> WaitForOutputAsync(
        string promptId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var response = await _httpClient.GetAsync($"/history/{Uri.EscapeDataString(promptId)}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var root = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
                var images = root?[promptId]?["outputs"]?[_options.OutputImageNodeId]?["images"]?.AsArray();
                var image = images?.FirstOrDefault()?.AsObject();
                var fileName = image?["filename"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    return (
                        fileName,
                        image?["subfolder"]?.GetValue<string>() ?? string.Empty,
                        image?["type"]?.GetValue<string>() ?? "output");
                }
            }
            await Task.Delay(Math.Clamp(_options.PollIntervalMilliseconds, 250, 5000), cancellationToken);
        }
    }

    private static JsonObject Inputs(JsonObject workflow, string nodeId) =>
        workflow[nodeId]?["inputs"]?.AsObject()
        ?? throw new JsonException($"Workflow thiếu node {nodeId} hoặc inputs.");

    private static int NormalizeDimension(int value) => Math.Clamp(value / 8 * 8, 256, 2048);

    private static string BuildPositivePrompt(string prompt) =>
        $"professional square product photography of {prompt.Trim()}, centered beverage or topping, clean studio background, appetizing, realistic, high detail, no people, no text, no logo, no watermark";

    private static ComfyUIImageResultDTO Failure(string message) => new() { ErrorMessage = message };
}
