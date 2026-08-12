using CafeChain.Application.DTOs.AI;

namespace CafeChain.Application.Interfaces.AI;

public interface IOllamaClient
{
    Task<OllamaResultDTO> ChatAsync(string systemPrompt, string userPayload, CancellationToken cancellationToken = default);
    Task<OllamaResultDTO> ChatAsync(
        string systemPrompt,
        string userPayload,
        string featureName,
        CancellationToken cancellationToken = default);
    Task<OllamaResultDTO> ChatStructuredAsync(
        string systemPrompt,
        string userPayload,
        object jsonSchema,
        string featureName,
        CancellationToken cancellationToken = default);
    Task<OllamaHealthDTO> CheckHealthAsync(CancellationToken cancellationToken = default);
}
