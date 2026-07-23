using CafeChain.Application.DTOs.AI;
using CafeChain.Application.DTOs.Admin.Actor;

namespace CafeChain.Application.Interfaces.AI;

public interface IForecastService
{
    Task<ForecastResultDto> GenerateRevenueAsync(int storeId, int horizonDays, CancellationToken cancellationToken = default);
    Task<ForecastResultDto> GenerateProductAsync(int storeId, int drinkId, int horizonDays, CancellationToken cancellationToken = default);
    Task<ForecastResultDto?> GetLatestAsync(AdminActorContext actor, string seriesType, int storeId, int? entityId, int horizonDays, CancellationToken cancellationToken = default);
}

public interface ISupplierIntelligenceService
{
    Task<SupplierRecommendationDto> CompareAsync(AdminActorContext actor, int storeId, int ingredientId, decimal requiredBaseQuantity, CancellationToken cancellationToken = default);
}
