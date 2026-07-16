using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.StoreScope;

namespace CafeChain.Application.Interfaces.Admin.StoreScope
{
    public interface IAdminStoreScopeResolver
    {
        Task<AdminStoreScopeResolution> ResolveAsync(
            AdminActorContext actor,
            int? requestedStoreId = null,
            CancellationToken cancellationToken = default);
    }
}
