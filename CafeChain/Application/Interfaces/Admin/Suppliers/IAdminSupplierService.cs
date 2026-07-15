using CafeChain.Application.DTOs.Admin.Suppliers;

namespace CafeChain.Application.Interfaces.Admin.Suppliers
{
    public interface IAdminSupplierService
    {
        // ===== LIST & DETAIL =====
        Task<List<AdminSupplierDTO>> GetAllAsync(string? search, bool? status);
        Task<AdminSupplierDetailDTO?> GetByIdAsync(int id);

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
        Task<List<object>> GetIngredientDropdownAsync();
        Task<List<object>> GetContentUnitDropdownAsync();
    }
}
