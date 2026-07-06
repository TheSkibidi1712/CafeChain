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

            return services;
        }
    }
}
