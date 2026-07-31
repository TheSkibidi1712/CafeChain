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

        public static bool IsSystemAdmin(ClaimsPrincipal user) =>
            user.IsInRole(RoleConstants.SystemAdmin);

        public static bool HasAnyRoleOrSystemAdmin(
            ClaimsPrincipal user,
            params string[] allowedRoles)
        {
            return IsSystemAdmin(user)
                || allowedRoles.Any(user.IsInRole);
        }

        public static bool HasAnyRoleOrSystemAdmin(
            IEnumerable<string> roleNames,
            params string[] allowedRoles)
        {
            var roles = roleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return roles.Contains(RoleConstants.SystemAdmin)
                || allowedRoles.Any(roles.Contains);
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
