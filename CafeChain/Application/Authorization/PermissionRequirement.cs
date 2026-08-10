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

    public PermissionAuthorizationHandler(IAdminPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var accountValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(accountValue, out var accountId) || accountId <= 0)
            return;

        var result = await _permissionService.GetEffectivePermissionCodesAsync(accountId);
        if (result.IsSuccess && result.Data?.Contains(requirement.PermissionCode) == true)
            context.Succeed(requirement);
    }
}

public sealed class AdminPanelAccessAuthorizationHandler
    : AuthorizationHandler<AdminPanelAccessRequirement>
{
    private readonly IAdminPermissionService _permissionService;

    public AdminPanelAccessAuthorizationHandler(IAdminPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminPanelAccessRequirement requirement)
    {
        var accountValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(accountValue, out var accountId) || accountId <= 0)
            return;

        var result = await _permissionService.GetEffectivePermissionCodesAsync(accountId);
        if (result.IsSuccess && result.Data?.Count > 0)
            context.Succeed(requirement);
    }
}
