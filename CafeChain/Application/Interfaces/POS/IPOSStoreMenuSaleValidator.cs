using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.POS
{
    public interface IPOSStoreMenuSaleValidator
    {
        Task<ServiceResult<POSAcceptedSaleLineDto>> ValidateOnlineAsync(
            POSOrderItemDto item,
            int storeId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<POSAcceptedSaleLineDto>> ValidateOfflineAsync(
            POSOrderItemDto item,
            int storeId,
            CancellationToken cancellationToken = default);
    }
}
