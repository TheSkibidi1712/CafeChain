using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace CafeChain.Extensions.Services
{
    public static class AuthenticationServiceExtensions
    {
        public static IServiceCollection AddCafeChainAuthentication(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
        {
            var jwtKey = configuration["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                if (environment.IsProduction())
                {
                    throw new InvalidOperationException(
                        "Jwt:Key is required in Production. Please configure it in appsettings or environment variables.");
                }

                jwtKey = "CafeChain-POS-JWT-Secret-Key-Change-In-Production-2026-Min32Chars!";
            }

            var jwtIssuer = configuration["Jwt:Issuer"] ?? "CafeChain";
            var jwtAudience = configuration["Jwt:Audience"] ?? "CafeChain.POS";

            services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.AccessDeniedPath = "/Account/AccessDenied";

                    options.ExpireTimeSpan = TimeSpan.FromDays(7);
                    options.SlidingExpiration = true;

                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer = jwtIssuer,
                        ValidAudience = jwtAudience,

                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtKey))
                    };
                });

            return services;
        }
    }
}
