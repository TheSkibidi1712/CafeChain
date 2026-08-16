using CafeChain.Application.DTOs.Admin.InventoryThresholds;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.StoreInventories
{
    /// <summary>Issue #104 — configure StoreInventory.MinStockLevel (Admin).</summary>
    public interface IInventoryThresholdService
    {
        Task<ServiceResult<InventoryThresholdListResultDto>> ListAsync(
            int accountId,
            int storeId,
            string? search,
            int page,
            int pageSize);

        Task<ServiceResult> UpdateMinStockLevelAsync(
            int accountId,
            int storeInventoryId,
            decimal? minStockLevel,
            string? rowVersion);

        Task<ServiceResult> UpdatePreparedItemPolicyAsync(
            int accountId,
            int storeInventoryId,
            decimal? minStockLevel,
            decimal? targetStockLevel,
            string? rowVersion);
    }
}
