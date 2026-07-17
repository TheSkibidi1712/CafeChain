namespace CafeChain.Application.DTOs.AI;

public enum ComfyUIGenerationMode
{
    ReferenceImage = 0,
    TextToImage = 1
}

public sealed class ComfyUIImageRequestDTO
{
    public ComfyUIGenerationMode GenerationMode { get; set; } = ComfyUIGenerationMode.ReferenceImage;
    public byte[] ReferenceImageBytes { get; set; } = [];
    public string ReferenceContentType { get; set; } = "image/jpeg";
    public string PositivePrompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public string FileNamePrefix { get; set; } = "cafechain_ai";
    public int OutputCount { get; set; } = 3;
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 1024;
    public double Denoise { get; set; } = 0.55;
    public int? Steps { get; set; }
    public double? Cfg { get; set; }
    public string? SamplerName { get; set; }
    public string? Scheduler { get; set; }
    public long? Seed { get; set; }
}

public sealed class ComfyUIImageOutputDTO
{
    public byte[] Bytes { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public sealed class ComfyUIImageResultDTO
{
    public bool Success { get; set; }
    public string? PromptId { get; set; }
    public List<ComfyUIImageOutputDTO> Images { get; set; } = [];
    public string? ErrorMessage { get; set; }
}
