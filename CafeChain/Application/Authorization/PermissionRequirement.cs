using System.Security.Claims;
using CafeChain.Application.Interfaces.Admin.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace CafeChain.Application.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode) => PermissionCode = permissionCode;
    public string PermissionCode { get; }
}

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

        var result = await _permissionService.HasPermissionAsync(accountId, requirement.PermissionCode);
        if (result.IsSuccess && result.Data?.Allowed == true)
            context.Succeed(requirement);
    }
}
