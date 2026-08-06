using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.DTOs.Admin.Actor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// POS Order API — Issue #64 + #65 + #66 + #68
    /// 
    /// Endpoints:
    ///   POST /api/v1/pos/orders/commit        — Issue #64/#65: Commit single order
    ///   POST /api/v1/pos/orders/sync-offline   — Issue #66: Batch sync offline orders
    ///   GET  /api/v1/pos/orders                — Issue #68: Order history (paginated)
    /// 
    /// Auth: JWT Bearer (iPad POS app)
    /// </summary>
    [Route("api/v1/pos/orders")]
    public class POSOrderController : PosApiController
    {
        private readonly IPOSOrderService _orderService;
        private readonly IInventoryDeductionService _inventoryService;
        private readonly ILogger<POSOrderController> _logger;
        private readonly IOrderAccessAuthorizationService? _orderAccessAuthorization;
        private readonly IPosSessionExchangeService? _posSessionExchangeService;

        public POSOrderController(
            IPOSOrderService orderService,
            IInventoryDeductionService inventoryService,
            ILogger<POSOrderController> logger,
            IOrderAccessAuthorizationService? orderAccessAuthorization = null,
            IPosSessionExchangeService? posSessionExchangeService = null)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
            _logger = logger;
            _orderAccessAuthorization = orderAccessAuthorization;
            _posSessionExchangeService = posSessionExchangeService;
        }

        // ============================================================
        // POST /api/v1/pos/orders/commit — Issue #64 + #65
        // ============================================================

        /// <summary>
        /// Commit đơn hàng POS — tạo Order mới hoặc trả Order cũ (idempotent).
        /// Side-effects: Inventory deduction + Print (fire-and-forget).
        /// </summary>
        [HttpPost("commit")]
        public async Task<IActionResult> CommitOrder([FromBody] POSOrderCommitDto dto)
        {
            var openingCashGuard = await EnsureOpeningCashReadyAsync();
            if (openingCashGuard != null) return openingCashGuard;

            if (dto == null || dto.Items == null || !dto.Items.Any())
                return BadRequest(new { success = false, message = "Giỏ hàng trống." });

            var result = await _orderService.CommitOrderAsync(dto, CurrentStaffId, CurrentStoreId);

            if (!result.IsSuccess)
            {
                var payload = new { success = false, message = result.Message, errorCode = result.ErrorCode };
                return POSCatalogSaleErrorCodes.IsConflict(result.ErrorCode) ||
                    string.Equals(result.ErrorCode, "IDEMPOTENCY_KEY_REUSED", StringComparison.Ordinal)
                    ? Conflict(payload)
                    : Ok(payload);
            }

            var orderId = ExtractOrderId(result.Data);
            var requiresPayment = RequiresPaymentResponse(result.Data);

            // Side-effects: only paid/committed orders attempt Inventory Deduction.
            // The inventory service is idempotent by ReferenceOrderId, so retries can repair
            // a missing post-commit deduction without deducting twice.
            var inventoryWarnings = orderId.HasValue && !requiresPayment
                ? await DeductInventorySafeAsync(dto.Items, CurrentStoreId, orderId.Value)
                : null;

            return Ok(new
            {
                success = true,
                message = result.Message,
                data = result.Data,
                inventoryWarnings
            });
        }

        // ============================================================
        // POST /api/v1/pos/orders/sync-offline — Issue #66
        // ============================================================

        /// <summary>
        /// Batch sync offline orders — xử lý từng đơn qua CommitOfflineSyncedOrderAsync.
        /// 
        /// Response trả partial success:
        ///   - results[]: { clientOrderId, status, orderId?, error? }
        ///   - status: "created" | "duplicate" | "failed"
        /// </summary>
        [HttpPost("sync-offline")]
        [Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
        public async Task<IActionResult> SyncOfflineOrders([FromBody] OfflineBatchSyncRequestDto request)
        {
            var openingCashGuard = await EnsureOpeningCashReadyAsync();
            if (openingCashGuard != null) return openingCashGuard;

            if (request?.Orders == null || !request.Orders.Any())
                return BadRequest(new { success = false, message = "Không có đơn hàng để đồng bộ." });

            var actor = GetCurrentActor();
            if (_orderAccessAuthorization?.AuthorizeAction(actor, OrderAccessActions.OfflineSync)
                != OrderAccessDecision.Allowed)
            {
                return Forbid();
            }

            var results = new List<OfflineSyncItemResult>();
            int createdCount = 0;
            int duplicateCount = 0;
            int failedCount = 0;

            foreach (var orderDto in request.Orders)
            {
                var itemResult = new OfflineSyncItemResult
                {
                    ClientOrderId = orderDto.ClientOrderId?.ToString() ?? orderDto.LocalId ?? "unknown"
                };

                try
                {
                    var validationError = ValidateOfflineSyncOrder(orderDto);
                    if (validationError != null)
                    {
                        itemResult.Status = "failed";
                        itemResult.Error = validationError;
                        failedCount++;
                        results.Add(itemResult);
                        continue;
                    }

                    // Chuyển OfflineOrderSyncDTO → POSOrderCommitDto
                    var commitDto = new POSOrderCommitDto
                    {
                        Items = orderDto.Details?.Select(d => new POSOrderItemDto
                        {
                            DrinkId = d.ItemId,
                            SizeId = d.SizeId,
                            StoreMenuItemId = d.StoreMenuItemId,
                            DrinkSizeId = d.DrinkSizeId,
                            AcceptedBasePrice = d.AcceptedBasePrice,
                            AcceptedUnitPrice = d.UnitPrice,
                            PriceSource = d.PriceSource,
                            CatalogVersion = d.CatalogVersion,
                            Quantity = d.Quantity,
                            IceLevelPercent = d.IceLevelPercent,
                            Note = d.Note,
                            Toppings = d.Toppings ?? new List<POSOrderToppingDto>()
                        }).ToList() ?? new List<POSOrderItemDto>(),
                        OrderTypeId = orderDto.OrderTypeId > 0 ? orderDto.OrderTypeId : 1,
                        ReceivedAmount = orderDto.ReceivedAmount,
                        PaymentMethodId = orderDto.PaymentMethodId,
                        Note = "[OFFLINE-SYNC] " + (orderDto.Note ?? ""),
                        ClientOrderId = orderDto.ClientOrderId,
                        SkipPrint = true  // Offline sync — không in bill
                    };

                    var commitResult = await _orderService.CommitOfflineSyncedOrderAsync(
                        commitDto,
                        new OfflineOrderSyncContext
                        {
                            ActorStaffId = actor.StaffId,
                            ActorRoleNames = actor.RoleNames,
                            ClaimedStaffId = orderDto.StaffId!.Value,
                            ClaimedStoreId = orderDto.StoreId!.Value,
                            WorkShiftId = orderDto.WorkShiftId!.Value,
                            SoldAt = orderDto.SoldAt ?? DateTime.Now
                        });

                    if (commitResult.IsSuccess)
                    {
                        // Kiểm tra isIdempotent flag trong response data
                        var isIdempotent = IsIdempotentResponse(commitResult.Data);

                        if (isIdempotent)
                        {
                            itemResult.Status = "duplicate";
                            itemResult.OrderId = ExtractOrderId(commitResult.Data);
                            duplicateCount++;

                            if (itemResult.OrderId.HasValue)
                            {
                                var committedStoreId = ExtractStoreId(commitResult.Data);
                                if (!committedStoreId.HasValue)
                                {
                                    itemResult.Status = "failed";
                                    itemResult.Error = "Không xác định được cửa hàng của đơn đã đồng bộ.";
                                    failedCount++;
                                    duplicateCount--;
                                    results.Add(itemResult);
                                    continue;
                                }

                                await DeductInventorySafeAsync(
                                    commitDto.Items,
                                    committedStoreId.Value,
                                    itemResult.OrderId.Value);
                            }
                        }
                        else
                        {
                            itemResult.Status = "created";
                            itemResult.OrderId = ExtractOrderId(commitResult.Data);
                            createdCount++;

                            // Side-effects: Inventory Deduction chạy qua guard committed-order.
                            if (itemResult.OrderId.HasValue)
                            {
                                var committedStoreId = ExtractStoreId(commitResult.Data);
                                if (!committedStoreId.HasValue)
                                {
                                    itemResult.Status = "failed";
                                    itemResult.Error = "Không xác định được cửa hàng của đơn đã đồng bộ.";
                                    failedCount++;
                                    createdCount--;
                                    results.Add(itemResult);
                                    continue;
                                }

                                await DeductInventorySafeAsync(
                                    commitDto.Items,
                                    committedStoreId.Value,
                                    itemResult.OrderId.Value);
                            }
                        }
                    }
                    else
                    {
                        if (commitResult.ErrorCode == OrderAccessErrorCodes.Forbidden)
                            return Forbid();

                        if (commitResult.ErrorCode == OrderAccessErrorCodes.WorkShiftNotFound)
                        {
                            return NotFound(new
                            {
                                success = false,
                                message = "Không tìm thấy WorkShift gốc cho đơn offline."
                            });
                        }

                        itemResult.Status = "failed";
                        itemResult.Error = commitResult.Message;
                        itemResult.ErrorCode = commitResult.ErrorCode;
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[SyncOffline] Exception processing ClientOrderId={ClientOrderId}",
                        orderDto.ClientOrderId);
                    itemResult.Status = "failed";
                    itemResult.Error = "Lỗi hệ thống: " + ex.Message;
                    failedCount++;
                }

                results.Add(itemResult);
            }

            return Ok(new
            {
                success = true,
                message = $"Đồng bộ hoàn tất: {createdCount} tạo mới, {duplicateCount} trùng lặp, {failedCount} lỗi.",
                summary = new { createdCount, duplicateCount, failedCount, total = request.Orders.Count },
                results
            });
        }

        private async Task<IActionResult?> EnsureOpeningCashReadyAsync()
        {
            if (_posSessionExchangeService == null) return null;
            if (CurrentExchangeContextId <= 0)
                return Conflict(new
                {
                    success = false,
                    errorCode = WorkShiftErrorCodes.StaffHubOpenRequired,
                    recommendedAction = WorkShiftRecommendedActions.OpenStaffHub,
                    message = "Phiên POS không có ngữ cảnh StaffHub hợp lệ."
                });

            var context = await _posSessionExchangeService.GetContextAsync(
                CurrentExchangeContextId,
                CurrentAccountId,
                CurrentStaffId,
                CurrentStoreId);
            if (context == null)
                return Conflict(new
                {
                    success = false,
                    errorCode = WorkShiftErrorCodes.StaffHubOpenRequired,
                    recommendedAction = WorkShiftRecommendedActions.OpenStaffHub,
                    message = "Ngữ cảnh POS đã hết hạn. Vui lòng mở lại từ StaffHub."
                });
            if (!context.RequiresOpeningCash) return null;

            return Conflict(new
            {
                success = false,
                errorCode = WorkShiftErrorCodes.OpeningCashRequired,
                recommendedAction = WorkShiftRecommendedActions.EnterOpeningCash,
                workShiftId = context.WorkShiftId,
                message = "Vui lòng xác nhận tiền đầu phiên trước khi bán hàng."
            });
        }

        // ============================================================
        // GET /api/v1/pos/orders — Issue #68
        // ============================================================

        /// <summary>
        /// Lấy lịch sử đơn hàng POS có phân trang.
        /// Query: ?page=1&amp;pageSize=20
        /// Response: { items[], pagination { page, pageSize, totalCount, totalPages } }
        /// </summary>
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
        public async Task<IActionResult> GetOrderHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var access = await AuthorizeCurrentStoreAsync(OrderAccessActions.PosHistory);
            if (access == OrderAccessDecision.Forbidden)
                return Forbid();
            if (access == OrderAccessDecision.NotFound)
                return NotFound();

            var result = await _orderService.GetOrderHistoryAsync(CurrentStoreId, page, pageSize);

            if (!result.IsSuccess)
                return Ok(new { success = false, message = result.Message });

            return Ok(new { success = true, data = result.Data });
        }

        // ============================================================
        // POST /api/v1/pos/orders/{orderId}/reprint — Issue #83
        // ============================================================

        /// <summary>
        /// Gửi lệnh in lại hóa đơn hoặc tem từ Order Detail drawer.
        /// Không tạo đơn, không tạo payment, không trừ kho.
        /// </summary>
        [HttpPost("{orderId:int}/reprint")]
        [Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
        public async Task<IActionResult> ReprintOrder(
            [FromRoute] int orderId,
            [FromBody] POSOrderReprintRequestDto dto)
        {
            var access = await AuthorizeCurrentStoreAsync(OrderAccessActions.Reprint);
            if (access == OrderAccessDecision.Forbidden)
                return Forbid();
            if (access == OrderAccessDecision.NotFound)
                return NotFound();

            var result = await _orderService.ReprintOrderAsync(orderId, dto, CurrentStoreId);

            if (!result.IsSuccess)
            {
                if (result.ErrorCode == OrderAccessErrorCodes.NotFound)
                    return NotFound(new { success = false, message = result.Message });

                return Ok(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message, data = result.Data });
        }

        // ============================================================
        // PRIVATE HELPERS
        // ============================================================

        private AdminActorContext GetCurrentActor()
        {
            var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value)
                .Concat(User.FindAll("role").Select(claim => claim.Value))
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AdminActorContext
            {
                StaffId = CurrentStaffId,
                StoreId = CurrentStoreId,
                RoleNames = roles
            };
        }

        private Task<OrderAccessDecision> AuthorizeCurrentStoreAsync(string action)
        {
            if (_orderAccessAuthorization == null)
                return Task.FromResult(OrderAccessDecision.Forbidden);

            return _orderAccessAuthorization.AuthorizeAsync(
                GetCurrentActor(),
                action,
                CurrentStoreId);
        }

        private static string? ValidateOfflineSyncOrder(OfflineOrderSyncDTO orderDto)
        {
            if (!orderDto.ClientOrderId.HasValue)
                return "Thiếu ClientOrderId cho đơn offline.";

            if (!orderDto.StoreId.HasValue)
                return "Thiếu StoreId cho đơn offline.";

            if (!orderDto.StaffId.HasValue)
                return "Thiếu StaffId cho đơn offline.";

            if (!orderDto.WorkShiftId.HasValue)
                return "Thiếu WorkShiftId cho đơn offline.";

            if (orderDto.PaymentMethodId != 1 ||
                (orderDto.PaymentSnapshot != null && orderDto.PaymentSnapshot.PaymentMethodId != 1))
            {
                return "Offline Sync chỉ hỗ trợ thanh toán tiền mặt.";
            }

            if (orderDto.Details == null || !orderDto.Details.Any())
                return "Giỏ hàng offline trống.";

            if (orderDto.Details.Any(item =>
                !item.StoreMenuItemId.HasValue
                || !item.DrinkSizeId.HasValue
                || !item.SizeId.HasValue
                || !item.AcceptedBasePrice.HasValue
                || !item.CatalogVersion.HasValue
                || string.IsNullOrWhiteSpace(item.PriceSource)
                || item.Quantity <= 0
                || item.UnitPrice < 0))
            {
                return $"{POSCatalogSaleErrorCodes.SnapshotRequired}: Thiếu snapshot Store Menu cho đơn offline.";
            }

            var calculatedTotal = orderDto.Details.Sum(item => item.UnitPrice * item.Quantity);
            if (calculatedTotal != orderDto.TotalAmount
                || orderDto.Details.Any(item => item.TotalPrice != item.UnitPrice * item.Quantity))
                return $"{POSCatalogSaleErrorCodes.SnapshotInvalid}: Tổng đơn offline không khớp snapshot chi tiết.";

            if (orderDto.PaymentSnapshot == null
                || orderDto.PaymentSnapshot.Amount != orderDto.TotalAmount
                || orderDto.PaymentSnapshot.ReceivedAmount < orderDto.TotalAmount
                || orderDto.PaymentSnapshot.ChangeAmount
                    != orderDto.PaymentSnapshot.ReceivedAmount - orderDto.TotalAmount
                || orderDto.ReceivedAmount != orderDto.PaymentSnapshot.ReceivedAmount
                || orderDto.ChangeAmount != orderDto.PaymentSnapshot.ChangeAmount)
            {
                return $"{POSCatalogSaleErrorCodes.SnapshotInvalid}: Snapshot thanh toán offline không hợp lệ.";
            }

            if (orderDto.CartSnapshot == null || orderDto.CartSnapshot.Count != orderDto.Details.Count)
                return $"{POSCatalogSaleErrorCodes.SnapshotInvalid}: Cart snapshot offline không khớp chi tiết đơn.";

            for (var index = 0; index < orderDto.Details.Count; index++)
            {
                var detail = orderDto.Details[index];
                var cart = orderDto.CartSnapshot[index];
                if (cart.MenuItemId != detail.ItemId
                    || cart.StoreMenuItemId != detail.StoreMenuItemId
                    || cart.DrinkSizeId != detail.DrinkSizeId
                    || cart.SizeId != detail.SizeId
                    || cart.Quantity != detail.Quantity
                    || cart.UnitPrice != detail.UnitPrice
                    || cart.EffectivePrice != detail.AcceptedBasePrice
                    || cart.CatalogVersion != detail.CatalogVersion
                    || !string.Equals(cart.PriceSource, detail.PriceSource, StringComparison.Ordinal))
                {
                    return $"{POSCatalogSaleErrorCodes.SnapshotInvalid}: Cart snapshot offline bị thay đổi so với chi tiết đơn.";
                }

                var detailToppings = (detail.Toppings ?? new List<POSOrderToppingDto>())
                    .OrderBy(x => x.ToppingId)
                    .Select(x => new { x.ToppingId, Price = x.AcceptedPrice })
                    .ToArray();
                var cartToppings = (cart.Toppings ?? new List<OfflineCartSnapshotToppingDTO>())
                    .OrderBy(x => x.ToppingId)
                    .Select(x => new { x.ToppingId, Price = x.AcceptedPrice ?? x.Price })
                    .ToArray();
                if (!detailToppings.SequenceEqual(cartToppings))
                    return $"{POSCatalogSaleErrorCodes.SnapshotInvalid}: Topping snapshot offline không khớp.";
            }

            // Soft-removal: reject legacy offline voucher/loyalty payloads (no silent reprice).
            if (!string.IsNullOrWhiteSpace(orderDto.VoucherCode)
                || orderDto.PointsUsed > 0
                || orderDto.VoucherDiscount > 0
                || orderDto.PointDiscount > 0)
            {
                return $"{ProductScopeErrorCodes.FeatureNotAvailable}: {ProductScopeErrorCodes.VoucherOrLoyaltyNotAvailableMessage}";
            }

            return null;
        }

        private static int? ExtractStoreId(object? data)
        {
            if (data == null) return null;
            var property = data.GetType().GetProperty("storeId")
                ?? data.GetType().GetProperty("StoreId");
            if (property == null) return null;

            var value = property.GetValue(data);
            return value is int storeId ? storeId : null;
        }

        /// <summary>
        /// Fire-and-forget inventory deduction — lỗi KHÔNG fail đơn đã commit
        /// </summary>
        private async Task<List<string>> DeductInventorySafeAsync(
            List<POSOrderItemDto> items,
            int storeId,
            int referenceOrderId)
        {
            try
            {
                var soldItems = items.Select(item => new POSSoldItemDto
                {
                    DrinkId = item.DrinkId,
                    SizeId = item.SizeId,
                    Quantity = item.Quantity,
                    Toppings = item.Toppings ?? new List<POSOrderToppingDto>()
                }).ToList();

                var inventoryResult = await _inventoryService.DeductStockForCommittedOrderAsync(
                    soldItems,
                    storeId,
                    referenceOrderId);

                if (inventoryResult.IsSuccess && inventoryResult.Errors != null && inventoryResult.Errors.Any())
                {
                    _logger.LogWarning(
                        "[POSOrderController] Inventory warnings: {WarningCount} items thiếu kho",
                        inventoryResult.Errors.Count);
                    return inventoryResult.Errors;
                }
                else if (!inventoryResult.IsSuccess)
                {
                    _logger.LogError(
                        "[POSOrderController] Inventory deduction failed: {Message}",
                        inventoryResult.Message);
                    return new List<string> { $"⚠️ Lỗi trừ kho: {inventoryResult.Message}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[POSOrderController] Unexpected error khi trừ kho.");
                return new List<string> { "⚠️ Lỗi hệ thống khi trừ kho." };
            }

            return null;
        }

        /// <summary>
        /// Extract isIdempotent flag từ anonymous response data
        /// </summary>
        private static bool IsIdempotentResponse(object data)
        {
            if (data == null) return false;
            var prop = data.GetType().GetProperty("isIdempotent");
            return prop != null && prop.GetValue(data) is true;
        }

        private static bool RequiresPaymentResponse(object data)
        {
            if (data == null) return false;
            var prop = data.GetType().GetProperty("requiresPayment");
            return prop != null && prop.GetValue(data) is true;
        }

        /// <summary>
        /// Extract orderId từ anonymous response data
        /// </summary>
        private static int? ExtractOrderId(object data)
        {
            if (data == null) return null;
            var prop = data.GetType().GetProperty("orderId");
            return prop?.GetValue(data) as int?;
        }
    }
}
