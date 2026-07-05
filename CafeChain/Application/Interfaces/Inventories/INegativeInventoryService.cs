using CafeChain.Application.DTOs.Inventories;
using CafeChain.Models.Stores;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface INegativeInventoryService
    {
        Task<NegativeStockValidationResult> ValidateIssueAsync(
            StoreInventory inventory,
            decimal issueQuantity,
            string ingredientName);
    }
}
