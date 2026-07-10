using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IInventoryDeductionService
    {
        /// <summary>
        /// Recipe EstimatedBomCost adapter (Issue #117): package-normalized read-only estimate.
        /// Complete → Success(total). Incomplete → Failure (never Success understated zero).
        /// Not StoreOperationalCost / HistoricalOrderCogs.
        /// Must NOT gate stock mutation — use DeductStock* which ignores cost completeness.
        /// </summary>
        Task<ServiceResult<decimal>> CalculateRecipeCogsAsync(int recipeId);

        /// <summary>
        /// Quantity inventory deduction only. Independent of EstimatedBomCost completeness.
        /// Missing package/price data does not block deduction; missing unit conversion fails closed.
        /// </summary>
        Task<ServiceResult> DeductStockForOrderAsync(List<POSSoldItemDto> soldItems, int storeId);
        Task<ServiceResult> DeductStockForCommittedOrderAsync(
            List<POSSoldItemDto> soldItems,
            int storeId,
            int referenceOrderId);
    }
}
