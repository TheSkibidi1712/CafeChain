using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.Mvc;
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
        private readonly Net.payOS.PayOS _payOS;
        private readonly IPayOSWebhookProcessor _webhookProcessor;
        private readonly ILogger<PayOSWebhookController> _logger;

        public PayOSWebhookController(
            Net.payOS.PayOS payOS,
            IPayOSWebhookProcessor webhookProcessor,
            ILogger<PayOSWebhookController> logger)
        {
            _payOS = payOS;
            _webhookProcessor = webhookProcessor;
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

                var result = await _webhookProcessor.ProcessAsync(new PayOSWebhookPayload
                {
                    OrderCodeText = orderCodeText,
                    Amount = amount,
                    TransactionId = transactionId,
                    Description = description,
                    Status = status,
                    RawBody = rawBody
                });

                return Ok(new { code = result.Code, message = result.Message });
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
