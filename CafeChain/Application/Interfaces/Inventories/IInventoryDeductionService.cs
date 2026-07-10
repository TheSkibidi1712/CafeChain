using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IInventoryDeductionService
    {
        /// <summary>
        /// Recipe COGS in base units. Failure when conversion is missing/invalid —
        /// never returns a partial understated cost as success.
        /// </summary>
        Task<ServiceResult<decimal>> CalculateRecipeCogsAsync(int recipeId);
        Task<ServiceResult> DeductStockForOrderAsync(List<POSSoldItemDto> soldItems, int storeId);
        Task<ServiceResult> DeductStockForCommittedOrderAsync(
            List<POSSoldItemDto> soldItems,
            int storeId,
            int referenceOrderId);
    }
}
