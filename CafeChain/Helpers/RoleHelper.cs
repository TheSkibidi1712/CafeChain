using CafeChain.Application.Constants;
using System.Security.Claims;

namespace CafeChain.Helpers
{
    public static class RoleHelper
    {
        public static bool IsAdminGroup(ClaimsPrincipal user)
        {
            return user.IsInRole(RoleConstants.BusinessOwner)
                || user.IsInRole(RoleConstants.AreaManager)
                || user.IsInRole(RoleConstants.StoreManager)
                || user.IsInRole(RoleConstants.AccountantWarehouse)
                || user.IsInRole(RoleConstants.SystemAdmin);
        }
    }
}
