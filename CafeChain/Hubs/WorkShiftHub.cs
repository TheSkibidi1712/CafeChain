using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Data;
using CafeChain.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Hubs;

[Authorize(AuthenticationSchemes =
    CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
public sealed class WorkShiftHub : Hub
{
    private readonly IScopeAuthorizationService _scope;
    private readonly IAdminPermissionService _permissions;
    private readonly AppDbContext _db;

    public WorkShiftHub(
        IScopeAuthorizationService scope,
        IAdminPermissionService permissions,
        AppDbContext db)
    {
        _scope = scope;
        _permissions = permissions;
        _db = db;
    }

    public override async Task OnConnectedAsync()
    {
        var staffId = Context.User?.GetStaffIdOrDefault() ?? 0;
        var accountClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (staffId <= 0 || !int.TryParse(accountClaim, out var accountId) || accountId <= 0)
        {
            Context.Abort();
            return;
        }

        foreach (var store in await _scope.GetAllowedStoresAsync(staffId))
        {
            var view = await _permissions.HasPermissionAsync(accountId, PermissionConstants.PosWorkShiftView, store.StoreId);
            if (view.IsSuccess && view.Data?.Allowed == true)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    WorkShiftGroups.ForStore(store.StoreId),
                    Context.ConnectionAborted);
            }
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            WorkShiftGroups.ForStaff(staffId),
            Context.ConnectionAborted);

        if (Guid.TryParse(Context.User?.FindFirstValue("PosSessionId"), out var sessionId))
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                WorkShiftGroups.ForSession(sessionId),
                Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinTerminal(string terminalId)
    {
        terminalId = terminalId?.Trim() ?? string.Empty;
        if (terminalId.Length == 0 || terminalId.Length > 100) return;

        var staffId = Context.User?.GetStaffIdOrDefault() ?? 0;
        var accountClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (staffId <= 0 || !int.TryParse(accountClaim, out var accountId) || accountId <= 0) return;

        var terminal = await _db.PosTerminals.AsNoTracking()
            .Where(x => x.TerminalId == terminalId && x.Active && x.Store.Active)
            .Select(x => new { x.TerminalId, x.StoreId })
            .FirstOrDefaultAsync(Context.ConnectionAborted);
        if (terminal == null || !await _scope.CanAccessStoreAsync(staffId, terminal.StoreId)) return;

        var view = await _permissions.HasPermissionAsync(
            accountId,
            PermissionConstants.PosWorkShiftView,
            terminal.StoreId);
        if (!view.IsSuccess || view.Data?.Allowed != true) return;

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            WorkShiftGroups.ForTerminal(terminal.TerminalId),
            Context.ConnectionAborted);
    }
}

public static class WorkShiftGroups
{
    public static string ForStore(int storeId) => $"store:{storeId}:permission:{PermissionConstants.PosWorkShiftView}";
    public static string ForStaff(int staffId) => $"staff:{staffId}:workshift";
    public static string ForTerminal(string terminalId) => $"terminal:{terminalId}:workshift";
    public static string ForSession(Guid sessionId) => $"pos-session:{sessionId:N}";
}
