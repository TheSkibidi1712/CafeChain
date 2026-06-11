using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;
using CafeChain.Application.Constants;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CafeChain.Controllers
{
    [Route("api/[controller]")]
    public class PaymentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly Net.payOS.PayOS _payOS;
        private readonly CafeChain.Application.Services.PayOSIntegration.IPayOSService _payOSService;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<CafeChain.Hubs.OrderHub> _orderHubContext;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<CafeChain.Hubs.PaymentHub> _paymentHubContext;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            AppDbContext context, 
            Net.payOS.PayOS payOS, 
            CafeChain.Application.Services.PayOSIntegration.IPayOSService payOSService,
            Microsoft.AspNetCore.SignalR.IHubContext<CafeChain.Hubs.OrderHub> orderHubContext,
            Microsoft.AspNetCore.SignalR.IHubContext<CafeChain.Hubs.PaymentHub> paymentHubContext,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _payOS = payOS;
            _payOSService = payOSService;
            _orderHubContext = orderHubContext;
            _paymentHubContext = paymentHubContext;
            _logger = logger;
        }

        // Action hiển thị View chuyển khoản (QR code)
        [HttpGet("GenerateQR")]
        public async Task<IActionResult> GenerateQR(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null || (order.OrderStatusId != SystemConstants.OrderStatuses.Pending && order.OrderStatusId != SystemConstants.OrderStatuses.AwaitingPayment))
            {
                TempData["ErrorMessage"] = order == null
                    ? "Đơn hàng không tồn tại."
                    : "Đơn hàng này đã được xử lý (thanh toán hoàn tất hoặc đã hủy). Không thể thanh toán tiếp.";
                return RedirectToAction("History", "Order");
            }

            var payment = order.Payments
                .OrderByDescending(p => p.PaymentId)
                .FirstOrDefault();

            if (payment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin thanh toán cho đơn hàng này.";
                return RedirectToAction("History", "Order");
            }

            // Nếu đã thanh toán hoàn tất thì không cần vào QR nữa
            if (payment.PaymentStatusId == SystemConstants.PaymentStatuses.Paid)
            {
                TempData["SuccessMessage"] = "Đơn hàng này đã thanh toán thành công.";
                return RedirectToAction("History", "Order");
            }

            // Nếu Payment đang là Failed (thanh toán lần trước thất bại), reset lại về Unpaid để Retry
            if (payment.PaymentStatusId == SystemConstants.PaymentStatuses.Failed)
            {
                payment.PaymentStatusId = SystemConstants.PaymentStatuses.Unpaid;
                await _context.SaveChangesAsync();
            }

            try
            {
                // [FIX] Gọi dịch vụ PayOS để lấy thông tin mã QR thật sự
                var linkResult = await _payOSService.CreatePaymentLinkAsync(orderId);
                
                // Truyền thông tin xuống View để sinh ảnh VietQR
                ViewBag.Bin = linkResult.Bin;
                ViewBag.AccountNumber = linkResult.AccountNumber;
                ViewBag.AccountName = linkResult.AccountName;
                ViewBag.Description = linkResult.Description;
                ViewBag.Amount = linkResult.Amount > 0 ? linkResult.Amount : payment.Amount;
                ViewBag.CheckoutUrl = linkResult.CheckoutUrl; 
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("History", "Order");
            }

            // Trả dữ liệu cho View QR
            ViewBag.OrderId = orderId;
            ViewBag.TotalAmount = payment.Amount;
            ViewBag.CreatedAt = order.CreatedAt;
            return View();
        }

        [HttpGet("~/api/payment/status/{orderId}")]
        public async Task<IActionResult> CheckPaymentStatus(int orderId)
        {
            var status = await _context.Payments
                .Where(p => p.OrderId == orderId)
                .Select(p => p.PaymentStatusId)
                .FirstOrDefaultAsync();
                
            return Json(new { IsCompleted = (status == SystemConstants.PaymentStatuses.Paid) });
        }

        // ================= [BƯỚC 2] Xử lý Webhook (Zero-Trust Logic) =================
        [HttpPost("payos-payment")] 
        [IgnoreAntiforgeryToken] 
        public async Task<IActionResult> ReceiveWebhook([FromBody] Net.payOS.Types.WebhookType body)
        {
            _logger.LogInformation("[WEBHOOK] Received request from PayOS.");
            try
            {
                // 1. Xác thực chữ ký (Signature) bằng SDK PayOS
                Net.payOS.Types.WebhookData verifiedData = _payOS.verifyPaymentWebhookData(body);
                _logger.LogInformation("[WEBHOOK] Verification successful.");

                string orderCodeStr = verifiedData.orderCode.ToString();
                // [Logic tách OrderId từ OrderCode]
                int orderId = int.Parse(orderCodeStr.Substring(0, orderCodeStr.Length - 9)); 
                
                // 2. Lấy Order từ DB lên kiểm tra (Zero-Trust)
                var payment = await _context.Payments
                    .Include(p => p.Order).ThenInclude(o => o.OrderDetails)
                    .FirstOrDefaultAsync(p => p.OrderId == orderId);

                if (payment == null) 
                {
                    _logger.LogWarning("[WEBHOOK] Payment not found for OrderId: {OrderId}", orderId);
                    return Ok(new { success = false, message = "Order not found" });
                }

                // Idempotency: Đã Paid rồi thì bỏ qua nhưng vẫn trả về thành công cho PayOS
                if (payment.PaymentStatusId == SystemConstants.PaymentStatuses.Paid)
                    return Ok(new { success = true, message = "Already processed" });

                // 3. Kiểm tra số tiền và cập nhật trạng thái
                if (verifiedData.amount >= (int)payment.Amount)
                {
                    payment.PaymentStatusId = SystemConstants.PaymentStatuses.Paid;
                    payment.TransactionCode = verifiedData.reference;
                    payment.PaidAt = DateTime.Now;

                    if (payment.Order != null)
                        payment.Order.OrderStatusId = SystemConstants.OrderStatuses.Pending; // AwaitingPayment -> Pending
                }
                
                await _context.SaveChangesAsync();

                // 4. Bắn tín hiệu SignalR ReceivePaymentSuccess kèm receiptData
                var receiptData = new {
                    OrderId = orderId,
                    TotalAmount = payment.Amount,
                    ItemCount = payment.Order?.OrderDetails.Sum(d => d.Quantity) ?? 0,
                    PaymentMethod = "Chuyển khoản VietQR - PayOS",
                    PaidAt = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy")
                };

                _logger.LogInformation("[WEBHOOK] Preparing to send SignalR for Order: {OrderId}", orderId);
                
                // Gửi tới khách hàng qua PaymentHub
                await _paymentHubContext.Clients.Group(orderId.ToString()).SendAsync("ReceivePaymentSuccess", receiptData);
                
                // Gửi tới Admin qua OrderHub (như cũ để giữ tính năng Kanban)
                await _orderHubContext.Clients.Group("AdminDashboard").SendAsync("ReceiveNewOrder", orderId);
                
                _logger.LogInformation("[WEBHOOK] SignalR sent successfully for Order: {OrderId}", orderId);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WEBHOOK ERROR] Error processing webhook.");
                return Ok(new { success = false, message = ex.Message });
            }
        }
    }
}
