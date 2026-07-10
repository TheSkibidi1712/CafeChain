using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>
    /// Issue #97 — detect LOW_STOCK / OUT_OF_STOCK and enforce duplicate guard.
    /// </summary>
    public interface IStockAlertService
    {
        Task<ServiceResult<StockAlertEvaluationResultDto>> EvaluateStoreInventoryItemAsync(
            int storeInventoryId,
            string source);

        Task<ServiceResult<StockAlertEvaluationResultDto>> EvaluateStoreAsync(
            int storeId,
            string source);

        Task<ServiceResult<StockAlertEvaluationResultDto>> EvaluateAfterInventoryChangeAsync(
            int storeId,
            int? ingredientId,
            int? recipeId,
            string source);
    }
}
