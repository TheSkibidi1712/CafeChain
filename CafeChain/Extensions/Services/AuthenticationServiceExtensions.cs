using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using CafeChain.Application.Interfaces.POS;

namespace CafeChain.Extensions.Services
{
    public static class AuthenticationServiceExtensions
    {
        public const string AuthenticationCookieName = ".CafeChain.Auth";

        public static IServiceCollection AddCafeChainAuthentication(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
        {
            var jwtKey = configuration["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException(
                    "Jwt:Key is required. Configure it with .NET User Secrets in Development " +
                    "or a deployment secret/environment variable in other environments.");
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

                    options.Cookie.Name = AuthenticationCookieName;
                    options.Cookie.Path = "/";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = environment.IsDevelopment()
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;
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
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrWhiteSpace(accessToken)
                                && (path.StartsWithSegments("/hubs/inventory-notifications")
                                    || path.StartsWithSegments("/hubs/workshifts")))
                            {
                                context.Token = accessToken;
                            }

                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("CafeChain.Authentication.Jwt");
                            logger.LogDebug(
                                "AUTH_JWT_TOKEN_DISCOVERY Path={Path} HeaderTokenPresent={HeaderTokenPresent} QueryTokenPresent={QueryTokenPresent}",
                                path.Value,
                                context.Request.Headers.Authorization.ToString()
                                    .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase),
                                !string.IsNullOrWhiteSpace(accessToken));

                            return Task.CompletedTask;
                        },
                        OnTokenValidated = async context =>
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("CafeChain.Authentication.Jwt");
                            var sessionIdText = context.Principal?.FindFirst("PosSessionId")?.Value;
                            var jwtId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                            logger.LogDebug(
                                "AUTH_JWT_CRYPTO_VALID Path={Path} Issuer={Issuer} Audience={Audience} ExpiresAtUnix={ExpiresAtUnix} PosSessionSuffix={PosSessionSuffix}",
                                context.HttpContext.Request.Path.Value,
                                context.Principal?.FindFirst(JwtRegisteredClaimNames.Iss)?.Value,
                                context.Principal?.FindFirst(JwtRegisteredClaimNames.Aud)?.Value,
                                context.Principal?.FindFirst(JwtRegisteredClaimNames.Exp)?.Value,
                                MaskSessionId(sessionIdText));
                            if (!Guid.TryParse(sessionIdText, out var sessionId)
                                || string.IsNullOrWhiteSpace(jwtId))
                            {
                                logger.LogWarning(
                                    "AUTH_POS_SESSION_REJECTED Path={Path} ErrorCode={ErrorCode} Issuer={Issuer} Audience={Audience} ExpiresAtUnix={ExpiresAtUnix} PosSessionSuffix={PosSessionSuffix}",
                                    context.HttpContext.Request.Path.Value,
                                    "POS_SESSION_CLAIMS_MISSING",
                                    context.Principal?.FindFirst(JwtRegisteredClaimNames.Iss)?.Value,
                                    context.Principal?.FindFirst(JwtRegisteredClaimNames.Aud)?.Value,
                                    context.Principal?.FindFirst(JwtRegisteredClaimNames.Exp)?.Value,
                                    MaskSessionId(sessionIdText));
                                context.Fail("POS access session claim is missing.");
                                return;
                            }

                            var validator = context.HttpContext.RequestServices
                                .GetRequiredService<IPosAccessSessionService>();
                            var validation = await validator.ValidateAsync(
                                sessionId,
                                jwtId,
                                context.HttpContext.RequestAborted);
                            if (!validation.IsSuccess)
                            {
                                context.HttpContext.Items["PosSessionErrorCode"] = validation.ErrorCode;
                                context.HttpContext.Items["PosSessionErrorMessage"] = validation.Message;
                                logger.LogWarning(
                                    "AUTH_POS_SESSION_REJECTED Path={Path} ErrorCode={ErrorCode} Issuer={Issuer} Audience={Audience} ExpiresAtUnix={ExpiresAtUnix} PosSessionSuffix={PosSessionSuffix}",
                                    context.HttpContext.Request.Path.Value,
                                    validation.ErrorCode,
                                    context.Principal?.FindFirst(JwtRegisteredClaimNames.Iss)?.Value,
                                    context.Principal?.FindFirst(JwtRegisteredClaimNames.Aud)?.Value,
                                    context.Principal?.FindFirst(JwtRegisteredClaimNames.Exp)?.Value,
                                    MaskSessionId(sessionIdText));
                                context.Fail(validation.Message ?? "POS access session is not active.");
                                return;
                            }
                            context.HttpContext.Items["PosAccessMode"] = validation.Data?.AccessMode;
                            context.HttpContext.Items["PosWorkShiftStatus"] = validation.Data?.WorkShiftStatus;
                            logger.LogDebug(
                                "AUTH_POS_SESSION_ACCEPTED Path={Path} AccessMode={AccessMode} WorkShiftStatus={WorkShiftStatus} PosSessionSuffix={PosSessionSuffix}",
                                context.HttpContext.Request.Path.Value,
                                validation.Data?.AccessMode,
                                validation.Data?.WorkShiftStatus,
                                MaskSessionId(sessionIdText));
                        },
                        OnAuthenticationFailed = context =>
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("CafeChain.Authentication.Jwt");
                            logger.LogWarning(
                                "AUTH_JWT_FAILED Path={Path} FailureType={FailureType} HeaderTokenPresent={HeaderTokenPresent} QueryTokenPresent={QueryTokenPresent}",
                                context.HttpContext.Request.Path.Value,
                                context.Exception.GetType().Name,
                                context.Request.Headers.Authorization.ToString()
                                    .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase),
                                context.Request.Query.ContainsKey("access_token"));
                            return Task.CompletedTask;
                        },
                        OnChallenge = async context =>
                        {
                            if (context.Response.HasStarted) return;
                            var errorCode = context.HttpContext.Items["PosSessionErrorCode"] as string;
                            var message = context.HttpContext.Items["PosSessionErrorMessage"] as string;
                            if (string.IsNullOrWhiteSpace(errorCode))
                            {
                                var logger = context.HttpContext.RequestServices
                                    .GetRequiredService<ILoggerFactory>()
                                    .CreateLogger("CafeChain.Authentication.Jwt");
                                logger.LogWarning(
                                    "AUTH_JWT_CHALLENGE Path={Path} HeaderTokenPresent={HeaderTokenPresent} QueryTokenPresent={QueryTokenPresent}",
                                    context.HttpContext.Request.Path.Value,
                                    context.Request.Headers.Authorization.ToString()
                                        .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase),
                                    context.Request.Query.ContainsKey("access_token"));
                                return;
                            }
                            context.HandleResponse();
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(JsonSerializer.Serialize(new
                            {
                                success = false,
                                errorCode,
                                message
                            }));
                        }
                    };
                });

            return services;
        }

        private static string? MaskSessionId(string? sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return null;
            var normalized = sessionId.Replace("-", string.Empty, StringComparison.Ordinal);
            return normalized.Length <= 8 ? normalized : normalized[^8..];
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
