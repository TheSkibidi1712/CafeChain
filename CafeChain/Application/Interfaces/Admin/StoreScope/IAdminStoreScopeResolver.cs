using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.StoreScope;

namespace CafeChain.Application.Interfaces.Admin.StoreScope
{
    public enum AdminStoreScopeMode
    {
        Standard = 0,
        ReorderSuggestion = 1
    }

    public interface IAdminStoreScopeResolver
    {
        Task<AdminStoreScopeResolution> ResolveAsync(
            AdminActorContext actor,
            int? requestedStoreId = null,
            CancellationToken cancellationToken = default);

        Task<AdminStoreScopeResolution> ResolveAsync(
            AdminActorContext actor,
            int? requestedStoreId,
            AdminStoreScopeMode mode,
            CancellationToken cancellationToken = default);
    }
}
