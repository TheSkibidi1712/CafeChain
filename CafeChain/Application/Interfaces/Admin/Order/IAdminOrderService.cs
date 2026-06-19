using CafeChain.Application.DTOs.Admin;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.Admin
{
    public interface IAdminOrderService
    {
        /// <summary>
        /// Lấy tất cả các đơn hàng cần thiết cho Kanban Dashboard (gồm đơn mới, đang làm and history limit 20).
        /// </summary>
        Task<List<AdminOrderKanbanDto>> GetKanbanOrdersAsync();

        /// <summary>
        /// Lấy chi tiết một đơn hàng dùng cho Offcanvas Panel.
        /// </summary>
        Task<AdminOrderDetailDto> GetOrderDetailsAsync(int orderId);

        // ===================================================
        // CÁC HÀM NGHIỆP VỤ CHUYỂN TRẠNG THÁI ĐƠN HÀNG
        // Di dời từ Controller xuống Service (Skill.md §1 Fat Service, Skinny Controller)
        // Mỗi hàm tự bắn SignalR sau khi SaveChanges thành công (Skill.md §1.1)
        // ===================================================

        /// <summary>
        /// Duyệt đơn: Chuyển trạng thái Pending(2) / WaitingForApproval(3) → Preparing(4).
        /// Guard: Chỉ chấp nhận đơn đang ở status 2 hoặc 3.
        /// </summary>
        Task AcceptOrderAsync(int orderId);

        /// <summary>
        /// Xong món: Chuyển trạng thái Preparing(4) → WaitingForPickUp(5).
        /// Guard: Chỉ chấp nhận đơn đang ở status 4.
        /// </summary>
        Task ReadyForPickupAsync(int orderId);

        /// <summary>
        /// Lấy danh sách nhân viên giao hàng đang Active.
        /// </summary>
        Task<List<ShipperDto>> GetShippersAsync();

        /// <summary>
        /// Giao shipper: Chuyển trạng thái Ready(3) → Delivering(4).
        /// </summary>
        Task DispatchOrderAsync(DispatchOrderRequest request);



        /// <summary>
        /// Hoàn thành đơn: Chuyển trạng thái WaitingForPickUp(5) hoặc Delivering(6) → Completed(7).
        /// Đồng thời cập nhật Payment thành Completed nếu chưa thanh toán (COD).
        /// Guard: Chỉ chấp nhận đơn đang ở status 5 hoặc 6.
        /// </summary>
        Task CompleteOrderAsync(int orderId);

        /// <summary>
        /// Hủy đơn: Chuyển bất kỳ trạng thái active nào → Cancel(8).
        /// Hoàn kho (ReleaseInventory) và cập nhật PaymentStatus tương ứng.
        /// Guard: Chỉ chấp nhận đơn đang ở status 2-6 (chưa hoàn thành/chưa hủy).
        /// </summary>
        Task CancelOrderAsync(int orderId, string reason);
        
        /// <summary>
        /// Mô phỏng việc Partner Webhook trả về kết quả thành công cho toàn bộ đơn Đang Giao.
        /// </summary>
        Task<int> SimulateWebhookAsync();

        // ===================================================
        // ORDER HISTORY — DataTables Server-Side Processing
        // ===================================================

        /// <summary>
        /// Lấy danh sách đơn hàng với DataTables server-side processing (phân trang, sắp xếp, lọc).
        /// </summary>
        Task<DataTablesResponse<AdminOrderHistoryRowDto>> GetOrderHistoryAsync(DataTablesRequest request);

        /// <summary>
        /// Lấy chi tiết đơn hàng cho Modal trong trang Lịch sử đơn hàng.
        /// </summary>
        Task<AdminOrderHistoryDetailDto> GetOrderHistoryDetailAsync(int orderId);

        /// <summary>
        /// Xuất dữ liệu đơn hàng đã lọc cho mục đích báo cáo (Export).
        /// </summary>
        Task<List<AdminOrderHistoryRowDto>> GetFilteredOrdersForExportAsync(
            string searchKeyword, string dateFrom, string dateTo, int? statusFilter, int? paymentMethodFilter);
    }
}
