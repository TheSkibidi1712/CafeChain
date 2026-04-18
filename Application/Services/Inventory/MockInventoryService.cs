using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces;
using CafeChain.ViewModels.Cart;

namespace CafeChain.Application.Services.Inventory
{
    public class MockInventoryService : IInventoryService
    {
        public Task<bool> ReserveInventoryForOrderAsync(int storeId, List<CartItemViewModel> items)
        {
            // Placeholder: Returns true assuming stock always exists
            // To be replaced with real EF Core logic inside the Inventory Module
            return Task.FromResult(true);
        }

        public Task ReleaseInventoryForOrderAsync(int orderId)
        {
            // Placeholder logic to release stock back to Inventory
            return Task.CompletedTask;
        }
    }
}
