using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>Issue #100 — RestockRequest from CONFIRMED StockAlert.</summary>
    public interface IRestockRequestService
    {
        Task<ServiceResult<CreateRestockRequestResultDto>> CreateFromConfirmedAlertAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId,
            decimal requestedQuantity,
            string? note,
            string? priority);

        Task<ServiceResult<RestockRequestListResultDto>> ListForStoreAsync(
            int storeId,
            string? statusFilter,
            int page,
            int pageSize);

        Task<ServiceResult<RestockRequestDetailDto>> GetDetailAsync(
            int requestId,
            int viewerStoreId);

        Task<ServiceResult<RestockRequestListItemDto?>> GetOpenByAlertAsync(
            int stockAlertId,
            int storeId);
    }
}
