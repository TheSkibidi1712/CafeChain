namespace CafeChain.Application.DTOs.AI;

public sealed class PexelsImageRequestDTO
{
    public string Query { get; set; } = string.Empty;
    public IReadOnlyCollection<long> ExcludedPhotoIds { get; set; } = [];
}

public sealed class PexelsImageResultDTO
{
    public bool Success { get; set; }
    public byte[]? Bytes { get; set; }
    public string? ContentType { get; set; }
    public string? FileName { get; set; }
    public long? PhotoId { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Photographer { get; set; }
    public string? PhotographerUrl { get; set; }
    public string? ErrorMessage { get; set; }
}
