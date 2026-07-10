using CafeChain.Application.DTOs.Admin.StockAlerts;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>Issue #99 — StoreManager confirm/reject StockAlert.</summary>
    public interface IStockAlertManagerService
    {
        Task<ServiceResult> ConfirmAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId,
            string note);

        Task<ServiceResult> RejectAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId,
            string reason);

        Task<ServiceResult<StockAlertListResultDto>> ListForStoreAsync(
            int storeId,
            string? statusFilter,
            int page,
            int pageSize);

        Task<ServiceResult<StockAlertDetailDto>> GetDetailAsync(int alertId, int managerStoreId);
    }
}
