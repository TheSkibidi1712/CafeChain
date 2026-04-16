using CafeChain.Application.DTOs.Admin.Suppliers;

namespace CafeChain.Application.Interfaces.Admin.Suppliers
{
    public interface IAdminSupplierService
    {
        // ===== LIST & DETAIL =====
        Task<List<AdminSupplierDTO>> GetAllAsync(string? search, bool? status);
        Task<AdminSupplierDetailDTO?> GetByIdAsync(int id);

        // ===== CRUD MAIN =====
        Task<int> CreateAsync(AdminSupplierCreateDTO dto);
        Task UpdateAsync(AdminSupplierUpdateDTO dto);
        Task ToggleStatusAsync(int id);

        // ===== PHONES =====
        Task AddPhoneAsync(AdminSupplierPhoneCreateDTO dto);
        Task DeletePhoneAsync(int supplierPhoneId);

        // ===== BANK ACCOUNTS =====
        Task AddBankAccountAsync(AdminSupplierBankAccountCreateDTO dto);
        Task DeleteBankAccountAsync(int supplierBankAccountId);

        // ===== CONTACTS =====
        Task AddContactAsync(AdminSupplierContactCreateDTO dto);
        Task DeleteContactAsync(int supplierContactId);
    }
}
