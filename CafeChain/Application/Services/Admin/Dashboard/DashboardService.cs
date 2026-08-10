using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Infrastrusture.Interfaces.Admin.Dashboard;
using CafeChain.ViewModels.Admin.Dashboard;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace CafeChain.Application.Services.Admin.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private sealed record ScopeResolution(
        DashboardFilterDto Filter,
        IReadOnlyList<int> StoreIds,
        IReadOnlyList<DashboardStoreOptionDto> StoreOptions,
        DateTimeOffset? GeneratedAt = null);

    private sealed record CachedContext(
        DashboardAnalysisContextDto Context,
        DashboardFilterDto Filter,
        IReadOnlyList<int> StoreIds,
        IReadOnlyList<DashboardStoreOptionDto> StoreOptions);

    private readonly IDashboardRepository _repository;
    private readonly IScopeAuthorizationService _scopeAuthorization;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;
    private readonly IReorderSuggestionService? _reorderSuggestions;
    private readonly IReorderSuggestionAuthorizationService? _reorderAuthorization;
    private readonly IDashboardAuthorizationService? _authorization;

    public DashboardService(
        IDashboardRepository repository,
        IScopeAuthorizationService scopeAuthorization,
        IMemoryCache? cache = null,
        TimeProvider? clock = null,
        IReorderSuggestionService? reorderSuggestions = null,
        IReorderSuggestionAuthorizationService? reorderAuthorization = null,
        IDashboardAuthorizationService? authorization = null)
    {
        _repository = repository;
        _scopeAuthorization = scopeAuthorization;
        _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
        _clock = clock ?? TimeProvider.System;
        _reorderSuggestions = reorderSuggestions;
        _reorderAuthorization = reorderAuthorization;
        _authorization = authorization;
    }

    public async Task<DashboardPageDto> GetPageAsync(
        AdminActorContext actor,
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(actor, cancellationToken);
        var scope = await ResolveScopeAsync(actor, filter, cancellationToken);
        var context = CreateContext(scope.Filter, scope.StoreIds, scope.StoreOptions, generatedAt: scope.GeneratedAt);
        context.Widgets = context.Widgets.Where(x => access.AllowedWidgets.Contains(x.Widget)).ToArray();
        StoreContext(actor, context, scope);
        return new DashboardPageDto
        {
            Filter = scope.Filter,
            Stores = scope.StoreOptions,
            RoleName = actor.RoleNames.FirstOrDefault() ?? string.Empty,
            AnalysisContext = context,
            AllowedSections = access.AllowedSections,
            AllowedWidgets = access.AllowedWidgets,
            AllowedCapabilities = access.AllowedCapabilities,
            CanUseAi = access.CanUseAi,
            Scope = access.Scope
        };
    }

    public async Task<DashboardAnalysisContextDto> CreateContextAsync(
        AdminActorContext actor,
        DashboardContextRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(actor, cancellationToken);
        var filter = CopyFilter(request);
        if (request.Preset.HasValue)
            ApplyPreset(filter, request.Preset.Value, _clock.GetLocalNow());
        var scope = await ResolveScopeAsync(actor, filter, cancellationToken);
        var context = CreateContext(scope.Filter, scope.StoreIds, scope.StoreOptions, request.Preset, scope.GeneratedAt);
        context.Widgets = context.Widgets.Where(x => access.AllowedWidgets.Contains(x.Widget)).ToArray();
        StoreContext(actor, context, scope);
        return context;
    }

    public async Task<DashboardAnalysisContextDto> GetContextAsync(
        AdminActorContext actor,
        Guid contextId,
        CancellationToken cancellationToken = default)
    {
        await RequireAccessAsync(actor, cancellationToken);
        if (TryGetContext(actor, contextId, out var cached))
            return cached.Context;
        throw new KeyNotFoundException("Dashboard context đã hết hạn. Vui lòng tải lại dữ liệu.");
    }

    public async Task<object> GetSectionAsync(
        AdminActorContext actor,
        DashboardSection section,
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default,
        Guid? contextId = null)
    {
        var authorization = _authorization
            ?? throw new UnauthorizedAccessException("Dashboard authorization service is required.");
        var access = await authorization.AuthorizeSectionAsync(actor, section, cancellationToken);
        ScopeResolution scope;
        if (contextId.HasValue)
        {
            if (!TryGetContext(actor, contextId.Value, out var cached))
                throw new KeyNotFoundException("Dashboard context đã hết hạn. Vui lòng tải lại dữ liệu.");
            scope = new ScopeResolution(cached.Filter, cached.StoreIds, cached.StoreOptions, cached.Context.GeneratedAt);
        }
        else
            scope = await ResolveScopeAsync(actor, filter, cancellationToken);
        filter = scope.Filter;
        var data = await LoadSectionAsync(actor, section, scope, cancellationToken);
        FilterUnauthorizedWidgets(section, data, access.AllowedWidgets);
        return CreateSectionResponse(section, scope, data, contextId);
    }

    private async Task<object> LoadSectionAsync(
        AdminActorContext actor,
        DashboardSection section,
        ScopeResolution scope,
        CancellationToken cancellationToken)
    {
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
        if (section == DashboardSection.Inventory
            && data is InventoryDashboardData inventory)
        {
            inventory.ReorderSuggestions = await BuildReorderWidgetAsync(
                actor,
                scope.StoreIds,
                inventory.ReorderSuggestions,
                cancellationToken);
        }
        return data;
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
            WardId = request.WardId,
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
                TodayOrders = ToInt(trend.Where(x => x.BucketDate.Date == today).Sum(x => x.TotalOrders))
            },
            Stores = scope.StoreOptions.Select(x => new StoreDropdownDto { StoreId=x.StoreId, StoreName=x.StoreName, ProvinceId=x.ProvinceId, ProvinceName=x.ProvinceName, WardId=x.WardId, WardName=x.WardName }).ToList(),
            Provinces = scope.StoreOptions.Where(x => x.ProvinceId.HasValue).Select(x => x.ProvinceId!.Value).Distinct().ToList(),
            Wards = scope.StoreOptions.Where(x => x.WardId.HasValue).Select(x => x.WardId!.Value).Distinct().ToList()
        };
    }

    public async Task<DashboardAnalyticsResponse> GetAnalyticsAsync(
        AdminActorContext actor,
        DashboardAnalyticsWidget widget,
        DashboardAnalyticsFilter filter,
        CancellationToken cancellationToken = default)
    {
        var batch = await GetAnalyticsBatchAsync(actor, [widget], filter, cancellationToken: cancellationToken);
        return batch.Widgets[widget];
    }

    public async Task<DashboardAnalyticsBatchResponse> GetAnalyticsBatchAsync(
        AdminActorContext actor,
        IReadOnlyCollection<DashboardAnalyticsWidget> widgets,
        DashboardAnalyticsFilter filter,
        string period = "Current",
        CancellationToken cancellationToken = default)
    {
        if (widgets.Count == 0)
            return new DashboardAnalyticsBatchResponse();

        var authorization = _authorization
            ?? throw new UnauthorizedAccessException("Dashboard authorization service is required.");
        await authorization.AuthorizeWidgetsAsync(actor, widgets, cancellationToken);

        var normalized = CopyFilter(filter);
        var scope = await ResolveScopeAsync(actor, normalized, cancellationToken);
        var results = new Dictionary<DashboardAnalyticsWidget, DashboardAnalyticsResponse>();
        var telemetry = new List<DashboardSectionTelemetryDto>();

        foreach (var group in widgets.Distinct().GroupBy(widget => DashboardWidgetCatalog.Get(widget).Section))
        {
            var stopwatch = Stopwatch.StartNew();
            var sectionData = await LoadSectionAsync(actor, group.Key, scope, cancellationToken);
            var response = CreateSectionResponse(group.Key, scope, sectionData, contextId: null);
            var failed = 0;
            foreach (var widget in group)
            {
                var extracted = ExtractWidget(widget, response);
                if (string.Equals(extracted.Status, "ERROR", StringComparison.OrdinalIgnoreCase))
                    failed++;
                results[widget] = new DashboardAnalyticsResponse
                {
                    Widget = widget,
                    FromDate = scope.Filter.FromDate,
                    ToExclusive = scope.Filter.PeriodEndOverride ?? scope.Filter.ToDate.AddDays(1),
                    Granularity = scope.Filter.Granularity,
                    StoreIds = scope.StoreIds,
                    Rows = extracted.Data,
                    DataStatus = extracted.Status,
                    Warnings = extracted.Warnings
                };
            }
            stopwatch.Stop();
            telemetry.Add(new DashboardSectionTelemetryDto
            {
                Period = period,
                Section = group.Key,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                WidgetCount = group.Count(),
                FailedWidgetCount = failed
            });
        }

        return new DashboardAnalyticsBatchResponse { Widgets = results, Telemetry = telemetry };
    }

    public Task WriteAnalysisAuditAsync(
        int staffId,
        DashboardAnalysisAuditDto audit,
        CancellationToken cancellationToken = default) =>
        _repository.WriteAnalysisAuditAsync(staffId, audit, cancellationToken);

    private async Task<DashboardWidgetResult<InventoryReorderRow>> BuildReorderWidgetAsync(
        AdminActorContext actor,
        IReadOnlyList<int> storeIds,
        DashboardWidgetResult<InventoryReorderRow> legacy,
        CancellationToken cancellationToken)
    {
        if (_reorderSuggestions == null || _reorderAuthorization == null)
            return legacy;

        var allowedStores = new List<int>();
        foreach (var storeId in storeIds)
        {
            if (await _reorderAuthorization.CanViewAsync(
                    actor,
                    storeId,
                    cancellationToken))
            {
                allowedStores.Add(storeId);
            }
        }
        if (allowedStores.Count == 0)
            return DashboardWidgetResult<InventoryReorderRow>.Failure(
                "Không có cửa hàng trong phạm vi được phép xem gợi ý nhập hàng.");

        try
        {
            var result = await _reorderSuggestions.CalculateForStoresAsync(
                allowedStores,
                analysisWindowDays: 30,
                cancellationToken: cancellationToken);
            if (!result.IsSuccess || result.Data == null)
                return DashboardWidgetResult<InventoryReorderRow>.Failure(
                    result.Message ?? "Không tải được gợi ý nhập hàng.");

            var rows = result.Data
                .SelectMany(x => x.Items)
                .Where(x => x.SuggestionStatus is
                    ReorderRecommendationLevels.Urgent
                    or ReorderRecommendationLevels.NearReorder
                    or ReorderRecommendationLevels.ProcurementInProgress)
                .Where(x => x.FinalSuggestedQuantity > 0m)
                .OrderByDescending(x =>
                    x.SuggestionStatus == ReorderRecommendationLevels.Urgent)
                .ThenByDescending(x => x.FinalSuggestedQuantity)
                .Take(10)
                .Select(x => new InventoryReorderRow
                {
                    StoreId = x.StoreId,
                    StoreName = x.StoreName,
                    IngredientId = x.IngredientId,
                    IngredientCode = x.IngredientCode,
                    IngredientName = x.IngredientName,
                    Unit = x.BaseUnitCode,
                    OnHandQuantity = x.OnHandQuantity,
                    ReservedQuantity = x.ReservedQuantity,
                    AvailableQuantity = x.AvailableStock,
                    MinimumStock = x.MinimumStock,
                    ShortageQuantity = x.RemainingDemand ?? 0m,
                    RequestedQuantity = x.FinalSuggestedQuantity ?? 0m,
                    SuggestedQuantity = x.FinalSuggestedQuantity,
                    EffectiveSuggestedQuantity = x.FinalSuggestedQuantity ?? 0m,
                    SuggestionAverageDailyUsageSnapshot = x.AverageDailyConsumption,
                    SuggestionLeadTimeDaysSnapshot = x.LeadTimeDays,
                    SuggestionIncomingQuantitySnapshot = x.IncomingQuantity,
                    SuggestionReason = x.Reason,
                    Status = x.SuggestionStatus,
                    SuggestionStatus = x.SuggestionStatus,
                    RawDemand = x.RawDemand,
                    ProcurementCoveredQuantity = x.ProcurementCoveredQuantity,
                    RemainingDemand = x.RemainingDemand,
                    IncomingQuantity = x.IncomingQuantity,
                    FinalSuggestedQuantity = x.FinalSuggestedQuantity,
                    MeaningfulSuggestionVersion = x.MeaningfulSuggestionVersion,
                    DataStatus = "OK"
                })
                .ToList();
            return DashboardWidgetResult<InventoryReorderRow>.Success(rows);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return DashboardWidgetResult<InventoryReorderRow>.Failure(
                "Khong tai duoc widget goi y nhap hang.");
        }
    }

    private async Task<ScopeResolution> ResolveScopeAsync(
        AdminActorContext actor,
        DashboardFilterDto filter,
        CancellationToken cancellationToken)
    {
        if (actor.StaffId <= 0) throw new UnauthorizedAccessException("Staff context is required.");
        var normalized = NormalizeFilter(filter);
        var now = _clock.GetLocalNow();
        if (normalized.ToDate.Date > now.Date)
            normalized.ToDate = now.Date;
        if (normalized.FromDate.Date > normalized.ToDate.Date)
            throw new ArgumentException("FromDate không được lớn hơn thời điểm hiện tại.", nameof(filter));
        normalized.PeriodStartOverride ??= normalized.FromDate.Date;
        normalized.PeriodEndOverride ??= normalized.ToDate.Date >= now.Date
            ? now.DateTime
            : normalized.ToDate.Date.AddDays(1);
        var allowed = await _scopeAuthorization.GetAllowedStoresAsync(actor.StaffId);
        var allowedIds = allowed.Select(x => x.StoreId).Distinct().ToArray();
        if (allowedIds.Length == 0) throw new UnauthorizedAccessException("Không có cửa hàng trong phạm vi được cấp.");
        if (normalized.StoreIdsOverride is { Count: > 0 })
        {
            var requestedIds = normalized.StoreIdsOverride.Where(x => x > 0).Distinct().ToArray();
            if (requestedIds.Length != normalized.StoreIdsOverride.Distinct().Count()
                || requestedIds.Except(allowedIds).Any())
            {
                throw new UnauthorizedAccessException(
                    "Danh sách cửa hàng yêu cầu có phần nằm ngoài phạm vi được cấp.");
            }
        }
        var options = await _repository.GetStoreOptionsAsync(allowedIds, cancellationToken);

        IEnumerable<DashboardStoreOptionDto> selected = options;
        if (normalized.StoreIdsOverride is { Count: > 0 })
            selected = selected.Where(x => normalized.StoreIdsOverride.Contains(x.StoreId));
        if (normalized.ProvinceId.HasValue) selected = selected.Where(x => x.ProvinceId == normalized.ProvinceId);
        if (normalized.WardId.HasValue) selected = selected.Where(x => x.WardId == normalized.WardId);
        if (normalized.StoreId.HasValue)
        {
            if (!selected.Any(x => x.StoreId == normalized.StoreId.Value))
                throw new UnauthorizedAccessException("Cửa hàng nằm ngoài phạm vi được cấp.");
            selected = selected.Where(x => x.StoreId == normalized.StoreId.Value);
        }

        var ids = selected.Select(x => x.StoreId).Distinct().OrderBy(x => x).ToArray();
        if (ids.Length == 0) throw new UnauthorizedAccessException("Bộ lọc không còn cửa hàng hợp lệ.");
        return new ScopeResolution(normalized, ids, options, now);
    }

    private Task<DashboardAuthorizationDto> RequireAccessAsync(
        AdminActorContext actor, CancellationToken cancellationToken)
    {
        if (_authorization == null)
            throw new UnauthorizedAccessException("Dashboard authorization service is required.");
        return _authorization.GetAccessAsync(actor, cancellationToken);
    }

    private static void FilterUnauthorizedWidgets(
        DashboardSection section, object data,
        IReadOnlyCollection<DashboardAnalyticsWidget> allowed)
    {
        var map = WidgetPropertyMap.TryGetValue(section, out var value)
            ? value : new Dictionary<string, DashboardAnalyticsWidget>();
        foreach (var property in data.GetType().GetProperties())
        {
            if (!map.TryGetValue(property.Name, out var widget) || allowed.Contains(widget)) continue;
            var replacement = Activator.CreateInstance(property.PropertyType);
            property.PropertyType.GetProperty("Status")?.SetValue(replacement, "FORBIDDEN");
            property.PropertyType.GetProperty("ErrorCode")?.SetValue(replacement, "DASHBOARD_WIDGET_FORBIDDEN");
            property.SetValue(data, replacement);
        }
    }

    private static readonly IReadOnlyDictionary<DashboardSection, IReadOnlyDictionary<string, DashboardAnalyticsWidget>> WidgetPropertyMap =
        new Dictionary<DashboardSection, IReadOnlyDictionary<string, DashboardAnalyticsWidget>>
        {
            [DashboardSection.Executive] = new Dictionary<string, DashboardAnalyticsWidget>
            {
                ["NetSalesTrend"] = DashboardAnalyticsWidget.NetSalesTrend, ["StoreRanking"] = DashboardAnalyticsWidget.StoreRanking,
                ["PaymentMethodMix"] = DashboardAnalyticsWidget.PaymentMethodMix, ["OrderHeatmap"] = DashboardAnalyticsWidget.OrderHeatmap,
                ["OperationalAlerts"] = DashboardAnalyticsWidget.OperationalAlerts, ["OrderStatusSummary"] = DashboardAnalyticsWidget.OrderStatusSummary
            },
            [DashboardSection.Operations] = new Dictionary<string, DashboardAnalyticsWidget>
            {
                ["CashDiscrepancy"] = DashboardAnalyticsWidget.WorkShiftCashDiscrepancy, ["ShiftSales"] = DashboardAnalyticsWidget.WorkShiftSales,
                ["PaymentMix"] = DashboardAnalyticsWidget.WorkShiftPaymentMix, ["OfflineReconciliation"] = DashboardAnalyticsWidget.OfflineReconciliationExceptions,
                ["HourlyOrders"] = DashboardAnalyticsWidget.HourlyOrders, ["TopDiscrepancies"] = DashboardAnalyticsWidget.WorkShiftTopDiscrepancies,
                ["Kpis"] = DashboardAnalyticsWidget.WorkShiftKpis
            },
            [DashboardSection.Inventory] = new Dictionary<string, DashboardAnalyticsWidget>
            {
                ["ShortageRisk"] = DashboardAnalyticsWidget.InventoryShortageRisk, ["Movement"] = DashboardAnalyticsWidget.InventoryMovementByType,
                ["ThresholdRisk"] = DashboardAnalyticsWidget.InventoryThresholdRisk, ["ReorderSuggestions"] = DashboardAnalyticsWidget.InventoryReorderSuggestions,
                ["Waste"] = DashboardAnalyticsWidget.InventoryWasteByStoreIngredient, ["FifoAge"] = DashboardAnalyticsWidget.InventoryFifoLayerAge,
                ["IngredientConsumptionTrend"] = DashboardAnalyticsWidget.IngredientConsumptionTrend
            },
            [DashboardSection.Procurement] = new Dictionary<string, DashboardAnalyticsWidget>
            {
                ["PurchaseOrderPipeline"] = DashboardAnalyticsWidget.PurchaseOrderPipeline, ["OverduePurchaseOrders"] = DashboardAnalyticsWidget.OverduePurchaseOrders,
                ["SupplierQuality"] = DashboardAnalyticsWidget.SupplierQuality, ["PurchasePriceTrend"] = DashboardAnalyticsWidget.PurchasePriceTrend,
                ["SpendBreakdown"] = DashboardAnalyticsWidget.ProcurementSpendBreakdown, ["SupplierIssueMix"] = DashboardAnalyticsWidget.SupplierIssueMix
            },
            [DashboardSection.Product] = new Dictionary<string, DashboardAnalyticsWidget>
            {
                ["TopProducts"] = DashboardAnalyticsWidget.TopProducts, ["VolumeMargin"] = DashboardAnalyticsWidget.VolumeMarginMatrix,
                ["SizeMargin"] = DashboardAnalyticsWidget.SizeMargin, ["TopToppings"] = DashboardAnalyticsWidget.TopToppings,
                ["BomHealth"] = DashboardAnalyticsWidget.BomHealth, ["LowEfficiency"] = DashboardAnalyticsWidget.HighConsumptionLowEfficiency,
                ["CategoryPerformance"] = DashboardAnalyticsWidget.CategoryPerformance, ["ProductPeriodPerformance"] = DashboardAnalyticsWidget.ProductPeriodPerformance,
                ["LowVolumeProducts"] = DashboardAnalyticsWidget.LowVolumeProducts, ["LowMarginProducts"] = DashboardAnalyticsWidget.LowMarginProducts
            },
            [DashboardSection.Workforce] = new Dictionary<string, DashboardAnalyticsWidget>
            {
                ["ShiftStatus"] = DashboardAnalyticsWidget.WorkforceShiftStatus, ["HourlyDemand"] = DashboardAnalyticsWidget.WorkforceHourlyDemand,
                ["StaffPerformance"] = DashboardAnalyticsWidget.WorkforceStaffPerformance
            }
        };

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
            WardId = filter.WardId,
            Granularity = NormalizeGranularity(filter.Granularity, filter.FromDate, filter.ToDate),
            Top = Math.Clamp(filter.Top, 1, 100),
            PeriodStartOverride = filter.PeriodStartOverride,
            PeriodEndOverride = filter.PeriodEndOverride,
            StoreIdsOverride = filter.StoreIdsOverride
        };
    }

    private static DashboardFilterDto CopyFilter(DashboardFilterDto filter) => new()
    {
        FromDate=filter.FromDate,ToDate=filter.ToDate,StoreId=filter.StoreId,
        ProvinceId=filter.ProvinceId,WardId=filter.WardId,
        Granularity=filter.Granularity,Top=filter.Top,
        PeriodStartOverride=filter.PeriodStartOverride,
        PeriodEndOverride=filter.PeriodEndOverride,
        StoreIdsOverride=filter.StoreIdsOverride
    };

    private DashboardAnalysisContextDto CreateContext(
        DashboardFilterDto filter,
        IReadOnlyList<int> storeIds,
        IReadOnlyList<DashboardStoreOptionDto> storeOptions,
        DashboardPeriodPreset? preset = null,
        DateTimeOffset? generatedAt = null)
    {
        var generated = generatedAt ?? _clock.GetLocalNow();
        var offset = generated.Offset;
        var start = new DateTimeOffset(
            filter.PeriodStartOverride ?? filter.FromDate.Date, offset);
        var requestedEnd = filter.PeriodEndOverride
            ?? filter.ToDate.Date.AddDays(1);
        var end = new DateTimeOffset(
            requestedEnd > generated.DateTime ? generated.DateTime : requestedEnd,
            offset);
        var comparison = ResolveComparison(start, end);
        return new DashboardAnalysisContextDto
        {
            ContextId = Guid.NewGuid(),
            GeneratedAt = generated,
            PeriodStart = start,
            PeriodEnd = end,
            FromDate = filter.FromDate.Date,
            ToDate = filter.ToDate.Date,
            Preset = preset,
            PeriodType = preset?.ToString() ?? "Custom",
            ComparisonStart = comparison.Start,
            ComparisonEnd = comparison.End,
            StoreId = filter.StoreId,
            StoreIds = storeIds,
            Stores = storeOptions.Where(store => storeIds.Contains(store.StoreId)).ToList(),
            FilterFingerprint = CreateFilterFingerprint(filter, storeIds),
            ProvinceId = filter.ProvinceId,
            WardId = filter.WardId,
            Granularity = filter.Granularity,
            Top = filter.Top,
            Widgets = DashboardWidgetCatalog.Metadata()
        };
    }

    internal static string CreateFilterFingerprint(
        DashboardFilterDto filter,
        IReadOnlyList<int> storeIds)
    {
        var canonical = string.Join(
            "|",
            (filter.PeriodStartOverride ?? filter.FromDate.Date).ToUniversalTime().ToString("O"),
            (filter.PeriodEndOverride ?? filter.ToDate.Date.AddDays(1)).ToUniversalTime().ToString("O"),
            string.Join(",", storeIds.OrderBy(id => id)),
            filter.Granularity.Trim().ToUpperInvariant(),
            Math.Clamp(filter.Top, 1, 100));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private void StoreContext(AdminActorContext actor, DashboardAnalysisContextDto context, ScopeResolution scope)
    {
        var filter = CopyFilter(scope.Filter);
        filter.PeriodStartOverride = context.PeriodStart.DateTime;
        filter.PeriodEndOverride = context.PeriodEnd.DateTime;
        _cache.Set(
            ContextKey(actor.StaffId, context.ContextId),
            new CachedContext(context, filter, scope.StoreIds, scope.StoreOptions),
            TimeSpan.FromMinutes(15));
    }

    private bool TryGetContext(AdminActorContext actor, Guid contextId, out CachedContext context) =>
        _cache.TryGetValue(ContextKey(actor.StaffId, contextId), out context!);

    private static string ContextKey(int staffId, Guid contextId) =>
        $"dashboard-context:{staffId}:{contextId:N}";

    private static void ApplyPreset(DashboardFilterDto filter, DashboardPeriodPreset preset, DateTimeOffset now)
    {
        var today = now.Date;
        filter.FromDate = preset switch
        {
            DashboardPeriodPreset.Today => today,
            DashboardPeriodPreset.Last7Days => today.AddDays(-7),
            DashboardPeriodPreset.Last30Days => today.AddDays(-30),
            DashboardPeriodPreset.ThisMonth => new DateTime(today.Year, today.Month, 1),
            _ => filter.FromDate
        };
        filter.ToDate = today;
    }

    private static (DateTimeOffset? Start, DateTimeOffset? End) ResolveComparison(
        DateTimeOffset start, DateTimeOffset end)
    {
        var duration = end - start;
        if (duration <= TimeSpan.Zero) return (null, null);
        return (start - duration, start);
    }

    private static object CreateSectionResponse(DashboardSection section, ScopeResolution scope, object data, Guid? contextId) => data switch
    {
        ExecutiveDashboardData value => Response(section, scope, value, contextId),
        OperationsDashboardData value => Response(section, scope, value, contextId),
        InventoryDashboardData value => Response(section, scope, value, contextId),
        ProcurementDashboardData value => Response(section, scope, value, contextId),
        ProductDashboardData value => Response(section, scope, value, contextId),
        WorkforceDashboardData value => Response(section, scope, value, contextId),
        _ => throw new InvalidOperationException("Unsupported dashboard section data.")
    };

    private static DashboardSectionResponse<T> Response<T>(DashboardSection section, ScopeResolution scope, T data, Guid? contextId) => new()
    {
        Section=section,ContextId=contextId,FromDate=scope.Filter.FromDate,
        ToExclusive=scope.Filter.PeriodEndOverride ?? scope.Filter.ToDate.AddDays(1),
        GeneratedAt=scope.GeneratedAt,
        Granularity=scope.Filter.Granularity,StoreIds=scope.StoreIds,Data=data
    };

    private static (object Data, string Status, List<string> Warnings) ExtractWidget(DashboardAnalyticsWidget widget, object response) => (widget, response) switch
    {
        (DashboardAnalyticsWidget.NetSalesTrend, DashboardSectionResponse<ExecutiveDashboardData> x) => Pack(x.Data.NetSalesTrend),
        (DashboardAnalyticsWidget.StoreRanking, DashboardSectionResponse<ExecutiveDashboardData> x) => Pack(x.Data.StoreRanking),
        (DashboardAnalyticsWidget.PaymentMethodMix, DashboardSectionResponse<ExecutiveDashboardData> x) => Pack(x.Data.PaymentMethodMix),
        (DashboardAnalyticsWidget.OrderHeatmap, DashboardSectionResponse<ExecutiveDashboardData> x) => Pack(x.Data.OrderHeatmap),
        (DashboardAnalyticsWidget.OperationalAlerts, DashboardSectionResponse<ExecutiveDashboardData> x) => Pack(x.Data.OperationalAlerts),
        (DashboardAnalyticsWidget.OrderStatusSummary, DashboardSectionResponse<ExecutiveDashboardData> x) => Pack(x.Data.OrderStatusSummary),
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
        (DashboardAnalyticsWidget.IngredientConsumptionTrend, DashboardSectionResponse<InventoryDashboardData> x) => Pack(x.Data.IngredientConsumptionTrend),
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
        (DashboardAnalyticsWidget.CategoryPerformance, DashboardSectionResponse<ProductDashboardData> x) => Pack(x.Data.CategoryPerformance),
        (DashboardAnalyticsWidget.ProductPeriodPerformance, DashboardSectionResponse<ProductDashboardData> x) => Pack(x.Data.ProductPeriodPerformance),
        (DashboardAnalyticsWidget.LowVolumeProducts, DashboardSectionResponse<ProductDashboardData> x) => Pack(x.Data.LowVolumeProducts),
        (DashboardAnalyticsWidget.LowMarginProducts, DashboardSectionResponse<ProductDashboardData> x) => Pack(x.Data.LowMarginProducts),
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
