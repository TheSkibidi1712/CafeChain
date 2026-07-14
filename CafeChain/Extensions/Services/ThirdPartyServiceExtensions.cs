using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Application.Services.Cloudinaries;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Infrastrusture.Configurations;
using CafeChain.Infrastructure.Configurations;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Services.AI;
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
            services.AddCafeChainOllama(configuration);
            services.AddCafeChainPexels(configuration);
            services.AddCafeChainComfyUI(configuration);

            return services;
        }

        private static IServiceCollection AddCafeChainComfyUI(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ComfyUIOptions>(configuration.GetSection(ComfyUIOptions.SectionName));
            var options = configuration.GetSection(ComfyUIOptions.SectionName).Get<ComfyUIOptions>() ?? new();
            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
                throw new InvalidOperationException("ComfyUI:BaseUrl phải là URL tuyệt đối hợp lệ.");

            services.AddHttpClient<IComfyUIClient, ComfyUIClient>(client =>
            {
                client.BaseAddress = baseUri;
                client.Timeout = Timeout.InfiniteTimeSpan;
            });
            return services;
        }

        private static IServiceCollection AddCafeChainPexels(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<PexelsOptions>(configuration.GetSection(PexelsOptions.SectionName));
            services.Configure<AIImageOptions>(configuration.GetSection(AIImageOptions.SectionName));
            services.Configure<AIImagePipelineOptions>(configuration.GetSection(AIImagePipelineOptions.SectionName));
            var options = configuration.GetSection(PexelsOptions.SectionName).Get<PexelsOptions>() ?? new();
            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
                throw new InvalidOperationException("Pexels:BaseUrl phải là URL tuyệt đối hợp lệ.");

            services.AddHttpClient<IPexelsClient, PexelsClient>(client =>
            {
                client.BaseAddress = baseUri;
                client.Timeout = Timeout.InfiniteTimeSpan;
            });
            return services;
        }

        private static IServiceCollection AddCafeChainOllama(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AIOptions>(configuration.GetSection(AIOptions.SectionName));
            services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
            var options = configuration.GetSection(OllamaOptions.SectionName).Get<OllamaOptions>() ?? new();
            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
                throw new InvalidOperationException("Ollama:BaseUrl phải là URL tuyệt đối hợp lệ.");

            services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
            {
                client.BaseAddress = baseUri;
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 600));
            });
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
