using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;

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

                    options.Events = new CookieAuthenticationEvents
                    {
                        OnRedirectToLogin = context =>
                            HandleAuthRedirectAsync(
                                context,
                                StatusCodes.Status401Unauthorized,
                                "Bạn cần đăng nhập để truy cập chức năng này."),

                        OnRedirectToAccessDenied = context =>
                            HandleAuthRedirectAsync(
                                context,
                                StatusCodes.Status403Forbidden,
                                "Bạn không có quyền truy cập chức năng này. Vui lòng liên hệ cấp trên hoặc quản trị viên để được cấp quyền.")
                    };
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
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"].ToString();
                            if (!string.IsNullOrWhiteSpace(accessToken)
                                && context.HttpContext.Request.Path.StartsWithSegments(
                                    "/hubs/inventory-notifications"))
                            {
                                context.Token = accessToken;
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

            return services;
        }

        private static Task HandleAuthRedirectAsync(
            RedirectContext<CookieAuthenticationOptions> context,
            int statusCode,
            string message)
        {
            if (!IsJsonRequest(context.Request))
            {
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            return context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                success = false,
                message
            }));
        }

        private static bool IsJsonRequest(HttpRequest request)
        {
            return request.Headers["X-Requested-With"].ToString()
                    .Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
                request.Headers["Accept"].ToString()
                    .Contains("application/json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
