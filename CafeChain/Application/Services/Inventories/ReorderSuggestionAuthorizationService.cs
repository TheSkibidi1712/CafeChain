using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

public sealed class ReorderSuggestionAuthorizationService
    : IReorderSuggestionAuthorizationService
{
    private readonly AppDbContext _context;
    private readonly IAdminPermissionService _permissions;

    public ReorderSuggestionAuthorizationService(
        AppDbContext context,
        IAdminPermissionService permissions)
    {
        _context = context;
        _permissions = permissions;
    }

    public Task<bool> CanViewAsync(
        AdminActorContext actor,
        int storeId,
        CancellationToken cancellationToken = default) =>
        HasRoleAndPermissionsAsync(
            actor,
            storeId,
            [
                RoleConstants.BusinessOwner,
                RoleConstants.AreaManager,
                RoleConstants.StoreManager,
                RoleConstants.AccountantWarehouse,
                RoleConstants.SystemAdmin
            ],
            [PermissionConstants.ReorderSuggestionView],
            cancellationToken);

    public Task<bool> CanConfirmAsync(
        AdminActorContext actor,
        int storeId,
        CancellationToken cancellationToken = default) =>
        HasRoleAndPermissionsAsync(
            actor,
            storeId,
            [
                RoleConstants.StoreManager,
                RoleConstants.AccountantWarehouse,
                RoleConstants.SystemAdmin
            ],
            [
                PermissionConstants.ReorderSuggestionView,
                PermissionConstants.RestockCreate
            ],
            cancellationToken);

    private async Task<bool> HasRoleAndPermissionsAsync(
        AdminActorContext actor,
        int storeId,
        IReadOnlyCollection<string> allowedRoles,
        IReadOnlyCollection<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        if (actor.StaffId <= 0 || storeId <= 0)
            return false;
        if (!actor.RoleNames.Any(role => allowedRoles.Contains(
                role,
                StringComparer.OrdinalIgnoreCase)))
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
                permissionCode,
                storeId);
            if (!result.IsSuccess
                || result.Data?.Allowed != true
                || result.Data.StaffId != actor.StaffId)
            {
                return false;
            }
        }

        return true;
    }
}
