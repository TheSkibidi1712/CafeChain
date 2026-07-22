using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Data;
using CafeChain.Application.Options;
using CafeChain.Application.Services.POS;
using CafeChain.Models.Payments;
using CafeChain.Models.Systems;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

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
        private readonly decimal _cashDenominationStep;

        public POSPaymentController(AppDbContext context, ILogger<POSPaymentController> logger)
            : this(context, logger, null)
        {
        }

        public POSPaymentController(
            AppDbContext context,
            ILogger<POSPaymentController> logger,
            IOptions<POSPaymentOptions>? paymentOptions)
        {
            _context = context;
            _logger = logger;
            _cashDenominationStep = paymentOptions?.Value.GetEffectiveCashDenominationStep()
                ?? POSPaymentOptions.DefaultCashDenominationStep;
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
                .AsNoTracking()
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

            var pendingCashAmount = order.Payments
                .Where(payment =>
                    payment.PaymentMethodId == 1 &&
                    payment.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid)
                .Sum(payment => payment.Amount);

            if (pendingCashAmount > 0m && !request.KeepTemporaryCash)
            {
                if (!request.CashReturnedConfirmed)
                {
                    return Conflict(new
                    {
                        success = false,
                        code = "CASH_RETURN_CONFIRMATION_REQUIRED",
                        message = $"Bạn đã nhận {pendingCashAmount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))}đ từ khách. Hãy hoàn lại đủ tiền trước khi hủy."
                    });
                }

                if (request.ReturnedAmount != pendingCashAmount)
                    return BadRequest(new { success = false, message = "Số tiền xác nhận hoàn lại phải đúng bằng tiền mặt đã nhận." });

                var denominationError = POSCashAmountValidator.Validate(request.ReturnedAmount, _cashDenominationStep);
                if (denominationError != null)
                    return BadRequest(new { success = false, message = denominationError });

                if (string.IsNullOrWhiteSpace(request.RequestKey))
                    return BadRequest(new { success = false, message = "Thiếu RequestKey cho xác nhận hoàn tiền." });

                var normalizedRequestKey = request.RequestKey.Trim();
                var existingReturnAudit = await _context.TransactionLogs
                    .AsNoTracking()
                    .AnyAsync(log =>
                        log.OrderId == order.OrderId &&
                        log.TransactionId == normalizedRequestKey &&
                        log.Status == "CASH_RETURNED");
                if (existingReturnAudit)
                    return Ok(new { success = true, code = "ALREADY_CANCELLED", message = "Tiền mặt đã được xác nhận hoàn và giao dịch đã hủy trước đó." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var reason = string.IsNullOrWhiteSpace(request.Reason)
                    ? "Thu ngân hủy giao dịch VietQR"
                    : request.Reason.Trim();

                var orderUpdated = await _context.Orders
                    .Where(candidate =>
                        candidate.OrderId == order.OrderId &&
                        candidate.StoreId == CurrentStoreId &&
                        candidate.OrderStatusId == SystemConstants.OrderStatuses.AwaitingPayment &&
                        candidate.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(candidate => candidate.OrderStatusId, SystemConstants.OrderStatuses.Cancelled)
                        .SetProperty(candidate => candidate.PaymentStatusId, SystemConstants.PaymentStatuses.Failed));

                if (orderUpdated == 0)
                {
                    await transaction.RollbackAsync();
                    var currentState = await _context.Orders
                        .AsNoTracking()
                        .Where(candidate => candidate.OrderId == order.OrderId)
                        .Select(candidate => new { candidate.OrderStatusId, candidate.PaymentStatusId })
                        .SingleAsync();
                    return currentState.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                        ? Ok(new { success = false, code = "ALREADY_PAID", message = "Giao dịch đã thanh toán thành công, không thể hủy." })
                        : Ok(new { success = true, code = "ALREADY_CANCELLED", message = "Giao dịch đã được hủy trước đó." });
                }

                await _context.Payments
                    .Where(payment =>
                        payment.OrderId == order.OrderId &&
                        payment.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(payment => payment.PaymentStatusId, SystemConstants.PaymentStatuses.Failed));

                if (pendingCashAmount > 0m && !request.KeepTemporaryCash)
                {
                    var normalizedRequestKey = request.RequestKey!.Trim();
                    _context.TransactionLogs.Add(new TransactionLog
                    {
                        OrderId = order.OrderId,
                        TransactionId = normalizedRequestKey,
                        Amount = request.ReturnedAmount,
                        Description = $"Hoàn tiền mặt tạm khi hủy thanh toán: {reason}",
                        Status = "CASH_RETURNED",
                        RawPayload = JsonSerializer.Serialize(new
                        {
                            requestKey = normalizedRequestKey,
                            pendingCashAmount,
                            returnedAmount = request.ReturnedAmount,
                            actorStaffId = CurrentStaffId,
                            reason,
                            returnedAt = DateTime.UtcNow
                        }),
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _context.ChangeTracker.Clear();

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

        /// <summary>
        /// Persist cash-return evidence for a frontend-only temporary tender.
        /// This endpoint never creates an Order/Payment and never posts WorkShift cash.
        /// </summary>
        [HttpPost("temporary-cash/cancel")]
        public async Task<IActionResult> CancelTemporaryCash([FromBody] CancelTemporaryCashRequestDto request)
        {
            if (request == null || request.ClientOrderId == Guid.Empty)
                return BadRequest(new { success = false, message = "Thiếu mã phiên thanh toán tạm." });

            if (!request.CashReturnedConfirmed)
                return Conflict(new { success = false, code = "CASH_RETURN_CONFIRMATION_REQUIRED", message = "Hãy hoàn lại đủ tiền cho khách trước khi hủy." });

            var amountError = POSCashAmountValidator.Validate(request.PendingCashAmount, _cashDenominationStep);
            if (amountError != null)
                return BadRequest(new { success = false, message = amountError });

            if (request.ReturnedAmount != request.PendingCashAmount)
                return BadRequest(new { success = false, message = "Số tiền xác nhận hoàn lại phải đúng bằng tiền mặt đã nhận." });

            if (string.IsNullOrWhiteSpace(request.RequestKey))
                return BadRequest(new { success = false, message = "Thiếu RequestKey cho xác nhận hoàn tiền." });

            var requestKey = request.RequestKey.Trim();
            const string actionName = "POS_TEMPORARY_CASH_CANCEL";
            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Thu ngân hủy thanh toán tạm"
                : request.Reason.Trim();
            var requestBody = JsonSerializer.Serialize(new
            {
                request.ClientOrderId,
                request.PendingCashAmount,
                request.ReturnedAmount,
                actorStaffId = CurrentStaffId,
                storeId = CurrentStoreId,
                reason
            });
            var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestBody)));
            var existing = await _context.RequestDeduplications
                .AsNoTracking()
                .SingleOrDefaultAsync(entry =>
                    entry.RequestKey == requestKey &&
                    entry.ActionName == actionName &&
                    entry.StaffId == CurrentStaffId);

            if (existing != null)
            {
                return string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal)
                    ? Ok(new { success = true, code = "ALREADY_CANCELLED", message = "Phiên tiền mặt tạm đã được xác nhận hoàn trước đó." })
                    : Conflict(new { success = false, code = "IDEMPOTENCY_KEY_REUSED", message = "RequestKey đã được dùng với nội dung khác." });
            }

            var now = DateTime.UtcNow;
            _context.RequestDeduplications.Add(new RequestDeduplication
            {
                RequestKey = requestKey,
                ActionName = actionName,
                StaffId = CurrentStaffId,
                Status = "SUCCESS",
                RequestBody = requestBody,
                PayloadHash = payloadHash,
                ResponseBody = JsonSerializer.Serialize(new
                {
                    status = "CASH_RETURNED",
                    request.ReturnedAmount,
                    returnedAt = now
                }),
                CreatedAt = now,
                ExpiredAt = now.AddDays(30)
            });

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Đã ghi nhận hoàn tiền và hủy thanh toán tạm." });
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                var raced = await _context.RequestDeduplications
                    .AsNoTracking()
                    .SingleOrDefaultAsync(entry =>
                        entry.RequestKey == requestKey &&
                        entry.ActionName == actionName &&
                        entry.StaffId == CurrentStaffId);
                return raced != null && string.Equals(raced.PayloadHash, payloadHash, StringComparison.Ordinal)
                    ? Ok(new { success = true, code = "ALREADY_CANCELLED", message = "Phiên tiền mặt tạm đã được xác nhận hoàn trước đó." })
                    : Conflict(new { success = false, code = "IDEMPOTENCY_KEY_REUSED", message = "RequestKey đã được dùng với nội dung khác." });
            }
        }
    }
}
