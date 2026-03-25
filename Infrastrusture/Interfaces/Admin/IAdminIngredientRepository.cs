using System;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Models.Inventories;

namespace CafeChain.Infrastrusture.Interfaces.Admin
{
    public interface IAdminIngredientRepository
    {
        IQueryable<Ingredient> GetAllIngredientsWithStock();
        Task<int> GetActiveIngredientsCountAsync();
        Task<int> GetLowStockIngredientsCountAsync(decimal threshold);
        Task<decimal> GetTotalInventoryValueAsync();
        Task<int> GetMonthlyImportBatchesCountAsync(int year, int month);
        Task<List<Ingredient>> GetAllActiveIngredientsAsync();
        Task<bool> CreateStockImportAsync(StockImport stockImport);
    }
}
