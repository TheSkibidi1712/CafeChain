using CafeChain.Application.Constants;
using System.Security.Claims;

namespace CafeChain.Helpers
{
    public static class RoleHelper
    {
        /// <summary>
        /// Roles allowed to create/update PreparedItem master (matches AdminPreparedItemController Authorize).
        /// SystemAdmin, BusinessOwner, AccountantWarehouse only — not StoreManager/AreaManager.
        /// </summary>
        public const string PreparedItemWriteRoles =
            RoleConstants.SystemAdmin + "," +
            RoleConstants.BusinessOwner + "," +
            RoleConstants.AccountantWarehouse;

        public const string RecipeWriteRoles = PreparedItemWriteRoles;

        public static bool IsAdminGroup(ClaimsPrincipal user)
        {
            return user.IsInRole(RoleConstants.BusinessOwner)
                || user.IsInRole(RoleConstants.AreaManager)
                || user.IsInRole(RoleConstants.StoreManager)
                || user.IsInRole(RoleConstants.AccountantWarehouse)
                || user.IsInRole(RoleConstants.SystemAdmin);
        }

        /// <summary>PreparedItem master write (create/edit/toggle) — same set as Create Authorize roles.</summary>
        public static bool CanWritePreparedItems(ClaimsPrincipal user)
        {
            return user.IsInRole(RoleConstants.SystemAdmin)
                || user.IsInRole(RoleConstants.BusinessOwner)
                || user.IsInRole(RoleConstants.AccountantWarehouse);
        }

        public static bool CanWriteRecipes(ClaimsPrincipal user)
        {
            return CanWritePreparedItems(user);
        }
    }
}
