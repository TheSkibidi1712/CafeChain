using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Infrastructure.Configurations;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace CafeChain.Application.Services.AI;

public sealed class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(HttpClient httpClient, IOptions<OllamaOptions> options, ILogger<OllamaClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OllamaResultDTO> ChatAsync(string systemPrompt, string userPayload, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var request = new OllamaChatRequestDTO
            {
                Model = _options.Model,
                Stream = false,
                Think = _options.Think,
                KeepAlive = _options.KeepAlive,
                Options = new OllamaRequestOptionsDTO
                {
                    Temperature = _options.Temperature,
                    TopP = _options.TopP,
                    TopK = _options.TopK,
                    RepeatPenalty = _options.RepeatPenalty
                },
                Messages =
                [
                    new() { Role = "system", Content = systemPrompt },
                    new() { Role = "user", Content = userPayload }
                ]
            };

            using var response = await _httpClient.PostAsJsonAsync("api/chat", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Failure($"Ollama trả về HTTP {(int)response.StatusCode}.");

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponseDTO>(cancellationToken: cancellationToken);
            var content = result?.Message?.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content))
                return Failure("Ollama trả về nội dung rỗng.");

            _logger.LogInformation("Ollama request completed. Model={Model} Feature={Feature} PayloadSize={PayloadSize} ElapsedMs={ElapsedMs}",
                _options.Model, "StructuredSuggestion", userPayload.Length, stopwatch.ElapsedMilliseconds);
            return new OllamaResultDTO { Success = true, Content = content };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("Ollama phản hồi quá thời gian cho phép.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Ollama connection failed. Model={Model} ErrorType={ErrorType}", _options.Model, ex.GetType().Name);
            return Failure("Không thể kết nối Ollama.");
        }
        catch (JsonException)
        {
            return Failure("Phản hồi HTTP của Ollama không đúng JSON.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Ollama error. Model={Model}", _options.Model);
            return Failure("Ollama hiện không khả dụng.");
        }
    }

    public async Task<OllamaHealthDTO> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new() { Message = $"Ollama trả về HTTP {(int)response.StatusCode}.", Model = _options.Model };

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var modelAvailable = document.RootElement.TryGetProperty("models", out var models)
                && models.EnumerateArray().Any(x =>
                    x.TryGetProperty("name", out var name)
                    && string.Equals(name.GetString(), _options.Model, StringComparison.OrdinalIgnoreCase));

            return new()
            {
                ServerAvailable = true,
                ModelAvailable = modelAvailable,
                Model = _options.Model,
                Message = modelAvailable ? "Ollama và model đã sẵn sàng." : "Ollama đang chạy nhưng chưa có model cấu hình."
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new() { Model = _options.Model, Message = "Kiểm tra Ollama quá thời gian." };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return new() { Model = _options.Model, Message = "Không thể kết nối Ollama." };
        }
    }

    private static OllamaResultDTO Failure(string message) => new()
    {
        Success = false,
        ErrorMessage = message,
        UsedFallback = true
    };
}
