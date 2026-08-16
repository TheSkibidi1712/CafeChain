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
            new AppDefinition(
                AppCode.OperationalIce,
                "Vận hành đá",
                "Theo dõi cấp đá, bổ sung, bàn giao và chốt ca được phân công.",
                "bi-snow",
                "/Admin/AdminOperationalIce",
                15,
                PermissionConstants.OperationalIceView,
                false),
            new AppDefinition(
                AppCode.ProductionOrders,
                "Lệnh sản xuất",
                "Theo dõi và thực hiện công đoạn sản xuất được phân công tại cửa hàng.",
                "bi-box-seam",
                "/Admin/AdminProductionOrder",
                16,
                PermissionConstants.ProductionOrderView,
                false),
            new AppDefinition(
                AppCode.SystemAdministration,
                "Quản trị hệ thống",
                "Quản lý phân quyền, chẩn đoán và các công cụ kỹ thuật của hệ thống.",
                "bi-shield-lock",
                "/Admin/AdminPermission",
                17,
                PermissionConstants.SystemPermissionManage,
                false),
            new AppDefinition(AppCode.AdminDashboard, "Admin Dashboard", "Theo dõi vận hành, doanh thu và các chỉ số quản trị.", "bi-speedometer2", "/Admin/Dashboard", 10, PermissionConstants.AppAdminDashboard, false),
            new AppDefinition(AppCode.StaffHub, "StaffHub", "Xem lịch dự kiến và truy cập các tác vụ hằng ngày của nhân viên.", "bi-people", "/StaffHub", 20, PermissionConstants.AppStaffHub, false),
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

        if (cards.Any(x => x.Code == AppCode.AdminDashboard))
        {
            cards.RemoveAll(x => x.Code == AppCode.OperationalIce);
            cards.RemoveAll(x => x.Code == AppCode.ProductionOrders);
            cards.RemoveAll(x => x.Code == AppCode.SystemAdministration);
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
