using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CafeChain.Application.Workers
{
    public class OrderCleanupWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderCleanupWorker> _logger;

        public OrderCleanupWorker(IServiceProvider serviceProvider, ILogger<OrderCleanupWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OrderCleanupWorker started.");
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await CleanupExpiredOrdersAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("OrderCleanupWorker is stopping.");
            }
        }

        private async Task CleanupExpiredOrdersAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

                // Đơn quá 5 phút chưa thanh toán → hết hạn
                var targetTime = DateTime.Now.AddMinutes(-5);

                // Load đơn hết hạn kèm Payment để update bằng EF (tránh Raw SQL FK error)
                var expiredOrders = await dbContext.Orders
                    .Include(o => o.Payments)
                    .Where(o => o.OrderStatusId == SystemConstants.OrderStatuses.Pending
                             && o.CreatedAt < targetTime)
                    .ToListAsync(stoppingToken);

                if (!expiredOrders.Any()) return;

                _logger.LogInformation($"[Worker] Phát hiện {expiredOrders.Count} đơn hết hạn cần dọn.");
                int successCount = 0;

                foreach (var order in expiredOrders)
                {
                    // Double-check: đơn vẫn còn WaitingForPayment (chưa bị Webhook lấy mất)
                    // Sử dụng EF Concurrency Token hoặc re-query để đảm bảo
                    var freshOrder = await dbContext.Orders
                        .Include(o => o.Payments)
                        .FirstOrDefaultAsync(o => o.OrderId == order.OrderId
                                               && o.OrderStatusId == SystemConstants.OrderStatuses.Pending,
                                               stoppingToken);

                    if (freshOrder == null)
                    {
                        // Webhook đã xử lý trước → Worker rút lui, không đụng Inventory
                        _logger.LogWarning($"[Worker] Order #{order.OrderId}: Đã được Webhook xử lý trước. Bỏ qua.");
                        continue;
                    }

                    // Worker chiếm quyền → Hủy đơn
                    freshOrder.OrderStatusId = SystemConstants.OrderStatuses.Cancelled;

                    // Hủy Payment đang Unpaid
                    foreach (var payment in freshOrder.Payments
                        .Where(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid))
                    {
                        payment.PaymentStatusId = SystemConstants.PaymentStatuses.Failed;
                    }

                    try
                    {
                        await dbContext.SaveChangesAsync(stoppingToken);

                        // Chỉ xả kho SAU KHI SaveChanges thành công
                        await inventoryService.ReleaseInventoryForOrderAsync(freshOrder.OrderId);
                        successCount++;
                        _logger.LogInformation($"[Worker] Order #{freshOrder.OrderId}: Đã hủy và xả kho thành công.");
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx, $"[Worker] Order #{freshOrder.OrderId}: Lỗi khi lưu hủy đơn.");
                    }
                }

                if (successCount > 0)
                    _logger.LogInformation($"[Worker] Hoàn tất: Đã dọn {successCount} đơn hết hạn.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Worker] Lỗi nghiêm trọng khi dọn dẹp đơn hàng hết hạn.");
            }
        }
    }
}
