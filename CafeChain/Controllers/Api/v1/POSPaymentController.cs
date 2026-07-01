using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// POS payment endpoints for cashier-driven payment actions.
    /// </summary>
    [Route("api/v1/pos/payments")]
    public class POSPaymentController : PosApiController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<POSPaymentController> _logger;

        public POSPaymentController(AppDbContext context, ILogger<POSPaymentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Cancel a POS VietQR/PayOS payment that is still awaiting payment.
        /// </summary>
        [HttpPost("cancel-payment")]
        public async Task<IActionResult> CancelPayment([FromBody] CancelPaymentRequestDto request)
        {
            if (request == null || request.OrderId <= 0)
                return BadRequest(new { success = false, message = "Thiếu mã đơn hàng cần hủy." });

            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o =>
                    o.OrderId == request.OrderId &&
                    o.StoreId == CurrentStoreId &&
                    o.Source == "POS");

            if (order == null)
                return NotFound(new { success = false, message = "Không tìm thấy giao dịch POS cần hủy." });

            if (order.PaymentStatusId == SystemConstants.PaymentStatuses.Paid ||
                order.Payments.Any(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid))
            {
                return Ok(new
                {
                    success = false,
                    code = "ALREADY_PAID",
                    message = "Giao dịch đã thanh toán thành công, không thể hủy."
                });
            }

            if (order.OrderStatusId == SystemConstants.OrderStatuses.Cancelled)
            {
                return Ok(new { success = true, message = "Giao dịch đã được hủy trước đó." });
            }

            if (order.OrderStatusId != SystemConstants.OrderStatuses.AwaitingPayment)
            {
                return Ok(new
                {
                    success = false,
                    code = "INVALID_STATE",
                    message = "Chỉ có thể hủy giao dịch VietQR đang chờ thanh toán."
                });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                order.OrderStatusId = SystemConstants.OrderStatuses.Cancelled;
                order.PaymentStatusId = SystemConstants.PaymentStatuses.Failed;

                var reason = string.IsNullOrWhiteSpace(request.Reason)
                    ? "Thu ngân hủy giao dịch VietQR"
                    : request.Reason.Trim();
                order.Note = string.IsNullOrWhiteSpace(order.Note)
                    ? $"[PAYOS-CANCELLED] {reason}"
                    : $"{order.Note} | [PAYOS-CANCELLED] {reason}";

                foreach (var payment in order.Payments.Where(p =>
                    p.PaymentMethodId == 2 &&
                    p.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid))
                {
                    payment.PaymentStatusId = SystemConstants.PaymentStatuses.Failed;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "[POSPayment] Cancelled pending PayOS payment for Order #{OrderId} by StaffId={StaffId}.",
                    order.OrderId,
                    CurrentStaffId);

                return Ok(new { success = true, message = "Đã hủy giao dịch VietQR." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "[POSPayment] Failed to cancel Order #{OrderId}.", request.OrderId);
                return Ok(new { success = false, message = "Lỗi hệ thống khi hủy giao dịch." });
            }
        }
    }
}
