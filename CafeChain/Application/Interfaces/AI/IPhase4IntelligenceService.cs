using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.AI;

namespace CafeChain.Application.Interfaces.AI;

public interface IPosRecommendationService
{
    Task RebuildStoreAsync(int storeId, CancellationToken cancellationToken = default);
    Task<PosRecommendationResultDto> GetAsync(int storeId, Guid sessionId, IReadOnlyCollection<int> triggerDrinkIds, CancellationToken cancellationToken = default);
    Task TrackAsync(int storeId, PosRecommendationInteractionDto input, CancellationToken cancellationToken = default);
    Task LinkOrderAsync(Guid sessionId, int orderId, CancellationToken cancellationToken = default);
}

public interface IAnomalyDetectionService
{
    Task AnalyzeStoreAsync(int storeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperationalAnomalyDto>> GetOpenAsync(AdminActorContext actor, int storeId, CancellationToken cancellationToken = default);
    Task<AnomalyExplanationContextDto> GetExplanationContextAsync(AdminActorContext actor, int anomalyId, CancellationToken cancellationToken = default);
    Task RecordFeedbackAsync(AdminActorContext actor, AnomalyFeedbackDto input, CancellationToken cancellationToken = default);
}
