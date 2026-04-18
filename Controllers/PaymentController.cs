using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;
using CafeChain.Application.Constants;
using System;
using System.Linq;

namespace CafeChain.Controllers
{
    [Route("api/[controller]")]
    public class PaymentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly Net.payOS.PayOS _payOS;
        private readonly CafeChain.Application.Services.PayOSIntegration.IPayOSService _payOSService;

        public PaymentController(AppDbContext context, Net.payOS.PayOS payOS, CafeChain.Application.Services.PayOSIntegration.IPayOSService payOSService)
        {
            _context = context;
            _payOS = payOS;
            _payOSService = payOSService;
        }

        // Action hiển thị View chuyển khoản (QR code)
        [HttpGet("GenerateQR")]
        public async Task<IActionResult> GenerateQR(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null || order.OrderStatusId != SystemConstants.OrderStatuses.Pending)
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

            // Trả dữ liệu cho View QR
            ViewBag.OrderId = orderId;
            ViewBag.TotalAmount = payment.Amount;
            ViewBag.CreatedAt = order.CreatedAt;
            return View();
        }

        [HttpPost("ResumePayment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResumePayment(int orderId)
        {
            try
            {
                var linkResult = await _payOSService.CreatePaymentLinkAsync(orderId);
                return Redirect(linkResult.CheckoutUrl);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("History", "Order");
            }
        }

        [HttpGet("api/payment/status/{orderId}")]
        public async Task<IActionResult> CheckPaymentStatus(int orderId)
        {
            var status = await _context.Payments
                .Where(p => p.OrderId == orderId)
                .Select(p => p.PaymentStatusId)
                .FirstOrDefaultAsync();
                
            return Json(new { IsCompleted = (status == SystemConstants.PaymentStatuses.Paid) });
        }

        // ================= Webhook API (PayOS - TIỀN THẬT) =================
        [HttpPost("payos-payment")] 
        [IgnoreAntiforgeryToken] 
        public async Task<IActionResult> WebhookIpn([FromBody] Net.payOS.Types.WebhookType body)
        {
            try
            {
                // Verify chữ ký bằng SDK PayOS
                Net.payOS.Types.WebhookData verifiedData = _payOS.verifyPaymentWebhookData(body);

                string orderCodeStr = verifiedData.orderCode.ToString();
                int orderId = int.Parse(orderCodeStr.Substring(0, orderCodeStr.Length - 10)); // Unix time is 10 chars
                
                var payment = await _context.Payments
                    .Include(p => p.Order)
                    .FirstOrDefaultAsync(p => p.OrderId == orderId);

                if (payment == null) 
                    return Ok(new { success = false, message = "Order not found" });

                // Idempotency: Đã Paid rồi thì bỏ qua
                if (payment.PaymentStatusId == SystemConstants.PaymentStatuses.Paid)
                    return Ok(new { success = true, message = "Already processed" });

                // Zero-Trust: Kiểm tra số tiền
                if (verifiedData.amount >= (int)payment.Amount)
                {
                    payment.PaymentStatusId = SystemConstants.PaymentStatuses.Paid;
                    payment.TransactionCode = verifiedData.reference;
                    payment.PaidAt = DateTime.Now;

                    if (payment.Order != null)
                        payment.Order.OrderStatusId = SystemConstants.OrderStatuses.Pending; // Chuyển từ AwaitingPayment -> Pending
                }
                
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine("PayOS Webhook Error: " + ex.Message);
                return Ok(new { success = false, message = ex.Message });
            }
        }
    }
}
