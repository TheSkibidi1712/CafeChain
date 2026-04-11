using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Models.Inventories;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.InventoryDocuments;


namespace CafeChain.Application.Interfaces.Admin.InventoryDocuments
{
    public interface IAdminInventoryDocumentService
    {
        Task<InventoryDocumentCreateVM> GetCreateDataAsync();
        Task<InventoryDocumentIndexVM> GetPagedAsync(InventoryDocumentFilterDTO filter);

        Task<InventoryDocumentDetailVM?> GetDetailAsync(int id);

        Task CreateAsync(InventoryDocumentVM vm);

        Task<decimal> GetStockAsync(int storeId, int ingredientId);
        Task<List<Unit>> GetUnitsByIngredientAsync(int ingredientId);
        Task<List<(int unitId, string unitName, decimal price)>> GetUnitsWithPriceAsync(int ingredientId, int? supplierId);
        Task<List<IngredientSupplier>> GetIngredientSuppliersBySupplierAsync(int supplierId);

        // ================= XUẤT KHO =================
        Task<List<StoreInventory>> GetIngredientsByStoreAsync(int storeId);

    }
}
