using CafeChain.Models.Inventories;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Suppliers
{
    public interface IAdminSupplierRepository
    {
        Task<IEnumerable<Supplier>> GetAllSuppliersAsync();
        Task<Supplier> GetSupplierByIdAsync(int id);
        Task CreateSupplierAsync(Supplier supplier);
        Task UpdateSupplierAsync(Supplier supplier);
        Task ToggleSupplierStatusAsync(int id);
        Task AdjustDebtAsync(int id, decimal amount);
        Task<bool> IsSupplierCodeExistsAsync(string code, int? excludeId = null);
        Task<bool> IsSupplierNameExistsAsync(string name, int? excludeId = null);
        Task<bool> IsSupplierPhoneExistsAsync(string phone, int? excludeId = null);
    }
}
