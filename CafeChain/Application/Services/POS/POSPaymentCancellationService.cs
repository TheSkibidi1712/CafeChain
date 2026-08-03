using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Options;
using CafeChain.Data;
using CafeChain.Models.Payments;
using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.POS;

public sealed class POSPaymentCancellationService : IPOSPaymentCancellationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<POSPaymentCancellationService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly decimal _cashDenominationStep;

    public POSPaymentCancellationService(
        AppDbContext db,
        ILogger<POSPaymentCancellationService> logger,
        TimeProvider timeProvider,
        IOptions<POSPaymentOptions>? paymentOptions = null)
    {
        _db = db;
        _logger = logger;
        _timeProvider = timeProvider;
        _cashDenominationStep = paymentOptions?.Value.GetEffectiveCashDenominationStep()
            ?? POSPaymentOptions.DefaultCashDenominationStep;
    }

    public async Task<POSPaymentOperationResultDto> CancelPaymentAsync(
        CancelPaymentRequestDto request,
        int actorStaffId,
        int storeId,
        CancellationToken cancellationToken = default)
    {
        if (request == null || request.OrderId <= 0)
            return BadRequest("Thiếu mã đơn hàng cần hủy.");

        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.OrderId == request.OrderId
                && o.StoreId == storeId && o.Source == "POS", cancellationToken);
        if (order == null)
            return Result(false, 404, "Không tìm thấy giao dịch POS cần hủy.", "PAYMENT_NOT_FOUND");

        if (order.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
            || order.Payments.Any(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid))
            return Result(false, 200, "Giao dịch đã thanh toán thành công, không thể hủy.", "ALREADY_PAID");

        if (order.OrderStatusId == SystemConstants.OrderStatuses.Cancelled)
            return Result(true, 200, "Giao dịch đã được hủy trước đó.", "ALREADY_CANCELLED");

        if (order.OrderStatusId != SystemConstants.OrderStatuses.AwaitingPayment)
            return Result(false, 200, "Chỉ có thể hủy giao dịch VietQR đang chờ thanh toán.", "INVALID_STATE");

        var pendingCashAmount = order.Payments
            .Where(x => x.PaymentMethodId == 1
                && x.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid)
            .Sum(x => x.Amount);

        if (pendingCashAmount > 0m && !request.KeepTemporaryCash)
        {
            if (!request.CashReturnedConfirmed)
                return Result(false, 409,
                    $"Bạn đã nhận {pendingCashAmount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))}đ từ khách. Hãy hoàn lại đủ tiền trước khi hủy.",
                    "CASH_RETURN_CONFIRMATION_REQUIRED");
            if (request.ReturnedAmount != pendingCashAmount)
                return BadRequest("Số tiền xác nhận hoàn lại phải đúng bằng tiền mặt đã nhận.");

            var denominationError = POSCashAmountValidator.Validate(request.ReturnedAmount, _cashDenominationStep);
            if (denominationError != null) return BadRequest(denominationError);
            if (string.IsNullOrWhiteSpace(request.RequestKey))
                return BadRequest("Thiếu RequestKey cho xác nhận hoàn tiền.", "INVALID_REQUEST_KEY");

            var normalizedRequestKey = request.RequestKey.Trim();
            var returned = await _db.TransactionLogs.AsNoTracking().AnyAsync(log =>
                log.OrderId == order.OrderId && log.TransactionId == normalizedRequestKey
                && log.Status == "CASH_RETURNED", cancellationToken);
            if (returned)
                return Result(true, 200,
                    "Tiền mặt đã được xác nhận hoàn và giao dịch đã hủy trước đó.",
                    "ALREADY_CANCELLED");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Thu ngân hủy giao dịch VietQR"
                : request.Reason.Trim();
            var orderUpdated = await _db.Orders
                .Where(x => x.OrderId == order.OrderId && x.StoreId == storeId
                    && x.OrderStatusId == SystemConstants.OrderStatuses.AwaitingPayment
                    && x.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.OrderStatusId, SystemConstants.OrderStatuses.Cancelled)
                    .SetProperty(x => x.PaymentStatusId, SystemConstants.PaymentStatuses.Failed),
                    cancellationToken);
            if (orderUpdated == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                var state = await _db.Orders.AsNoTracking()
                    .Where(x => x.OrderId == order.OrderId)
                    .Select(x => new { x.OrderStatusId, x.PaymentStatusId })
                    .SingleAsync(cancellationToken);
                return state.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                    ? Result(false, 200, "Giao dịch đã thanh toán thành công, không thể hủy.", "ALREADY_PAID")
                    : Result(true, 200, "Giao dịch đã được hủy trước đó.", "ALREADY_CANCELLED");
            }

            await _db.Payments
                .Where(x => x.OrderId == order.OrderId
                    && x.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.PaymentStatusId, SystemConstants.PaymentStatuses.Failed),
                    cancellationToken);

            if (pendingCashAmount > 0m && !request.KeepTemporaryCash)
            {
                var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
                var requestKey = request.RequestKey!.Trim();
                _db.TransactionLogs.Add(new TransactionLog
                {
                    OrderId = order.OrderId,
                    TransactionId = requestKey,
                    Amount = request.ReturnedAmount,
                    Description = $"Hoàn tiền mặt tạm khi hủy thanh toán: {reason}",
                    Status = "CASH_RETURNED",
                    RawPayload = JsonSerializer.Serialize(new
                    {
                        requestKey,
                        pendingCashAmount,
                        returnedAmount = request.ReturnedAmount,
                        actorStaffId,
                        reason,
                        returnedAtUtc = nowUtc
                    }),
                    CreatedAt = nowUtc
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            _logger.LogInformation(
                "POS_PAYMENT_CANCELLED | OrderId={OrderId} StoreId={StoreId} StaffId={StaffId}",
                order.OrderId, storeId, actorStaffId);
            return Result(true, 200, "Đã hủy giao dịch VietQR.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _logger.LogError(ex,
                "POS_PAYMENT_CANCEL_FAILED | OrderId={OrderId} StoreId={StoreId} StaffId={StaffId}",
                request.OrderId, storeId, actorStaffId);
            return Result(false, 500, "Lỗi hệ thống khi hủy giao dịch.", "PAYMENT_CANCEL_FAILED");
        }
    }

    public async Task<POSPaymentOperationResultDto> CancelTemporaryCashAsync(
        CancelTemporaryCashRequestDto request,
        int actorStaffId,
        int storeId,
        CancellationToken cancellationToken = default)
    {
        if (request == null || request.ClientOrderId == Guid.Empty)
            return BadRequest("Thiếu mã phiên thanh toán tạm.");
        if (!request.CashReturnedConfirmed)
            return Result(false, 409, "Hãy hoàn lại đủ tiền cho khách trước khi hủy.",
                "CASH_RETURN_CONFIRMATION_REQUIRED");

        var amountError = POSCashAmountValidator.Validate(request.PendingCashAmount, _cashDenominationStep);
        if (amountError != null) return BadRequest(amountError);
        if (request.ReturnedAmount != request.PendingCashAmount)
            return BadRequest("Số tiền xác nhận hoàn lại phải đúng bằng tiền mặt đã nhận.");
        if (string.IsNullOrWhiteSpace(request.RequestKey))
            return BadRequest("Thiếu RequestKey cho xác nhận hoàn tiền.", "INVALID_REQUEST_KEY");

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
            actorStaffId,
            storeId,
            reason
        });
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestBody)));
        var existing = await _db.RequestDeduplications.AsNoTracking().SingleOrDefaultAsync(x =>
            x.RequestKey == requestKey && x.ActionName == actionName
            && x.StaffId == actorStaffId && x.StoreId == storeId, cancellationToken);
        if (existing != null)
            return string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal)
                ? Result(true, 200, "Phiên tiền mặt tạm đã được xác nhận hoàn trước đó.", "ALREADY_CANCELLED")
                : Result(false, 409, "RequestKey đã được dùng với nội dung khác.", "IDEMPOTENCY_KEY_REUSED");

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        _db.RequestDeduplications.Add(new RequestDeduplication
        {
            RequestKey = requestKey,
            ActionName = actionName,
            StaffId = actorStaffId,
            StoreId = storeId,
            Status = "SUCCESS",
            RequestBody = requestBody,
            PayloadHash = payloadHash,
            ResponseBody = JsonSerializer.Serialize(new
            {
                status = "CASH_RETURNED",
                request.ReturnedAmount,
                returnedAtUtc = nowUtc
            }),
            CreatedAt = nowUtc,
            ExpiredAt = nowUtc.AddDays(30)
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return Result(true, 200, "Đã ghi nhận hoàn tiền và hủy thanh toán tạm.");
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            var raced = await _db.RequestDeduplications.AsNoTracking().SingleOrDefaultAsync(x =>
                x.RequestKey == requestKey && x.ActionName == actionName
                && x.StaffId == actorStaffId && x.StoreId == storeId, cancellationToken);
            return raced != null && string.Equals(raced.PayloadHash, payloadHash, StringComparison.Ordinal)
                ? Result(true, 200, "Phiên tiền mặt tạm đã được xác nhận hoàn trước đó.", "ALREADY_CANCELLED")
                : Result(false, 409, "RequestKey đã được dùng với nội dung khác.", "IDEMPOTENCY_KEY_REUSED");
        }
    }

    private static POSPaymentOperationResultDto BadRequest(string message, string? code = null) =>
        Result(false, 400, message, code);

    private static POSPaymentOperationResultDto Result(
        bool success, int status, string message, string? code = null) =>
        new(success, status, message, code);
}
