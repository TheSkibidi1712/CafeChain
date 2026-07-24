using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;

namespace CafeChain.Tests.Testing;

internal sealed class AllowAllOrderAccessAuthorizationService : IOrderAccessAuthorizationService
{
    public static AllowAllOrderAccessAuthorizationService Instance { get; } = new();

    public OrderAccessDecision AuthorizeAction(AdminActorContext actor, string action) =>
        OrderAccessDecision.Allowed;

    public Task<OrderAccessDecision> AuthorizeAsync(
        AdminActorContext actor,
        string action,
        int targetStoreId) =>
        Task.FromResult(OrderAccessDecision.Allowed);
}

internal sealed class HomeStoreOrderAccessAuthorizationService : IOrderAccessAuthorizationService
{
    public static HomeStoreOrderAccessAuthorizationService Instance { get; } = new();

    public OrderAccessDecision AuthorizeAction(AdminActorContext actor, string action)
    {
        var roles = action switch
        {
            OrderAccessActions.RefundRequest => new[]
            {
                RoleConstants.StoreManager,
                RoleConstants.ShiftSupervisor,
                RoleConstants.AreaManager,
                RoleConstants.BusinessOwner,
                RoleConstants.SystemAdmin
            },
            OrderAccessActions.RefundConfirm => new[]
            {
                RoleConstants.StoreManager,
                RoleConstants.AreaManager,
                RoleConstants.BusinessOwner,
                RoleConstants.SystemAdmin
            },
            _ => Array.Empty<string>()
        };

        return actor.StaffId > 0 &&
               actor.RoleNames.Any(role => roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            ? OrderAccessDecision.Allowed
            : OrderAccessDecision.Forbidden;
    }

    public Task<OrderAccessDecision> AuthorizeAsync(
        AdminActorContext actor,
        string action,
        int targetStoreId)
    {
        var actionDecision = AuthorizeAction(actor, action);
        if (actionDecision != OrderAccessDecision.Allowed)
            return Task.FromResult(actionDecision);

        return Task.FromResult(
            actor.RoleNames.Contains(RoleConstants.SystemAdmin, StringComparer.OrdinalIgnoreCase)
            || actor.StoreId == targetStoreId
                ? OrderAccessDecision.Allowed
                : OrderAccessDecision.NotFound);
    }
}

internal static class OfflineSyncTestExtensions
{
    public static Task<ServiceResult<object>> CommitOfflineSyncedOrderAsync(
        this IPOSOrderService service,
        POSOrderCommitDto dto,
        int staffId,
        int storeId,
        int workShiftId,
        DateTime soldAt)
    {
        return service.CommitOfflineSyncedOrderAsync(dto, new OfflineOrderSyncContext
        {
            ActorStaffId = staffId,
            ActorRoleNames = new[] { RoleConstants.SalesStaff },
            ClaimedStaffId = staffId,
            ClaimedStoreId = storeId,
            WorkShiftId = workShiftId,
            SoldAt = soldAt
        });
    }
}

internal static class RefundTestExtensions
{
    public static Task<ServiceResult<OrderRefundResultDto>> RequestFullRefundAsync(
        this IOrderRefundService service,
        RequestFullOrderRefundDto dto,
        int staffId,
        int storeId,
        IReadOnlyList<string> roles)
    {
        return service.RequestFullRefundAsync(dto, Actor(staffId, storeId, roles));
    }

    public static Task<ServiceResult<OrderRefundResultDto>> ConfirmCashRefundAsync(
        this IOrderRefundService service,
        ConfirmCashRefundDto dto,
        int staffId,
        int storeId,
        IReadOnlyList<string> roles)
    {
        return service.ConfirmCashRefundAsync(dto, Actor(staffId, storeId, roles));
    }

    private static AdminActorContext Actor(
        int staffId,
        int storeId,
        IReadOnlyList<string> roles) =>
        new()
        {
            StaffId = staffId,
            StoreId = storeId,
            RoleNames = roles
        };
}
