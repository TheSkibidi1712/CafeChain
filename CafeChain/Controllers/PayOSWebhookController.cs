using CafeChain.Application.Constants;
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
        private readonly IPayOSService _payOSService;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly ILogger<PayOSWebhookController> _logger;

        public PayOSWebhookController(
            AppDbContext context,
            IPayOSService payOSService,
            IHubContext<OrderHub> hubContext,
            ILogger<PayOSWebhookController> logger)
        {
            _context = context;
            _payOSService = payOSService;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpPost("api/payos-webhook")]
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
                var signature = Request.Headers["x-payos-signature"].FirstOrDefault();
                if (!_payOSService.VerifyWebhookSignature(rawBody, signature))
                {
                    _logger.LogWarning("[PayOS Webhook] SIGNATURE INVALID.");
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

                var orderCode = data.GetProperty("orderCode").GetInt64();
                var amount = data.GetProperty("amount").GetDecimal();
                var description = data.TryGetProperty("description", out var desc)
                    ? desc.GetString() : "";
                var transactionId = data.TryGetProperty("reference", out var refProp)
                    ? refProp.GetString()
                    : data.TryGetProperty("paymentLinkId", out var plId)
                        ? plId.GetString() : $"PAYOS_{orderCode}";
                var status = data.TryGetProperty("code", out var codeProp)
                    ? codeProp.GetString() : "00"; // "00" = success

                int orderId = (int)orderCode; // Mapping ngược: long → int

                // ===== BƯỚC 3: IDEMPOTENCY CHECK =====
                var order = await _context.Orders
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                {
                    _logger.LogWarning("[PayOS Webhook] Order #{OrderId} not found.", orderId);
                    return Ok(new { code = "ORDER_NOT_FOUND", message = $"Đơn #{orderId} không tồn tại." });
                }

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
                    // 5a. Cập nhật Order: PaymentStatus = Paid, giữ OrderStatus = Pending
                    order.PaymentStatusId = SystemConstants.PaymentStatuses.Paid;
                    // OrderStatus giữ nguyên Pending — Admin sẽ duyệt trên Kanban

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
                // [FIX.md §2] Chỉ bắn tín hiệu cho Group("Order_{orderId}") — KHÔNG dùng Clients.All
                await _hubContext.Clients.Group($"Order_{orderId}")
                    .SendAsync("PaymentCompleted", orderId);

                // Đồng thời thông báo Admin Kanban
                await _hubContext.Clients.Group("AdminDashboard")
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
    }
}
