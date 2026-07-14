using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AI;

public sealed class PexelsClient : IPexelsClient
{
    private static readonly HashSet<string> AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];
    private readonly HttpClient _httpClient;
    private readonly PexelsOptions _options;
    private readonly AIImagePipelineOptions _pipelineOptions;
    private readonly ILogger<PexelsClient> _logger;

    public PexelsClient(
        HttpClient httpClient,
        IOptions<PexelsOptions> options,
        IOptions<AIImagePipelineOptions> pipelineOptions,
        ILogger<PexelsClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _pipelineOptions = pipelineOptions.Value;
        _logger = logger;
    }

    public async Task<PexelsSearchResultDTO> SearchAsync(
        PexelsSearchRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var configurationError = ValidateConfiguration();
        if (configurationError != null) return SearchFailure(configurationError, false);
        var query = request.Query?.Trim() ?? string.Empty;
        if (query.Length is < 2 or > 500) return SearchFailure("Từ khóa tìm ảnh Pexels không hợp lệ.", false);

        using var timeout = CreateTimeout(cancellationToken);
        try
        {
            var perPage = Math.Clamp(request.PerPage, 1, 40);
            var orientation = NormalizeOrientation(request.Orientation);
            var path = $"/v1/search?query={Uri.EscapeDataString(query)}&orientation={orientation}&per_page={perPage}";
            using var response = await SendWithRetryAsync(() => CreateAuthorizedRequest(HttpMethod.Get, path), timeout.Token);
            if (!response.IsSuccessStatusCode)
                return SearchFailure($"Pexels trả về HTTP {(int)response.StatusCode}.", IsTransient(response.StatusCode));

            var payload = await response.Content.ReadFromJsonAsync<PexelsSearchResponse>(cancellationToken: timeout.Token);
            var excluded = request.ExcludedPhotoIds.ToHashSet();
            var photos = payload?.Photos
                .Where(x => !excluded.Contains(x.Id))
                .Select(Map)
                .Where(x => IsAllowedImageUrl(x.PreviewUrl) && IsAllowedImageUrl(x.DownloadUrl))
                .ToList() ?? [];
            return new PexelsSearchResultDTO { Success = true, Photos = photos };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SearchFailure("Pexels phản hồi quá thời gian cho phép.", true);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Pexels search connection failed. ErrorType={ErrorType}", ex.GetType().Name);
            return SearchFailure("Không thể kết nối Pexels.", true);
        }
        catch (JsonException)
        {
            return SearchFailure("Phản hồi Pexels không đúng JSON.", false);
        }
    }

    public async Task<PexelsImageResultDTO> DownloadPhotoAsync(long photoId, CancellationToken cancellationToken = default)
    {
        var configurationError = ValidateConfiguration();
        if (configurationError != null) return DownloadFailure(configurationError, false);
        if (photoId <= 0) return DownloadFailure("Pexels Photo ID không hợp lệ.", false);

        using var timeout = CreateTimeout(cancellationToken);
        try
        {
            using var metadataResponse = await SendWithRetryAsync(
                () => CreateAuthorizedRequest(HttpMethod.Get, $"/v1/photos/{photoId}"), timeout.Token);
            if (!metadataResponse.IsSuccessStatusCode)
                return DownloadFailure($"Pexels trả về HTTP {(int)metadataResponse.StatusCode}.", IsTransient(metadataResponse.StatusCode));
            var raw = await metadataResponse.Content.ReadFromJsonAsync<PexelsPhoto>(cancellationToken: timeout.Token);
            var photo = raw == null ? null : Map(raw);
            if (photo == null || !IsAllowedImageUrl(photo.DownloadUrl))
                return DownloadFailure("Pexels không trả về URL ảnh hợp lệ.", false);

            using var imageResponse = await SendWithRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Get, photo.DownloadUrl), timeout.Token,
                HttpCompletionOption.ResponseHeadersRead);
            if (!imageResponse.IsSuccessStatusCode)
                return DownloadFailure($"Không thể tải ảnh Pexels (HTTP {(int)imageResponse.StatusCode}).", IsTransient(imageResponse.StatusCode));
            var contentType = imageResponse.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (contentType == null || !AllowedImageTypes.Contains(contentType))
                return DownloadFailure("Pexels trả về định dạng ảnh không được hỗ trợ.", false);
            var bytes = await ReadLimitedAsync(imageResponse.Content, timeout.Token);
            if (bytes == null) return DownloadFailure("Ảnh Pexels rỗng hoặc vượt quá kích thước cho phép.", false);

            _logger.LogInformation("Pexels reference downloaded. PhotoId={PhotoId} Bytes={Bytes}", photoId, bytes.Length);
            return new PexelsImageResultDTO
            {
                Success = true,
                Bytes = bytes,
                ContentType = contentType,
                FileName = $"pexels-{photoId}{GetExtension(contentType)}",
                Photo = photo
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DownloadFailure("Pexels phản hồi quá thời gian cho phép.", true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogWarning("Pexels photo download failed. PhotoId={PhotoId} ErrorType={ErrorType}", photoId, ex.GetType().Name);
            return DownloadFailure("Không thể tải ảnh tham chiếu Pexels.", ex is HttpRequestException);
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        var attempts = Math.Clamp(_pipelineOptions.RetryCount, 0, 5) + 1;
        for (var attempt = 1; ; attempt++)
        {
            using var request = requestFactory();
            var response = await _httpClient.SendAsync(request, completion, cancellationToken);
            if (!IsTransient(response.StatusCode) || attempt >= attempts) return response;
            response.Dispose();
            await Task.Delay(Math.Clamp(_pipelineOptions.RetryDelayMilliseconds * attempt, 50, 5000), cancellationToken);
        }
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Authorization", _options.ApiKey.Trim());
        return request;
    }

    private CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 3, 120)));
        return source;
    }

    private async Task<byte[]?> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var maxBytes = Math.Clamp(_options.MaxImageBytes, 1024, 10 * 1024 * 1024);
        if (content.Headers.ContentLength > maxBytes) return null;
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (destination.Length + read > maxBytes) return null;
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return destination.Length == 0 ? null : destination.ToArray();
    }

    private string? ValidateConfiguration() => !_options.Enabled ? "Pexels đang bị tắt trong cấu hình."
        : string.IsNullOrWhiteSpace(_options.ApiKey) ? "Pexels chưa được cấu hình API key." : null;

    private static string NormalizeOrientation(string value) => value.ToLowerInvariant() switch
    {
        "portrait" => "portrait", "landscape" => "landscape", _ => "square"
    };

    private static bool IsTransient(HttpStatusCode status) => status == HttpStatusCode.RequestTimeout
        || status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    private static bool IsAllowedImageUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, "images.pexels.com", StringComparison.OrdinalIgnoreCase);

    private static string GetExtension(string contentType) => contentType switch
    {
        "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg"
    };

    private static PexelsPhotoDTO Map(PexelsPhoto photo) => new()
    {
        Id = photo.Id,
        Width = photo.Width,
        Height = photo.Height,
        Url = photo.Url,
        Photographer = photo.Photographer,
        PhotographerUrl = photo.PhotographerUrl,
        Alt = photo.Alt,
        AverageColor = photo.AverageColor,
        PreviewUrl = photo.Source.Medium ?? photo.Source.Large,
        DownloadUrl = photo.Source.Large2X ?? photo.Source.Large
    };

    private static PexelsSearchResultDTO SearchFailure(string message, bool retryable) =>
        new() { ErrorMessage = message, Retryable = retryable };
    private static PexelsImageResultDTO DownloadFailure(string message, bool retryable) =>
        new() { ErrorMessage = message, Retryable = retryable };

    private sealed class PexelsSearchResponse
    {
        [JsonPropertyName("photos")] public List<PexelsPhoto> Photos { get; set; } = [];
    }

    private sealed class PexelsPhoto
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("photographer")] public string? Photographer { get; set; }
        [JsonPropertyName("photographer_url")] public string? PhotographerUrl { get; set; }
        [JsonPropertyName("alt")] public string? Alt { get; set; }
        [JsonPropertyName("avg_color")] public string? AverageColor { get; set; }
        [JsonPropertyName("src")] public PexelsPhotoSource Source { get; set; } = new();
    }

    private sealed class PexelsPhotoSource
    {
        [JsonPropertyName("medium")] public string? Medium { get; set; }
        [JsonPropertyName("large2x")] public string? Large2X { get; set; }
        [JsonPropertyName("large")] public string? Large { get; set; }
    }
}
