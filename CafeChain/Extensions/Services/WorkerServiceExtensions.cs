using CafeChain.Application.Services.Workers;
using CafeChain.Application.Workers;

namespace CafeChain.Extensions.Services
{
    public static class WorkerServiceExtensions
    {
        public static IServiceCollection AddCafeChainWorkers(this IServiceCollection services)
        {
            services.AddHostedService<OrderCleanupWorker>();
            services.AddHostedService<PaymentCleanupWorker>();
            services.AddHostedService<InventoryReorderNotificationWorker>();
            services.AddHostedService<ForecastGenerationWorker>();
            services.AddHostedService<PosRecommendationWorker>();
            services.AddHostedService<AnomalyDetectionWorker>();
            services.AddHostedService<WorkShiftExpiryWorker>();

            return services;
        }
    }
}
