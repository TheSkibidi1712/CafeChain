using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Actor;

namespace CafeChain.Application.Services.Admin.Actor
{
    public sealed class AdminActorContextAccessor : IAdminActorContextAccessor
    {
        public AdminActorContext Get(ClaimsPrincipal user)
        {
            if (user == null)
            {
                return new AdminActorContext();
            }

            var staffClaim = user.FindFirst("StaffId")?.Value
                             ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var accountClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var storeClaim = user.FindFirst("StoreId")?.Value;

            int.TryParse(accountClaim, out var accountId);
            int.TryParse(staffClaim, out var staffId);
            int.TryParse(storeClaim, out var storeId);

            var roles = user.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToList();

            return new AdminActorContext
            {
                AccountId = accountId > 0 ? accountId : 0,
                StaffId = staffId > 0 ? staffId : 0,
                StoreId = storeId > 0 ? storeId : 0,
                RoleNames = roles
            };
        }
    }
}
