using CafeChain.Data;
using CafeChain.Application.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CafeChain.Application.Services.PayOSIntegration
{
    /// <summary>
    /// DTO chứa kết quả tạo Payment Link từ PayOS.
    /// </summary>
    public class PayOSCreateLinkResult
    {
        public string CheckoutUrl { get; set; }
        public string QrCode { get; set; }
        public long OrderCode { get; set; }
    }

    /// <summary>
    /// Interface cho PayOS Service — Dependency Injection.
    /// </summary>
    public interface IPayOSService
    {
        Task<PayOSCreateLinkResult> CreatePaymentLinkAsync(int orderId);
        bool VerifyWebhookSignature(string rawBody, string signature);
    }

    /// <summary>
    /// Service tích hợp PayOS (VietQR).
    /// - OrderCode mapping: Order.OrderId (int) → cast sang long (an toàn vì int < int53).
    /// - Tạo Payment Link qua PayOS REST API.
    /// - Verify Webhook Signature bằng HMAC-SHA256.
    /// </summary>
    public class PayOSService : IPayOSService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        // PayOS config keys from appsettings.json
        private string ClientId => _config["PayOS:ClientId"];
        private string ApiKey => _config["PayOS:ApiKey"];
        private string ChecksumKey => _config["PayOS:ChecksumKey"];
        private string ReturnUrl => _config["PayOS:ReturnUrl"];
        private string CancelUrl => _config["PayOS:CancelUrl"];

        public PayOSService(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public async Task<PayOSCreateLinkResult> CreatePaymentLinkAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                throw new Exception($"Đơn hàng #{orderId} không tồn tại.");

            string orderCodeStr = $"{order.OrderId}{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            long orderCode = long.Parse(orderCodeStr);

            order.PaymentReference = orderCodeStr;
            await _context.SaveChangesAsync();

            var items = order.OrderDetails.Select(od => new
            {
                name = od.DrinkName ?? "Sản phẩm",
                quantity = od.Quantity,
                price = (int)od.Price
            }).ToList();

            int amount = (int)order.Total;
            string description = $"CafeChain #{order.OrderId}";

            // 1. Tạo chuỗi ký (signature data) dựa trên các key theo alphabet
            string signatureData = $"amount={amount}&cancelUrl={CancelUrl}&description={description}&orderCode={orderCode}&returnUrl={ReturnUrl}";

            // 2. Băm HMAC-SHA256 bằng ChecksumKey
            string signature = "";
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ChecksumKey)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureData));
                signature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }

            // 3. Payload
            var requestBody = new
            {
                orderCode = orderCode,
                amount = amount,
                description = description,
                returnUrl = ReturnUrl,
                cancelUrl = CancelUrl,
                items = items,
                signature = signature
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            // Bỏ qua SSL validation (fix lỗi "The SSL connection could not be established")
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            };

            using var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("x-client-id", ClientId);
            httpClient.DefaultRequestHeaders.Add("x-api-key", ApiKey);

            try
            {
                var response = await httpClient.PostAsync(
                    "https://api-merchant.payos.vn/v2/payment-requests",
                    jsonContent
                );

                var responseBody = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseBody);
                var code = doc.RootElement.GetProperty("code").GetString();
                if (code != "00")
                {
                    var desc = doc.RootElement.GetProperty("desc").GetString();
                    throw new Exception($"PayOS API Error [{code}]: {desc}");
                }

                var data = doc.RootElement.GetProperty("data");

                return new PayOSCreateLinkResult
                {
                    CheckoutUrl = data.GetProperty("checkoutUrl").GetString(),
                    QrCode = data.TryGetProperty("qrCode", out var qr) ? qr.GetString() : null,
                    OrderCode = orderCode
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi kết nối PayOS: {ex.Message}", ex);
            }
        }

        public bool VerifyWebhookSignature(string rawBody, string signature)
        {
            if (string.IsNullOrEmpty(ChecksumKey) || string.IsNullOrEmpty(signature))
                return false;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ChecksumKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
            var computed = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            return computed == signature.ToLowerInvariant();
        }
    }
}
