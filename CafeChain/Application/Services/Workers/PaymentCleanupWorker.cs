using CafeChain.Data;
using CafeChain.Application.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Workers
{
    public class PaymentCleanupWorker : BackgroundService
    {
        private readonly ILogger<PaymentCleanupWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public PaymentCleanupWorker(ILogger<PaymentCleanupWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Payment Cleanup Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredPaymentsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing Payment Cleanup Worker.");
                }

                // Chạy mỗi phút (60 giây)
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CleanupExpiredPaymentsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var expiryTime = DateTime.Now.AddMinutes(-2);

            // Project only fields this worker mutates — avoids SQL 207 when schema lags model
            // (e.g. CostStatus/TotalCogs added in model but not yet on local DB).
            var expiredOrderIds = await context.Orders
                .AsNoTracking()
                .Where(o => o.OrderStatusId == SystemConstants.OrderStatuses.AwaitingPayment
                            && o.CreatedAt <= expiryTime)
                .Select(o => o.OrderId)
                .ToListAsync(stoppingToken);

            if (expiredOrderIds.Count == 0)
                return;

            var expiredOrders = await context.Orders
                .Include(o => o.Payments)
                .Where(o => expiredOrderIds.Contains(o.OrderId))
                .ToListAsync(stoppingToken);

            if (expiredOrders.Any())
            {
                var inventoryService = scope.ServiceProvider.GetRequiredService<CafeChain.Application.Interfaces.IInventoryService>();

                foreach (var order in expiredOrders)
                {
                    order.OrderStatusId = SystemConstants.OrderStatuses.Cancelled;
                    order.Note = (string.IsNullOrEmpty(order.Note) ? "" : order.Note + " | ") + "Hủy tự động: Quá hạn thanh toán";
                    
                    // [Phase 4] Cập nhật PaymentStatus
                    foreach (var p in order.Payments.Where(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid))
                    {
                        p.PaymentStatusId = SystemConstants.PaymentStatuses.Failed;
                    }

                    // [Phase 4] Hoàn kho
                    await inventoryService.ReleaseInventoryForOrderAsync(order.OrderId);
                }

                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation($"Cancelled {expiredOrders.Count} expired orders.");
            }
        }
    }
}
