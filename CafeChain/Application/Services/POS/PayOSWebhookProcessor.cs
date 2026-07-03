using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Data;
using CafeChain.Hubs;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.POS
{
    public class PayOSWebhookProcessor : IPayOSWebhookProcessor
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<OrderHub> _orderHubContext;
        private readonly IHubContext<PaymentHub> _paymentHubContext;
        private readonly IPrintDispatcher _printDispatcher;
        private readonly IInventoryDeductionService _inventoryService;
        private readonly ILogger<PayOSWebhookProcessor> _logger;

        public PayOSWebhookProcessor(
            AppDbContext context,
            IHubContext<OrderHub> orderHubContext,
            IHubContext<PaymentHub> paymentHubContext,
            IPrintDispatcher printDispatcher,
            IInventoryDeductionService inventoryService,
            ILogger<PayOSWebhookProcessor> logger)
        {
            _context = context;
            _orderHubContext = orderHubContext;
            _paymentHubContext = paymentHubContext;
            _printDispatcher = printDispatcher;
            _inventoryService = inventoryService;
            _logger = logger;
        }

        public async Task<PayOSWebhookProcessResult> ProcessAsync(PayOSWebhookPayload payload)
        {
            var order = await FindOrderForWebhookAsync(payload.OrderCodeText);
            if (order == null)
            {
                _logger.LogWarning("[PayOS Webhook] OrderCode {OrderCode} not found.", payload.OrderCodeText);
                return PayOSWebhookProcessResult.From(
                    "ORDER_NOT_FOUND",
                    $"Không tìm thấy đơn cho orderCode {payload.OrderCodeText}.");
            }

            var orderId = order.OrderId;

            if (order.PaymentStatusId == SystemConstants.PaymentStatuses.Paid ||
                order.Payments.Any(payment => payment.PaymentStatusId == SystemConstants.PaymentStatuses.Paid))
            {
                _logger.LogInformation("[PayOS Webhook] Order #{OrderId} already PAID. Skipping.", orderId);
                return PayOSWebhookProcessResult.From("ALREADY_PAID", "Đơn đã thanh toán.", orderId);
            }

            if (payload.Status != "00")
            {
                _logger.LogWarning(
                    "[PayOS Webhook] Order #{OrderId} status={Status} (not success).",
                    orderId,
                    payload.Status);

                await AddTransactionLogAsync(
                    orderId,
                    payload,
                    $"FAILED_{payload.Status}");

                return PayOSWebhookProcessResult.From(
                    "PAYMENT_NOT_SUCCESS",
                    $"Trạng thái: {payload.Status}",
                    orderId);
            }

            if (payload.Amount < order.Total)
            {
                _logger.LogWarning(
                    "[PayOS Webhook] Amount mismatch: received={Amount}, expected={Total}.",
                    payload.Amount,
                    order.Total);

                await AddTransactionLogAsync(orderId, payload, "AMOUNT_MISMATCH");
                return PayOSWebhookProcessResult.From("AMOUNT_MISMATCH", "Số tiền không khớp.", orderId);
            }

            var transitionResult = await ConfirmPaymentTransactionAsync(orderId, payload);
            if (!transitionResult.ConfirmedPayment)
                return transitionResult;

            var confirmedOrder = await LoadOrderForSideEffectsAsync(orderId);
            if (confirmedOrder == null)
            {
                _logger.LogError(
                    "[PayOS Webhook] Order #{OrderId} confirmed but could not be reloaded for side-effects.",
                    orderId);
                return PayOSWebhookProcessResult.From(
                    "SUCCESS",
                    "Thanh toán thành công.",
                    orderId,
                    confirmedPayment: true);
            }

            if (string.Equals(confirmedOrder.Source, "POS", StringComparison.OrdinalIgnoreCase))
            {
                await DeductInventoryForPaidPosOrderSafeAsync(confirmedOrder);
                await DispatchPrintForPaidPosOrderSafeAsync(confirmedOrder);
            }

            await NotifyPaymentCompletedSafeAsync(confirmedOrder);

            return PayOSWebhookProcessResult.From(
                "SUCCESS",
                "Thanh toán thành công.",
                orderId,
                confirmedPayment: true);
        }

        private async Task<PayOSWebhookProcessResult> ConfirmPaymentTransactionAsync(
            int orderId,
            PayOSWebhookPayload payload)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var state = await _context.Orders
                    .AsNoTracking()
                    .Where(order => order.OrderId == orderId)
                    .Select(order => new
                    {
                        order.OrderId,
                        order.Source,
                        order.OrderStatusId,
                        order.PaymentStatusId
                    })
                    .FirstOrDefaultAsync();

                if (state == null)
                {
                    await transaction.RollbackAsync();
                    return PayOSWebhookProcessResult.From("ORDER_NOT_FOUND", "Không tìm thấy đơn hàng.", orderId);
                }

                if (state.PaymentStatusId == SystemConstants.PaymentStatuses.Paid)
                {
                    await transaction.CommitAsync();
                    return PayOSWebhookProcessResult.From("ALREADY_PAID", "Đơn đã thanh toán.", orderId);
                }

                if (state.OrderStatusId != SystemConstants.OrderStatuses.AwaitingPayment ||
                    state.PaymentStatusId != SystemConstants.PaymentStatuses.Unpaid)
                {
                    await transaction.CommitAsync();
                    return PayOSWebhookProcessResult.From(
                        "PAYMENT_NOT_PAYABLE",
                        "Giao dịch không còn ở trạng thái chờ thanh toán.",
                        orderId);
                }

                var nextOrderStatusId = string.Equals(state.Source, "POS", StringComparison.OrdinalIgnoreCase)
                    ? SystemConstants.OrderStatuses.Completed
                    : SystemConstants.OrderStatuses.Pending;

                var paidAt = DateTime.Now;

                var orderUpdated = await _context.Orders
                    .Where(order =>
                        order.OrderId == orderId &&
                        order.OrderStatusId == SystemConstants.OrderStatuses.AwaitingPayment &&
                        order.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(order => order.PaymentStatusId, SystemConstants.PaymentStatuses.Paid)
                        .SetProperty(order => order.OrderStatusId, nextOrderStatusId));

                if (orderUpdated == 0)
                {
                    await transaction.CommitAsync();
                    return await BuildIdempotentStateResultAsync(orderId);
                }

                var paymentUpdated = await _context.Payments
                    .Where(payment =>
                        payment.OrderId == orderId &&
                        payment.PaymentMethodId == 2 &&
                        payment.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(payment => payment.PaymentStatusId, SystemConstants.PaymentStatuses.Paid)
                        .SetProperty(payment => payment.TransactionCode, payload.TransactionId)
                        .SetProperty(payment => payment.PaidAt, paidAt));

                if (paymentUpdated == 0)
                {
                    await transaction.RollbackAsync();
                    return PayOSWebhookProcessResult.From(
                        "PAYMENT_NOT_PAYABLE",
                        "Không tìm thấy dòng thanh toán VietQR đang chờ.",
                        orderId);
                }

                _context.Set<TransactionLog>().Add(new TransactionLog
                {
                    OrderId = orderId,
                    TransactionId = payload.TransactionId,
                    Amount = payload.Amount,
                    Description = payload.Description,
                    Status = "PAID",
                    RawPayload = payload.RawBody
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "[PayOS Webhook] Order #{OrderId} PAID successfully. Amount={Amount}",
                    orderId,
                    payload.Amount);

                _context.ChangeTracker.Clear();
                return PayOSWebhookProcessResult.From(
                    "SUCCESS",
                    "Thanh toán thành công.",
                    orderId,
                    confirmedPayment: true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "[PayOS Webhook] Transaction FAILED for Order #{OrderId}.", orderId);
                return PayOSWebhookProcessResult.From(
                    "TRANSACTION_ERROR",
                    "Lỗi nội bộ khi xử lý thanh toán.",
                    orderId);
            }
        }

        private async Task<PayOSWebhookProcessResult> BuildIdempotentStateResultAsync(int orderId)
        {
            var state = await _context.Orders
                .AsNoTracking()
                .Where(order => order.OrderId == orderId)
                .Select(order => new
                {
                    order.PaymentStatusId,
                    order.OrderStatusId
                })
                .FirstOrDefaultAsync();

            if (state?.PaymentStatusId == SystemConstants.PaymentStatuses.Paid)
                return PayOSWebhookProcessResult.From("ALREADY_PAID", "Đơn đã thanh toán.", orderId);

            return PayOSWebhookProcessResult.From(
                "PAYMENT_NOT_PAYABLE",
                "Giao dịch không còn ở trạng thái chờ thanh toán.",
                orderId);
        }

        private async Task AddTransactionLogAsync(
            int orderId,
            PayOSWebhookPayload payload,
            string status)
        {
            _context.Set<TransactionLog>().Add(new TransactionLog
            {
                OrderId = orderId,
                TransactionId = payload.TransactionId,
                Amount = payload.Amount,
                Description = payload.Description,
                Status = status,
                RawPayload = payload.RawBody
            });

            await _context.SaveChangesAsync();
        }

        private async Task<Order?> FindOrderForWebhookAsync(string orderCodeText)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.PaymentReference == orderCodeText);

            if (order != null)
                return order;

            return TryExtractOrderId(orderCodeText, out var fallbackOrderId)
                ? await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.OrderId == fallbackOrderId)
                : null;
        }

        private async Task<Order?> LoadOrderForSideEffectsAsync(int orderId)
        {
            return await _context.Orders
                .Include(o => o.Payments)
                .Include(o => o.OrderDetails).ThenInclude(od => od.OrderToppings)
                .Include(o => o.Store)
                .Include(o => o.Staff)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
        }

        private async Task DeductInventoryForPaidPosOrderSafeAsync(Order order)
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

        private async Task DispatchPrintForPaidPosOrderSafeAsync(Order order)
        {
            try
            {
                var cashierName = order.Staff?.FullName ?? "POS";
                await _printDispatcher.DispatchPrintJobAsync(
                    order,
                    order.StoreId,
                    cashierName,
                    order.Total,
                    isCashPayment: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[PayOS Webhook] Print dispatch failed for POS Order #{OrderId}. Payment remains confirmed.",
                    order.OrderId);
            }
        }

        private async Task NotifyPaymentCompletedSafeAsync(Order order)
        {
            try
            {
                await _orderHubContext.Clients.Group($"Order_{order.OrderId}")
                    .SendAsync("PaymentCompleted", order.OrderId);

                await _paymentHubContext.Clients.Group(order.OrderId.ToString())
                    .SendAsync("ReceivePaymentSuccess", new
                    {
                        OrderId = order.OrderId,
                        TotalAmount = order.Total,
                        ItemCount = order.OrderDetails?.Sum(d => d.Quantity) ?? 0,
                        PaymentMethod = "Chuyển khoản VietQR - PayOS",
                        PaidAt = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy")
                    });

                await _orderHubContext.Clients.Group("AdminDashboard")
                    .SendAsync("ReceiveNewOrder", order.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[PayOS Webhook] SignalR notification failed for Order #{OrderId}. Payment remains confirmed.",
                    order.OrderId);
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
