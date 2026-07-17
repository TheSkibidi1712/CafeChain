using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Infrastrusture.Interfaces.Admin.Dashboard;
using CafeChain.ViewModels.Admin.Dashboard;

namespace CafeChain.Application.Services.Admin.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private sealed record ScopeResolution(
        DashboardFilterDto Filter,
        IReadOnlyList<int> StoreIds,
        IReadOnlyList<DashboardStoreOptionDto> StoreOptions);

    private readonly IDashboardRepository _repository;
    private readonly IScopeAuthorizationService _scopeAuthorization;

    public DashboardService(
        IDashboardRepository repository,
        IScopeAuthorizationService scopeAuthorization)
    {
        _repository = repository;
        _scopeAuthorization = scopeAuthorization;
    }

    public async Task<DashboardPageDto> GetPageAsync(
        AdminActorContext actor,
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveScopeAsync(actor, filter, cancellationToken);
        return new DashboardPageDto
        {
            Filter = scope.Filter,
            Stores = scope.StoreOptions,
            RoleName = actor.RoleNames.FirstOrDefault() ?? string.Empty
        };
    }

    public async Task<object> GetSectionAsync(
        AdminActorContext actor,
        DashboardSection section,
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveScopeAsync(actor, filter, cancellationToken);
        object data = section switch
        {
            DashboardSection.Executive => await _repository.GetExecutiveAsync(scope.Filter, scope.StoreIds, cancellationToken),
            DashboardSection.Operations => await _repository.GetOperationsAsync(scope.Filter, scope.StoreIds, cancellationToken),
            DashboardSection.Inventory => await _repository.GetInventoryAsync(scope.Filter, scope.StoreIds, cancellationToken),
            DashboardSection.Procurement => await _repository.GetProcurementAsync(scope.Filter, scope.StoreIds, cancellationToken),
            DashboardSection.Product => await _repository.GetProductAsync(scope.Filter, scope.StoreIds, cancellationToken),
            DashboardSection.Workforce => await _repository.GetWorkforceAsync(scope.Filter, scope.StoreIds, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };

        return CreateSectionResponse(section, scope, data);
    }

    public async Task<DashboardVM> GetDashboardAsync(DashboardRequest request)
    {
        if (!request.StaffId.HasValue) throw new UnauthorizedAccessException("Staff context is required.");
        var actor = new AdminActorContext { StaffId = request.StaffId.Value };
        var filter = new DashboardFilterDto
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            StoreId = request.StoreId,
            ProvinceId = request.ProvinceId,
            DistrictId = request.DistrictId,
            Granularity = "Day",
            Top = 10
        };
        var scope = await ResolveScopeAsync(actor, filter, CancellationToken.None);
        var executive = await _repository.GetExecutiveAsync(scope.Filter, scope.StoreIds);
        var product = await _repository.GetProductAsync(scope.Filter, scope.StoreIds);
        var workforce = await _repository.GetWorkforceAsync(scope.Filter, scope.StoreIds);
        var inventory = await _repository.GetInventoryAsync(scope.Filter, scope.StoreIds);

        var trend = executive.NetSalesTrend.Data;
        var today = DateTime.Today;
        return new DashboardVM
        {
            Revenue = trend.Select(x => new RevenueDto { Date=x.BucketDate, TotalOrders=ToInt(x.TotalOrders), Revenue=x.NetSales }).ToList(),
            RevenueByStore = executive.StoreRanking.Data.Select(x => new RevenueByStoreDto { StoreId=x.StoreId, Name=x.StoreName, TotalOrders=ToInt(x.TotalOrders), Revenue=x.NetSales }).ToList(),
            TopDrinks = product.TopProducts.Data.Select(x => new TopDrinkDto { DrinkId=x.DrinkId, DrinkName=x.DrinkName, TotalSold=x.TotalSold, Revenue=x.ProductRevenue }).ToList(),
            TopToppings = product.TopToppings.Data.Select(x => new TopToppingDto { ToppingId=x.ToppingId, ToppingName=x.ToppingName, TotalUsed=x.TotalUsed, Revenue=x.Revenue }).ToList(),
            PaymentMethods = executive.PaymentMethodMix.Data.Select(x => new PaymentMethodDto { Name=x.PaymentMethodName, TotalTransactions=ToInt(x.TotalTransactions), Revenue=x.Amount }).ToList(),
            StaffPerformance = workforce.StaffPerformance.Data.Select(x => new StaffPerformanceDto { StaffId=x.StaffId, FullName=x.FullName, TotalOrders=ToInt(x.TotalOrders), Revenue=x.NetSales }).ToList(),
            Inventory = inventory.ThresholdRisk.Data.Select(x => new InventoryDto { IngredientId=x.IngredientId, Name=x.IngredientName, CurrentStock=x.AvailableQty }).ToList(),
            Waste = inventory.Waste.Data.Select(x => new WasteDto { StoreId=x.StoreId, StoreName=x.StoreName, IngredientId=x.IngredientId, IngredientName=x.IngredientName, TotalWasteQty=x.WasteQuantity, TotalWasteValue=x.WasteValue }).ToList(),
            CashFlows = [],
            Summary = new DashboardSummaryDto
            {
                TotalOrders = ToInt(trend.Sum(x => x.TotalOrders)),
                Revenue = trend.Sum(x => x.NetSales),
                TotalCustomers = 0,
                TodayOrders = ToInt(trend.Where(x => x.BucketDate.Date == today).Sum(x => x.TotalOrders))
            },
            Stores = scope.StoreOptions.Select(x => new StoreDropdownDto { StoreId=x.StoreId, StoreName=x.StoreName, ProvinceId=x.ProvinceId, ProvinceName=x.ProvinceName, DistrictId=x.DistrictId, DistrictName=x.DistrictName }).ToList(),
            Provinces = scope.StoreOptions.Where(x => x.ProvinceId.HasValue).Select(x => x.ProvinceId!.Value).Distinct().ToList(),
            Districts = scope.StoreOptions.Where(x => x.DistrictId.HasValue).Select(x => x.DistrictId!.Value).Distinct().ToList()
        };
    }

    public async Task<DashboardAnalyticsResponse> GetAnalyticsAsync(
        DashboardAnalyticsWidget widget,
        DashboardAnalyticsFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (!filter.StaffId.HasValue) throw new UnauthorizedAccessException("Staff context is required.");
        var section = SectionFor(widget);
        var actor = new AdminActorContext { StaffId = filter.StaffId.Value };
        var normalized = CopyFilter(filter);
        var response = await GetSectionAsync(actor, section, normalized, cancellationToken);
        var extracted = ExtractWidget(widget, response);
        var storeIds = response switch
        {
            DashboardSectionResponse<ExecutiveDashboardData> x => x.StoreIds,
            DashboardSectionResponse<OperationsDashboardData> x => x.StoreIds,
            DashboardSectionResponse<InventoryDashboardData> x => x.StoreIds,
            DashboardSectionResponse<ProcurementDashboardData> x => x.StoreIds,
            DashboardSectionResponse<ProductDashboardData> x => x.StoreIds,
            DashboardSectionResponse<WorkforceDashboardData> x => x.StoreIds,
            _ => []
        };
        return new DashboardAnalyticsResponse
        {
            Widget = widget,
            FromDate = normalized.FromDate,
            ToExclusive = normalized.ToDate.AddDays(1),
            Granularity = normalized.Granularity,
            StoreIds = storeIds,
            Rows = extracted.Data,
            DataStatus = extracted.Status,
            Warnings = extracted.Warnings
        };
    }

    private async Task<ScopeResolution> ResolveScopeAsync(
        AdminActorContext actor,
        DashboardFilterDto filter,
        CancellationToken cancellationToken)
    {
        if (actor.StaffId <= 0) throw new UnauthorizedAccessException("Staff context is required.");
        var normalized = NormalizeFilter(filter);
        var allowed = await _scopeAuthorization.GetAllowedStoresAsync(actor.StaffId);
        var allowedIds = allowed.Select(x => x.StoreId).Distinct().ToArray();
        if (allowedIds.Length == 0) throw new UnauthorizedAccessException("Không có cửa hàng trong phạm vi được cấp.");
        var options = await _repository.GetStoreOptionsAsync(allowedIds, cancellationToken);

        IEnumerable<DashboardStoreOptionDto> selected = options;
        if (normalized.ProvinceId.HasValue) selected = selected.Where(x => x.ProvinceId == normalized.ProvinceId);
        if (normalized.DistrictId.HasValue) selected = selected.Where(x => x.DistrictId == normalized.DistrictId);
        if (normalized.StoreId.HasValue)
        {
            if (!selected.Any(x => x.StoreId == normalized.StoreId.Value))
                throw new UnauthorizedAccessException("Cửa hàng nằm ngoài phạm vi được cấp.");
            selected = selected.Where(x => x.StoreId == normalized.StoreId.Value);
        }

        var ids = selected.Select(x => x.StoreId).Distinct().OrderBy(x => x).ToArray();
        if (ids.Length == 0) throw new UnauthorizedAccessException("Bộ lọc không còn cửa hàng hợp lệ.");
        return new ScopeResolution(normalized, ids, options);
    }

    private static DashboardFilterDto NormalizeFilter(DashboardFilterDto filter)
    {
        if (filter.FromDate.Date > filter.ToDate.Date)
            throw new ArgumentException("FromDate không được lớn hơn ToDate.", nameof(filter));
        if ((filter.ToDate.Date - filter.FromDate.Date).TotalDays > 3660)
            throw new ArgumentException("Khoảng dữ liệu dashboard tối đa 10 năm.", nameof(filter));
        return new DashboardFilterDto
        {
            FromDate = filter.FromDate.Date,
            ToDate = filter.ToDate.Date,
            StoreId = filter.StoreId,
            ProvinceId = filter.ProvinceId,
            DistrictId = filter.DistrictId,
            Granularity = NormalizeGranularity(filter.Granularity, filter.FromDate, filter.ToDate),
            Top = Math.Clamp(filter.Top, 1, 100)
        };
    }

    private static DashboardFilterDto CopyFilter(DashboardFilterDto filter) => new()
    {
        FromDate=filter.FromDate,ToDate=filter.ToDate,StoreId=filter.StoreId,
        ProvinceId=filter.ProvinceId,DistrictId=filter.DistrictId,
        Granularity=filter.Granularity,Top=filter.Top
    };

    private static object CreateSectionResponse(DashboardSection section, ScopeResolution scope, object data) => data switch
    {
        ExecutiveDashboardData value => Response(section, scope, value),
        OperationsDashboardData value => Response(section, scope, value),
        InventoryDashboardData value => Response(section, scope, value),
        ProcurementDashboardData value => Response(section, scope, value),
        ProductDashboardData value => Response(section, scope, value),
        WorkforceDashboardData value => Response(section, scope, value),
        _ => throw new InvalidOperationException("Unsupported dashboard section data.")
    };

    private static DashboardSectionResponse<T> Response<T>(DashboardSection section, ScopeResolution scope, T data) => new()
    {
        Section=section,FromDate=scope.Filter.FromDate,ToExclusive=scope.Filter.ToDate.AddDays(1),
        Granularity=scope.Filter.Granularity,StoreIds=scope.StoreIds,Data=data
    };

    private static DashboardSection SectionFor(DashboardAnalyticsWidget widget)
    {
        var value = (int)widget;
        if (value <= (int)DashboardAnalyticsWidget.OperationalAlerts)
            return DashboardSection.Executive;
        if (value <= (int)DashboardAnalyticsWidget.WorkShiftKpis)
            return DashboardSection.Operations;
        if (value <= (int)DashboardAnalyticsWidget.InventoryFifoLayerAge)
            return DashboardSection.Inventory;
        if (value <= (int)DashboardAnalyticsWidget.SupplierIssueMix)
            return DashboardSection.Procurement;
        if (value <= (int)DashboardAnalyticsWidget.HighConsumptionLowEfficiency)
            return DashboardSection.Product;
        return DashboardSection.Workforce;
    }

    private static (object Data, string Status, List<string> Warnings) ExtractWidget(DashboardAnalyticsWidget widget, object response) => (widget, response) switch
    {
        (DashboardAnalyticsWidget.NetSalesTrend, DashboardSectionResponse<ExecutiveDashboardData> x) => Pack(x.Data.NetSalesTrend),
        (DashboardAnalyticsWidget.StoreRanking, DashboardSectionResponse<ExecutiveDashboardData> x) => Pack(x.Data.StoreRanking),
        (DashboardAnalyticsWidget.PaymentMethodMix, DashboardSectionResponse<ExecutiveDashboardData> x) => Pack(x.Data.PaymentMethodMix),
        (DashboardAnalyticsWidget.OrderHeatmap, DashboardSectionResponse<ExecutiveDashboardData> x) => Pack(x.Data.OrderHeatmap),
        (DashboardAnalyticsWidget.OperationalAlerts, DashboardSectionResponse<ExecutiveDashboardData> x) => Pack(x.Data.OperationalAlerts),
        (DashboardAnalyticsWidget.WorkShiftCashDiscrepancy, DashboardSectionResponse<OperationsDashboardData> x) => Pack(x.Data.CashDiscrepancy),
        (DashboardAnalyticsWidget.WorkShiftSales, DashboardSectionResponse<OperationsDashboardData> x) => Pack(x.Data.ShiftSales),
        (DashboardAnalyticsWidget.WorkShiftPaymentMix, DashboardSectionResponse<OperationsDashboardData> x) => Pack(x.Data.PaymentMix),
        (DashboardAnalyticsWidget.OfflineReconciliationExceptions, DashboardSectionResponse<OperationsDashboardData> x) => Pack(x.Data.OfflineReconciliation),
        (DashboardAnalyticsWidget.HourlyOrders, DashboardSectionResponse<OperationsDashboardData> x) => Pack(x.Data.HourlyOrders),
        (DashboardAnalyticsWidget.WorkShiftTopDiscrepancies, DashboardSectionResponse<OperationsDashboardData> x) => Pack(x.Data.TopDiscrepancies),
        (DashboardAnalyticsWidget.WorkShiftKpis, DashboardSectionResponse<OperationsDashboardData> x) => Pack(x.Data.Kpis),
        (DashboardAnalyticsWidget.InventoryShortageRisk, DashboardSectionResponse<InventoryDashboardData> x) => Pack(x.Data.ShortageRisk),
        (DashboardAnalyticsWidget.InventoryMovementByType, DashboardSectionResponse<InventoryDashboardData> x) => Pack(x.Data.Movement),
        (DashboardAnalyticsWidget.InventoryThresholdRisk, DashboardSectionResponse<InventoryDashboardData> x) => Pack(x.Data.ThresholdRisk),
        (DashboardAnalyticsWidget.InventoryReorderSuggestions, DashboardSectionResponse<InventoryDashboardData> x) => Pack(x.Data.ReorderSuggestions),
        (DashboardAnalyticsWidget.InventoryWasteByStoreIngredient, DashboardSectionResponse<InventoryDashboardData> x) => Pack(x.Data.Waste),
        (DashboardAnalyticsWidget.InventoryFifoLayerAge, DashboardSectionResponse<InventoryDashboardData> x) => Pack(x.Data.FifoAge),
        (DashboardAnalyticsWidget.PurchaseOrderPipeline, DashboardSectionResponse<ProcurementDashboardData> x) => Pack(x.Data.PurchaseOrderPipeline),
        (DashboardAnalyticsWidget.OverduePurchaseOrders, DashboardSectionResponse<ProcurementDashboardData> x) => Pack(x.Data.OverduePurchaseOrders),
        (DashboardAnalyticsWidget.SupplierQuality, DashboardSectionResponse<ProcurementDashboardData> x) => Pack(x.Data.SupplierQuality),
        (DashboardAnalyticsWidget.PurchasePriceTrend, DashboardSectionResponse<ProcurementDashboardData> x) => Pack(x.Data.PurchasePriceTrend),
        (DashboardAnalyticsWidget.ProcurementSpendBreakdown, DashboardSectionResponse<ProcurementDashboardData> x) => Pack(x.Data.SpendBreakdown),
        (DashboardAnalyticsWidget.SupplierIssueMix, DashboardSectionResponse<ProcurementDashboardData> x) => Pack(x.Data.SupplierIssueMix),
        (DashboardAnalyticsWidget.TopProducts, DashboardSectionResponse<ProductDashboardData> x) => Pack(x.Data.TopProducts),
        (DashboardAnalyticsWidget.VolumeMarginMatrix, DashboardSectionResponse<ProductDashboardData> x) => Pack(x.Data.VolumeMargin),
        (DashboardAnalyticsWidget.SizeMargin, DashboardSectionResponse<ProductDashboardData> x) => Pack(x.Data.SizeMargin),
        (DashboardAnalyticsWidget.TopToppings, DashboardSectionResponse<ProductDashboardData> x) => Pack(x.Data.TopToppings),
        (DashboardAnalyticsWidget.BomHealth, DashboardSectionResponse<ProductDashboardData> x) => Pack(x.Data.BomHealth),
        (DashboardAnalyticsWidget.HighConsumptionLowEfficiency, DashboardSectionResponse<ProductDashboardData> x) => Pack(x.Data.LowEfficiency),
        (DashboardAnalyticsWidget.WorkforceShiftStatus, DashboardSectionResponse<WorkforceDashboardData> x) => Pack(x.Data.ShiftStatus),
        (DashboardAnalyticsWidget.WorkforceHourlyDemand, DashboardSectionResponse<WorkforceDashboardData> x) => Pack(x.Data.HourlyDemand),
        (DashboardAnalyticsWidget.WorkforceStaffPerformance, DashboardSectionResponse<WorkforceDashboardData> x) => Pack(x.Data.StaffPerformance),
        _ => throw new InvalidOperationException("Widget response mismatch.")
    };

    private static (object Data, string Status, List<string> Warnings) Pack<T>(DashboardWidgetResult<T> value) =>
        (value.Data, value.Status, value.Warnings);

    private static int ToInt(long value) => value > int.MaxValue ? int.MaxValue : Convert.ToInt32(value);
    private static string NormalizeGranularity(string? requested, DateTime from, DateTime to)
    {
        var normalized = requested?.Trim().ToUpperInvariant();
        if (normalized is "HOUR" or "DAY" or "WEEK" or "MONTH")
            return char.ToUpperInvariant(normalized[0]) + normalized[1..].ToLowerInvariant();
        var days = (to.Date - from.Date).TotalDays;
        return days <= 2 ? "Hour" : days <= 62 ? "Day" : days <= 730 ? "Week" : "Month";
    }
}
