using CafeChain.Constants;
using System.Text.Json.Serialization;

namespace CafeChain.Extensions.Services
{
    public static class WebServiceExtensions
    {
        public static IServiceCollection AddCafeChainWeb(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services
                .AddControllersWithViews()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.Converters.Add(
                        new JsonStringEnumConverter()
                    );
                });

            services.AddMemoryCache();

            var sessionConnectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(sessionConnectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection is required for the distributed SQL session cache.");
            }

            services.AddDistributedSqlServerCache(options =>
            {
                options.ConnectionString = sessionConnectionString;
                options.SchemaName = configuration["SessionCache:SchemaName"] ?? "dbo";
                options.TableName = configuration["SessionCache:TableName"] ?? "SessionCache";
                options.DefaultSlidingExpiration = TimeSpan.FromMinutes(30);
            });

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.Name = ".CafeChain.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddHttpContextAccessor();

            services.AddSignalR();

            services.AddCors(options =>
            {
                options.AddPolicy(CorsPolicyNames.AllowVitePOS, policy =>
                {
                    policy.WithOrigins(
                            "http://localhost:5173",
                            "http://127.0.0.1:5173",
                            "https://localhost:5173",
                            "https://127.0.0.1:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            return services;
        }
    }
}
