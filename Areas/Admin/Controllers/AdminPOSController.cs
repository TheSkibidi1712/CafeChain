using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.DTOs.POS;
using CafeChain.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Thin Controller cho POS — chỉ điều phối request/response, business logic nằm trong Services
    /// Tuân thủ: SRP, DIP, Thin Controller pattern
    /// </summary>
    [Area("Admin")]
    [Authorize]
    public class AdminPOSController : Controller
    {
        private readonly IWorkShiftService _workShiftService;
        private readonly IPOSOrderService _orderService;
        private readonly ISupervisorAuthService _supervisorAuthService;
        private readonly AppDbContext _context;

        public AdminPOSController(
            IWorkShiftService workShiftService,
            IPOSOrderService orderService,
            ISupervisorAuthService supervisorAuthService,
            AppDbContext context)
        {
            _workShiftService = workShiftService;
            _orderService = orderService;
            _supervisorAuthService = supervisorAuthService;
            _context = context;
        }

        // ============================================================
        // VIEW: Main POS Screen
        // ============================================================
        public async Task<IActionResult> Index()
        {
            var accountIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(accountIdStr) || !int.TryParse(accountIdStr, out int accountId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var isCashier = role == CafeChain.Application.Constants.RoleConstants.Cashier;
            var isShiftSupervisor = role == CafeChain.Application.Constants.RoleConstants.ShiftSupervisor;
            var isStoreManager = role == CafeChain.Application.Constants.RoleConstants.StoreManager;

            if (!(isCashier || isShiftSupervisor || isStoreManager))
                return RedirectToAction("AccessDenied", "Account", new { area = "" });

            var staff = await _context.Staffs
                .Include(s => s.Store)
                .FirstOrDefaultAsync(s => s.AccountId == accountId);

            if (staff == null) return RedirectToAction("AccessDenied", "Account", new { area = "" });

            ViewBag.StaffName = staff.FullName;
            ViewBag.StoreName = staff.Store?.Name ?? "N/A";
            ViewBag.StaffId = staff.StaffId;
            ViewBag.StoreId = staff.StoreId;
            ViewBag.Role = role;

            return View();
        }

        // ============================================================
        // API: Get Active Shift
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetActiveShift()
        {
            var (userId, storeId) = await ResolveUserStoreAsync();
            if (userId == 0) return Json(new { success = false, message = "Không xác định được tài khoản." });

            var shift = await _workShiftService.GetActiveShiftAsync(userId, storeId);
            if (shift == null)
                return Json(new { success = true, hasActiveShift = false });

            return Json(new
            {
                success = true,
                hasActiveShift = true,
                shift = new { shift.ShiftId, startTime = shift.StartTime.ToString("o"), shift.StartingCash }
            });
        }

        // ============================================================
        // API: Open Shift — delegate to WorkShiftService
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> OpenShift([FromBody] OpenShiftRequest request)
        {
            var (userId, storeId) = await ResolveUserStoreAsync();
            if (userId == 0) return Json(new { success = false, message = "Không xác định được tài khoản." });

            try
            {
                var result = await _workShiftService.OpenShiftAsync(userId, storeId, request?.StartingCash ?? 0);
                return Json(new { success = result.IsSuccess, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // ============================================================
        // API: Close Shift — delegate to WorkShiftService
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> CloseShift([FromBody] CafeChain.Application.DTOs.POS.CloseShiftRequestDto request)
        {
            var (userId, storeId) = await ResolveUserStoreAsync();
            if (userId == 0) return Json(new { success = false, message = "Không xác định được tài khoản." });

            try
            {
                var result = await _workShiftService.CloseShiftAsync(userId, storeId, request);
                return Json(new { success = result.IsSuccess, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // ============================================================
        // API: Get Menu Data — delegate to POSOrderService
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetMenuData()
        {
            var (_, storeId) = await ResolveUserStoreAsync();
            var result = await _orderService.GetMenuDataAsync(storeId);

            if (!result.IsSuccess) return Json(new { success = false, message = result.Message });
            return Json(new { success = true, categories = ((dynamic)result.Data).categories, storeToppings = ((dynamic)result.Data).storeToppings });
        }

        // ============================================================
        // API: Commit POS Order — delegate to POSOrderService
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> CommitOrder([FromBody] POSOrderCommitDto dto)
        {
            if (dto == null || dto.Items == null || !dto.Items.Any())
                return Json(new { success = false, message = "Giỏ hàng trống." });

            var (userId, storeId) = await ResolveUserStoreAsync();
            if (userId == 0) return Json(new { success = false, message = "Không xác định được tài khoản." });

            var result = await _orderService.CommitOrderAsync(dto, userId, storeId);
            return Json(new { success = result.IsSuccess, message = result.Message, data = result.Data });
        }

        // ============================================================
        // API: Search Customer — delegate to POSOrderService
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> SearchCustomer(string phone)
        {
            var result = await _orderService.SearchCustomerAsync(phone);
            if (!result.IsSuccess) return Json(new { success = false, message = result.Message });
            return Json(new { success = true, customer = result.Data });
        }

        // ============================================================
        // API: Get Close Shift Data — delegate to POSOrderService
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetCloseShiftData()
        {
            var (userId, storeId) = await ResolveUserStoreAsync();
            if (userId == 0) return Json(new { success = false, message = "Không xác định được tài khoản." });

            var result = await _orderService.GetCloseShiftDataAsync(userId, storeId);
            if (!result.IsSuccess) return Json(new { success = false, message = result.Message });
            return Json(new { success = true, shift = result.Data });
        }

        // ============================================================
        // API: Supervisor PIN Authorization — delegate to SupervisorAuthService
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> AuthorizeSupervisor([FromBody] SupervisorAuthRequestDto request)
        {
            try
            {
                var (userId, storeId) = await ResolveUserStoreAsync();
                if (userId == 0) return Json(new { success = false, message = "Không xác định được tài khoản." });

                var result = await _supervisorAuthService.AuthorizePinAsync(
                    request.Pin, userId, storeId, request.ActionName, request.TargetId, request.Reason);

                var remaining = await _supervisorAuthService.GetRemainingAttemptsAsync(storeId);
                return Json(new { success = result.IsSuccess, message = result.Message, remainingAttempts = remaining });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // ============================================================
        // API: Sync Offline Orders
        // ============================================================
        [HttpPost("Admin/AdminPOS/SyncOfflineOrders")]
        public async Task<IActionResult> SyncOfflineOrders([FromBody] System.Collections.Generic.List<OfflineOrderSyncDTO> offlineOrders)
        {
            if (offlineOrders == null || offlineOrders.Count == 0)
                return Json(new { success = false, message = "Không có dữ liệu đồng bộ." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                int syncedCount = 0;
                foreach (var orderDto in offlineOrders)
                {
                    var newOrder = new CafeChain.Models.Orders.Order
                    {
                        Total = orderDto.TotalAmount,
                        OrderTypeId = orderDto.OrderTypeId > 0 ? orderDto.OrderTypeId : 1,
                        OrderStatusId = 4,
                        CreatedAt = DateTime.UtcNow,
                        StoreId = orderDto.StoreId ?? 1,
                        Note = "[OFFLINE-SYNC] " + orderDto.Note
                    };
                    _context.Orders.Add(newOrder);
                    await _context.SaveChangesAsync();

                    foreach (var item in orderDto.Details)
                    {
                        _context.OrderDetails.Add(new CafeChain.Models.Orders.OrderDetail
                        {
                            OrderId = newOrder.OrderId, DrinkId = item.ItemId,
                            Quantity = item.Quantity, Price = item.UnitPrice
                        });
                    }
                    await _context.SaveChangesAsync();
                    syncedCount++;
                }

                await transaction.CommitAsync();
                return Json(new { success = true, message = $"Đã đồng bộ thành công {syncedCount} đơn hàng ngoại tuyến." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống khi đồng bộ: " + ex.Message });
            }
        }

        // ============================================================
        // PRIVATE: Resolve UserId (StaffId) and StoreId from Claims
        // ============================================================
        private async Task<(int userId, int storeId)> ResolveUserStoreAsync()
        {
            var accountIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(accountIdStr) || !int.TryParse(accountIdStr, out int accountId))
                return (0, 0);

            var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.AccountId == accountId);
            if (staff == null) return (0, 0);

            var storeIdClaim = User.FindFirst("StoreId")?.Value;
            int storeId = int.TryParse(storeIdClaim, out int sid) ? sid : staff.StoreId;

            return (staff.StaffId, storeId);
        }
    }

    // ============================================================
    // Internal DTOs (small, controller-scoped)
    // ============================================================
    public class OpenShiftRequest
    {
        public decimal StartingCash { get; set; }
    }
}
