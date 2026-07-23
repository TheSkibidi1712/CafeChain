using CafeChain.Models.Analytics;

namespace CafeChain.Infrastructure.Interfaces.Analytics;

public sealed record BasketFact(int OrderId, IReadOnlyList<int> DrinkIds);
public sealed record RecommendationCandidateData(
    int DrinkId,
    string Name,
    string? ImageUrl,
    decimal Price,
    decimal Margin,
    bool IsAvailable,
    IReadOnlyList<int> DrinkSizeIds);
public sealed record DailyMetricPoint(DateTime Date, decimal Value);

public interface IPosRecommendationRepository
{
    Task<IReadOnlyList<int>> GetActiveStoreIdsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BasketFact>> GetBasketsAsync(int storeId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, RecommendationCandidateData>> GetCandidatesAsync(int storeId, IReadOnlyCollection<int> drinkIds, DateTime asOfUtc, CancellationToken cancellationToken = default);
    Task ReplaceCatalogAsync(int storeId, string modelVersion, IReadOnlyCollection<PosRecommendationCatalog> rows, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PosRecommendationCatalog>> GetCatalogAsync(int storeId, IReadOnlyCollection<int> triggerDrinkIds, DateTime asOfUtc, int take, CancellationToken cancellationToken = default);
    Task<PosRecommendationExposure?> GetExposureAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task ReconcileConversionsAsync(int storeId, CancellationToken cancellationToken = default);
    Task AddExposureAsync(PosRecommendationExposure exposure, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IAnomalyDetectionRepository
{
    Task<IReadOnlyList<int>> GetActiveStoreIdsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyMetricPoint>> GetDailyRevenueAsync(int storeId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, IReadOnlyList<DailyMetricPoint>>> GetDailyOperationalMetricsAsync(int storeId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<OperationalAnomaly?> GetByKeyAsync(int storeId, string metricCode, string periodKey, CancellationToken cancellationToken = default);
    Task AddAsync(OperationalAnomaly anomaly, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperationalAnomaly>> GetOpenAsync(int storeId, CancellationToken cancellationToken = default);
    Task<OperationalAnomaly?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
