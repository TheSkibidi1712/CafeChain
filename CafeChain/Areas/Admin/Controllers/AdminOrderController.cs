using CafeChain.Application.Interfaces.Admin;
using CafeChain.Application.Constants;
using CafeChain.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Skinny Controller cho Order Management Dashboard & KDS.
    /// [Skill.md §1] Controller chỉ điều hướng + gọi Service. Tối đa 3-5 dòng/action.
    /// [REFACTOR] Đã xóa: AppDbContext, IHubContext, IInventoryService — toàn bộ nằm trong AdminOrderService.
    /// </summary>
    [Area("Admin")]
    [Authorize]
    public class AdminOrderController : Controller
    {
        private readonly IAdminOrderService _adminOrderService;
        private readonly AppDbContext _context;

        public AdminOrderController(IAdminOrderService adminOrderService, AppDbContext context)
        {
            _adminOrderService = adminOrderService;
            _context = context;
        }

        // Màn hình Dashboard Kanban (Bảng điều phối)
        public IActionResult Index()
        {
            return View();
        }

        // Màn hình Lịch sử Đơn hàng (Quản lý - DataTables)
        public IActionResult History()
        {
            return View();
        }

        // Lấy danh sách đơn hàng cho Kanban board
        [HttpGet("/api/AdminOrder/GetOrders")]
        public async Task<IActionResult> GetOrders()
        {
            var data = await _adminOrderService.GetKanbanOrdersAsync();
            return Ok(data);
        }

        // Chi tiết đơn hàng cho Offcanvas
        [HttpGet("/api/AdminOrder/GetOrderDetails/{orderId}")]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            var data = await _adminOrderService.GetOrderDetailsAsync(orderId);
            if (data == null) return NotFound();
            return Ok(data);
        }

        // ===================================================
        // CÁC ACTION CHUYỂN TRẠNG THÁI — Skinny: chỉ gọi Service + trả kết quả
        // Guard Clauses + SignalR đều nằm trong Service (Skill.md §1, §1.1)
        // ===================================================

        [HttpPost("/api/AdminOrder/AcceptOrder/{orderId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptOrder(int orderId)
        {
            try
            {
                await _adminOrderService.AcceptOrderAsync(orderId);
                return Ok("Đơn hàng đã được duyệt và chuyển sang Pha chế.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("/api/AdminOrder/ReadyForPickup/{orderId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReadyForPickup(int orderId)
        {
            try
            {
                await _adminOrderService.ReadyForPickupAsync(orderId);
                return Ok("Đơn hàng đã xong món, chờ lấy.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // API lấy danh sách Shipper cho Dropdown
        [HttpGet("/api/AdminOrder/GetShippers")]
        public async Task<IActionResult> GetShippers()
        {
            var data = await _adminOrderService.GetShippersAsync();
            return Ok(data);
        }

        [HttpPost("/api/AdminOrder/Dispatched")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dispatched([FromBody] CafeChain.Application.DTOs.Admin.DispatchOrderRequest request)
        {
            try
            {
                await _adminOrderService.DispatchOrderAsync(request);
                return Ok("Đơn hàng đã giao cho Shipper.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }



        [HttpPost("/api/AdminOrder/CompleteOrder/{orderId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteOrder(int orderId)
        {
            try
            {
                await _adminOrderService.CompleteOrderAsync(orderId);
                return Ok("Đơn hàng đã hoàn thành.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("/api/AdminOrder/FailDelivery/{orderId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FailDelivery(int orderId, [FromQuery] string reason)
        {
            try
            {
                await _adminOrderService.CancelOrderAsync(orderId, reason);
                return Ok("Đơn hàng đã bị hủy.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("/api/AdminOrder/SimulateWebhook")]
        [IgnoreAntiforgeryToken] // Tạm thời bỏ qua AntiForgery để dễ test API từ bên thứ 3 (như Postman hoặc webhook thật)
        public async Task<IActionResult> SimulateWebhook()
        {
            try
            {
                int updatedCount = await _adminOrderService.SimulateWebhookAsync();
                return Ok($"Đã giả lập cập nhật thành công {updatedCount} đơn hàng thành 'Hoàn Thành'.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Lấy chi tiết đơn hàng cho modal lịch sử
        [HttpGet("/api/AdminOrder/GetOrderHistoryDetail/{orderId}")]
        public async Task<IActionResult> GetHistoryDetail(int orderId)
        {
            var data = await _adminOrderService.GetOrderHistoryDetailAsync(orderId);
            if (data == null) return NotFound();
            return Ok(data);
        }

        // ===================================================
        // ORDER HISTORY — Custom AJAX & Export
        // ===================================================

        [HttpGet("/api/AdminOrder/GetOrderHistoryData")]
        public async Task<IActionResult> GetOrderHistoryData(string keyword, string fromDate, string toDate, int? statusId, int? paymentId)
        {
            try
            {
                var query = _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderStatus)
                    .Include(o => o.Payments).ThenInclude(p => p.PaymentMethod)
                    .AsQueryable();

                // Lọc theo từ khóa
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var k = keyword.Trim().ToLower();
                    query = query.Where(o => o.OrderId.ToString().Contains(k) || 
                                           (o.ReceiverPhone != null && o.ReceiverPhone.Contains(k)) || 
                                           (o.ReceiverName != null && o.ReceiverName.ToLower().Contains(k)));
                }

                // Lọc theo ngày
                if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParse(fromDate, out var from))
                    query = query.Where(o => o.CreatedAt >= from);
                if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParse(toDate, out var to))
                    query = query.Where(o => o.CreatedAt <= to.AddDays(1).AddTicks(-1));

                // Lọc theo trạng thái
                if (statusId.HasValue)
                    query = query.Where(o => o.OrderStatusId == statusId.Value);

                // Lọc theo phương thức thanh toán
                if (paymentId.HasValue)
                    query = query.Where(o => o.Payments.Any(p => p.PaymentMethodId == paymentId.Value));

                var allData = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

                // Tính toán thống kê
                var stats = new
                {
                    TotalOrders = allData.Count,
                    CompletedOrders = allData.Count(o => o.OrderStatusId == SystemConstants.OrderStatuses.Completed),
                    CancelledOrders = allData.Count(o => o.OrderStatusId == SystemConstants.OrderStatuses.Cancelled),
                    TotalRevenue = allData.Where(o => o.OrderStatusId == SystemConstants.OrderStatuses.Completed).Sum(o => o.Total)
                };

                var result = allData.Select(o => new {
                    o.OrderId,
                    o.CreatedAt,
                    CustomerName = o.Customer?.FullName ?? o.ReceiverName ?? "Khách vãng lai",
                    CustomerPhone = o.ReceiverPhone,
                    o.Total,
                    PaymentMethodName = o.Payments.FirstOrDefault()?.PaymentMethod?.Name ?? "N/A",
                    o.OrderStatusId,
                    OrderStatusName = o.OrderStatus?.Name,
                    OrderStatusBadge = o.OrderStatus?.BadgeColor ?? "bg-secondary"
                });

                return Ok(new { stats, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("/api/AdminOrder/ExportCSV")]
        public async Task<IActionResult> ExportCSV(string keyword, string fromDate, string toDate, int? statusId, int? paymentId)
        {
            try
            {
                var query = _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderStatus)
                    .Include(o => o.Payments).ThenInclude(p => p.PaymentMethod)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var k = keyword.Trim().ToLower();
                    query = query.Where(o => o.OrderId.ToString().Contains(k) || 
                                           (o.ReceiverPhone != null && o.ReceiverPhone.Contains(k)) || 
                                           (o.ReceiverName != null && o.ReceiverName.ToLower().Contains(k)));
                }

                if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParse(fromDate, out var from))
                    query = query.Where(o => o.CreatedAt >= from);
                if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParse(toDate, out var to))
                    query = query.Where(o => o.CreatedAt <= to.AddDays(1).AddTicks(-1));

                if (statusId.HasValue)
                    query = query.Where(o => o.OrderStatusId == statusId.Value);

                if (paymentId.HasValue)
                    query = query.Where(o => o.Payments.Any(p => p.PaymentMethodId == paymentId.Value));

                var data = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

                var csv = new System.Text.StringBuilder();
                // Thêm BOM để Excel nhận đúng UTF-8
                csv.Append('\uFEFF');
                csv.AppendLine("Mã đơn,Ngày đặt,Khách hàng,Số điện thoại,Tổng tiền,Thanh toán,Trạng thái");

                foreach (var o in data)
                {
                    csv.AppendLine($"#CC{o.OrderId:D5},{o.CreatedAt:dd/MM/yyyy HH:mm},{o.Customer?.FullName ?? o.ReceiverName ?? "Khách vãng lai"},{o.ReceiverPhone},{o.Total},{o.Payments.FirstOrDefault()?.PaymentMethod?.Name ?? "N/A"},{o.OrderStatus?.Name}");
                }

                var fileName = $"LichSuDonHang_{DateTime.Now:yyyyMMdd}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Lỗi xuất file: " + ex.Message);
            }
        }
    }
}
