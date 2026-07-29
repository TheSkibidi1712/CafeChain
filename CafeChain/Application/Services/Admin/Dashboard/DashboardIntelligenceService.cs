using System.Globalization;
using System.Text;
using System.Text.Json;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using CafeChain.Application.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.Admin.Dashboard;

public sealed partial class DashboardIntelligenceService : IDashboardIntelligenceService
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, object> RateLocks = new();
    private static readonly HashSet<DashboardAnalyticsWidget> AllowedWidgets =
    [
        DashboardAnalyticsWidget.NetSalesTrend, DashboardAnalyticsWidget.StoreRanking,
        DashboardAnalyticsWidget.TopProducts, DashboardAnalyticsWidget.HourlyOrders,
        DashboardAnalyticsWidget.InventoryWasteByStoreIngredient,
        DashboardAnalyticsWidget.OverduePurchaseOrders, DashboardAnalyticsWidget.SupplierQuality,
        DashboardAnalyticsWidget.WorkforceShiftStatus
    ];

    private static readonly IReadOnlyList<DashboardAnalyticsWidget> DashboardWidgets =
    [
        DashboardAnalyticsWidget.NetSalesTrend, DashboardAnalyticsWidget.StoreRanking,
        DashboardAnalyticsWidget.PaymentMethodMix, DashboardAnalyticsWidget.OrderHeatmap,
        DashboardAnalyticsWidget.OperationalAlerts,
        DashboardAnalyticsWidget.WorkShiftCashDiscrepancy, DashboardAnalyticsWidget.WorkShiftSales,
        DashboardAnalyticsWidget.WorkShiftPaymentMix, DashboardAnalyticsWidget.HourlyOrders,
        DashboardAnalyticsWidget.OfflineReconciliationExceptions,
        DashboardAnalyticsWidget.WorkShiftTopDiscrepancies, DashboardAnalyticsWidget.WorkShiftKpis,
        DashboardAnalyticsWidget.InventoryShortageRisk, DashboardAnalyticsWidget.InventoryMovementByType,
        DashboardAnalyticsWidget.InventoryThresholdRisk, DashboardAnalyticsWidget.InventoryReorderSuggestions,
        DashboardAnalyticsWidget.InventoryWasteByStoreIngredient, DashboardAnalyticsWidget.InventoryFifoLayerAge,
        DashboardAnalyticsWidget.PurchaseOrderPipeline, DashboardAnalyticsWidget.OverduePurchaseOrders,
        DashboardAnalyticsWidget.SupplierQuality, DashboardAnalyticsWidget.PurchasePriceTrend,
        DashboardAnalyticsWidget.ProcurementSpendBreakdown, DashboardAnalyticsWidget.SupplierIssueMix,
        DashboardAnalyticsWidget.TopProducts, DashboardAnalyticsWidget.VolumeMarginMatrix,
        DashboardAnalyticsWidget.SizeMargin, DashboardAnalyticsWidget.TopToppings,
        DashboardAnalyticsWidget.BomHealth, DashboardAnalyticsWidget.HighConsumptionLowEfficiency,
        DashboardAnalyticsWidget.WorkforceShiftStatus, DashboardAnalyticsWidget.WorkforceHourlyDemand,
        DashboardAnalyticsWidget.WorkforceStaffPerformance
    ];

    private readonly IDashboardService _dashboard;
    private readonly IAIService _ai;
    private readonly IMemoryCache _cache;
    private readonly DashboardIntelligenceOptions _options;
    private readonly ILogger<DashboardIntelligenceService> _logger;

    public DashboardIntelligenceService(
        IDashboardService dashboard, IAIService ai, IMemoryCache cache,
        IOptions<DashboardIntelligenceOptions> options,
        ILogger<DashboardIntelligenceService>? logger = null)
    {
        _dashboard = dashboard;
        _ai = ai;
        _cache = cache;
        _options = options.Value;
        _logger = logger ?? NullLogger<DashboardIntelligenceService>.Instance;
    }

    public async Task<DashboardIntentParseResultDto> ParseAsync(
        AdminActorContext actor, DashboardPromptRequestDto request, CancellationToken cancellationToken = default)
    {
        RequireActor(actor);
        EnforceRate(actor);
        var prompt = request.Prompt?.Trim() ?? string.Empty;
        if (prompt.Length > 500 || prompt.Length > _options.MaximumPromptLength)
            throw new ArgumentException("Câu hỏi tối đa 500 ký tự.");
        if (prompt.Length == 0)
            return new DashboardIntentParseResultDto
            {
                Success = true,
                Message = "Phân tích toàn bộ Dashboard theo context hiện tại.",
                Intent = new DashboardIntentDto
                {
                    BusinessIntent = DashboardBusinessIntent.GeneralBusinessSummary,
                    Widget = DashboardAnalyticsWidget.NetSalesTrend,
                    Period = new DashboardPeriodDto { Type = DashboardPeriodType.LastNDays, Value = 7 },
                    Comparison = DashboardComparison.PreviousPeriod
                },
                UsedFallback = true
            };

        var page = await _dashboard.GetPageAsync(actor, new DashboardFilterDto(), cancellationToken);
        DashboardIntentParseResultDto? aiFailure = null;
        if (_options.IntentParserEnabled)
        {
            var ai = await _ai.ParseDashboardIntentAsync(
                request,
                page.Stores.Select(x => x.StoreName).ToList(),
                cancellationToken);
            if (ai.Success && ai.Intent != null)
            {
                ValidateIntent(ai.Intent);
                ResolveNamedStore(ai.Intent, page.Stores);
                return ai;
            }
            aiFailure = ai;
        }

        var deterministic = ParseDeterministic(prompt, page.Stores.Select(x => x.StoreName).ToList());
        if (deterministic != null)
            return new DashboardIntentParseResultDto
            {
                Success = true,
                Intent = deterministic,
                UsedFallback = true,
                Message = "AI không khả dụng hoặc không trả đúng contract; đã dùng catalog intent an toàn.",
                Warnings = aiFailure == null ? [] : [aiFailure.Message]
            };

        return aiFailure ?? Unsupported();
    }

    public async Task<DashboardAnalysisResultDto> ExecuteAsync(
        AdminActorContext actor, DashboardIntentDto intent, CancellationToken cancellationToken = default)
    {
        RequireActor(actor);
        EnforceRate(actor);
        ValidateIntent(intent);
        var page = await _dashboard.GetPageAsync(actor, new DashboardFilterDto(), cancellationToken);
        var storeId = ResolveNamedStore(intent, page.Stores);
        var window = ResolveWindow(intent.Period, DateTime.Today);
        var currentFilter = Filter(actor, intent, window.From, window.To, storeId);
        var current = await _dashboard.GetAnalyticsAsync(intent.Widget, currentFilter, cancellationToken);
        DashboardAnalyticsResponse? baseline = null;
        if (intent.Comparison != DashboardComparison.None)
        {
            var baselineWindow = ResolveBaseline(window, intent.Comparison);
            baseline = await _dashboard.GetAnalyticsAsync(intent.Widget,
                Filter(actor, intent, baselineWindow.From, baselineWindow.To, storeId), cancellationToken);
        }

        var currentMetric = Metric(intent.Widget, current.Rows);
        MetricValue? baselineMetric = baseline == null ? null : Metric(intent.Widget, baseline.Rows);
        var comparison = Compare(currentMetric, baselineMetric);
        var insights = BuildInsights(intent.Widget, comparison);
        var result = new DashboardAnalysisResultDto
        {
            AnalysisId = Guid.NewGuid(), Intent = intent, FromDate = window.From,
            ToDate = window.To, StoreIds = current.StoreIds, DataStatus = current.DataStatus,
            Comparison = comparison, Insights = insights,
            Chart = new DashboardChartDto
            {
                Type = ChartFor(intent.Widget),
                WidgetKey = intent.Widget.ToString(),
                Section = DashboardWidgetCatalog.Get(intent.Widget).Section,
                Title = DashboardWidgetCatalog.Get(intent.Widget).Title,
                Rows = current.Rows
            },
            Warnings = current.Warnings.Concat(baseline?.Warnings ?? []).Distinct().ToList()
        };
        _cache.Set(CacheKey(actor.StaffId, result.AnalysisId), result,
            TimeSpan.FromMinutes(Math.Clamp(_options.AnalysisCacheMinutes, 1, 60)));
        return result;
    }

    public async Task<DashboardExplanationResultDto> ExplainAsync(
        AdminActorContext actor, Guid analysisId, CancellationToken cancellationToken = default)
    {
        RequireActor(actor);
        EnforceRate(actor);
        if (!_cache.TryGetValue(CacheKey(actor.StaffId, analysisId), out DashboardAnalysisResultDto? result) || result == null)
            throw new KeyNotFoundException("Kết quả phân tích đã hết hạn. Vui lòng chạy lại câu hỏi.");
        var context = new DashboardInsightExplanationContextDto
        {
            AnalysisId = result.AnalysisId, Widget = result.Intent.Widget,
            FromDate = result.FromDate, ToDate = result.ToDate,
            Comparison = result.Comparison, Insights = result.Insights,
            DataStatus = string.Equals(result.DataStatus, "AVAILABLE", StringComparison.OrdinalIgnoreCase)
                ? "OK"
                : result.DataStatus,
            Confidence = string.Equals(result.DataStatus, "AVAILABLE", StringComparison.OrdinalIgnoreCase)
                ? 0.85m
                : 0.60m
        };
        if (!_options.ExplanationEnabled)
            return DeterministicExplanation(context, "Giải thích AI đang tắt; sử dụng kết quả rule/statistics.");
        return await _ai.ExplainDashboardInsightAsync(context, cancellationToken);
    }

    private static DashboardIntentDto? ParseDeterministic(string prompt, IReadOnlyList<string> storeNames)
    {
        var text = Normalize(prompt);
        DashboardBusinessIntent? businessIntent = null;
        if (text.Contains("nha cung cap") || text.Contains("supplier") || text.Contains("gia nhap")
            || text.Contains("mua hang"))
            businessIntent = DashboardBusinessIntent.SupplierAnalysis;
        else if (text.Contains("nhap hang") || text.Contains("reorder") || text.Contains("dat hang")
                 || text.StartsWith("po ") || text.Contains(" po "))
            businessIntent = DashboardBusinessIntent.ReorderAnalysis;
        else if (text.Contains("ton kho") || text.Contains("kho") || text.Contains("nguyen lieu")
                 || text.Contains("sap thieu") || text.Contains("thieu hang")
                 || text.Contains("waste") || text.Contains("hao hut"))
            businessIntent = DashboardBusinessIntent.InventoryAnalysis;
        else if (text.Contains("huy don") || text.Contains("don huy") || text.Contains("so don")
                 || text.Contains("don hang") || text.Contains("thanh toan"))
            businessIntent = DashboardBusinessIntent.OrderAnalysis;
        else if (text.Contains("san pham") || text.Contains("do uong") || text.Contains("mon ")
                 || text.Contains("ban chay") || text.Contains("ban cham") || text.Contains("top "))
            businessIntent = DashboardBusinessIntent.ProductPerformance;
        else if (text.Contains("ca lam") || text.Contains("nhan su"))
            businessIntent = DashboardBusinessIntent.GeneralBusinessSummary;
        else if (text.Contains("chi nhanh") || text.Contains("cua hang"))
            businessIntent = DashboardBusinessIntent.StoreComparison;
        else if (text.Contains("bat thuong") || text.Contains("anomaly") || text.Contains("can chu y")
                 || text.Contains("canh bao"))
            businessIntent = DashboardBusinessIntent.AnomalyDetection;
        else if (text.Contains("doanh thu") || text.Contains("doanh so") || text.Contains("ban hang"))
            businessIntent = text.Contains("xu huong")
                ? DashboardBusinessIntent.SalesTrend
                : DashboardBusinessIntent.RevenueAnalysis;
        else if (text.Contains("tinh hinh") || text.Contains("tong quan") || text.Contains("phan tich"))
            businessIntent = DashboardBusinessIntent.GeneralBusinessSummary;
        if (!businessIntent.HasValue) return null;
        var widget = PrimaryWidget(businessIntent.Value);

        var period = new DashboardPeriodDto { Type = DashboardPeriodType.LastNDays, Value = 7 };
        if (text.Contains("hom nay")) period = new() { Type = DashboardPeriodType.Today };
        else if (text.Contains("hom qua")) period = new() { Type = DashboardPeriodType.Yesterday };
        else if (text.Contains("thang nay")) period = new() { Type = DashboardPeriodType.ThisMonth };
        else if (text.Contains("thang truoc")) period = new() { Type = DashboardPeriodType.LastMonth };
        else if (text.Contains("tuan nay")) period = new() { Type = DashboardPeriodType.ThisWeek };
        else if (text.Contains("tuan truoc")) period = new() { Type = DashboardPeriodType.LastWeek };
        else
        {
            var number = System.Text.RegularExpressions.Regex.Match(text, @"\b(\d{1,3})\s*ngay\b");
            if (number.Success && int.TryParse(number.Groups[1].Value, out var days))
                period.Value = Math.Clamp(days, 1, 366);
        }

        var comparison = text.Contains("so sanh") || text.Contains("giam") || text.Contains("tang")
            ? DashboardComparison.PreviousPeriod : DashboardComparison.None;
        var named = storeNames.FirstOrDefault(x => text.Contains(Normalize(x), StringComparison.Ordinal));
        return new DashboardIntentDto
        {
            IntentVersion = DashboardIntentVersions.V2,
            BusinessIntent = businessIntent.Value,
            Widget = widget, Period = period, Comparison = comparison,
            Granularity = widget == DashboardAnalyticsWidget.HourlyOrders ? "Hour" : "Day",
            Top = ExtractTop(text), Chart = ChartFor(widget),
            StoreSelector = named == null ? new() : new()
            {
                Mode = DashboardStoreSelectorMode.NamedStore, StoreName = named
            }
        };
    }

    private void ValidateIntent(DashboardIntentDto intent)
    {
        if (intent.IntentVersion == DashboardIntentVersions.V2)
        {
            if (!Enum.IsDefined(intent.BusinessIntent))
                throw new ArgumentException("UNSUPPORTED_INTENT: Business intent không thuộc catalog được cho phép.");
            intent.Widget = PrimaryWidget(intent.BusinessIntent);
        }
        else if (intent.IntentVersion != DashboardIntentVersions.V1 || !AllowedWidgets.Contains(intent.Widget))
        {
            throw new ArgumentException("UNSUPPORTED_INTENT: Dashboard intent không thuộc catalog được cho phép.");
        }
        if (intent.Top is < 1 or > 100) throw new ArgumentException("Top phải từ 1 đến 100.");
        if (intent.Granularity is not ("Hour" or "Day" or "Week" or "Month"))
            throw new ArgumentException("Granularity không hợp lệ.");
        var window = ResolveWindow(intent.Period, DateTime.Today);
        if ((window.To - window.From).TotalDays + 1 > Math.Clamp(_options.MaximumPeriodDays, 1, 366))
            throw new ArgumentException("Khoảng phân tích tối đa 366 ngày.");
        var expected = ChartFor(intent.Widget);
        if (intent.Chart != expected) intent.Chart = expected;
    }

    private static DashboardAnalyticsWidget PrimaryWidget(DashboardBusinessIntent intent) => intent switch
    {
        DashboardBusinessIntent.RevenueAnalysis or DashboardBusinessIntent.SalesTrend =>
            DashboardAnalyticsWidget.NetSalesTrend,
        DashboardBusinessIntent.OrderAnalysis => DashboardAnalyticsWidget.HourlyOrders,
        DashboardBusinessIntent.ProductPerformance => DashboardAnalyticsWidget.TopProducts,
        DashboardBusinessIntent.StoreComparison => DashboardAnalyticsWidget.StoreRanking,
        DashboardBusinessIntent.InventoryAnalysis => DashboardAnalyticsWidget.InventoryShortageRisk,
        DashboardBusinessIntent.ReorderAnalysis => DashboardAnalyticsWidget.InventoryReorderSuggestions,
        DashboardBusinessIntent.SupplierAnalysis => DashboardAnalyticsWidget.SupplierQuality,
        DashboardBusinessIntent.AnomalyDetection => DashboardAnalyticsWidget.OperationalAlerts,
        DashboardBusinessIntent.StatisticsRequest => DashboardAnalyticsWidget.NetSalesTrend,
        _ => DashboardAnalyticsWidget.NetSalesTrend
    };

    private static int? ResolveNamedStore(DashboardIntentDto intent, IReadOnlyList<DashboardStoreOptionDto> stores)
    {
        if (intent.StoreSelector.Mode == DashboardStoreSelectorMode.AllowedScope) return null;
        var name = intent.StoreSelector.StoreName?.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tên cửa hàng là bắt buộc.");
        var matches = stores.Where(x => string.Equals(x.StoreName.Trim(), name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count != 1) throw new UnauthorizedAccessException("Cửa hàng không thuộc phạm vi hoặc tên không xác định duy nhất.");
        return matches[0].StoreId;
    }

    private static DashboardAnalyticsFilter Filter(
        AdminActorContext actor,
        DashboardIntentDto intent,
        DateTime from,
        DateTime to,
        int? storeId,
        IReadOnlyList<int>? storeIds = null) => new()
    {
        StaffId = actor.StaffId, FromDate = from, ToDate = to, StoreId = storeId,
        Granularity = intent.Granularity, Top = intent.Top,
        PeriodStartOverride = from,
        PeriodEndOverride = to,
        StoreIdsOverride = storeIds
    };

    private static (DateTime From, DateTime To) ResolveWindow(DashboardPeriodDto period, DateTime today)
    {
        today = today.Date;
        return period.Type switch
        {
            DashboardPeriodType.Today => (today, today),
            DashboardPeriodType.Yesterday => (today.AddDays(-1), today.AddDays(-1)),
            DashboardPeriodType.LastNDays => (today.AddDays(-Math.Clamp(period.Value ?? 7, 1, 366) + 1), today),
            DashboardPeriodType.ThisWeek => (StartOfWeek(today), today),
            DashboardPeriodType.LastWeek => (StartOfWeek(today).AddDays(-7), StartOfWeek(today).AddDays(-1)),
            DashboardPeriodType.ThisMonth => (new DateTime(today.Year, today.Month, 1), today),
            DashboardPeriodType.LastMonth => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            DashboardPeriodType.Custom when period.FromDate.HasValue && period.ToDate.HasValue && period.FromDate.Value.Date <= period.ToDate.Value.Date
                => (period.FromDate.Value.Date, period.ToDate.Value.Date),
            _ => throw new ArgumentException("Khoảng thời gian intent không hợp lệ.")
        };
    }

    private static (DateTime From, DateTime To) ResolveBaseline((DateTime From, DateTime To) current, DashboardComparison comparison)
    {
        var days = (current.To - current.From).Days + 1;
        return comparison switch
        {
            DashboardComparison.PreviousPeriod => (current.From.AddDays(-days), current.From.AddDays(-1)),
            DashboardComparison.PreviousWeek => (current.From.AddDays(-7), current.To.AddDays(-7)),
            DashboardComparison.PreviousMonth => (current.From.AddMonths(-1), current.To.AddMonths(-1)),
            DashboardComparison.PreviousYear => (current.From.AddYears(-1), current.To.AddYears(-1)),
            _ => throw new ArgumentException("Comparison không hợp lệ.")
        };
    }

    private static MetricValue Metric(DashboardAnalyticsWidget widget, object rows)
    {
        var definition = DashboardWidgetCatalog.Get(widget).Metric
            ?? throw new InvalidOperationException($"Widget {widget} has no metric contract.");
        var json = JsonSerializer.SerializeToElement(rows, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (json.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Rows for widget {widget} must be a JSON array.");

        var rowList = json.EnumerateArray().ToList();
        decimal value;
        switch (definition.Aggregation)
        {
            case DashboardMetricAggregation.Sum:
                value = rowList.Sum(row => MetricNumber(row, definition.ValueField) ?? 0);
                break;
            case DashboardMetricAggregation.Max:
            {
                var values = rowList.Select(row => MetricNumber(row, definition.ValueField))
                    .Where(item => item.HasValue)
                    .Select(item => item!.Value)
                    .ToList();
                value = values.Count == 0 ? 0 : values.Max();
                break;
            }
            case DashboardMetricAggregation.CountRows:
                foreach (var row in rowList)
                    _ = MetricNumber(row, definition.ValueField);
                value = rowList.Count;
                break;
            case DashboardMetricAggregation.WeightedAverage:
            {
                var weightedTotal = rowList.Sum(row =>
                    (MetricNumber(row, definition.ValueField) ?? 0) * (MetricNumber(row, definition.WeightField) ?? 0));
                var totalWeight = rowList.Sum(row => MetricNumber(row, definition.WeightField) ?? 0);
                value = totalWeight == 0 ? 0 : weightedTotal / totalWeight;
                break;
            }
            case DashboardMetricAggregation.RatioOfSums:
            {
                var numerator = rowList.Sum(row => MetricNumber(row, definition.NumeratorField) ?? 0);
                var denominator = rowList.Sum(row => MetricNumber(row, definition.DenominatorField) ?? 0);
                if (!string.IsNullOrWhiteSpace(definition.AdditionalDenominatorField))
                    denominator += rowList.Sum(row => MetricNumber(row, definition.AdditionalDenominatorField) ?? 0);
                value = denominator == 0 ? 0 : numerator / denominator;
                break;
            }
            default:
                throw new InvalidOperationException(
                    $"Aggregation {definition.Aggregation} is not supported for widget {widget}.");
        }

        var sampleDecimal = string.IsNullOrWhiteSpace(definition.SampleField)
            ? rowList.Count
            : rowList.Sum(row => MetricNumber(row, definition.SampleField) ?? 0);
        var sample = sampleDecimal >= long.MaxValue
            ? long.MaxValue
            : sampleDecimal <= 0
                ? 0
                : decimal.ToInt64(decimal.Truncate(sampleDecimal));
        return new MetricValue(value, sample);
    }

    private static DashboardComparisonResultDto Compare(MetricValue current, MetricValue? baseline)
    {
        var result = new DashboardComparisonResultDto { CurrentValue = current.Value, CurrentSampleSize = current.Sample };
        if (baseline == null) return result;
        result.BaselineValue = baseline.Value.Value;
        result.BaselineSampleSize = baseline.Value.Sample;
        result.AbsoluteDifference = current.Value - baseline.Value.Value;
        if (baseline.Value.Value != 0)
            result.PercentageDifference = result.AbsoluteDifference / Math.Abs(baseline.Value.Value) * 100;
        return result;
    }

    private List<DashboardInsightDto> BuildInsights(DashboardAnalyticsWidget widget, DashboardComparisonResultDto comparison)
    {
        var result = new List<DashboardInsightDto>();
        if (!comparison.BaselineValue.HasValue || !comparison.PercentageDifference.HasValue) return result;
        if (widget == DashboardAnalyticsWidget.NetSalesTrend
            && comparison.CurrentSampleSize >= _options.MinimumOrderSample
            && comparison.PercentageDifference <= -_options.RevenueDropPercent
            && comparison.AbsoluteDifference <= -_options.RevenueDropAmount)
            result.Add(Insight("REVENUE_DROP", "WARNING", "Doanh thu giảm đáng kể so với kỳ trước.", comparison));
        if (widget == DashboardAnalyticsWidget.InventoryWasteByStoreIngredient
            && comparison.PercentageDifference >= _options.WasteIncreasePercent
            && comparison.AbsoluteDifference >= _options.WasteIncreaseAmount)
            result.Add(Insight("WASTE_INCREASE", "WARNING", "Giá trị hao hụt tăng đáng kể so với kỳ trước.", comparison));
        return result;
    }

    private static DashboardInsightDto Insight(string code, string severity, string message, DashboardComparisonResultDto c) => new()
    {
        Code = code, Severity = severity, Message = message,
        CurrentValue = c.CurrentValue, BaselineValue = c.BaselineValue, DeviationPercent = c.PercentageDifference
    };

    private static DashboardChartType ChartFor(DashboardAnalyticsWidget widget) =>
        DashboardWidgetCatalog.Get(widget).ChartType;

    private static string TitleFor(DashboardAnalyticsWidget widget) => widget switch
    {
        DashboardAnalyticsWidget.NetSalesTrend => "Doanh thu theo thời gian",
        DashboardAnalyticsWidget.StoreRanking => "Doanh thu theo cửa hàng",
        DashboardAnalyticsWidget.TopProducts => "Top sản phẩm",
        DashboardAnalyticsWidget.HourlyOrders => "Doanh thu và đơn hàng theo giờ",
        DashboardAnalyticsWidget.InventoryWasteByStoreIngredient => "Hao hụt kho",
        DashboardAnalyticsWidget.OverduePurchaseOrders => "Đơn mua quá hạn",
        DashboardAnalyticsWidget.SupplierQuality => "Chất lượng nhà cung cấp",
        _ => "Tình trạng ca làm việc"
    };

    private static int ExtractTop(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"\btop\s*(\d{1,3})\b");
        return match.Success && int.TryParse(match.Groups[1].Value, out var top) ? Math.Clamp(top, 1, 100) : 10;
    }

    private static string Normalize(string value)
    {
        var normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) builder.Append(character);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static DateTime StartOfWeek(DateTime date) => date.AddDays(-((7 + date.DayOfWeek - DayOfWeek.Monday) % 7));
    private static decimal? MetricNumber(JsonElement row, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Metric contract contains an empty numeric field.");
        if (!row.TryGetProperty(name, out var property))
            throw new InvalidOperationException($"Metric field '{name}' does not exist in the widget row contract.");
        if (property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (property.TryGetDecimal(out var value))
            return value;
        throw new InvalidOperationException($"Metric field '{name}' is not numeric.");
    }

    private static long Long(JsonElement row, string name)
    {
        var value = MetricNumber(row, name) ?? 0;
        return value >= long.MaxValue
            ? long.MaxValue
            : value <= long.MinValue
                ? long.MinValue
                : decimal.ToInt64(decimal.Truncate(value));
    }
    private static string CacheKey(int staffId, Guid analysisId) => $"dashboard-intelligence:{staffId}:{analysisId:N}";
    private static void RequireActor(AdminActorContext actor) { if (actor.StaffId <= 0) throw new UnauthorizedAccessException("Staff context is required."); }
    private static DashboardIntentParseResultDto Unsupported() => new()
    {
        Success = false,
        ErrorCode = "UNSUPPORTED_INTENT",
        Message = "Câu hỏi chưa thuộc các nhóm phân tích Dashboard được hỗ trợ.",
        UsedFallback = true
    };
    private void EnforceRate(AdminActorContext actor)
    {
        var key = $"dashboard-ai-rate:{actor.StaffId}";
        lock (RateLocks.GetOrAdd(actor.StaffId, _ => new object()))
        {
            var now = DateTime.UtcNow;
            var state = _cache.TryGetValue<RateState>(key, out var existing) && existing.WindowStartUtc > now.AddMinutes(-1)
                ? existing : new RateState(now, 0);
            if (state.Count >= Math.Clamp(_options.RequestsPerMinute, 1, 120))
                throw new InvalidOperationException("Bạn gửi yêu cầu quá nhanh. Vui lòng thử lại sau một phút.");
            _cache.Set(key, state with { Count = state.Count + 1 }, TimeSpan.FromMinutes(1));
        }
    }
    private static DashboardExplanationResultDto DeterministicExplanation(DashboardInsightExplanationContextDto context, string warning)
    {
        var text = context.Insights.Count > 0
            ? string.Join(" ", context.Insights.Select(x => x.Message))
            : context.Comparison.BaselineValue.HasValue
                ? $"Giá trị hiện tại là {context.Comparison.CurrentValue:N0}, so với kỳ trước {context.Comparison.BaselineValue:N0}."
                : $"Giá trị trong kỳ là {context.Comparison.CurrentValue:N0}.";
        return new DashboardExplanationResultDto
        {
            Success = true,
            Explanation = text,
            Summary = text,
            UsedFallback = true,
            Warnings = [warning]
        };
    }
    private readonly record struct MetricValue(decimal Value, long Sample);
    private sealed record RateState(DateTime WindowStartUtc, int Count);
}
