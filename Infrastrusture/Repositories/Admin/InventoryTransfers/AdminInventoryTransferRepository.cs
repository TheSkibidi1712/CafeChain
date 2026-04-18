using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace CafeChain.Infrastrusture.Repositories.Admin.InventoryTransfers
{
    public class AdminInventoryTransferRepository : IAdminInventoryTransferRepository
    {
        private readonly AppDbContext _context;
        public AdminInventoryTransferRepository(AppDbContext context)
        {
            _context = context;
        }
        // ======================== TRANSFER ========================
        public async Task AddTransferAsync(InventoryTransfer transfer)
        {
            await _context.InventoryTransfers.AddAsync(transfer);
        }

        public async Task<InventoryTransfer?> GetTransferByIdAsync(int id)
        {
            return await _context.InventoryTransfers
                .Include(x => x.Details)
                .Include(x => x.ExportDocument)
                .Include(x => x.ImportDocument)
                .FirstOrDefaultAsync(x => x.InventoryTransferId == id);
        }

        public async Task<InventoryDocument> GetDocumentWithDetailsAsync(int id)
        {
            return await _context.InventoryDocuments
                .Include(x => x.Details)
                .FirstAsync(x => x.InventoryDocumentId == id);
        }

        public async Task<List<Store>> GetStoresByIdsAsync(List<int> ids)
        {
            return await _context.Stores
                .Where(x => ids.Contains(x.StoreId))
                .ToListAsync();
        }

        public async Task<List<Store>> GetStoresHasPendingTransferToStore(int storeId)
        {
            return await _context.InventoryTransfers
                .Where(x => x.ToStoreId == storeId
                         && x.Status != InventoryTransferStatus.COMPLETED
                         && x.Status != InventoryTransferStatus.CANCELLED)
                .Select(x => x.FromStore)
                .Distinct()
                .ToListAsync();
        }
    }
}
