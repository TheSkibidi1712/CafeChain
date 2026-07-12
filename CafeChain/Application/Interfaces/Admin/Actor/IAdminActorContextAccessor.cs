using CafeChain.Application.DTOs.Admin.Actor;
using System.Security.Claims;

namespace CafeChain.Application.Interfaces.Admin.Actor
{
    public interface IAdminActorContextAccessor
    {
        AdminActorContext Get(ClaimsPrincipal user);
    }
}
