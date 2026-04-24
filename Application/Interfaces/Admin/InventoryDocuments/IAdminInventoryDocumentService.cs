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

        // ================= TẠO PHIẾU=================
        Task CreateAsync(InventoryDocumentVM vm);


        // PHỤ
        Task<decimal?> GetLastPriceAsync(int storeId, int ingredientId);
        Task<decimal> GetAveragePriceAsync(int storeId, int ingredientId);
        Task<ImportInfoDTO> GetImportInfoAsync(int ingredientId, int supplierId);
        Task<decimal> GetStockAsync(int storeId, int ingredientId);
        Task<List<Unit>> GetUnitsByIngredientAsync(int ingredientId);
        Task<List<IngredientSupplier>> GetIngredientSuppliersBySupplierAsync(int supplierId);

        Task<List<StoreInventory>> GetStoreInventoriesAsync(int storeId, bool onlyAvailable = false);


    }
}
