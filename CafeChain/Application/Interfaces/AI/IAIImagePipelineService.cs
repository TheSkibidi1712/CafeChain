using CafeChain.Application.DTOs.AI;

namespace CafeChain.Application.Interfaces.AI;

public interface IAIImagePipelineService
{
    Task<AIReferenceSearchResultDTO> SearchReferenceImagesAsync(
        AIReferenceSearchRequestDTO request,
        CancellationToken cancellationToken = default);

    Task<AIGenerateFromReferenceResultDTO> GenerateFromReferenceAsync(
        AIGenerateFromReferenceRequestDTO request,
        CancellationToken cancellationToken = default);

    Task<AIGenerateFromReferenceResultDTO> UsePexelsImageAsync(
        AIUsePexelsImageRequestDTO request,
        CancellationToken cancellationToken = default);

    Task<AIGenerateFromReferenceResultDTO> GenerateFromPromptAsync(
        AIGenerateFromPromptRequestDTO request,
        CancellationToken cancellationToken = default);
}

public interface IVisualSpecificationBuilder
{
    VisualSpecificationDTO BuildDrink(string name, string description, string? proposedPrompt = null);
    VisualSpecificationDTO BuildTopping(string name, string? proposedPrompt = null);
}

public interface IPexelsMetadataScorer
{
    PexelsCandidateScoreResult Score(
        PexelsPhotoDTO photo,
        VisualSpecificationDTO specification,
        string matchedQuery);
}

public sealed record PexelsCandidateScoreResult(bool Rejected, double Score, IReadOnlyList<string> Warnings);
