using CafeChain.Application.DTOs.Admin.Suppliers;

namespace CafeChain.Application.Interfaces.Admin.Suppliers
{
    public interface IAdminSupplierService
    {
        Task<IEnumerable<AdminSupplierDTO>> GetAllSuppliersAsync();
        Task<AdminSupplierUpdateDTO> GetSupplierForUpdateAsync(int id);
        Task CreateSupplierAsync(AdminSupplierCreateDTO dto);
        Task UpdateSupplierAsync(AdminSupplierUpdateDTO dto);
        Task ToggleSupplierStatusAsync(int id);
        Task AdjustDebtAsync(int id, decimal amount);
    }
}
