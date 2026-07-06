using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Application.Services.Cloudinaries;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Infrastrusture.Configurations;
using CloudinaryDotNet;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;

namespace CafeChain.Extensions.Services
{
    public static class ThirdPartyServiceExtensions
    {
        public static IServiceCollection AddCafeChainThirdPartyServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
        {
            services.AddCafeChainCloudinary(configuration);
            services.AddCafeChainQuestPdf();
            services.AddCafeChainPayOS(environment);

            return services;
        }

        private static IServiceCollection AddCafeChainCloudinary(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<CloudinarySettings>(
                configuration.GetSection("Cloudinary"));

            services.AddSingleton(sp =>
            {
                var settings = sp
                    .GetRequiredService<IOptions<CloudinarySettings>>()
                    .Value;

                var account = new Account(
                    settings.CloudName,
                    settings.ApiKey,
                    settings.ApiSecret);

                var cloudinary = new Cloudinary(account)
                {
                    Api =
                {
                    Secure = true
                }
                };

                return cloudinary;
            });

            services.AddScoped<ICloudinaryService, CloudinaryService>();

            return services;
        }

        private static IServiceCollection AddCafeChainQuestPdf(this IServiceCollection services)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            return services;
        }

        private static IServiceCollection AddCafeChainPayOS(this IServiceCollection services, IWebHostEnvironment environment)
        {
            services.AddScoped<IPayOSService, PayOSService>();

            services.AddHttpClient("PayOS")
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    var handler = new HttpClientHandler
                    {
                        SslProtocols =
                            System.Security.Authentication.SslProtocols.Tls12 |
                            System.Security.Authentication.SslProtocols.Tls13
                    };

                    if (environment.IsDevelopment())
                    {
                        handler.ServerCertificateCustomValidationCallback =
                            (_, _, _, _) => true;
                    }

                    return handler;
                });

            services.AddSingleton(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();

                return new Net.payOS.PayOS(
                    configuration["PayOS:ClientId"],
                    configuration["PayOS:ApiKey"],
                    configuration["PayOS:ChecksumKey"]
                );
            });

            return services;
        }
    }
}
