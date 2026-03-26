using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.DTOs.Admin.Inventory;
using CafeChain.ViewModels.Admin.Inventories;

namespace CafeChain.Application.Interfaces.Admin
{
    public interface IAdminIngredientService
    {
        Task<AdminInventoryListViewModel> GetInventoryDashboardAsync(int pageIndex, int pageSize, string searchTerm, string type, string status);
        Task<byte[]> ExportInventoryCsvAsync(string searchTerm, string type, string status);
        Task<List<AdminIngredientDropdownDto>> GetIngredientsForDropdownAsync();
        Task<bool> CreateStockImportAsync(AdminCreateStockImportDto dto, int storeId, int staffId);
    }
}
