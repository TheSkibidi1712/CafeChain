using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.ViewModels.Cart;

namespace CafeChain.Application.Interfaces
{
    public interface IInventoryService
    {
        /// <summary>
        /// Dự trừ nguyên liệu trong kho khi đặt đơn. Ném lỗi nếu không đủ hàng.
        /// </summary>
        Task ReserveInventoryForOrderAsync(int storeId, List<CartItemViewModel> items);

        /// <summary>
        /// Hoàn trả lại nguyên liệu vào kho (AvailableQty) khi đơn bị hủy.
        /// </summary>
        Task ReleaseInventoryForOrderAsync(int orderId);
    }
}
