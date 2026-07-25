using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CafeChain.Hubs;

[Authorize(AuthenticationSchemes =
    CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
public sealed class InventoryNotificationHub : Hub
{
    public const string PermissionCode = PermissionConstants.NotificationView;

    private readonly IInventoryNotificationAudienceResolver _audience;

    public InventoryNotificationHub(IInventoryNotificationAudienceResolver audience) =>
        _audience = audience;

    public override async Task OnConnectedAsync()
    {
        var staffId = Context.User?.GetStaffIdOrDefault() ?? 0;
        if (staffId <= 0)
        {
            Context.Abort();
            return;
        }

        var storeIds = await _audience.ResolveStoreIdsAsync(
            staffId,
            Context.ConnectionAborted);
        if (storeIds.Count == 0)
        {
            Context.Abort();
            return;
        }

        foreach (var storeId in storeIds)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                InventoryNotificationGroups.ForStore(storeId),
                Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }
}

public static class InventoryNotificationGroups
{
    public static string ForStore(int storeId) =>
        $"store:{storeId}:permission:{PermissionConstants.NotificationView}";
}
