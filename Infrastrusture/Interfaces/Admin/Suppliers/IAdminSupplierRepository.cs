using CafeChain.Models.Inventories.Suppliers;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Suppliers
{
    public interface IAdminSupplierRepository
    {
        // ===== LIST & DETAIL =====
        Task<List<Supplier>> GetAllAsync(string? search, bool? status);
        Task<Supplier?> GetByIdAsync(int id);

        // ===== SUPPLIER =====
        Task CreateAsync(Supplier supplier);
        Task<bool> IsCodeExists(string code, int? excludeId = null);
        Task<string> GenerateNextCodeAsync();   // Sinh mã NCC tự động: NCC00001, NCC00002, ...
        Task ToggleStatus(int id);

        // ===== PHONES =====
        Task AddPhoneAsync(SupplierPhone phone);
        Task<SupplierPhone?> GetPhoneByIdAsync(int supplierPhoneId);
        Task DeletePhoneAsync(SupplierPhone phone);

        // ===== BANK ACCOUNTS =====
        Task AddBankAccountAsync(SupplierBankAccount bankAccount);
        Task<SupplierBankAccount?> GetBankAccountByIdAsync(int supplierBankAccountId);
        Task DeleteBankAccountAsync(SupplierBankAccount bankAccount);

        // ===== CONTACTS =====
        Task AddContactAsync(SupplierContact contact);
        Task<SupplierContact?> GetContactByIdAsync(int supplierContactId);
        Task<List<SupplierContact>> GetContactsBySupplierIdAsync(int supplierId);
        Task DeleteContactAsync(SupplierContact contact);

        // ===== SAVE =====
        Task SaveChangesAsync();
    }
}
