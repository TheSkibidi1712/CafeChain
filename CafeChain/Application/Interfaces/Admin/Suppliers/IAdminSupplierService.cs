using CafeChain.Application.DTOs.Admin.Suppliers;

namespace CafeChain.Application.Interfaces.Admin.Suppliers
{
    public interface IAdminSupplierService
    {
        // ===== LIST & DETAIL =====
        Task<List<AdminSupplierDTO>> GetAllAsync(
            string? search,
            bool? status,
            IReadOnlyCollection<int>? storeScope = null);
        Task<AdminSupplierDetailDTO?> GetByIdAsync(
            int id,
            IReadOnlyCollection<int>? storeScope = null);

        // ===== CRUD MAIN =====
        Task<string> GenerateNextCodeAsync();   // Trả về mã NCC tiếp theo (NCC00001)
        Task<int> CreateAsync(AdminSupplierCreateDTO dto);
        Task UpdateAsync(AdminSupplierUpdateDTO dto);
        Task ToggleStatusAsync(int id);

        // ===== PHONES =====
        Task AddPhoneAsync(AdminSupplierPhoneCreateDTO dto);
        Task DeletePhoneAsync(int supplierPhoneId);

        // ===== CONTACTS =====
        Task AddContactAsync(AdminSupplierContactCreateDTO dto);
        Task UpdateContactAsync(AdminSupplierContactUpdateDTO dto);
        Task DeleteContactAsync(int supplierContactId);
        Task SetPrimaryContactAsync(int supplierContactId);

        // ===== INGREDIENT SUPPLIER OFFERS (#111) =====
        Task<List<AdminIngredientSupplierDTO>> GetIngredientOffersAsync(int supplierId);
        Task<AdminIngredientSupplierDTO?> GetIngredientOfferByIdAsync(int ingredientSupplierId);
        Task<int> CreateIngredientOfferAsync(AdminIngredientSupplierSaveDTO dto);
        Task UpdateIngredientOfferAsync(AdminIngredientSupplierSaveDTO dto);
        Task ToggleIngredientOfferActiveAsync(int ingredientSupplierId, bool active);
        Task ChangeIngredientOfferPriceAsync(AdminIngredientSupplierPriceChangeDTO dto, int actorStaffId);
        Task<List<AdminIngredientSupplierPriceHistoryDTO>> GetIngredientOfferPriceHistoryAsync(int ingredientSupplierId);
        Task<List<object>> GetIngredientDropdownAsync();
        Task<List<object>> GetContentUnitDropdownAsync();

        // ===== STORE SCOPE =====
        Task<List<AdminSupplierStoreDTO>> GetSupplierStoresAsync(
            int supplierId,
            IReadOnlyCollection<int>? storeScope = null);
        Task<List<object>> GetStoreDropdownAsync(IReadOnlyCollection<int>? storeScope = null);
        Task SaveSupplierStoreAsync(AdminSupplierStoreSaveDTO dto);
    }
}
