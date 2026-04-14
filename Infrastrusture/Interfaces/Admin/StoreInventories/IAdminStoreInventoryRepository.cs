using CafeChain.Models.Inventories;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Infrastrusture.Interfaces.Admin.StoreInventories
{
    public interface IAdminStoreInventoryRepository
    {
        // Inventory
        Task<(List<StoreInventory>, int total)> GetPagedAsync(int storeId, string? search, int page, int pageSize);

        // Transaction theo từng nguyên liệu
        Task<(List<InventoryTransaction>, int total)> GetTransactionsByInventoryIdAsync(int storeInventoryId, int page, int pageSize);

        Task<(List<InventoryTransaction>, int total)> GetTransactionsByStoreIdAsync(int storeId, int page, int pageSize);

        // Staff
        Task<Staff?> GetStaffByAccountIdAsync(int accountId);

        Task SaveChangesAsync();
    }
}
