using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.POS;

public interface IPOSIceCustomizationService
{
    Task<ServiceResult<POSIceEligibilityDto>> GetEligibilityAsync(
        int storeId,
        int drinkId,
        int? sizeId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<POSIceOrderSnapshotDto?>> CreateOrderSnapshotAsync(
        int storeId,
        int drinkId,
        int? sizeId,
        int quantity,
        int? iceLevelPercent,
        CancellationToken cancellationToken = default);
}
