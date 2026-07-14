namespace CafeChain.Infrastructure.Configurations;

public sealed class ComfyUIOptions
{
    public const string SectionName = "ComfyUI";
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://localhost:8188";
    public string WorkflowPath { get; set; } = "Resources/AI/ComfyUI/product-img2img.json";
    public string TextToImageWorkflowPath { get; set; } = "Resources/AI/ComfyUI/product-txt2img.json";
    public string CheckpointName { get; set; } = string.Empty;
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 1024;
    public int TimeoutSeconds { get; set; } = 180;
    public int PollIntervalMilliseconds { get; set; } = 1000;
    public int MaxImageBytes { get; set; } = 5 * 1024 * 1024;
    public string CheckpointNodeId { get; set; } = "4";
    public string SamplerNodeId { get; set; } = "3";
    public string ReferenceImageNodeId { get; set; } = "1";
    public string ImageScaleNodeId { get; set; } = "2";
    public string BatchNodeId { get; set; } = "10";
    public string TextLatentNodeId { get; set; } = "5";
    public string PositivePromptNodeId { get; set; } = "6";
    public string NegativePromptNodeId { get; set; } = "7";
    public string OutputImageNodeId { get; set; } = "9";
    public string NegativePrompt { get; set; } = "text, logo, watermark, people, hands, low quality, blurry, distorted product, duplicate objects";
}
