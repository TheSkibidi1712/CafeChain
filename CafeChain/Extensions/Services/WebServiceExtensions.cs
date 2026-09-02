using CafeChain.Constants;
using System.Text.Json.Serialization;

namespace CafeChain.Extensions.Services
{
    public static class WebServiceExtensions
    {
        public static IServiceCollection AddCafeChainWeb(this IServiceCollection services)
        {
            services.AddLocalization(options => options.ResourcesPath = "Resources");

            services
                .AddControllersWithViews()
                .AddViewLocalization()
                .AddDataAnnotationsLocalization()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.Converters.Add(
                        new JsonStringEnumConverter()
                    );
                });

            services.AddMemoryCache();

            services.AddDistributedMemoryCache();

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
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
