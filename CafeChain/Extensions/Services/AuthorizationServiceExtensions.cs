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
                        RoleConstants.BusinessOwner,
                        RoleConstants.AreaManager,
                        RoleConstants.StoreManager,
                        RoleConstants.AccountantWarehouse,
                        RoleConstants.SystemAdmin
                    );
                });
            });

            return services;
        }
    }
}
