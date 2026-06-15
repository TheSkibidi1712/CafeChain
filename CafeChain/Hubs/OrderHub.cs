using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace CafeChain.Hubs
{
    /// <summary>
    /// Hub real-time cho hệ thống Order Management Dashboard & KDS.
    /// - ReceiveNewOrder: Khi có đơn hàng mới từ khách (COD hoặc Online đã thanh toán).
    /// - ReceiveOrderStatusUpdate: Khi trạng thái đơn hàng thay đổi trên Kanban.
    /// - PaymentCompleted: Khi Webhook PayOS xác nhận thanh toán thành công.
    ///
    /// [Skill.md §1.1] Admin broadcast dùng Group("AdminDashboard").
    /// [FIX.md §2] Customer payment polling dùng Group("Order_{orderId}").
    /// TUYỆT ĐỐI KHÔNG dùng Clients.All.
    /// </summary>
    public class OrderHub : Hub
    {
        /// <summary>
        /// Admin Dashboard JS gọi method này ngay sau khi connect thành công
        /// để tự đăng ký vào group "AdminDashboard".
        /// </summary>
        public async Task JoinAdminGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "AdminDashboard");
        }

        /// <summary>
        /// Customer Checkout page gọi method này để theo dõi trạng thái thanh toán
        /// của đúng đơn hàng đang chờ. Mỗi đơn có group riêng "Order_{orderId}".
        /// Khi Webhook PayOS confirm → chỉ bắn tín hiệu cho group này.
        /// </summary>
        public async Task JoinOrderGroup(int orderId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Order_{orderId}");
        }

        public async Task SendOrderStatusUpdate(int orderId, int newStatusId)
        {
            await Clients.Group("AdminDashboard").SendAsync("ReceiveOrderStatusUpdate", orderId, newStatusId);
        }

        public async Task SendNewOrder(int orderId)
        {
            await Clients.Group("AdminDashboard").SendAsync("ReceiveNewOrder", orderId);
        }
    }
}
