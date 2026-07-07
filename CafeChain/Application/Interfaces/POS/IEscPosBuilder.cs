using CafeChain.Models.Orders;

namespace CafeChain.Application.Interfaces.POS
{
    /// <summary>
    /// ADR-0003: Sinh mảng byte ESC/POS từ Order data để gửi qua PrintBridgeHub.
    /// Output là raw bytes gửi thẳng sang máy in qua TCP:9100.
    /// </summary>
    public interface IEscPosBuilder
    {
        /// <summary>
        /// Build receipt ESC/POS bytes cho một Order.
        /// </summary>
        /// <param name="order">Order entity (include OrderDetails, OrderToppings)</param>
        /// <param name="storeName">Tên quán hiển thị trên header bill</param>
        /// <param name="cashierName">Tên thu ngân</param>
        /// <param name="cashReceived">Tiền khách đưa (để tính tiền thối)</param>
        /// <param name="isCashPayment">true = thanh toán tiền mặt → kick cash drawer</param>
        /// <returns>Raw ESC/POS byte array</returns>
        byte[] BuildReceipt(Order order, string storeName, string cashierName, decimal cashReceived, bool isCashPayment);

        /// <summary>
        /// Build receipt ESC/POS bytes với quyền điều khiển riêng việc mở két.
        /// Dùng cho reprint: vẫn hiển thị thông tin tiền mặt nhưng không kick cash drawer.
        /// </summary>
        byte[] BuildReceipt(
            Order order,
            string storeName,
            string cashierName,
            decimal cashReceived,
            bool isCashPayment,
            bool kickCashDrawer);

        /// <summary>
        /// Build cup label ESC/POS bytes cho khu vực pha chế.
        /// Mỗi ly tương ứng một tem; nếu item quantity > 1 thì lặp tem theo số lượng.
        /// </summary>
        /// <param name="order">Order entity (include OrderDetails, OrderToppings)</param>
        /// <param name="storeName">Tên quán hiển thị trên tem</param>
        /// <param name="cashierName">Tên thu ngân tạo đơn</param>
        /// <returns>Raw ESC/POS byte array cho toàn bộ tem trong đơn</returns>
        byte[] BuildCupLabels(Order order, string storeName, string cashierName);
    }
}
