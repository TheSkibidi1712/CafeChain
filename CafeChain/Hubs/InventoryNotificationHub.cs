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

        // Exact identity group. Operational OTP is never published to store groups.
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            InventoryNotificationGroups.ForStaff(staffId),
            Context.ConnectionAborted);

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

    public static string ForStaff(int staffId) =>
        $"staff:{staffId}:operational-notifications";
}
