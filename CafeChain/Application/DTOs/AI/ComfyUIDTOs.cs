namespace CafeChain.Application.DTOs.AI;

public sealed class ComfyUIImageRequestDTO
{
    public string Prompt { get; set; } = string.Empty;
    public string FileNamePrefix { get; set; } = "cafechain_ai";
}

public sealed class ComfyUIImageResultDTO
{
    public bool Success { get; set; }
    public byte[]? Bytes { get; set; }
    public string? ContentType { get; set; }
    public string? FileName { get; set; }
    public string? ErrorMessage { get; set; }
}
