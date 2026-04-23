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

        /// <summary>
        /// [MISSION 2] Trừ tồn kho thực tế khi đơn hàng Hoàn thành.
        /// Option B: Trừ Bán thành phẩm trực tiếp (không explode ra NL thô).
        /// POS (DineIn/TakeAway): Trừ thẳng AvailableQty.
        /// Online (Delivery): Trừ ReservedQty (đã reserve lúc đặt đơn).
        /// Ghi log InventoryTransaction (Type: SALES_DEDUCTION).
        /// </summary>
        Task ConfirmInventoryDeductionAsync(int orderId);
    }
}
