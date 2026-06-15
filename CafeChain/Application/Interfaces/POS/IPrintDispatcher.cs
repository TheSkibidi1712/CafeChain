using CafeChain.Models.Orders;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.POS
{
    /// <summary>
    /// ADR-0003: Trung gian giữa POS Service và Print Bridge.
    /// Nhận Order → build ESC/POS bytes → gửi qua SignalR đến Print Bridge Worker.
    /// 
    /// Tách riêng để:
    ///   1. POSOrderService không cần biết SignalR (SRP)
    ///   2. Unit test có thể mock print mà không cần SignalR context
    ///   3. Dễ mở rộng: queue, retry, logging — tất cả nằm trong dispatcher
    /// </summary>
    public interface IPrintDispatcher
    {
        /// <summary>
        /// Build receipt ESC/POS từ Order và gửi qua SignalR đến Print Bridge Worker của store.
        /// Fire-and-forget — print failure KHÔNG block order commit.
        /// </summary>
        /// <param name="order">Order entity (cần include OrderDetails + OrderToppings)</param>
        /// <param name="storeId">Store ID để route đến đúng Print Bridge group</param>
        /// <param name="cashierName">Tên thu ngân hiển thị trên bill</param>
        /// <param name="cashReceived">Tiền khách đưa (tính tiền thối)</param>
        /// <param name="isCashPayment">true = tiền mặt → kick cash drawer</param>
        /// <returns>true nếu gửi thành công, false nếu lỗi (log nhưng không throw)</returns>
        Task<bool> DispatchPrintJobAsync(Order order, int storeId, string cashierName, decimal cashReceived, bool isCashPayment);
    }
}
