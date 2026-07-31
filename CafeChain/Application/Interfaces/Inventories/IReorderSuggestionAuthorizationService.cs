using CafeChain.Application.DTOs.Admin.Actor;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IReorderSuggestionAuthorizationService
{
    Task<bool> CanViewAsync(
        AdminActorContext actor,
        int storeId,
        CancellationToken cancellationToken = default);

    Task<bool> CanConfirmAsync(
        AdminActorContext actor,
        int storeId,
        CancellationToken cancellationToken = default);
}
