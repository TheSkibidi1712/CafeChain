using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

public sealed class ReorderSuggestionAuthorizationService
    : IReorderSuggestionAuthorizationService
{
    private readonly AppDbContext _context;
    private readonly IAdminPermissionService _permissions;
    private readonly IScopeAuthorizationService _scopeAuthorization;

    public ReorderSuggestionAuthorizationService(
        AppDbContext context,
        IAdminPermissionService permissions,
        IScopeAuthorizationService scopeAuthorization)
    {
        _context = context;
        _permissions = permissions;
        _scopeAuthorization = scopeAuthorization;
    }

    public Task<bool> CanViewAsync(
        AdminActorContext actor,
        int storeId,
        CancellationToken cancellationToken = default) =>
        HasPermissionsAndScopeAsync(
            actor,
            storeId,
            [PermissionConstants.ReorderSuggestionView],
            cancellationToken);

    public Task<bool> CanConfirmAsync(
        AdminActorContext actor,
        int storeId,
        CancellationToken cancellationToken = default) =>
        HasPermissionsAndScopeAsync(
            actor,
            storeId,
            [
                PermissionConstants.ReorderSuggestionView,
                PermissionConstants.RestockCreate
            ],
            cancellationToken);

    private async Task<bool> HasPermissionsAndScopeAsync(
        AdminActorContext actor,
        int storeId,
        IReadOnlyCollection<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        if (actor.StaffId <= 0 || storeId <= 0)
            return false;

        var accountId = actor.AccountId;
        if (accountId <= 0)
        {
            accountId = await _context.Staffs
                .AsNoTracking()
                .Where(x =>
                    x.StaffId == actor.StaffId
                    && x.Active
                    && x.Account.Active)
                .Select(x => x.AccountId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (accountId <= 0)
            return false;

        foreach (var permissionCode in permissionCodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _permissions.HasPermissionAsync(
                accountId,
                permissionCode);
            if (!result.IsSuccess
                || result.Data?.Allowed != true
                || result.Data.StaffId != actor.StaffId)
            {
                return false;
            }
        }

        return await _scopeAuthorization.CanAccessStoreAsync(
            actor.StaffId,
            storeId,
            StoreScopePurpose.ReorderSuggestion);
    }
}
