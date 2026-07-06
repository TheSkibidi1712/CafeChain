using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public POSOrderController(
            IPOSOrderService orderService,
            IInventoryDeductionService inventoryService,
            ILogger<POSOrderController> logger)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
            _logger = logger;
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
            if (dto == null || dto.Items == null || !dto.Items.Any())
                return BadRequest(new { success = false, message = "Giỏ hàng trống." });

            var result = await _orderService.CommitOrderAsync(dto, CurrentStaffId, CurrentStoreId);

            if (!result.IsSuccess)
                return Ok(new { success = false, message = result.Message });

            var isIdempotent = IsIdempotentResponse(result.Data);

            // Side-effects: Cash/Paid orders deduct stock once for newly-created orders only.
            // VietQR/PayOS orders are still AwaitingPayment here, so stock is deducted by webhook after Paid.
            // Idempotent retries return the existing order and must not re-run Inventory Deduction.
            var inventoryWarnings = IsPayOsPayment(dto) || isIdempotent
                ? null
                : await DeductInventorySafeAsync(dto.Items, CurrentStoreId);

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
        public async Task<IActionResult> SyncOfflineOrders([FromBody] OfflineBatchSyncRequestDto request)
        {
            if (request?.Orders == null || !request.Orders.Any())
                return BadRequest(new { success = false, message = "Không có đơn hàng để đồng bộ." });

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
                            Quantity = d.Quantity,
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
                        orderDto.StaffId!.Value,
                        orderDto.StoreId!.Value,
                        orderDto.WorkShiftId!.Value,
                        orderDto.SoldAt ?? DateTime.Now);

                    if (commitResult.IsSuccess)
                    {
                        // Kiểm tra isIdempotent flag trong response data
                        var isIdempotent = IsIdempotentResponse(commitResult.Data);

                        if (isIdempotent)
                        {
                            itemResult.Status = "duplicate";
                            itemResult.OrderId = ExtractOrderId(commitResult.Data);
                            duplicateCount++;
                        }
                        else
                        {
                            itemResult.Status = "created";
                            itemResult.OrderId = ExtractOrderId(commitResult.Data);
                            createdCount++;

                            // Side-effects: Inventory deduction chỉ chạy cho đơn offline tạo mới.
                            // Duplicate/idempotent sync tuyệt đối không trừ kho lại.
                            await DeductInventorySafeAsync(commitDto.Items, orderDto.StoreId!.Value);
                        }
                    }
                    else
                    {
                        itemResult.Status = "failed";
                        itemResult.Error = commitResult.Message;
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

        // ============================================================
        // GET /api/v1/pos/orders — Issue #68
        // ============================================================

        /// <summary>
        /// Lấy lịch sử đơn hàng POS có phân trang.
        /// Query: ?page=1&amp;pageSize=20
        /// Response: { items[], pagination { page, pageSize, totalCount, totalPages } }
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetOrderHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _orderService.GetOrderHistoryAsync(CurrentStoreId, page, pageSize);

            if (!result.IsSuccess)
                return Ok(new { success = false, message = result.Message });

            return Ok(new { success = true, data = result.Data });
        }

        // ============================================================
        // PRIVATE HELPERS
        // ============================================================

        private static bool IsPayOsPayment(POSOrderCommitDto dto)
        {
            if (dto.Payments != null && dto.Payments.Any())
                return dto.Payments.Any(payment => payment.PaymentMethodId == 2);

            return (dto.Payments == null || dto.Payments.Count == 0) && dto.PaymentMethodId == 2;
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

            return null;
        }

        /// <summary>
        /// Fire-and-forget inventory deduction — lỗi KHÔNG fail đơn đã commit
        /// </summary>
        private async Task<List<string>> DeductInventorySafeAsync(List<POSOrderItemDto> items, int storeId)
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

                var inventoryResult = await _inventoryService.DeductStockForOrderAsync(soldItems, storeId);

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
