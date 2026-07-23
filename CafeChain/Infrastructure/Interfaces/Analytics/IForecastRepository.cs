using CafeChain.Application.DTOs.AI;
using CafeChain.Models.Analytics;

namespace CafeChain.Infrastructure.Interfaces.Analytics;

public interface IForecastRepository
{
    Task<List<int>> GetActiveStoreIdsAsync(CancellationToken cancellationToken);
    Task<List<int>> GetProductIdsAsync(int storeId, DateTime from, DateTime toExclusive, CancellationToken cancellationToken);
    Task<List<ForecastSeriesPointDto>> GetRevenueSeriesAsync(int storeId, DateTime from, DateTime toExclusive, CancellationToken cancellationToken);
    Task<List<ForecastSeriesPointDto>> GetProductSeriesAsync(int storeId, int drinkId, DateTime from, DateTime toExclusive, CancellationToken cancellationToken);
    Task<ForecastRun?> GetExistingAsync(string seriesType, int storeId, int? entityId, DateTime trainingToExclusive, int horizon, string modelVersion, CancellationToken cancellationToken);
    Task<ForecastRun?> GetLatestAsync(string seriesType, int storeId, int? entityId, int horizon, CancellationToken cancellationToken);
    void Add(ForecastRun run);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
