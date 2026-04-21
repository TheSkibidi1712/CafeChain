using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Models.Inventories;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Infrastrusture.Interfaces.Admin.StoreInventories
{
    public interface IAdminStoreInventoryRepository
    {
        // Inventory
        Task<(List<InventoryDTO>, int total)> GetPagedAsync(
            List<int> storeIds,
            int storeId,
            string? search,
            int page,
            int pageSize);

        // Transactions
        Task<(List<InventoryTransaction>, int total)> GetTransactionsByStoreIdsAsync(
            List<int> storeIds,
            int storeId,
            int page,
            int pageSize);

        Task<(List<InventoryTransaction>, int total)> GetTransactionsByStoreIdAsync(
            int storeId,
            int page,
            int pageSize);

        // Staff
        Task<Staff?> GetStaffByAccountIdAsync(
            int accountId);

        Task SaveChangesAsync();
    }
}
