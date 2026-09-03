using System.Security.Claims;
using CafeChain.Application.Interfaces.Admin.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace CafeChain.Application.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode) => PermissionCode = permissionCode;
    public string PermissionCode { get; }
}

/// <summary>
/// Grants entry to the Admin area only when the account has at least one
/// effective permission from the authoritative permission service.
/// Feature controllers still apply their narrower permission requirements.
/// </summary>
public sealed class AdminPanelAccessRequirement : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IAdminPermissionService _permissionService;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IAdminPermissionService permissionService,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var accountValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(accountValue, out var accountId) || accountId <= 0)
        {
            _logger.LogWarning(
                "AUTHZ_PERMISSION_DENIED PermissionCode={PermissionCode} Reason={Reason} Authenticated={Authenticated}",
                requirement.PermissionCode,
                "ACCOUNT_CLAIM_MISSING",
                context.User.Identity?.IsAuthenticated == true);
            return;
        }

        var result = await _permissionService.GetEffectivePermissionCodesAsync(accountId);
        if (result.IsSuccess && result.Data?.Contains(requirement.PermissionCode) == true)
        {
            context.Succeed(requirement);
            return;
        }

        _logger.LogWarning(
            "AUTHZ_PERMISSION_DENIED PermissionCode={PermissionCode} Reason={Reason}",
            requirement.PermissionCode,
            result.IsSuccess ? "PERMISSION_NOT_GRANTED" : result.ErrorCode ?? "PERMISSION_LOOKUP_FAILED");
    }
}

public sealed class AdminPanelAccessAuthorizationHandler
    : AuthorizationHandler<AdminPanelAccessRequirement>
{
    private readonly IAdminPermissionService _permissionService;
    private readonly ILogger<AdminPanelAccessAuthorizationHandler> _logger;

    public AdminPanelAccessAuthorizationHandler(
        IAdminPermissionService permissionService,
        ILogger<AdminPanelAccessAuthorizationHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminPanelAccessRequirement requirement)
    {
        var accountValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(accountValue, out var accountId) || accountId <= 0)
        {
            _logger.LogWarning(
                "AUTHZ_ADMIN_PANEL_DENIED Reason={Reason} Authenticated={Authenticated}",
                "ACCOUNT_CLAIM_MISSING",
                context.User.Identity?.IsAuthenticated == true);
            return;
        }

        var result = await _permissionService.GetEffectivePermissionCodesAsync(accountId);
        if (result.IsSuccess && result.Data?.Count > 0)
        {
            context.Succeed(requirement);
            return;
        }

        _logger.LogWarning(
            "AUTHZ_ADMIN_PANEL_DENIED Reason={Reason}",
            result.IsSuccess ? "NO_EFFECTIVE_PERMISSION" : result.ErrorCode ?? "PERMISSION_LOOKUP_FAILED");
    }
}
