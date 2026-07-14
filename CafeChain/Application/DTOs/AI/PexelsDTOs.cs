namespace CafeChain.Application.DTOs.AI;

public sealed class PexelsSearchRequestDTO
{
    public string Query { get; set; } = string.Empty;
    public string Orientation { get; set; } = "square";
    public int PerPage { get; set; } = 15;
    public IReadOnlyCollection<long> ExcludedPhotoIds { get; set; } = [];
}

public sealed class PexelsSearchResultDTO
{
    public bool Success { get; set; }
    public bool Retryable { get; set; }
    public List<PexelsPhotoDTO> Photos { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public sealed class PexelsPhotoDTO
{
    public long Id { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Url { get; set; }
    public string? Photographer { get; set; }
    public string? PhotographerUrl { get; set; }
    public string? Alt { get; set; }
    public string? AverageColor { get; set; }
    public string? PreviewUrl { get; set; }
    public string? DownloadUrl { get; set; }
}

public sealed class PexelsImageResultDTO
{
    public bool Success { get; set; }
    public bool Retryable { get; set; }
    public byte[]? Bytes { get; set; }
    public string? ContentType { get; set; }
    public string? FileName { get; set; }
    public PexelsPhotoDTO? Photo { get; set; }
    public string? ErrorMessage { get; set; }
}
