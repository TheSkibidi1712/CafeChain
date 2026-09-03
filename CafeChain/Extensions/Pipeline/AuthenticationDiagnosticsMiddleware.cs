using CafeChain.Extensions.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace CafeChain.Extensions.Pipeline;

public sealed class AuthenticationDiagnosticsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationDiagnosticsMiddleware> _logger;

    public AuthenticationDiagnosticsMiddleware(
        RequestDelegate next,
        ILogger<AuthenticationDiagnosticsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var cookiePresent = context.Request.Cookies.ContainsKey(
            AuthenticationServiceExtensions.AuthenticationCookieName);
        var bearerHeaderPresent = context.Request.Headers.Authorization.ToString()
            .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        var bearerQueryPresent = context.Request.Query.ContainsKey("access_token");
        var cookieResult = await context.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
        var requiresAuthorization = context.GetEndpoint()?.Metadata
            .GetOrderedMetadata<IAuthorizeData>().Count > 0;

        if (cookieResult.Failure != null)
        {
            _logger.LogWarning(
                "AUTH_COOKIE_FAILED Utc={Utc} Machine={Machine} ProcessId={ProcessId} Path={Path} " +
                "CookiePresent={CookiePresent} FailureType={FailureType}",
                DateTimeOffset.UtcNow,
                Environment.MachineName,
                Environment.ProcessId,
                context.Request.Path.Value,
                cookiePresent,
                cookieResult.Failure.GetType().Name);
        }
        else if (cookieResult.Succeeded && IsDiagnosticRequest(context.Request.Path))
        {
            _logger.LogInformation(
                "AUTH_COOKIE_ACCEPTED Utc={Utc} Machine={Machine} ProcessId={ProcessId} Path={Path} " +
                "IssuedUtc={IssuedUtc} ExpiresUtc={ExpiresUtc}",
                DateTimeOffset.UtcNow,
                Environment.MachineName,
                Environment.ProcessId,
                context.Request.Path.Value,
                cookieResult.Properties?.IssuedUtc,
                cookieResult.Properties?.ExpiresUtc);
        }
        else if (requiresAuthorization
                 && context.User.Identity?.IsAuthenticated != true
                 && !bearerHeaderPresent
                 && !bearerQueryPresent)
        {
            _logger.LogWarning(
                "AUTH_CREDENTIAL_MISSING Utc={Utc} Machine={Machine} ProcessId={ProcessId} Path={Path} CookiePresent={CookiePresent}",
                DateTimeOffset.UtcNow,
                Environment.MachineName,
                Environment.ProcessId,
                context.Request.Path.Value,
                cookiePresent);
        }

        await _next(context);
    }

    private static bool IsDiagnosticRequest(PathString path) =>
        path.StartsWithSegments("/hubs")
        || path.StartsWithSegments("/api/v1/pos")
        || path.StartsWithSegments("/StaffHub");
}
