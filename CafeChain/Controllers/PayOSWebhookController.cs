using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Data;
using CafeChain.Hubs;
using CafeChain.Models.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CafeChain.Controllers
{
    /// <summary>
    /// IPN Webhook Controller cho PayOS.
    /// [FIX.md §4] Zero-Trust Pipeline:
    ///   1. Verify Signature (HMAC-SHA256)
    ///   2. Idempotency Check (đã Paid chưa?)
    ///   3. Validate Amount (Amount == Order.Total)
    ///   4. Transaction: Update DB → Audit Log → Commit → SignalR
    /// 
    /// Luôn trả 200 OK để PayOS không spam retry.
    /// </summary>
    [ApiController]
    public class PayOSWebhookController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly Net.payOS.PayOS _payOS;
        private readonly IHubContext<OrderHub> _orderHubContext;
        private readonly IHubContext<PaymentHub> _paymentHubContext;
        private readonly IPrintDispatcher _printDispatcher;
        private readonly IInventoryDeductionService _inventoryService;
        private readonly ILogger<PayOSWebhookController> _logger;

        public PayOSWebhookController(
            AppDbContext context,
            Net.payOS.PayOS payOS,
            IHubContext<OrderHub> orderHubContext,
            IHubContext<PaymentHub> paymentHubContext,
            IPrintDispatcher printDispatcher,
            IInventoryDeductionService inventoryService,
            ILogger<PayOSWebhookController> logger)
        {
            _context = context;
            _payOS = payOS;
            _orderHubContext = orderHubContext;
            _paymentHubContext = paymentHubContext;
            _printDispatcher = printDispatcher;
            _inventoryService = inventoryService;
            _logger = logger;
        }

        [HttpPost("api/payos-webhook")]
        [HttpPost("api/v1/pos/payments/webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            string rawBody = "";
            try
            {
                // ===== BƯỚC 0: Đọc Raw Body =====
                using var reader = new StreamReader(Request.Body);
                rawBody = await reader.ReadToEndAsync();

                _logger.LogInformation("[PayOS Webhook] Received: {Body}", rawBody);

                // ===== BƯỚC 1: VERIFY SIGNATURE (Zero-Trust) =====
                var webhookBody = JsonSerializer.Deserialize<Net.payOS.Types.WebhookType>(
                    rawBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (webhookBody == null)
                {
                    return Ok(new { code = "INVALID_PAYLOAD", message = "Payload không hợp lệ." });
                }

                Net.payOS.Types.WebhookData verifiedData;
                try
                {
                    verifiedData = _payOS.verifyPaymentWebhookData(webhookBody);
                }
                catch (Exception verifyEx)
                {
                    _logger.LogWarning(verifyEx, "[PayOS Webhook] SIGNATURE INVALID.");
                    return Ok(new { code = "INVALID_SIGNATURE", message = "Chữ ký không hợp lệ." });
                }

                // ===== BƯỚC 2: PARSE PAYLOAD =====
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                // PayOS gửi data bên trong property "data"
                if (!root.TryGetProperty("data", out var data))
                {
                    return Ok(new { code = "INVALID_PAYLOAD", message = "Thiếu trường data." });
                }

                var orderCode = verifiedData.orderCode;
                var orderCodeText = orderCode.ToString();
                var amount = Convert.ToDecimal(verifiedData.amount);
                var description = data.TryGetProperty("description", out var desc)
                    ? desc.GetString() ?? ""
                    : "";
                var transactionId = verifiedData.reference;
                if (string.IsNullOrWhiteSpace(transactionId))
                {
                    transactionId = data.TryGetProperty("paymentLinkId", out var plId)
                        ? plId.GetString()
                        : $"PAYOS_{orderCodeText}";
                }
                var status = data.TryGetProperty("code", out var codeProp)
                    ? codeProp.GetString() : "00"; // "00" = success

                // ===== BƯỚC 3: IDEMPOTENCY CHECK =====
                var order = await _context.Orders
                    .Include(o => o.Payments)
                    .Include(o => o.OrderDetails).ThenInclude(od => od.OrderToppings)
                    .Include(o => o.Store)
                    .Include(o => o.Staff)
                    .FirstOrDefaultAsync(o => o.PaymentReference == orderCodeText);

                if (order == null && TryExtractOrderId(orderCodeText, out var fallbackOrderId))
                {
                    order = await _context.Orders
                        .Include(o => o.Payments)
                        .Include(o => o.OrderDetails).ThenInclude(od => od.OrderToppings)
                        .Include(o => o.Store)
                        .Include(o => o.Staff)
                        .FirstOrDefaultAsync(o => o.OrderId == fallbackOrderId);
                }

                if (order == null)
                {
                    _logger.LogWarning("[PayOS Webhook] OrderCode {OrderCode} not found.", orderCodeText);
                    return Ok(new { code = "ORDER_NOT_FOUND", message = $"Không tìm thấy đơn cho orderCode {orderCodeText}." });
                }

                var orderId = order.OrderId;

                // Đã thanh toán rồi → skip (Idempotent)
                if (order.PaymentStatusId == SystemConstants.PaymentStatuses.Paid)
                {
                    _logger.LogInformation("[PayOS Webhook] Order #{OrderId} already PAID. Skipping.", orderId);
                    return Ok(new { code = "ALREADY_PAID", message = "Đơn đã thanh toán." });
                }

                // PayOS gửi status khác success → log nhưng không update
                if (status != "00")
                {
                    _logger.LogWarning("[PayOS Webhook] Order #{OrderId} status={Status} (not success).", orderId, status);

                    // Vẫn ghi Audit Log cho trường hợp thất bại
                    _context.Set<TransactionLog>().Add(new TransactionLog
                    {
                        OrderId = orderId,
                        TransactionId = transactionId,
                        Amount = amount,
                        Description = description,
                        Status = $"FAILED_{status}",
                        RawPayload = rawBody
                    });
                    await _context.SaveChangesAsync();

                    return Ok(new { code = "PAYMENT_NOT_SUCCESS", message = $"Trạng thái: {status}" });
                }

                // ===== BƯỚC 4: VALIDATE AMOUNT (Zero-Trust) =====
                if (amount < order.Total)
                {
                    _logger.LogWarning("[PayOS Webhook] Amount mismatch: received={Amount}, expected={Total}.",
                        amount, order.Total);

                    _context.Set<TransactionLog>().Add(new TransactionLog
                    {
                        OrderId = orderId,
                        TransactionId = transactionId,
                        Amount = amount,
                        Description = description,
                        Status = "AMOUNT_MISMATCH",
                        RawPayload = rawBody
                    });
                    await _context.SaveChangesAsync();

                    return Ok(new { code = "AMOUNT_MISMATCH", message = "Số tiền không khớp." });
                }

                // ===== BƯỚC 5: DATABASE TRANSACTION (Atomic) =====
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 5a. Cập nhật Order: POS hoàn tất ngay, Website chuyển sang Pending để Admin/KDS xử lý
                    order.PaymentStatusId = SystemConstants.PaymentStatuses.Paid;
                    order.OrderStatusId = string.Equals(order.Source, "POS", StringComparison.OrdinalIgnoreCase)
                        ? SystemConstants.OrderStatuses.Completed
                        : SystemConstants.OrderStatuses.Pending;

                    // 5b. Cập nhật Payment record
                    var payment = order.Payments.FirstOrDefault(p =>
                        p.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid);

                    if (payment != null)
                    {
                        payment.PaymentStatusId = SystemConstants.PaymentStatuses.Paid;
                        payment.TransactionCode = transactionId;
                        payment.PaidAt = DateTime.Now;
                    }

                    // 5c. Audit Log — INSERT TransactionLog
                    _context.Set<TransactionLog>().Add(new TransactionLog
                    {
                        OrderId = orderId,
                        TransactionId = transactionId,
                        Amount = amount,
                        Description = description,
                        Status = "PAID",
                        RawPayload = rawBody
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("[PayOS Webhook] ✅ Order #{OrderId} PAID successfully. Amount={Amount}",
                        orderId, amount);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "[PayOS Webhook] Transaction FAILED for Order #{OrderId}.", orderId);
                    return Ok(new { code = "TRANSACTION_ERROR", message = "Lỗi nội bộ khi xử lý thanh toán." });
                }

                // ===== BƯỚC 6: SIGNALR — SAU KHI COMMIT THÀNH CÔNG =====
                if (string.Equals(order.Source, "POS", StringComparison.OrdinalIgnoreCase))
                {
                    await DeductInventoryForPaidPosOrderSafeAsync(order);

                    var cashierName = order.Staff?.FullName ?? "POS";
                    await _printDispatcher.DispatchPrintJobAsync(
                        order,
                        order.StoreId,
                        cashierName,
                        order.Total,
                        isCashPayment: false);
                }

                // [FIX.md §2] Chỉ bắn tín hiệu cho đúng group — KHÔNG dùng Clients.All
                await _orderHubContext.Clients.Group($"Order_{orderId}")
                    .SendAsync("PaymentCompleted", orderId);

                await _paymentHubContext.Clients.Group(orderId.ToString())
                    .SendAsync("ReceivePaymentSuccess", new
                    {
                        OrderId = orderId,
                        TotalAmount = order.Total,
                        ItemCount = order.OrderDetails?.Sum(d => d.Quantity) ?? 0,
                        PaymentMethod = "Chuyển khoản VietQR - PayOS",
                        PaidAt = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy")
                    });

                // Đồng thời thông báo Admin Kanban
                await _orderHubContext.Clients.Group("AdminDashboard")
                    .SendAsync("ReceiveNewOrder", orderId);

                return Ok(new { code = "SUCCESS", message = "Thanh toán thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayOS Webhook] Unhandled exception. Body={Body}", rawBody);
                // Trả 200 OK để PayOS không retry
                return Ok(new { code = "INTERNAL_ERROR", message = "Lỗi hệ thống." });
            }
        }

        private async Task DeductInventoryForPaidPosOrderSafeAsync(CafeChain.Models.Orders.Order order)
        {
            try
            {
                var soldItems = order.OrderDetails
                    .Select(item => new POSSoldItemDto
                    {
                        DrinkId = item.DrinkId,
                        SizeId = item.SizeId,
                        Quantity = item.Quantity,
                        Toppings = item.OrderToppings
                            .Select(topping => new POSOrderToppingDto { ToppingId = topping.ToppingId })
                            .ToList()
                    })
                    .ToList();

                var result = await _inventoryService.DeductStockForOrderAsync(soldItems, order.StoreId);
                if (!result.IsSuccess)
                {
                    _logger.LogError(
                        "[PayOS Webhook] Inventory deduction failed for POS Order #{OrderId}: {Message}",
                        order.OrderId,
                        result.Message);
                }
                else if (result.Errors != null && result.Errors.Any())
                {
                    _logger.LogWarning(
                        "[PayOS Webhook] Inventory warnings for POS Order #{OrderId}: {Warnings}",
                        order.OrderId,
                        string.Join(" | ", result.Errors));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayOS Webhook] Unexpected inventory error for POS Order #{OrderId}.", order.OrderId);
            }
        }

        private static bool TryExtractOrderId(string orderCodeText, out int orderId)
        {
            orderId = 0;

            if (int.TryParse(orderCodeText, out orderId))
                return true;

            if (orderCodeText.Length <= 9)
                return false;

            var prefix = orderCodeText[..^9];
            return int.TryParse(prefix, out orderId);
        }
    }
}
