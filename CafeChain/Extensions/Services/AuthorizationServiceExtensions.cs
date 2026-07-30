using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using Microsoft.AspNetCore.Authorization;

namespace CafeChain.Extensions.Services;

public static class AuthorizationServiceExtensions
{
    public static IServiceCollection AddCafeChainAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicyConstants.AdminPanelAccess, policy =>
            {
                policy.RequireRole(
                    RoleConstants.BusinessOwner,
                    RoleConstants.AreaManager,
                    RoleConstants.StoreManager,
                    RoleConstants.AccountantWarehouse,
                    RoleConstants.SystemAdmin);
            });
            options.AddPolicy(AuthorizationPolicyConstants.AdminDashboardApp, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(
                    RoleConstants.BusinessOwner,
                    RoleConstants.AreaManager,
                    RoleConstants.StoreManager,
                    RoleConstants.AccountantWarehouse,
                    RoleConstants.SystemAdmin);
                policy.AddRequirements(
                    new PermissionRequirement(PermissionConstants.AppAdminDashboard));
            });
            AddPermissionPolicy(
                options,
                AuthorizationPolicyConstants.StaffHubApp,
                PermissionConstants.AppStaffHub);
            AddPermissionPolicy(
                options,
                AuthorizationPolicyConstants.PosApp,
                PermissionConstants.AppPos);
        });

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        return services;
    }

    private static void AddPermissionPolicy(
        AuthorizationOptions options,
        string policyName,
        string permissionCode)
    {
        options.AddPolicy(policyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement(permissionCode));
        });
    }
}
