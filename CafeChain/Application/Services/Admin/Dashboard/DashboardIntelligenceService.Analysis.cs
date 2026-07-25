using System.Text.Json;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Dashboard;
using Microsoft.Extensions.Caching.Memory;

namespace CafeChain.Application.Services.Admin.Dashboard;

public sealed partial class DashboardIntelligenceService
{
    public async Task<DashboardStructuredAnalysisResultDto> AnalyzeAsync(
        AdminActorContext actor,
        DashboardPromptRequestDto request,
        CancellationToken cancellationToken = default)
    {
        RequireActor(actor);
        var parsed = await ParseAsync(actor, request, cancellationToken);
        if (!parsed.Success || parsed.Intent == null)
            throw new ArgumentException(parsed.Message);

        var intent = parsed.Intent;
        var page = await _dashboard.GetPageAsync(actor, new DashboardFilterDto(), cancellationToken);
        ApplyExplicitMvcFilters(request, intent, page.Stores);
        ValidateIntent(intent);

        var storeId = ResolveNamedStore(intent, page.Stores);
        var currentWindow = ResolveWindow(intent.Period, DateTime.Today);
        var baselineWindow = intent.Comparison == DashboardComparison.None
            ? ((DateTime From, DateTime To)?)null
            : ResolveBaseline(currentWindow, intent.Comparison);
        var widgets = DataPlan(intent.BusinessIntent, intent.FocusMetrics);
        var facts = new List<DashboardEvidenceDto>();
        var statistics = new List<DashboardEvidenceDto>();
        var anomalies = new List<DashboardAnomalyResultDto>();
        var charts = new List<DashboardChartDto>();
        var warnings = new List<string>(parsed.Warnings);
        var scopedStoreIds = new HashSet<int>();
        var availableDatasets = 0;

        foreach (var widget in widgets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = await _dashboard.GetAnalyticsAsync(
                widget,
                Filter(actor, intent, currentWindow.From, currentWindow.To, storeId),
                cancellationToken);
            DashboardAnalyticsResponse? baseline = null;
            if (baselineWindow.HasValue)
            {
                baseline = await _dashboard.GetAnalyticsAsync(
                    widget,
                    Filter(actor, intent, baselineWindow.Value.From, baselineWindow.Value.To, storeId),
                    cancellationToken);
            }

            foreach (var id in current.StoreIds)
                scopedStoreIds.Add(id);
            warnings.AddRange(current.Warnings);
            if (baseline != null)
                warnings.AddRange(baseline.Warnings);

            var currentMetric = Metric(widget, current.Rows);
            MetricValue? baselineMetric = baseline == null ? null : Metric(widget, baseline.Rows);
            var comparison = Compare(currentMetric, baselineMetric);
            var evidence = Evidence(widget, comparison, current.DataStatus);
            facts.Add(evidence);
            statistics.Add(CloneAsStatistic(evidence));
            charts.Add(new DashboardChartDto
            {
                Type = ChartFor(widget),
                Title = TitleFor(widget),
                Rows = current.Rows
            });
            if (HasRows(current.Rows))
                availableDatasets++;

            foreach (var signal in BuildInsights(widget, comparison))
            {
                anomalies.Add(new DashboardAnomalyResultDto
                {
                    Code = signal.Code,
                    Severity = signal.Severity,
                    Message = signal.Message,
                    EvidenceIds = [evidence.EvidenceId]
                });
            }
        }

        var dataStatus = availableDatasets == 0
            ? "Insufficient"
            : availableDatasets == widgets.Count
                ? "Complete"
                : "Partial";
        var deterministicSummary = dataStatus == "Insufficient"
            ? "Không đủ dữ liệu trong giai đoạn đã chọn để kết luận."
            : anomalies.Count == 0
                ? "Không phát hiện tín hiệu bất thường theo các rule hiện có trong phạm vi dữ liệu đã chọn."
                : string.Join(" ", anomalies.Select(x => x.Message));

        var primary = facts.FirstOrDefault();
        var explanation = new DashboardExplanationResultDto
        {
            Success = true,
            Summary = deterministicSummary,
            Explanation = deterministicSummary,
            UsedFallback = true
        };
        if (_options.ExplanationEnabled && primary != null)
        {
            explanation = await _ai.ExplainDashboardInsightAsync(
                new DashboardInsightExplanationContextDto
                {
                    AnalysisId = Guid.NewGuid(),
                    Widget = primary.SourceWidget,
                    BusinessIntent = intent.BusinessIntent,
                    FromDate = currentWindow.From,
                    ToDate = currentWindow.To,
                    Comparison = new DashboardComparisonResultDto
                    {
                        CurrentValue = primary.CurrentValue,
                        BaselineValue = primary.BaselineValue,
                        PercentageDifference = primary.DeviationPercent,
                        CurrentSampleSize = primary.SampleSize
                    },
                    Evidence = facts,
                    Insights = anomalies.Select(x => new DashboardInsightDto
                    {
                        Code = x.Code,
                        Severity = x.Severity,
                        Message = x.Message
                    }).ToList()
                },
                cancellationToken);
            warnings.AddRange(explanation.Warnings);
        }

        var evidenceIds = facts.Select(x => x.EvidenceId).ToHashSet(StringComparer.Ordinal);
        var inferences = ValidateNarratives(explanation.Inferences, evidenceIds);
        var recommendations = ValidateNarratives(explanation.Recommendations, evidenceIds);
        if (inferences.Count != explanation.Inferences.Count
            || recommendations.Count != explanation.Recommendations.Count)
        {
            warnings.Add("Một số nhận định AI bị loại vì không tham chiếu evidence hợp lệ.");
        }

        var result = new DashboardStructuredAnalysisResultDto
        {
            AnalysisId = Guid.NewGuid(),
            Intent = intent.BusinessIntent,
            DataPeriod = new DashboardDataPeriodResultDto
            {
                From = currentWindow.From,
                To = currentWindow.To,
                ComparisonFrom = baselineWindow?.From,
                ComparisonTo = baselineWindow?.To
            },
            StoreIds = scopedStoreIds.OrderBy(x => x).ToList(),
            DataStatus = dataStatus,
            Summary = string.IsNullOrWhiteSpace(explanation.Summary)
                ? deterministicSummary
                : explanation.Summary.Trim(),
            Facts = facts,
            Statistics = statistics,
            Inferences = inferences,
            Anomalies = anomalies,
            Recommendations = recommendations,
            Confidence = dataStatus switch
            {
                "Complete" => explanation.UsedOllama ? 0.9m : 0.8m,
                "Partial" => 0.6m,
                _ => 0m
            },
            Charts = charts,
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToList(),
            AiStatus = explanation.UsedOllama ? "Available" : "Fallback",
            UsedFallback = parsed.UsedFallback || explanation.UsedFallback
        };
        _cache.Set(
            CacheKey(actor.StaffId, result.AnalysisId),
            result,
            TimeSpan.FromMinutes(Math.Clamp(_options.AnalysisCacheMinutes, 1, 60)));
        return result;
    }

    private static void ApplyExplicitMvcFilters(
        DashboardPromptRequestDto request,
        DashboardIntentDto intent,
        IReadOnlyList<DashboardStoreOptionDto> stores)
    {
        if (request.FromDate.HasValue || request.ToDate.HasValue)
        {
            if (!request.FromDate.HasValue || !request.ToDate.HasValue
                || request.FromDate.Value.Date > request.ToDate.Value.Date)
            {
                throw new ArgumentException("Khoảng thời gian Dashboard không hợp lệ.");
            }
            intent.Period = new DashboardPeriodDto
            {
                Type = DashboardPeriodType.Custom,
                FromDate = request.FromDate.Value.Date,
                ToDate = request.ToDate.Value.Date,
                Value = null
            };
        }

        if (request.StoreId is > 0)
        {
            var store = stores.SingleOrDefault(x => x.StoreId == request.StoreId.Value)
                ?? throw new UnauthorizedAccessException("Cửa hàng không thuộc phạm vi được cấp quyền.");
            intent.StoreSelector = new DashboardStoreSelectorDto
            {
                Mode = DashboardStoreSelectorMode.NamedStore,
                StoreName = store.StoreName
            };
        }
    }

    private static IReadOnlyList<DashboardAnalyticsWidget> DataPlan(
        DashboardBusinessIntent intent,
        IReadOnlyCollection<string> focusMetrics) => intent switch
    {
        DashboardBusinessIntent.RevenueAnalysis or DashboardBusinessIntent.SalesTrend =>
        [
            DashboardAnalyticsWidget.NetSalesTrend,
            DashboardAnalyticsWidget.StoreRanking,
            DashboardAnalyticsWidget.OrderHeatmap,
            DashboardAnalyticsWidget.CategoryPerformance,
            DashboardAnalyticsWidget.ProductPeriodPerformance
        ],
        DashboardBusinessIntent.OrderAnalysis =>
        [
            DashboardAnalyticsWidget.OrderStatusSummary,
            DashboardAnalyticsWidget.HourlyOrders,
            DashboardAnalyticsWidget.PaymentMethodMix,
            DashboardAnalyticsWidget.OrderHeatmap
        ],
        DashboardBusinessIntent.ProductPerformance =>
        [
            DashboardAnalyticsWidget.TopProducts,
            DashboardAnalyticsWidget.ProductPeriodPerformance,
            DashboardAnalyticsWidget.CategoryPerformance,
            DashboardAnalyticsWidget.VolumeMarginMatrix,
            DashboardAnalyticsWidget.SizeMargin,
            DashboardAnalyticsWidget.TopToppings
        ],
        DashboardBusinessIntent.StoreComparison =>
        [
            DashboardAnalyticsWidget.StoreRanking,
            DashboardAnalyticsWidget.InventoryShortageRisk,
            DashboardAnalyticsWidget.OperationalAlerts
        ],
        DashboardBusinessIntent.InventoryAnalysis =>
        [
            DashboardAnalyticsWidget.InventoryShortageRisk,
            DashboardAnalyticsWidget.InventoryThresholdRisk,
            DashboardAnalyticsWidget.InventoryMovementByType,
            DashboardAnalyticsWidget.IngredientConsumptionTrend,
            DashboardAnalyticsWidget.InventoryWasteByStoreIngredient
        ],
        DashboardBusinessIntent.ReorderAnalysis =>
        [
            DashboardAnalyticsWidget.InventoryReorderSuggestions,
            DashboardAnalyticsWidget.InventoryShortageRisk,
            DashboardAnalyticsWidget.IngredientConsumptionTrend
        ],
        DashboardBusinessIntent.SupplierAnalysis =>
        [
            DashboardAnalyticsWidget.SupplierQuality,
            DashboardAnalyticsWidget.PurchasePriceTrend,
            DashboardAnalyticsWidget.ProcurementSpendBreakdown,
            DashboardAnalyticsWidget.SupplierIssueMix,
            DashboardAnalyticsWidget.OverduePurchaseOrders
        ],
        DashboardBusinessIntent.AnomalyDetection =>
        [
            DashboardAnalyticsWidget.OperationalAlerts,
            DashboardAnalyticsWidget.NetSalesTrend,
            DashboardAnalyticsWidget.IngredientConsumptionTrend,
            DashboardAnalyticsWidget.InventoryWasteByStoreIngredient,
            DashboardAnalyticsWidget.SupplierQuality
        ],
        DashboardBusinessIntent.GeneralBusinessSummary =>
        [
            DashboardAnalyticsWidget.NetSalesTrend,
            DashboardAnalyticsWidget.StoreRanking,
            DashboardAnalyticsWidget.TopProducts,
            DashboardAnalyticsWidget.InventoryShortageRisk,
            DashboardAnalyticsWidget.SupplierQuality,
            DashboardAnalyticsWidget.WorkforceShiftStatus
        ],
        _ => FocusPlan(focusMetrics)
    };

    private static IReadOnlyList<DashboardAnalyticsWidget> FocusPlan(
        IReadOnlyCollection<string> focusMetrics)
    {
        var normalized = focusMetrics.Select(x => x.Trim().ToUpperInvariant()).ToHashSet();
        if (normalized.Contains("INVENTORY"))
            return [DashboardAnalyticsWidget.InventoryShortageRisk, DashboardAnalyticsWidget.IngredientConsumptionTrend];
        if (normalized.Contains("PRODUCT"))
            return [DashboardAnalyticsWidget.TopProducts, DashboardAnalyticsWidget.VolumeMarginMatrix];
        if (normalized.Contains("SUPPLIER"))
            return [DashboardAnalyticsWidget.SupplierQuality, DashboardAnalyticsWidget.PurchasePriceTrend];
        if (normalized.Contains("ORDER"))
            return [DashboardAnalyticsWidget.HourlyOrders, DashboardAnalyticsWidget.OrderHeatmap];
        return [DashboardAnalyticsWidget.NetSalesTrend, DashboardAnalyticsWidget.StoreRanking];
    }

    private static DashboardEvidenceDto Evidence(
        DashboardAnalyticsWidget widget,
        DashboardComparisonResultDto comparison,
        string dataStatus)
    {
        var unit = widget switch
        {
            DashboardAnalyticsWidget.NetSalesTrend
                or DashboardAnalyticsWidget.StoreRanking
                or DashboardAnalyticsWidget.HourlyOrders
                or DashboardAnalyticsWidget.TopProducts
                or DashboardAnalyticsWidget.InventoryWasteByStoreIngredient => "VND",
            DashboardAnalyticsWidget.SupplierQuality => "PERCENT",
            _ => "COUNT"
        };
        var title = TitleFor(widget);
        var statement = dataStatus == "NO_DATA"
            ? $"{title}: không có dữ liệu trong kỳ."
            : comparison.BaselineValue.HasValue
                ? $"{title}: giá trị kỳ hiện tại {comparison.CurrentValue:N2}, kỳ so sánh {comparison.BaselineValue:N2}."
                : $"{title}: giá trị kỳ hiện tại {comparison.CurrentValue:N2}.";
        return new DashboardEvidenceDto
        {
            EvidenceId = $"E-{widget}",
            Kind = "FACT",
            SourceWidget = widget,
            Title = title,
            Statement = statement,
            CurrentValue = comparison.CurrentValue,
            BaselineValue = comparison.BaselineValue,
            DeviationPercent = comparison.PercentageDifference,
            SampleSize = comparison.CurrentSampleSize,
            Unit = unit
        };
    }

    private static DashboardEvidenceDto CloneAsStatistic(DashboardEvidenceDto source) => new()
    {
        EvidenceId = source.EvidenceId,
        Kind = "STATISTIC",
        SourceWidget = source.SourceWidget,
        Title = source.Title,
        Statement = source.Statement,
        CurrentValue = source.CurrentValue,
        BaselineValue = source.BaselineValue,
        DeviationPercent = source.DeviationPercent,
        SampleSize = source.SampleSize,
        Unit = source.Unit
    };

    private static bool HasRows(object rows)
    {
        var json = JsonSerializer.SerializeToElement(rows, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return json.ValueKind == JsonValueKind.Array && json.GetArrayLength() > 0;
    }

    private static List<DashboardNarrativeItemDto> ValidateNarratives(
        IEnumerable<DashboardNarrativeItemDto> items,
        IReadOnlySet<string> evidenceIds) =>
        items
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.Text)
                && x.EvidenceIds.Count > 0
                && x.EvidenceIds.All(evidenceIds.Contains))
            .Select(x => new DashboardNarrativeItemDto
            {
                Text = x.Text.Trim(),
                EvidenceIds = x.EvidenceIds.Distinct(StringComparer.Ordinal).ToList()
            })
            .ToList();
}
