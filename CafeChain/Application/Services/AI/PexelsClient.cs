using System.Text.Json;
using System.Text.Json.Serialization;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AI;

public sealed class PexelsClient : IPexelsClient
{
    private static readonly HashSet<string> AllowedImageTypes =
        ["image/jpeg", "image/png", "image/webp"];

    private readonly HttpClient _httpClient;
    private readonly PexelsOptions _options;
    private readonly ILogger<PexelsClient> _logger;

    public PexelsClient(
        HttpClient httpClient,
        IOptions<PexelsOptions> options,
        ILogger<PexelsClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PexelsImageResultDTO> FindImageAsync(
        PexelsImageRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return Failure("Pexels đang bị tắt trong cấu hình.");
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return Failure("Pexels chưa được cấu hình API key.");

        var query = request.Query?.Trim() ?? string.Empty;
        if (query.Length is < 2 or > 500)
            return Failure("Từ khóa tìm ảnh Pexels không hợp lệ.");

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 3, 120)));
        var token = timeoutSource.Token;

        try
        {
            var perPage = Math.Clamp(_options.PerPage, 1, 40);
            var searchUrl = $"/v1/search?query={Uri.EscapeDataString(query)}&orientation=square&per_page={perPage}";
            using var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            searchRequest.Headers.TryAddWithoutValidation("Authorization", _options.ApiKey.Trim());
            using var searchResponse = await _httpClient.SendAsync(searchRequest, token);
            if (!searchResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Pexels search failed. StatusCode={StatusCode}", (int)searchResponse.StatusCode);
                return Failure($"Pexels trả về HTTP {(int)searchResponse.StatusCode}.");
            }

            var payload = await searchResponse.Content.ReadFromJsonAsync<PexelsSearchResponse>(cancellationToken: token);
            var excluded = request.ExcludedPhotoIds?.ToHashSet() ?? [];
            var candidates = payload?.Photos
                .Where(x => !excluded.Contains(x.Id) && IsAllowedImageUrl(x.Source.Large2X ?? x.Source.Large))
                .ToList() ?? [];
            if (candidates.Count == 0)
                return Failure("Pexels không tìm thấy ảnh phù hợp chưa được sử dụng.");

            var photo = candidates[Random.Shared.Next(candidates.Count)];
            var imageUrl = photo.Source.Large2X ?? photo.Source.Large!;
            using var imageResponse = await _httpClient.GetAsync(
                imageUrl, HttpCompletionOption.ResponseHeadersRead, token);
            if (!imageResponse.IsSuccessStatusCode)
                return Failure($"Không thể tải ảnh Pexels (HTTP {(int)imageResponse.StatusCode}).");

            var contentType = imageResponse.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (contentType == null || !AllowedImageTypes.Contains(contentType))
                return Failure("Pexels trả về định dạng ảnh không được hỗ trợ.");

            var maxBytes = Math.Clamp(_options.MaxImageBytes, 1024, 10 * 1024 * 1024);
            if (imageResponse.Content.Headers.ContentLength > maxBytes)
                return Failure("Ảnh Pexels vượt quá kích thước cho phép.");

            await using var source = await imageResponse.Content.ReadAsStreamAsync(token);
            using var destination = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var read = await source.ReadAsync(buffer, token);
                if (read == 0) break;
                if (destination.Length + read > maxBytes)
                    return Failure("Ảnh Pexels vượt quá kích thước cho phép.");
                await destination.WriteAsync(buffer.AsMemory(0, read), token);
            }
            if (destination.Length == 0)
                return Failure("Ảnh Pexels rỗng.");

            _logger.LogInformation("Pexels image downloaded. PhotoId={PhotoId} Bytes={Bytes}", photo.Id, destination.Length);
            return new PexelsImageResultDTO
            {
                Success = true,
                Bytes = destination.ToArray(),
                ContentType = contentType,
                FileName = $"pexels-{photo.Id}{GetExtension(contentType)}",
                PhotoId = photo.Id,
                PhotoUrl = photo.Url,
                Photographer = photo.Photographer,
                PhotographerUrl = photo.PhotographerUrl
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("Pexels phản hồi quá thời gian cho phép.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Pexels connection failed. ErrorType={ErrorType}", ex.GetType().Name);
            return Failure("Không thể kết nối Pexels.");
        }
        catch (JsonException)
        {
            return Failure("Phản hồi Pexels không đúng JSON.");
        }
    }

    private static bool IsAllowedImageUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, "images.pexels.com", StringComparison.OrdinalIgnoreCase);

    private static string GetExtension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg"
    };

    private static PexelsImageResultDTO Failure(string message) => new() { ErrorMessage = message };

    private sealed class PexelsSearchResponse
    {
        [JsonPropertyName("photos")] public List<PexelsPhoto> Photos { get; set; } = [];
    }

    private sealed class PexelsPhoto
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("photographer")] public string? Photographer { get; set; }
        [JsonPropertyName("photographer_url")] public string? PhotographerUrl { get; set; }
        [JsonPropertyName("src")] public PexelsPhotoSource Source { get; set; } = new();
    }

    private sealed class PexelsPhotoSource
    {
        [JsonPropertyName("large2x")] public string? Large2X { get; set; }
        [JsonPropertyName("large")] public string? Large { get; set; }
    }
}
