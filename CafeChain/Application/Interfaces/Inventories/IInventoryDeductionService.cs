using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IInventoryDeductionService
    {
        Task<decimal> CalculateRecipeCogsAsync(int recipeId);
        Task<ServiceResult> DeductStockForOrderAsync(List<POSSoldItemDto> soldItems, int storeId);
    }
}
