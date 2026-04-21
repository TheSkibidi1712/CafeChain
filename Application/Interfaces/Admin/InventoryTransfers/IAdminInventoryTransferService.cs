using CafeChain.Models.Inventories;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.InventoryDocuments;

namespace CafeChain.Application.Interfaces.Admin.InventoryTransfers
{
    public interface IAdminInventoryTransferService
    {
        Task CreateInternalTransferAsync(InventoryTransferCreateVM vm);
        Task ConfirmTransferReceiveAsync(int transferId);
        Task ReceiveTransferAsync(int transferId, List<InventoryTransferReceiveItemVM> receivedItems);
        Task CancelTransferAsync(int transferId);
        Task<List<InventoryTransfer>> GetPendingTransfersToStore(int storeId);
        Task<InventoryTransfer?> GetTransferByIdAsync(int id);
        Task ConfirmAllAsync(int transferId);
    }
}
