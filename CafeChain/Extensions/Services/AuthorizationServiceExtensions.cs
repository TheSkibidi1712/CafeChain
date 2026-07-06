using CafeChain.Application.Constants;

namespace CafeChain.Extensions.Services
{
    public static class AuthorizationServiceExtensions
    {
        public static IServiceCollection AddCafeChainAuthorization(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy("RequireAdminPanelAccess", policy =>
                {
                    policy.RequireRole(
                        RoleConstants.SuperAdmin,
                        RoleConstants.CEO,
                        RoleConstants.CFO,
                        RoleConstants.MarketingManager,
                        RoleConstants.OperationsManager,
                        RoleConstants.HRManager,
                        RoleConstants.AreaManager,
                        RoleConstants.StoreManager
                    );
                });
            });

            return services;
        }
    }
}
