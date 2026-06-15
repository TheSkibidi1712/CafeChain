using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Debts;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.InventoryDocuments;
using CafeChain.ViewModels.Shared;
using Microsoft.EntityFrameworkCore.Storage;


namespace CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments
{
    public interface IAdminInventoryDocumentRepository
    {
        // =====================================================
        // UNIT OF WORK
        // =====================================================

        Task BeginTransactionAsync();

        Task CommitTransactionAsync();

        Task RollbackTransactionAsync();

        Task SaveChangesAsync();

        // =====================================================
        // DOCUMENT QUERY
        // =====================================================

        IQueryable<InventoryDocument> GetDocumentsQuery();

        Task<InventoryDocument?> GetByIdAsync(int documentId);

        Task<InventoryDocument?> GetDocumentForConfirmAsync(int documentId);

        Task<InventoryDocument?> GetDocumentWithDetailsAsync(int documentId);

        void UpdateDocument(InventoryDocument document);

        // =====================================================
        // DOCUMENT DETAIL
        // =====================================================

        void UpdateDocumentDetail(InventoryDocumentDetail detail);

        // =====================================================
        // SNAPSHOT
        // =====================================================

        Task<bool> SnapshotExistsAsync(int documentId);

        Task<InventoryDocumentSnapshot?> GetSnapshotAsync(int documentId);

        Task AddSnapshotAsync(InventoryDocumentSnapshot snapshot);

        Task AddSnapshotDetailsAsync(IEnumerable<InventoryDocumentSnapshotDetail> details);

        // =====================================================
        // STORE INVENTORY
        // =====================================================

        Task<StoreInventory?> GetStoreInventoryAsync(int storeId, int ingredientId);

        Task<List<StoreInventory>> GetStoreInventoriesAsync(int storeId);

        Task AddStoreInventoryAsync(StoreInventory inventory);

        void UpdateStoreInventory(StoreInventory inventory);

        // =====================================================
        // INVENTORY SNAPSHOT
        // =====================================================

        Task AddStoreInventorySnapshotAsync(StoreInventorySnapshot snapshot);

        Task AddStoreInventorySnapshotsAsync(IEnumerable<StoreInventorySnapshot> snapshots);

        // =====================================================
        // INVENTORY TRANSACTION
        // =====================================================

        Task AddInventoryTransactionAsync(InventoryTransaction transaction);

        Task AddInventoryTransactionsAsync(IEnumerable<InventoryTransaction> transactions);

        // =====================================================
        // COST LAYER
        // =====================================================

        Task AddCostLayerAsync(InventoryCostLayer layer);

        Task AddCostLayersAsync(IEnumerable<InventoryCostLayer> layers);

        Task<List<InventoryCostLayer>> GetAvailableCostLayersAsync(int storeId, int ingredientId);

        Task<decimal> GetAvailableQuantityAsync(int storeId, int ingredientId);

        Task<InventoryCostLayer?> GetCostLayerByIdAsync(int costLayerId);

        void UpdateCostLayer(InventoryCostLayer layer);

        // =====================================================
        // COST ALLOCATION
        // =====================================================

        Task AddCostAllocationAsync(InventoryCostAllocation allocation);

        Task AddCostAllocationsAsync(IEnumerable<InventoryCostAllocation> allocations);

        // =====================================================
        // DEBT
        // =====================================================

        Task AddDebtAsync(InventoryDebt debt);

        // =====================================================
        // MASTER DATA
        // =====================================================
        Task<List<StoreDropdownVM>> GetStoreDropdownAsync();

        Task<Ingredient?> GetIngredientAsync(int ingredientId);

        Task<Unit?> GetUnitAsync(int unitId);

        Task<Store?> GetStoreAsync(int storeId);

        Task<Supplier?> GetSupplierAsync(int supplierId);

        Task<Staff?> GetStaffAsync(int staffId);

        // =====================================================
        // AUDIT
        // =====================================================

        Task AddAuditLogAsync(AuditLog auditLog);
    }
}
