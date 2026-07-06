using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Extensions.Services
{
    public static class DatabaseServiceExtensions
    {
        public static IServiceCollection AddCafeChainDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options
                    .UseSqlServer(
                        configuration.GetConnectionString("DefaultConnection"),
                        sqlOptions => sqlOptions.CommandTimeout(120))
                    .UseLazyLoadingProxies();
            });

            return services;
        }
    }
}
