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
        Task<List<Store>> GetStoresAsync();
        Task<List<Staff>> GetStaffsAsync();
        Task<int?> GetStoreIdByStaffAsync(int staffId);
        Task<List<Supplier>> GetSuppliersAsync();
        Task<List<Ingredient>> GetIngredientsAsync();
        Task<List<Unit>> GetUnitsAsync();

        // ================= DOCUMENT =================
        Task<(List<InventoryDocument>, int)> GetPagedAsync(InventoryDocumentFilterDTO filter);
        Task<InventoryDocument?> GetDetailAsync(int id);
        Task AddAsync(InventoryDocument document);

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
        Task<List<StoreInventory>> GetStoreInventoriesAsync(int storeId);

        // ================= TRANSACTION =================
        Task AddTransactionAsync(InventoryTransaction transaction);

        // ================= SAVE =================
        Task SaveChangesAsync();

        // ================= TRANSACTION =================
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}
