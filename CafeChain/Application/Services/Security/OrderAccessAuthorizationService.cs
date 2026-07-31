using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Interfaces.Security;

namespace CafeChain.Application.Services.Security;

public sealed class OrderAccessAuthorizationService : IOrderAccessAuthorizationService
{
    private static readonly string[] AdminReadRoles =
    {
        RoleConstants.BusinessOwner,
        RoleConstants.AreaManager,
        RoleConstants.StoreManager,
        RoleConstants.AccountantWarehouse,
        RoleConstants.SystemAdmin
    };

    private static readonly string[] PosOperatorRoles =
    {
        RoleConstants.BusinessOwner,
        RoleConstants.AreaManager,
        RoleConstants.StoreManager,
        RoleConstants.ShiftSupervisor,
        RoleConstants.SalesStaff,
        RoleConstants.SystemAdmin
    };

    private static readonly string[] RefundRequestRoles =
    {
        RoleConstants.BusinessOwner,
        RoleConstants.AreaManager,
        RoleConstants.StoreManager,
        RoleConstants.ShiftSupervisor,
        RoleConstants.SystemAdmin
    };

    private static readonly string[] RefundConfirmRoles =
    {
        RoleConstants.BusinessOwner,
        RoleConstants.AreaManager,
        RoleConstants.StoreManager,
        RoleConstants.SystemAdmin
    };

    private readonly IScopeAuthorizationService _scopeAuthorization;

    public OrderAccessAuthorizationService(IScopeAuthorizationService scopeAuthorization)
    {
        _scopeAuthorization = scopeAuthorization;
    }

    public OrderAccessDecision AuthorizeAction(AdminActorContext actor, string action)
    {
        if (actor == null || actor.StaffId <= 0)
            return OrderAccessDecision.Forbidden;

        var allowedRoles = action switch
        {
            OrderAccessActions.AdminList or
            OrderAccessActions.AdminDetail or
            OrderAccessActions.AdminExport => AdminReadRoles,

            OrderAccessActions.PosHistory or
            OrderAccessActions.Reprint or
            OrderAccessActions.OfflineSync => PosOperatorRoles,

            OrderAccessActions.RefundRequest => RefundRequestRoles,
            OrderAccessActions.RefundConfirm => RefundConfirmRoles,
            _ => Array.Empty<string>()
        };

        return actor.RoleNames.Any(role =>
                allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            ? OrderAccessDecision.Allowed
            : OrderAccessDecision.Forbidden;
    }

    public async Task<OrderAccessDecision> AuthorizeAsync(
        AdminActorContext actor,
        string action,
        int targetStoreId)
    {
        var actionDecision = AuthorizeAction(actor, action);
        if (actionDecision != OrderAccessDecision.Allowed)
            return actionDecision;

        if (targetStoreId <= 0)
            return OrderAccessDecision.NotFound;

        return await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, targetStoreId)
            ? OrderAccessDecision.Allowed
            : OrderAccessDecision.NotFound;
    }
}
