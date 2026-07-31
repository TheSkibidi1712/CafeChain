using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.StoreScope;
using CafeChain.Application.Interfaces.Security;

namespace CafeChain.Application.Interfaces.Admin.StoreScope
{
    public interface IAdminStoreScopeResolver
    {
        Task<AdminStoreScopeResolution> ResolveAsync(
            AdminActorContext actor,
            int? requestedStoreId = null,
            CancellationToken cancellationToken = default);

        Task<AdminStoreScopeResolution> ResolveAsync(
            AdminActorContext actor,
            StoreScopePurpose purpose,
            int? requestedStoreId = null,
            CancellationToken cancellationToken = default) =>
            purpose == StoreScopePurpose.Default
                ? ResolveAsync(actor, requestedStoreId, cancellationToken)
                : Task.FromResult(new AdminStoreScopeResolution
                {
                    Status = AdminStoreScopeResolutionStatus.NoAccessibleStore,
                    ErrorCode = AdminStoreScopeErrorCodes.StoreScopeNotConfigured,
                    Message = "Store scope purpose is not supported by this resolver."
                });
    }
}
