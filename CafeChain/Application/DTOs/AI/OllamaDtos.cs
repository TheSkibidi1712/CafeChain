using System.Text.Json.Serialization;

namespace CafeChain.Application.DTOs.AI;

public sealed class OllamaResultDTO
{
    public bool Success { get; set; }
    public string? Content { get; set; }
    public string? ErrorMessage { get; set; }
    public bool UsedFallback { get; set; }
}

public sealed class OllamaHealthDTO
{
    public bool ServerAvailable { get; set; }
    public bool ModelAvailable { get; set; }
    public string? Model { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class OllamaChatRequestDTO
{
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
    [JsonPropertyName("messages")] public List<OllamaMessageDTO> Messages { get; set; } = [];
    [JsonPropertyName("stream")] public bool Stream { get; set; }
    [JsonPropertyName("think")] public bool Think { get; set; }
    [JsonPropertyName("keep_alive")] public string KeepAlive { get; set; } = string.Empty;
    [JsonPropertyName("options")] public OllamaRequestOptionsDTO Options { get; set; } = new();
}

public sealed class OllamaMessageDTO
{
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
}

public sealed class OllamaRequestOptionsDTO
{
    [JsonPropertyName("temperature")] public decimal Temperature { get; set; }
    [JsonPropertyName("top_p")] public decimal TopP { get; set; }
    [JsonPropertyName("top_k")] public int TopK { get; set; }
    [JsonPropertyName("repeat_penalty")] public decimal RepeatPenalty { get; set; }
}

public sealed class OllamaChatResponseDTO
{
    [JsonPropertyName("message")] public OllamaMessageDTO? Message { get; set; }
}
