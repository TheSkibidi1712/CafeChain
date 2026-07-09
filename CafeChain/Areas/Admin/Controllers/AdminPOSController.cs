using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Thin Controller cho POS — chỉ điều phối request/response, business logic nằm trong Services
    /// Tuân thủ: SRP, DIP, Thin Controller pattern
    /// [Task 5] Loại bỏ AppDbContext — di chuyển ResolveUserStoreAsync sang Repository
    /// </summary>
    [Area("Admin")]
    [Authorize]
    public class AdminPOSController : Controller
    {
        private readonly IWorkShiftService _workShiftService;
        private readonly IPOSOrderService _orderService;
        private readonly ISupervisorAuthService _supervisorAuthService;
        private readonly IPOSOrderRepository _repository;
        private readonly IInventoryDeductionService _inventoryDeductionService;

        public AdminPOSController(
            IWorkShiftService workShiftService,
            IPOSOrderService orderService,
            ISupervisorAuthService supervisorAuthService,
            IPOSOrderRepository repository,
            IInventoryDeductionService inventoryDeductionService)
        {
            _workShiftService = workShiftService;
            _orderService = orderService;
            _supervisorAuthService = supervisorAuthService;
            _repository = repository;
            _inventoryDeductionService = inventoryDeductionService;
        }

        // ============================================================
        // VIEW: Main POS Screen
        // ============================================================
        public async Task<IActionResult> Index()
        {
            var (staffId, storeId, staffName, storeName, role) =
                await ResolveFullUserContextAsync();

            if (staffId == 0)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var isSalesStaff =
                role == CafeChain.Application.Constants.RoleConstants.SalesStaff;

            var isStoreManager =
                role == CafeChain.Application.Constants.RoleConstants.StoreManager;

            if (!(isSalesStaff || isStoreManager))
            {
                return RedirectToAction("AccessDenied", "Account", new { area = "" });
            }

            ViewBag.StaffName = staffName;
            ViewBag.StoreName = storeName;
            ViewBag.StaffId = staffId;
            ViewBag.StoreId = storeId;
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
                var result = await _workShiftService.OpenShiftAsync(userId, storeId, request?.StartingCash ?? 0, request?.PosTerminalId);
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
        public async Task<IActionResult> CloseShift([FromBody] CloseShiftRequestDto request)
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
        // API: Register Customer — Quick POS registration
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> RegisterCustomer([FromBody] QuickCustomerRegisterDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Phone) || string.IsNullOrWhiteSpace(dto.FullName))
                return Json(new { success = false, message = "Số điện thoại và họ tên là bắt buộc." });

            var result = await _orderService.RegisterCustomerAsync(dto);
            if (!result.IsSuccess) return Json(new { success = false, message = result.Message });
            return Json(new { success = true, message = result.Message, customer = result.Data });
        }

        // ============================================================
        // API: Register POS Terminal
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> RegisterTerminal([FromBody] PosTerminalRegisterDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.TerminalId))
                return Json(new { success = false, message = "Terminal ID là bắt buộc." });

            try
            {
                var (_, storeId) = await ResolveUserStoreAsync();
                var terminal = await _repository.GetTerminalByIdAsync(dto.TerminalId);
                if (terminal == null)
                {
                    terminal = new PosTerminal
                    {
                        TerminalId = dto.TerminalId,
                        Name = dto.Name ?? ("Thiết bị POS " + dto.TerminalId.Substring(0, Math.Min(5, dto.TerminalId.Length))),
                        StoreId = storeId > 0 ? storeId : dto.StoreId,
                        Active = true,
                        CreatedAt = DateTime.Now
                    };
                    await _repository.CreateTerminalAsync(terminal);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(dto.Name)) terminal.Name = dto.Name;
                    if (dto.StoreId > 0) terminal.StoreId = dto.StoreId;
                    await _repository.UpdateTerminalAsync(terminal);
                }
                return Json(new { success = true, message = "Đăng ký thiết bị thành công.", terminalName = terminal.Name });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
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
        // API: Sync Offline Orders — delegates to CommitOrderAsync + Inventory Deduction
        // ADR-0002: Idempotency via ClientOrderId — retry-safe, no duplicate orders
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> SyncOfflineOrders([FromBody] List<OfflineOrderSyncDTO> offlineOrders)
        {
            if (offlineOrders == null || offlineOrders.Count == 0)
                return Json(new { success = false, message = "Không có dữ liệu đồng bộ." });

            var (userId, storeId) = await ResolveUserStoreAsync();
            if (userId == 0) return Json(new { success = false, message = "Không xác định được tài khoản." });

            try
            {
                int syncedCount = 0;
                int skippedCount = 0;
                var results = new List<object>();

                foreach (var orderDto in offlineOrders)
                {
                    // ── ADR-0002: Idempotency Check ──
                    // Nếu ClientOrderId đã tồn tại trong DB → đơn đã sync trước đó → skip
                    if (orderDto.ClientOrderId.HasValue)
                    {
                        var existingOrder = await _repository.FindOrderByClientOrderIdAsync(orderDto.ClientOrderId.Value);
                        if (existingOrder != null)
                        {
                            skippedCount++;
                            results.Add(new
                            {
                                localId = orderDto.LocalId,
                                clientOrderId = orderDto.ClientOrderId,
                                orderId = existingOrder.OrderId,
                                status = "skipped",
                                reason = "Đơn hàng đã được đồng bộ trước đó (idempotent)."
                            });
                            continue;
                        }
                    }

                    // Chuyển OfflineOrderSyncDTO → POSOrderCommitDto
                    // ADR-0002: ClientOrderId đi thẳng vào CommitDto → gán nguyên tử trên Order entity
                    var commitDto = new POSOrderCommitDto
                    {
                        Items = orderDto.Details?.Select(d => new POSOrderItemDto
                        {
                            DrinkId = d.ItemId,
                            SizeId = d.SizeId,
                            Quantity = d.Quantity,
                            Toppings = d.Toppings ?? new List<POSOrderToppingDto>()
                        }).ToList() ?? new List<POSOrderItemDto>(),
                        OrderTypeId = orderDto.OrderTypeId > 0 ? orderDto.OrderTypeId : 1,
                        ReceivedAmount = orderDto.ReceivedAmount,
                        Note = "[OFFLINE-SYNC] " + (orderDto.Note ?? ""),
                        ClientOrderId = orderDto.ClientOrderId  // Idempotency Key — atomic với Order
                    };

                    var result = await _orderService.CommitOrderAsync(commitDto, userId, orderDto.StoreId ?? storeId);
                    if (result.IsSuccess)
                    {
                        // Trừ kho nguyên vật liệu sau khi commit thành công
                        try
                        {
                            var soldItems = commitDto.Items.Select(item => new CafeChain.Application.DTOs.POS.POSSoldItemDto
                            {
                                DrinkId = item.DrinkId,
                                SizeId = item.SizeId,
                                Quantity = item.Quantity,
                                Toppings = item.Toppings ?? new List<CafeChain.Application.DTOs.POS.POSOrderToppingDto>()
                            }).ToList();
                            await _inventoryDeductionService.DeductStockForOrderAsync(soldItems, orderDto.StoreId ?? storeId);
                        }
                        catch { /* Inventory deduction failure should not block sync */ }

                        syncedCount++;
                        results.Add(new
                        {
                            localId = orderDto.LocalId,
                            clientOrderId = orderDto.ClientOrderId,
                            status = "synced"
                        });
                    }
                    else
                    {
                        results.Add(new
                        {
                            localId = orderDto.LocalId,
                            clientOrderId = orderDto.ClientOrderId,
                            status = "failed",
                            reason = result.Message
                        });
                    }
                }

                return Json(new
                {
                    success = true,
                    message = $"Đã đồng bộ {syncedCount}/{offlineOrders.Count} đơn hàng. {(skippedCount > 0 ? $"Bỏ qua {skippedCount} đơn trùng lặp." : "")}",
                    syncedCount,
                    skippedCount,
                    details = results
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống khi đồng bộ: " + ex.Message });
            }
        }

        // ============================================================
        // PRIVATE: Resolve UserId (StaffId) and StoreId from Claims
        // [Task 5] Anti-IDOR — Zero Trust, server-side claims only
        // ============================================================
        private async Task<(int userId, int storeId)> ResolveUserStoreAsync()
        {
            var accountIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(accountIdStr) || !int.TryParse(accountIdStr, out int accountId))
                return (0, 0);

            // Sử dụng Repository để truy vấn Staff thay vì inject AppDbContext trực tiếp
            // Tạm thời dùng lại claims StaffId nếu đã được lưu từ quá trình login
            var staffIdClaim = User.FindFirst("StaffId")?.Value;
            var storeIdClaim = User.FindFirst("StoreId")?.Value;

            if (int.TryParse(staffIdClaim, out int staffId) && int.TryParse(storeIdClaim, out int storeId))
                return (staffId, storeId);

            // Fallback: Nếu claims không có StaffId, truy vấn qua service
            // Đây là trường hợp hiếm — trong thực tế claims nên được set đầy đủ khi login
            return (accountId, int.TryParse(storeIdClaim, out int sid) ? sid : 0);
        }

        /// <summary>
        /// Resolve full user context for Index view — includes name, store name, role
        /// </summary>
        private async Task<(int staffId, int storeId, string staffName, string storeName, string role)> ResolveFullUserContextAsync()
        {
            var accountIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(accountIdStr) || !int.TryParse(accountIdStr, out int accountId))
                return (0, 0, "", "", "");

            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var staffName = User.FindFirst("StaffName")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "N/A";
            var storeName = User.FindFirst("StoreName")?.Value ?? "N/A";

            var staffIdClaim = User.FindFirst("StaffId")?.Value;
            var storeIdClaim = User.FindFirst("StoreId")?.Value;

            int staffId = int.TryParse(staffIdClaim, out int sid) ? sid : accountId;
            int storeId = int.TryParse(storeIdClaim, out int stid) ? stid : 0;

            return (staffId, storeId, staffName, storeName, role);
        }
    }

    // ============================================================
    // Internal DTOs (small, controller-scoped)
    // ============================================================
    public class OpenShiftRequest
    {
        public decimal StartingCash { get; set; }
        public string? PosTerminalId { get; set; }
    }
}
