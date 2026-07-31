using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Infrastructure.Interfaces.Operations;

namespace CafeChain.Application.Services.Operations;

public sealed class InventoryNotificationAudienceResolver : IInventoryNotificationAudienceResolver
{
    private readonly IInventoryReorderNotificationRepository _repository;
    private readonly IScopeAuthorizationService _scopeAuthorization;
    private readonly IAdminPermissionService _permissions;

    public InventoryNotificationAudienceResolver(
        IInventoryReorderNotificationRepository repository,
        IScopeAuthorizationService scopeAuthorization,
        IAdminPermissionService permissions)
    {
        _repository = repository;
        _scopeAuthorization = scopeAuthorization;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<InventoryNotificationRecipient>> ResolveAsync(
        int storeId,
        CancellationToken cancellationToken = default) =>
        await ResolveForPermissionsAsync(
            storeId,
            Array.Empty<string>(),
            cancellationToken);

    public async Task<IReadOnlyList<InventoryNotificationRecipient>> ResolveForPermissionsAsync(
        int storeId,
        IReadOnlyCollection<string> requiredPermissionCodes,
        CancellationToken cancellationToken = default)
    {
        if (storeId <= 0)
            return [];

        var recipients = new List<InventoryNotificationRecipient>();
        foreach (var candidate in await _repository.GetRecipientCandidatesAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await _scopeAuthorization.CanAccessStoreAsync(candidate.StaffId, storeId))
                continue;

            if (!await IsEligibleAsync(candidate, storeId, requiredPermissionCodes))
                continue;

            recipients.Add(new InventoryNotificationRecipient(
                candidate.StaffId,
                candidate.AccountId,
                candidate.Email,
                candidate.FullName));
        }

        return recipients
            .GroupBy(x => x.StaffId)
            .Select(x => x.First())
            .ToList();
    }

    public async Task<IReadOnlyList<int>> ResolveStoreIdsAsync(
        int staffId,
        CancellationToken cancellationToken = default)
    {
        if (staffId <= 0)
            return [];

        var candidate = (await _repository.GetRecipientCandidatesAsync())
            .FirstOrDefault(x => x.StaffId == staffId);
        if (candidate == null)
            return [];

        var allowedStores = await _scopeAuthorization.GetAllowedStoresAsync(staffId);
        var storeIds = new List<int>();
        foreach (var store in allowedStores)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsEligibleAsync(candidate, store.StoreId, Array.Empty<string>()))
                storeIds.Add(store.StoreId);
        }

        return storeIds.Distinct().ToList();
    }

    private async Task<bool> IsEligibleAsync(
        ReorderNotificationRecipientRow candidate,
        int storeId,
        IReadOnlyCollection<string> requiredPermissionCodes)
    {
        var permission = await _permissions.HasPermissionAsync(
            candidate.AccountId,
            PermissionConstants.NotificationView,
            storeId);
        if (!permission.IsSuccess || permission.Data?.Allowed != true)
            return false;

        foreach (var permissionCode in requiredPermissionCodes
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.Ordinal))
        {
            var required = await _permissions.HasPermissionAsync(
                candidate.AccountId,
                permissionCode,
                storeId);
            if (!required.IsSuccess || required.Data?.Allowed != true)
                return false;
        }

        return true;
    }
}
