using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Models.Inventories;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore.Storage;
using CafeChain.Models.Staffs;


namespace CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments
{
    public interface IAdminInventoryDocumentRepository
    {
        // ================= LOOKUP =================
        Task<List<Store>> GetStoresByStaffAsync(int staffId);
        Task<bool> CheckStaffHasStoreAsync(int staffId, int storeId);
        Task<List<Supplier>> GetSuppliersAsync();

        // ================= DOCUMENT =================
        Task<(List<InventoryDocument>, int)> GetPagedAsync(InventoryDocumentFilterDTO filter);
        Task<InventoryDocument?> GetDetailAsync(int id);
        Task AddAsync(InventoryDocument document);

        Task AddDebtAsync(InventoryDebt debt);
        Task<InventoryDebt?> GetDebtByDocumentIdAsync(int documentId);
        // ================= INGREDIENT =================
        Task<Ingredient?> GetIngredientAsync(int ingredientId);
        Task<List<IngredientSupplier>> GetIngredientSuppliersAsync(int ingredientId, int? supplierId);
        Task<List<IngredientSupplier>> GetIngredientSuppliersBySupplierAsync(int supplierId);
        // ================= STORE INVENTORY =================
        Task<StoreInventory?> GetStoreInventoryAsync(int storeId, int ingredientId);
        Task AddStoreInventoryAsync(StoreInventory stock);
        // ================= UNIT =================
        Task<UnitConversion?> GetConversionAsync(int ingredientId, int fromUnitId, int toUnitId);
        Task<List<Unit>> GetUnitsByIngredientAsync(int ingredientId);

        // ================= STORE INVENTORY =================
        // kiểm kê
        Task<List<StoreInventory>> GetStoreInventoriesAsync(int storeId);

        // xuất kho
        Task<List<StoreInventory>> GetStoreInventoriesForExportAsync(int storeId);
        // ================= TRANSACTION =================
        Task AddTransactionAsync(InventoryTransaction transaction);

        // ================= SAVE =================
        Task SaveChangesAsync();

        // ================= TRANSACTION =================
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}
