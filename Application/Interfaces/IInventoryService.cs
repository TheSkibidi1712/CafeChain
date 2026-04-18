using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.ViewModels.Cart;

namespace CafeChain.Application.Interfaces
{
    public interface IInventoryService
    {
        Task<bool> ReserveInventoryForOrderAsync(int storeId, List<CartItemViewModel> items);
        Task ReleaseInventoryForOrderAsync(int orderId);
    }
}
