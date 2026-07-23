using CafeChain.Application.Interfaces.Admin;
using CafeChain.Application.Constants;
using CafeChain.Data;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Filters;
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
    public class AdminOrderController : AdminBaseController
    {
        private readonly IAdminOrderService _adminOrderService;
        private readonly IAdminActorContextAccessor _actor;
        private readonly IAdminStoreScopeResolver _storeScopeResolver;

        public AdminOrderController(
            IAdminOrderService adminOrderService,
            IAdminActorContextAccessor actor,
            IAdminStoreScopeResolver storeScopeResolver)
        {
            _adminOrderService = adminOrderService;
            _actor = actor;
            _storeScopeResolver = storeScopeResolver;
        }

        // Màn hình Dashboard Kanban (Bảng điều phối)
        [DevelopmentOnlyLegacyEntryPoint]
        public async Task<IActionResult> Index()
        {
            var scope = await ResolveScopeAsync();
            if (!scope.IsResolved) return StoreScopeFailure(scope);
            SetStoreScopeViewData(scope);
            return View();
        }

        // Màn hình Lịch sử Đơn hàng (Quản lý - DataTables)
        public async Task<IActionResult> History()
        {
            var scope = await ResolveScopeAsync();
            if (!scope.IsResolved) return StoreScopeFailure(scope);
            SetStoreScopeViewData(scope);
            return View();
        }

        // Lấy danh sách đơn hàng cho Kanban board
        [HttpGet("/api/AdminOrder/GetOrders")]
        [DevelopmentOnlyLegacyEntryPoint]
        public async Task<IActionResult> GetOrders()
        {
            var storeId = await ResolveStoreIdAsync();
            if (!storeId.HasValue) return Forbid();
            var data = await _adminOrderService.GetKanbanOrdersAsync(storeId.Value);
            return Ok(data);
        }

        // Chi tiết đơn hàng cho Offcanvas
        [HttpGet("/api/AdminOrder/GetOrderDetails/{orderId}")]
        [DevelopmentOnlyLegacyEntryPoint]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            var storeId = await ResolveStoreIdAsync();
            if (!storeId.HasValue) return Forbid();
            var data = await _adminOrderService.GetOrderDetailsAsync(orderId, storeId.Value);
            if (data == null) return NotFound();
            return Ok(data);
        }

        // ===================================================
        // CÁC ACTION CHUYỂN TRẠNG THÁI — Skinny: chỉ gọi Service + trả kết quả
        // Guard Clauses + SignalR đều nằm trong Service (Skill.md §1, §1.1)
        // ===================================================

        [HttpPost("/api/AdminOrder/AcceptOrder/{orderId}")]
        [ValidateAntiForgeryToken]
        [DevelopmentOnlyLegacyEntryPoint]
        public async Task<IActionResult> AcceptOrder(int orderId)
        {
            try
            {
                var storeId = await ResolveStoreIdAsync();
                if (!storeId.HasValue) return Forbid();
                await _adminOrderService.AcceptOrderAsync(orderId, storeId.Value);
                return Ok("Đơn hàng đã được duyệt và chuyển sang Pha chế.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("/api/AdminOrder/ReadyForPickup/{orderId}")]
        [ValidateAntiForgeryToken]
        [DevelopmentOnlyLegacyEntryPoint]
        public async Task<IActionResult> ReadyForPickup(int orderId)
        {
            try
            {
                var storeId = await ResolveStoreIdAsync();
                if (!storeId.HasValue) return Forbid();
                await _adminOrderService.ReadyForPickupAsync(orderId, storeId.Value);
                return Ok("Đơn hàng đã xong món, chờ lấy.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // API lấy danh sách Shipper cho Dropdown
        [HttpGet("/api/AdminOrder/GetShippers")]
        [DevelopmentOnlyLegacyEntryPoint]
        public async Task<IActionResult> GetShippers()
        {
            var storeId = await ResolveStoreIdAsync();
            if (!storeId.HasValue) return Forbid();
            var data = await _adminOrderService.GetShippersAsync(storeId.Value);
            return Ok(data);
        }

        [HttpPost("/api/AdminOrder/Dispatched")]
        [ValidateAntiForgeryToken]
        [DevelopmentOnlyLegacyEntryPoint]
        public async Task<IActionResult> Dispatched([FromBody] CafeChain.Application.DTOs.Admin.DispatchOrderRequest request)
        {
            try
            {
                var storeId = await ResolveStoreIdAsync();
                if (!storeId.HasValue) return Forbid();
                await _adminOrderService.DispatchOrderAsync(request, storeId.Value);
                return Ok("Đơn hàng đã giao cho Shipper.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }



        [HttpPost("/api/AdminOrder/CompleteOrder/{orderId}")]
        [ValidateAntiForgeryToken]
        [DevelopmentOnlyLegacyEntryPoint]
        public async Task<IActionResult> CompleteOrder(int orderId)
        {
            try
            {
                var storeId = await ResolveStoreIdAsync();
                if (!storeId.HasValue) return Forbid();
                await _adminOrderService.CompleteOrderAsync(orderId, storeId.Value);
                return Ok("Đơn hàng đã hoàn thành.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("/api/AdminOrder/FailDelivery/{orderId}")]
        [ValidateAntiForgeryToken]
        [DevelopmentOnlyLegacyEntryPoint]
        public async Task<IActionResult> FailDelivery(int orderId, [FromQuery] string reason)
        {
            try
            {
                var storeId = await ResolveStoreIdAsync();
                if (!storeId.HasValue) return Forbid();
                await _adminOrderService.CancelOrderAsync(orderId, storeId.Value, reason);
                return Ok("Đơn hàng đã bị hủy.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("/api/AdminOrder/SimulateWebhook")]
        [IgnoreAntiforgeryToken] // Tạm thời bỏ qua AntiForgery để dễ test API từ bên thứ 3 (như Postman hoặc webhook thật)
        [DevelopmentOnlyLegacyEntryPoint]
        public async Task<IActionResult> SimulateWebhook()
        {
            try
            {
                var storeId = await ResolveStoreIdAsync();
                if (!storeId.HasValue) return Forbid();
                int updatedCount = await _adminOrderService.SimulateWebhookAsync(storeId.Value);
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
            var storeId = await ResolveStoreIdAsync();
            if (!storeId.HasValue) return Forbid();
            var data = await _adminOrderService.GetOrderHistoryDetailAsync(orderId, storeId.Value);
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
                var storeId = await ResolveStoreIdAsync();
                if (!storeId.HasValue) return Forbid();
                var allData = await _adminOrderService.GetFilteredOrdersForExportAsync(
                    keyword, fromDate, toDate, statusId, paymentId, storeId.Value);

                // Tính toán thống kê
                var stats = new
                {
                    TotalOrders = allData.Count,
                    CompletedOrders = allData.Count(o => o.OrderStatusId == SystemConstants.PaymentStatuses.Paid),
                    CancelledOrders = allData.Count(o => o.OrderStatusId == SystemConstants.PaymentStatuses.Refunded),
                    TotalRevenue = allData.Where(o => o.OrderStatusId == SystemConstants.PaymentStatuses.Paid).Sum(o => o.Total)
                };
                return Ok(new { stats, data = allData });
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
                var storeId = await ResolveStoreIdAsync();
                if (!storeId.HasValue) return Forbid();
                var data = await _adminOrderService.GetFilteredOrdersForExportAsync(
                    keyword, fromDate, toDate, statusId, paymentId, storeId.Value);

                var csv = new System.Text.StringBuilder();
                // Thêm BOM để Excel nhận đúng UTF-8
                csv.Append('\uFEFF');
                csv.AppendLine("Mã đơn,Thời gian thanh toán,Khách hàng,Số điện thoại,Cửa hàng,Thu ngân,Loại đơn,Tổng tiền,Thanh toán,Trạng thái tài chính,Trạng thái hóa đơn,Trạng thái tem");

                foreach (var o in data)
                {
                    csv.AppendLine($"#CC{o.OrderId:D5},{o.CreatedAt:dd/MM/yyyy HH:mm},{Csv(o.CustomerName)},{Csv(o.CustomerPhone)},{Csv(o.StoreName)},{Csv(o.StaffName)},{Csv(o.OrderTypeName)},{o.Total},{Csv(o.PaymentMethodName)},{Csv(o.OrderStatusName)},{Csv(o.ReceiptState)},{Csv(o.DrinkLabelState)}");
                }

                var fileName = $"LichSuDonHang_{DateTime.Now:yyyyMMdd}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Lỗi xuất file: " + ex.Message);
            }
        }

        private Task<Application.DTOs.Admin.StoreScope.AdminStoreScopeResolution> ResolveScopeAsync()
            => _storeScopeResolver.ResolveAsync(_actor.Get(User));

        private async Task<int?> ResolveStoreIdAsync()
        {
            var scope = await ResolveScopeAsync();
            return scope.IsResolved ? scope.StoreId : null;
        }

        private static string Csv(string? value)
            => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
    }
}
