using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.AppLauncher;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.AppLauncher;

namespace CafeChain.Application.Services.AppLauncher;

public sealed class AppLauncherService : IAppLauncherService
{
    private readonly IAdminPermissionService _permissionService;

    public AppLauncherService(IAdminPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public async Task<AppLauncherVM> GetAppsAsync(
        int accountId,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var definitions = new[]
        {
            new AppDefinition(AppCode.AdminDashboard, "Admin Dashboard", "Theo dõi vận hành, doanh thu và các chỉ số quản trị.", "bi-speedometer2", "/Admin/Dashboard", 10, PermissionConstants.AppAdminDashboard, false),
            new AppDefinition(AppCode.StaffHub, "StaffHub", "Chấm công, theo dõi ca và tác vụ hằng ngày của nhân viên.", "bi-people", "/StaffHub", 20, PermissionConstants.AppStaffHub, false),
            new AppDefinition(AppCode.Pos, "POS", "Khởi chạy giao diện bán hàng mới.", "bi-receipt-cutoff", "#", 30, PermissionConstants.AppPos, true)
        };

        var cards = new List<AppLauncherCardDTO>();
        foreach (var app in definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decision = await _permissionService.HasPermissionAsync(accountId, app.PermissionCode);
            var allowed = decision.IsSuccess && decision.Data?.Allowed == true;
            if (!allowed)
                continue;

            cards.Add(new AppLauncherCardDTO
            {
                Code = app.Code,
                Title = app.Title,
                Description = app.Description,
                Icon = app.Icon,
                Route = app.Route,
                DisplayOrder = app.DisplayOrder,
                IsAvailable = true,
                RequiresLaunch = app.RequiresLaunch
            });
        }

        return new AppLauncherVM
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Nhân viên" : displayName.Trim(),
            Apps = cards.OrderBy(x => x.DisplayOrder).ToList()
        };
    }

    private sealed record AppDefinition(
        AppCode Code,
        string Title,
        string Description,
        string Icon,
        string Route,
        int DisplayOrder,
        string PermissionCode,
        bool RequiresLaunch);
}
