using CafeChain.Application.Interfaces.Admin;
using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Data;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Skinny Controller cho Order Management Dashboard & KDS.
    /// [Skill.md §1] Controller chỉ điều hướng + gọi Service. Tối đa 3-5 dòng/action.
    /// [REFACTOR] Đã xóa: AppDbContext, IHubContext, IInventoryService — toàn bộ nằm trong AdminOrderService.
    /// </summary>
    [RequirePermission(PermissionConstants.OrderView)]
    public class AdminOrderController : AdminBaseController
    {
        private readonly IAdminOrderService _adminOrderService;
        private readonly IAdminActorContextAccessor _actor;
        private readonly IAdminStoreScopeResolver _storeScopeResolver;
        private readonly IOrderAccessAuthorizationService _orderAccessAuthorization;

        public AdminOrderController(
            IAdminOrderService adminOrderService,
            IAdminActorContextAccessor actor,
            IAdminStoreScopeResolver storeScopeResolver,
            IOrderAccessAuthorizationService orderAccessAuthorization)
        {
            _adminOrderService = adminOrderService;
            _actor = actor;
            _storeScopeResolver = storeScopeResolver;
            _orderAccessAuthorization = orderAccessAuthorization;
        }

        // Màn hình Dashboard Kanban (Bảng điều phối)
        [LegacyEntryPointGone]
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
            var accessFailure = await GetOrderAccessFailureAsync(
                OrderAccessActions.AdminList,
                scope.StoreId!.Value);
            if (accessFailure != null) return accessFailure;
            SetStoreScopeViewData(scope);
            return View();
        }

        // Lấy danh sách đơn hàng cho Kanban board
        [HttpGet("/api/AdminOrder/GetOrders")]
        [LegacyEntryPointGone]
        public async Task<IActionResult> GetOrders()
        {
            var storeId = await ResolveStoreIdAsync();
            if (!storeId.HasValue) return Forbid();
            var data = await _adminOrderService.GetKanbanOrdersAsync(storeId.Value);
            return Ok(data);
        }

        // Chi tiết đơn hàng cho Offcanvas
        [HttpGet("/api/AdminOrder/GetOrderDetails/{orderId}")]
        [LegacyEntryPointGone]
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
        [RequirePermission(PermissionConstants.OrderUpdateStatus)]
        [LegacyEntryPointGone]
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
        [RequirePermission(PermissionConstants.OrderUpdateStatus)]
        [LegacyEntryPointGone]
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
        [LegacyEntryPointGone]
        public async Task<IActionResult> GetShippers()
        {
            var storeId = await ResolveStoreIdAsync();
            if (!storeId.HasValue) return Forbid();
            var data = await _adminOrderService.GetShippersAsync(storeId.Value);
            return Ok(data);
        }

        [HttpPost("/api/AdminOrder/Dispatched")]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.OrderUpdateStatus)]
        [LegacyEntryPointGone]
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
        [RequirePermission(PermissionConstants.OrderUpdateStatus)]
        [LegacyEntryPointGone]
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
        [RequirePermission(PermissionConstants.OrderCancel)]
        [LegacyEntryPointGone]
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
        [LegacyEntryPointGone]
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
        public async Task<IActionResult> GetHistoryDetail(int orderId, int? storeId)
        {
            var filter = await ResolveHistoryStoreFilterAsync(
                OrderAccessActions.AdminDetail,
                storeId,
                allWithinScope: false);
            if (filter.Failure != null) return filter.Failure;

            var targetStoreId = filter.StoreIds.Single();
            var data = await _adminOrderService.GetOrderHistoryDetailAsync(orderId, targetStoreId);
            if (data == null) return NotFound();
            return Ok(data);
        }

        // ===================================================
        // ORDER HISTORY — Custom AJAX & Export
        // ===================================================

        [HttpGet("/api/AdminOrder/GetOrderHistoryData")]
        public async Task<IActionResult> GetOrderHistoryData(
            string keyword,
            string fromDate,
            string toDate,
            int? statusId,
            int? paymentId,
            int? storeId,
            bool allWithinScope = false,
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                var filter = await ResolveHistoryStoreFilterAsync(
                    OrderAccessActions.AdminList,
                    storeId,
                    allWithinScope || !storeId.HasValue);
                if (filter.Failure != null) return filter.Failure;

                var result = await _adminOrderService.GetPosSalesHistoryAsync(
                    page,
                    pageSize,
                    keyword,
                    fromDate,
                    toDate,
                    statusId,
                    paymentId,
                    filter.StoreIds);

                return Ok(new
                {
                    result.Page,
                    result.PageSize,
                    result.TotalItems,
                    result.TotalPages,
                    result.Stats,
                    data = result.Items,
                    storeScope = new
                    {
                        allWithinScope = !storeId.HasValue || allWithinScope,
                        selectedStoreId = storeId,
                        stores = filter.Scope!.AccessibleStores
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("/api/AdminOrder/ExportCSV")]
        [RequirePermission(PermissionConstants.OrderExport)]
        public async Task<IActionResult> ExportCSV(
            string keyword,
            string fromDate,
            string toDate,
            int? statusId,
            int? paymentId,
            int? storeId,
            bool allWithinScope = false)
        {
            try
            {
                var filter = await ResolveHistoryStoreFilterAsync(
                    OrderAccessActions.AdminExport,
                    storeId,
                    allWithinScope || !storeId.HasValue);
                if (filter.Failure != null) return filter.Failure;

                var data = await _adminOrderService.GetFilteredOrdersForExportAsync(
                    keyword,
                    fromDate,
                    toDate,
                    statusId,
                    paymentId,
                    filter.StoreIds);

                var csv = new System.Text.StringBuilder();
                // Thêm BOM để Excel nhận đúng UTF-8
                csv.Append('\uFEFF');
                csv.AppendLine("Mã đơn,Thời gian thanh toán,Cửa hàng,Thu ngân,Loại đơn,Tổng tiền,Thanh toán,Trạng thái tài chính,Đồng bộ,Hóa đơn,Tem,Kho");

                foreach (var o in data)
                {
                    csv.AppendLine(
                        $"#CC{o.OrderId:D5},{o.CreatedAt:dd/MM/yyyy HH:mm}," +
                        $"{Csv(o.StoreName)},{Csv(o.StaffName)},{Csv(o.OrderTypeName)}," +
                        $"{o.Total.ToString(CultureInfo.InvariantCulture)},{Csv(o.PaymentMethodName)},{Csv(o.OrderStatusName)}," +
                        $"{Csv(o.SyncState)},{Csv(o.ReceiptState)},{Csv(o.DrinkLabelState)}," +
                        $"{Csv(o.InventoryPostingState)}");
                }

                var fileName = $"LichSuDonHang_{DateTime.Now:yyyyMMdd}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Lỗi xuất file: " + ex.Message);
            }
        }

        [HttpGet("/api/AdminOrder/GetAuthorizedStores")]
        public async Task<IActionResult> GetAuthorizedStores()
        {
            var actor = _actor.Get(User);
            if (_orderAccessAuthorization.AuthorizeAction(
                    actor,
                    OrderAccessActions.AdminList) != OrderAccessDecision.Allowed)
            {
                return Forbid();
            }

            var scope = await _storeScopeResolver.ResolveAsync(actor);
            if (!scope.IsResolved) return StoreScopeApiFailure(scope);
            return Ok(scope.AccessibleStores);
        }

        private Task<Application.DTOs.Admin.StoreScope.AdminStoreScopeResolution> ResolveScopeAsync()
            => _storeScopeResolver.ResolveAsync(_actor.Get(User));

        private async Task<int?> ResolveStoreIdAsync()
        {
            var scope = await ResolveScopeAsync();
            return scope.IsResolved ? scope.StoreId : null;
        }

        private async Task<IActionResult?> GetOrderAccessFailureAsync(string action, int storeId)
        {
            var decision = await _orderAccessAuthorization.AuthorizeAsync(
                _actor.Get(User),
                action,
                storeId);

            return decision switch
            {
                OrderAccessDecision.Forbidden => Forbid(),
                OrderAccessDecision.NotFound => NotFound(),
                _ => null
            };
        }

        private async Task<HistoryStoreFilterResult> ResolveHistoryStoreFilterAsync(
            string action,
            int? requestedStoreId,
            bool allWithinScope)
        {
            var actor = _actor.Get(User);
            if (_orderAccessAuthorization.AuthorizeAction(actor, action)
                != OrderAccessDecision.Allowed)
            {
                return HistoryStoreFilterResult.Failed(Forbid());
            }

            if (requestedStoreId.HasValue && allWithinScope)
            {
                return HistoryStoreFilterResult.Failed(BadRequest(new
                {
                    message = "Chỉ được chọn một cửa hàng hoặc tất cả cửa hàng trong phạm vi."
                }));
            }

            var scope = await _storeScopeResolver.ResolveAsync(actor, requestedStoreId);
            if (!scope.IsResolved)
            {
                return HistoryStoreFilterResult.Failed(StoreScopeApiFailure(scope));
            }

            var storeIds = requestedStoreId.HasValue
                ? new[] { scope.StoreId!.Value }
                : allWithinScope
                    ? scope.AccessibleStores
                    .Select(store => store.StoreId)
                    .Distinct()
                    .ToArray()
                    : new[] { scope.StoreId!.Value };

            return storeIds.Length == 0
                ? HistoryStoreFilterResult.Failed(Forbid())
                : HistoryStoreFilterResult.Resolved(scope, storeIds);
        }

        private IActionResult StoreScopeApiFailure(
            Application.DTOs.Admin.StoreScope.AdminStoreScopeResolution scope)
        {
            if (scope.Status == Application.DTOs.Admin.StoreScope.AdminStoreScopeResolutionStatus.StoreNotFound
                || scope.Status == Application.DTOs.Admin.StoreScope.AdminStoreScopeResolutionStatus.RequestedStoreForbidden)
            {
                return NotFound(new
                {
                    code = scope.ErrorCode,
                    message = "Không tìm thấy cửa hàng trong phạm vi được cấp."
                });
            }

            return Forbid();
        }

        private sealed record HistoryStoreFilterResult(
            Application.DTOs.Admin.StoreScope.AdminStoreScopeResolution? Scope,
            IReadOnlyList<int> StoreIds,
            IActionResult? Failure)
        {
            public static HistoryStoreFilterResult Resolved(
                Application.DTOs.Admin.StoreScope.AdminStoreScopeResolution scope,
                IReadOnlyList<int> storeIds) =>
                new(scope, storeIds, null);

            public static HistoryStoreFilterResult Failed(IActionResult failure) =>
                new(null, Array.Empty<int>(), failure);
        }

        private static string Csv(string? value)
            => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
    }
}
