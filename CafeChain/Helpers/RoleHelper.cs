using CafeChain.Application.Constants;
using System.Security.Claims;

namespace CafeChain.Helpers
{
    public static class RoleHelper
    {
        public static bool IsAdminGroup(ClaimsPrincipal user)
        {
            return user.IsInRole(RoleConstants.SuperAdmin)
                || user.IsInRole(RoleConstants.CEO)
                || user.IsInRole(RoleConstants.CFO)
                || user.IsInRole(RoleConstants.MarketingManager)
                || user.IsInRole(RoleConstants.OperationsManager)
                || user.IsInRole(RoleConstants.HRManager)
                || user.IsInRole(RoleConstants.AreaManager)
                || user.IsInRole(RoleConstants.StoreManager)
                || user.IsInRole(RoleConstants.ShiftSupervisor)
                || user.IsInRole(RoleConstants.WarehouseKeeper);
        }
    }
}
