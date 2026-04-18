using CafeChain.Models.Inventories;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore.Storage;

namespace CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers
{
    public interface IAdminInventoryTransferRepository
    {
        // ================= TRANSFER =================
        Task AddTransferAsync(InventoryTransfer transfer);
        Task<InventoryTransfer?> GetTransferByIdAsync(int id);
        Task<InventoryDocument> GetDocumentWithDetailsAsync(int id);
        Task<List<Store>> GetStoresByIdsAsync(List<int> ids);
        Task<List<Store>> GetStoresHasPendingTransferToStore(int storeId);
    }
}
