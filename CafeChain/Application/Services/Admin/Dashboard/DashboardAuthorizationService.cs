using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Security;

namespace CafeChain.Application.Services.Admin.Dashboard;

public sealed class DashboardAuthorizationService : IDashboardAuthorizationService
{
    private readonly IAdminPermissionService _permissions;
    private readonly IScopeAuthorizationService _scope;

    public DashboardAuthorizationService(
        IAdminPermissionService permissions,
        IScopeAuthorizationService scope)
    {
        _permissions = permissions;
        _scope = scope;
    }

    public async Task<DashboardAuthorizationDto> GetAccessAsync(
        AdminActorContext actor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (actor.AccountId <= 0 || actor.StaffId <= 0)
            throw new UnauthorizedAccessException("Dashboard yêu cầu account và staff context hợp lệ.");

        var effectiveResult = await _permissions.GetEffectivePermissionCodesAsync(actor.AccountId);
        if (!effectiveResult.IsSuccess || effectiveResult.Data == null)
            throw new UnauthorizedAccessException("Không xác định được quyền Dashboard.");
        var effective = effectiveResult.Data;
        if (!effective.Contains(PermissionConstants.AppAdminDashboard))
            throw new UnauthorizedAccessException("Bạn không có quyền mở Admin Dashboard.");

        var sections = Enum.GetValues<DashboardSection>()
            .Where(section => CanViewSection(section, effective))
            .ToArray();
        if (sections.Length == 0)
            throw new UnauthorizedAccessException("Tài khoản không có section Dashboard hợp lệ.");

        var widgets = Enum.GetValues<DashboardAnalyticsWidget>()
            .Where(widget => sections.Contains(DashboardWidgetCatalog.Get(widget).Section)
                && CanViewWidget(widget, effective))
            .ToArray();
        var stores = await _scope.GetAllowedStoresAsync(actor.StaffId);
        var storeIds = stores.Select(x => x.StoreId).Distinct().OrderBy(x => x).ToArray();
        if (storeIds.Length == 0)
            throw new UnauthorizedAccessException("Không có StaffScope hợp lệ cho Dashboard.");

        var capabilities = effective.Where(code =>
                code.StartsWith("Dashboard.", StringComparison.Ordinal)
                || code.StartsWith("OperationalAnomaly.", StringComparison.Ordinal)
                || code is "PurchaseOrder.Create" or "PurchaseOrder.Approve"
                || code == PermissionConstants.PurchaseAdviceSelectSupplier)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        return new DashboardAuthorizationDto
        {
            EffectivePermissions = effective,
            AllowedSections = sections,
            AllowedWidgets = widgets,
            AllowedCapabilities = capabilities,
            CanUseAi = effective.Contains(PermissionConstants.DashboardAiUse),
            Scope = new DashboardScopeDto
            {
                Type = storeIds.Length == 1 ? "STORE" : "MULTI_STORE",
                Label = storeIds.Length == 1 ? stores[0].Name : $"{storeIds.Length} cửa hàng được phân công",
                AllowedStoreIds = storeIds,
                CanSelectProvince = storeIds.Length > 1,
                CanSelectDistrict = storeIds.Length > 1,
                CanSelectStore = storeIds.Length > 1,
                CanAggregateMultipleStores = storeIds.Length > 1
            }
        };
    }

    public async Task<DashboardAuthorizationDto> AuthorizeSectionAsync(
        AdminActorContext actor, DashboardSection section,
        CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(actor, cancellationToken);
        if (!access.AllowedSections.Contains(section))
            throw new UnauthorizedAccessException($"Không có quyền xem section {section}.");
        return access;
    }

    public async Task<DashboardAuthorizationDto> AuthorizeWidgetsAsync(
        AdminActorContext actor, IReadOnlyCollection<DashboardAnalyticsWidget> widgets,
        CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(actor, cancellationToken);
        if (widgets.Any(widget => !access.AllowedWidgets.Contains(widget)))
            throw new UnauthorizedAccessException("Yêu cầu chứa widget không được phép truy cập.");
        return access;
    }

    private static bool CanViewSection(DashboardSection section, IReadOnlySet<string> p) => section switch
    {
        DashboardSection.Executive => Has(p, PermissionConstants.DashboardExecutiveView)
            && (Has(p, PermissionConstants.DashboardFinancialSummaryView) || Has(p, "Order.View")),
        DashboardSection.Operations => Has(p, PermissionConstants.DashboardOperationsView)
            && Has(p, PermissionConstants.PosWorkShiftView),
        DashboardSection.Inventory => Has(p, PermissionConstants.DashboardInventoryView)
            && Has(p, "Inventory.View"),
        DashboardSection.Procurement => Has(p, PermissionConstants.DashboardProcurementView)
            && (Has(p, "PurchaseOrder.View") || Has(p, "PurchaseAdvice.View")),
        DashboardSection.Product => Has(p, PermissionConstants.DashboardProductView)
            && Has(p, PermissionConstants.DrinkView),
        DashboardSection.Workforce => Has(p, PermissionConstants.DashboardWorkforceView)
            && Has(p, PermissionConstants.ShiftView),
        _ => false
    };

    private static bool CanViewWidget(DashboardAnalyticsWidget widget, IReadOnlySet<string> p) => widget switch
    {
        DashboardAnalyticsWidget.NetSalesTrend or DashboardAnalyticsWidget.StoreRanking
            or DashboardAnalyticsWidget.PaymentMethodMix => Has(p, PermissionConstants.DashboardFinancialSummaryView),
        DashboardAnalyticsWidget.OrderHeatmap or DashboardAnalyticsWidget.OrderStatusSummary
            or DashboardAnalyticsWidget.HourlyOrders => Has(p, "Order.View"),
        DashboardAnalyticsWidget.WorkShiftTopDiscrepancies => Has(p, PermissionConstants.PosWorkShiftView)
            && Has(p, PermissionConstants.StaffView),
        DashboardAnalyticsWidget.WorkShiftCashDiscrepancy or DashboardAnalyticsWidget.WorkShiftSales
            or DashboardAnalyticsWidget.WorkShiftPaymentMix or DashboardAnalyticsWidget.OfflineReconciliationExceptions
            or DashboardAnalyticsWidget.WorkShiftKpis => Has(p, PermissionConstants.PosWorkShiftView),
        DashboardAnalyticsWidget.InventoryThresholdRisk => Has(p, "InventoryThreshold.View"),
        DashboardAnalyticsWidget.InventoryReorderSuggestions => Has(p, PermissionConstants.ReorderSuggestionView),
        DashboardAnalyticsWidget.InventoryShortageRisk or DashboardAnalyticsWidget.InventoryMovementByType
            or DashboardAnalyticsWidget.InventoryWasteByStoreIngredient or DashboardAnalyticsWidget.InventoryFifoLayerAge
            or DashboardAnalyticsWidget.IngredientConsumptionTrend => Has(p, "Inventory.View"),
        DashboardAnalyticsWidget.SupplierQuality or DashboardAnalyticsWidget.SupplierIssueMix => Has(p, "SupplierQuality.View"),
        DashboardAnalyticsWidget.PurchasePriceTrend or DashboardAnalyticsWidget.ProcurementSpendBreakdown => Has(p, "Receipt.ViewCost"),
        DashboardAnalyticsWidget.PurchaseOrderPipeline or DashboardAnalyticsWidget.OverduePurchaseOrders => Has(p, "PurchaseOrder.View"),
        DashboardAnalyticsWidget.VolumeMarginMatrix or DashboardAnalyticsWidget.SizeMargin
            or DashboardAnalyticsWidget.HighConsumptionLowEfficiency or DashboardAnalyticsWidget.CategoryPerformance
            or DashboardAnalyticsWidget.ProductPeriodPerformance or DashboardAnalyticsWidget.LowMarginProducts
            => Has(p, "Profitability.View"),
        DashboardAnalyticsWidget.TopProducts or DashboardAnalyticsWidget.TopToppings
            or DashboardAnalyticsWidget.BomHealth or DashboardAnalyticsWidget.LowVolumeProducts => Has(p, PermissionConstants.DrinkView),
        DashboardAnalyticsWidget.WorkforceStaffPerformance => Has(p, PermissionConstants.ShiftView)
            && Has(p, PermissionConstants.StaffView) && Has(p, PermissionConstants.PosWorkShiftView),
        DashboardAnalyticsWidget.WorkforceShiftStatus or DashboardAnalyticsWidget.WorkforceHourlyDemand => Has(p, PermissionConstants.ShiftView),
        DashboardAnalyticsWidget.OperationalAlerts => Has(p, "Inventory.View") || Has(p, PermissionConstants.PosWorkShiftView)
            || Has(p, "PurchaseOrder.View") || Has(p, "SupplierQuality.View"),
        _ => false
    };

    private static bool Has(IReadOnlySet<string> permissions, string code) => permissions.Contains(code);
}
