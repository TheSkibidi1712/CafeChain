using CafeChain.Application.DTOs.Admin.StoreInventories;

namespace CafeChain.Application.Interfaces.Admin.StoreInventories
{
    public interface IAdminStoreInventoryService
    {
        Task<(List<InventoryDTO>, int total)> GetInventoryByStaffAsync(
            int accountId,
            int storeId,
            string? search,
            int page,
            int pageSize);

        Task<(List<InventoryTransactionDTO>, int total)> GetAllTransactionsByStaffAsync(
            int accountId,
            int storeId,
            int page,
            int pageSize);

        Task<List<InventoryStoreDTO>> GetStoresByStaffAsync(
            int accountId);

        Task<(List<InventoryTransactionDTO>, int total)> GetTransactionsByInventoryAsync(
            int accountId,
            int storeInventoryId,
            int page,
            int pageSize);

    }
}
