using System.Text.Json;
using System.Diagnostics;
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
        var stopwatch = Stopwatch.StartNew();
        var analysisId = Guid.NewGuid();
        RequireActor(actor);
        var parsed = await ParseAsync(actor, request, cancellationToken);
        if (!parsed.Success || parsed.Intent == null)
            throw new ArgumentException(parsed.Message);

        var intent = parsed.Intent;
        var context = request.ContextId.HasValue
            ? await _dashboard.GetContextAsync(actor, request.ContextId.Value, cancellationToken)
            : await _dashboard.CreateContextAsync(actor, new DashboardContextRequestDto
            {
                FromDate = request.FromDate?.Date ?? DateTime.Today.AddDays(-7),
                ToDate = request.ToDate?.Date ?? DateTime.Today,
                StoreId = request.StoreId
            }, cancellationToken);

        var currentWindow = (From: context.PeriodStart.DateTime, To: context.PeriodEnd.DateTime);
        var baselineWindow = context.ComparisonStart.HasValue && context.ComparisonEnd.HasValue
            ? (context.ComparisonStart.Value.DateTime, context.ComparisonEnd.Value.DateTime)
            : ((DateTime From, DateTime To)?)null;
        intent.Understanding.EffectiveStoreIds = context.StoreIds.Distinct().OrderBy(x => x).ToList();
        intent.DataPlan = DashboardQuestionCatalog.CreateDataPlan(
            intent.Understanding,
            intent.Understanding.EffectiveStoreIds,
            currentWindow.From,
            currentWindow.To);
        var widgets = new[] { intent.DataPlan.PrimaryWidget }
            .Concat(intent.DataPlan.SupportingWidgets)
            .Distinct()
            .ToList();
        var warnings = new List<string>(parsed.Warnings);
        if (ContainsTimeExpression(request.Prompt))
            warnings.Add("Câu hỏi có phạm vi thời gian riêng; phân tích vẫn sử dụng bộ lọc Dashboard hiện tại.");
        if (ContainsStoreExpression(request.Prompt))
            warnings.Add("Tên cửa hàng trong câu hỏi không thay đổi phạm vi; phân tích sử dụng các cửa hàng trong bộ lọc Dashboard.");
        warnings.Add(
            $"Phạm vi được sử dụng: {context.PeriodStart:yyyy-MM-dd HH:mm} → {context.PeriodEnd:yyyy-MM-dd HH:mm}; " +
            $"cửa hàng: {string.Join(", ", context.Stores.Select(x => x.StoreName))}.");

        var currentBatch = await _dashboard.GetAnalyticsBatchAsync(
            actor,
            widgets,
            Filter(actor, intent, currentWindow.From, currentWindow.To, context.StoreId, context.StoreIds),
            "Current",
            cancellationToken);
        var comparableWidgets = widgets
            .Where(widget => DashboardWidgetCatalog.Get(widget).SupportsComparison)
            .ToList();
        DashboardAnalyticsBatchResponse? baselineBatch = null;
        if (baselineWindow.HasValue && comparableWidgets.Count > 0)
        {
            baselineBatch = await _dashboard.GetAnalyticsBatchAsync(
                actor,
                comparableWidgets,
                Filter(actor, intent, baselineWindow.Value.From, baselineWindow.Value.To, context.StoreId, context.StoreIds),
                "Baseline",
                cancellationToken);
        }

        var facts = new List<DashboardEvidenceDto>();
        var statistics = new List<DashboardEvidenceDto>();
        var anomalies = new List<DashboardAnomalyResultDto>();
        var charts = new List<DashboardChartDto>();
        var chartAnalyses = new List<DashboardChartAnalysisDto>();
        var widgetStatuses = new List<string>();
        var scopedStoreIds = context.StoreIds.ToHashSet();
        var missingBaseline = false;
        var entityEvidenceExpected = false;
        var entityEvidenceFound = false;

        foreach (var widget in widgets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = currentBatch.Widgets[widget];
            DashboardAnalyticsResponse? baseline = null;
            baselineBatch?.Widgets.TryGetValue(widget, out baseline);
            warnings.AddRange(current.Warnings);
            if (baseline != null)
                warnings.AddRange(baseline.Warnings);
            foreach (var storeId in current.StoreIds)
                scopedStoreIds.Add(storeId);

            var widgetStatus = EvaluateWidgetStatus(current, baseline, baselineWindow.HasValue);
            widgetStatuses.Add(widgetStatus);
            if (baselineWindow.HasValue
                && DashboardWidgetCatalog.Get(widget).SupportsComparison
                && (baseline == null || EvaluateRowsStatus(baseline.Rows, baseline.DataStatus) != "OK"))
                missingBaseline = true;

            var comparison = Compare(
                Metric(widget, current.Rows),
                baseline == null ? null : Metric(widget, baseline.Rows));
            var bundle = BuildWidgetEvidence(widget, current.Rows, comparison, widgetStatus, intent.Top);
            facts.AddRange(bundle.Facts);
            statistics.AddRange(bundle.Statistics);
            entityEvidenceExpected |= RequiresEntityEvidence(widget);
            entityEvidenceFound |= bundle.Facts.Concat(bundle.Statistics)
                .Any(item => !string.IsNullOrWhiteSpace(item.EntityName));

            var totalEvidence = bundle.Facts.Concat(bundle.Statistics).First();
            charts.Add(CreateChart(widget, current.Rows));
            chartAnalyses.Add(BuildChartAnalysis(widget, current, baseline, comparison, totalEvidence));
            foreach (var signal in BuildInsights(widget, comparison))
            {
                anomalies.Add(new DashboardAnomalyResultDto
                {
                    Code = signal.Code,
                    Severity = signal.Severity,
                    Message = signal.Message,
                    EvidenceIds = [totalEvidence.EvidenceId]
                });
            }
            AddBackendAnomalies(widget, bundle, anomalies);
        }

        var dataStatus = AggregateDataStatus(widgetStatuses);
        var totalFailedWidgetCount = currentBatch.Telemetry.Sum(x => x.FailedWidgetCount)
            + (baselineBatch?.Telemetry.Sum(x => x.FailedWidgetCount) ?? 0);
        var confidence = CalculateConfidence(
            dataStatus,
            facts.Concat(statistics).Sum(x => x.SampleSize),
            missingBaseline,
            entityEvidenceExpected && !entityEvidenceFound,
            totalFailedWidgetCount);
        var deterministicSummary = dataStatus is "NO_DATA" or "ERROR"
            ? "Không đủ dữ liệu trong giai đoạn đã chọn để kết luận."
            : anomalies.Count == 0
                ? "Dữ liệu hiện tại chưa cho thấy bất thường theo các quy tắc backend đã kiểm tra."
                : string.Join(" ", anomalies.Select(x => x.Message).Distinct().Take(5));
        var allEvidence = facts.Concat(statistics).ToList();
        var limitations = BuildLimitations(
            intent.Understanding,
            intent.DataPlan,
            dataStatus,
            missingBaseline,
            allEvidence);
        var evidencePack = BuildEvidencePack(
            intent.Understanding,
            intent.DataPlan,
            dataStatus,
            allEvidence,
            limitations);
        var overview = BuildOverview(chartAnalyses, deterministicSummary);
        var conclusions = BuildConclusions(chartAnalyses, deterministicSummary);
        var analysisContext = BuildAnalysisContext(
            intent.Understanding,
            intent.DataPlan,
            context,
            dataStatus);
        var keyConclusion = BuildKeyConclusion(
            intent.Understanding,
            chartAnalyses,
            allEvidence,
            deterministicSummary,
            dataStatus);
        var explanation = new DashboardExplanationResultDto
        {
            Success = true,
            Summary = deterministicSummary,
            Explanation = deterministicSummary,
            UsedFallback = true
        };
        var primary = allEvidence.FirstOrDefault();
        if (_options.ExplanationEnabled && primary != null && dataStatus is not ("NO_DATA" or "ERROR"))
        {
            explanation = await _ai.ExplainDashboardInsightAsync(
                new DashboardInsightExplanationContextDto
                {
                    AnalysisId = analysisId,
                    Widget = primary.SourceWidget,
                    BusinessIntent = intent.BusinessIntent,
                    DataStatus = dataStatus,
                    Confidence = confidence,
                    FromDate = currentWindow.From,
                    ToDate = currentWindow.To,
                    Comparison = new DashboardComparisonResultDto
                    {
                        CurrentValue = primary.CurrentValue,
                        BaselineValue = primary.BaselineValue,
                        PercentageDifference = primary.DeviationPercent,
                        CurrentSampleSize = primary.SampleSize
                    },
                    Evidence = allEvidence,
                    Context = context,
                    ChartAnalyses = chartAnalyses,
                    Understanding = intent.Understanding,
                    DataPlan = intent.DataPlan,
                    EvidencePack = evidencePack,
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

        var evidenceIds = allEvidence.Select(x => x.EvidenceId).ToHashSet(StringComparer.Ordinal);
        var inferences = ValidateNarratives(explanation.Inferences, evidenceIds);
        var recommendations = ValidateNarratives(explanation.Recommendations, evidenceIds);
        var canRecommend = CanRecommend(intent.Understanding, dataStatus, allEvidence);
        if (canRecommend && recommendations.Count == 0)
            recommendations = BuildRecommendations(chartAnalyses, allEvidence, anomalies);
        if (!canRecommend)
            recommendations = [];
        if (inferences.Count != explanation.Inferences.Count
            || (explanation.Recommendations.Count > 0 && recommendations.Count != explanation.Recommendations.Count))
        {
            warnings.Add("Một số nhận định AI bị loại vì không tham chiếu evidence hợp lệ.");
        }

        var result = new DashboardStructuredAnalysisResultDto
        {
            AnalysisId = analysisId,
            Intent = intent.BusinessIntent,
            OriginalQuestion = intent.Understanding.OriginalQuestion,
            TabCode = intent.Understanding.TabCode,
            AnswerStyleId = intent.Understanding.AnswerStyleId,
            AnswerFocus = intent.Understanding.AnswerFocus,
            FocusType = intent.Understanding.FocusType,
            FocusConfidence = intent.Understanding.FocusConfidence,
            QuestionUnderstanding = intent.Understanding,
            DataPlan = intent.DataPlan,
            EvidencePack = evidencePack,
            AnalysisContext = analysisContext,
            KeyConclusion = keyConclusion,
            DataPeriod = new DashboardDataPeriodResultDto
            {
                From = currentWindow.From,
                To = currentWindow.To,
                ComparisonFrom = baselineWindow?.From,
                ComparisonTo = baselineWindow?.To
            },
            StoreIds = scopedStoreIds.OrderBy(x => x).ToList(),
            Stores = context.Stores,
            FilterFingerprint = context.FilterFingerprint,
            Context = context,
            DataStatus = dataStatus,
            Summary = string.IsNullOrWhiteSpace(explanation.Summary)
                ? deterministicSummary
                : explanation.Summary.Trim(),
            Facts = facts,
            Statistics = statistics,
            Inferences = inferences,
            Anomalies = anomalies,
            Recommendations = recommendations,
            Confidence = confidence,
            Charts = charts,
            PrimaryChart = charts.FirstOrDefault(x =>
                string.Equals(
                    x.WidgetKey,
                    intent.DataPlan.PrimaryWidget.ToString(),
                    StringComparison.OrdinalIgnoreCase)) ?? charts.FirstOrDefault(),
            SupportingCharts = charts.Skip(1).ToList(),
            EvidenceTable = evidencePack.TableEvidence,
            Limitations = limitations,
            Recommendation = recommendations.FirstOrDefault(),
            GeneratedBy = explanation.UsedOllama ? "OllamaGrounded" : "DeterministicFallback",
            ChartAnalyses = MergeChartAnalyses(chartAnalyses, explanation.ChartAnalyses, evidenceIds),
            Overview = explanation.Overview.Count > 0
                ? ValidateNarratives(explanation.Overview, evidenceIds)
                : overview,
            Conclusions = explanation.Conclusions.Count > 0
                ? ValidateNarratives(explanation.Conclusions, evidenceIds)
                : conclusions,
            NotablePoints = explanation.NotablePoints.Count > 0
                ? ValidateNarratives(explanation.NotablePoints, evidenceIds)
                : anomalies.Select(x => new DashboardNarrativeItemDto
                {
                    Text = x.Message,
                    EvidenceIds = x.EvidenceIds
                }).ToList(),
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToList(),
            AiStatus = explanation.UsedOllama ? "Available" : "Fallback",
            FallbackReason = explanation.UsedOllama
                ? null
                : explanation.Warnings.FirstOrDefault() ?? "OLLAMA_UNAVAILABLE_OR_INVALID_RESPONSE",
            UsedFallback = parsed.UsedFallback || explanation.UsedFallback,
            SectionTelemetry = currentBatch.Telemetry.Concat(baselineBatch?.Telemetry ?? []).ToList()
        };
        stopwatch.Stop();
        var failedWidgetCount = result.SectionTelemetry.Sum(item => item.FailedWidgetCount);
        _logger.LogInformation(
            "Dashboard analysis completed. AnalysisId={AnalysisId} StaffId={StaffId} StoreIds={StoreIds} From={From} To={To} Intent={Intent} Sections={Sections} AIStatus={AIStatus} DataStatus={DataStatus} ElapsedMs={ElapsedMs} FallbackReason={FallbackReason} WidgetFailures={WidgetFailures}",
            analysisId,
            actor.StaffId,
            string.Join(",", result.StoreIds),
            currentWindow.From,
            currentWindow.To,
            intent.BusinessIntent,
            string.Join(",", result.SectionTelemetry.Select(item => item.Section).Distinct()),
            result.AiStatus,
            result.DataStatus,
            stopwatch.ElapsedMilliseconds,
            result.FallbackReason,
            failedWidgetCount);
        try
        {
            await _dashboard.WriteAnalysisAuditAsync(actor.StaffId, new DashboardAnalysisAuditDto
            {
                AnalysisId = analysisId,
                From = currentWindow.From,
                To = currentWindow.To,
                StoreIds = result.StoreIds,
                Intent = intent.BusinessIntent.ToString(),
                Sections = result.SectionTelemetry.Select(item => item.Section.ToString()).Distinct().ToList(),
                AiStatus = result.AiStatus,
                DataStatus = result.DataStatus,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                FallbackReason = result.FallbackReason,
                WidgetFailureCount = failedWidgetCount
            }, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Dashboard analysis audit failed. AnalysisId={AnalysisId}",
                analysisId);
        }
        _cache.Set(
            CacheKey(actor.StaffId, result.AnalysisId),
            result,
            TimeSpan.FromMinutes(Math.Clamp(_options.AnalysisCacheMinutes, 1, 60)));
        return result;
    }

    private static bool ContainsTimeExpression(string? prompt)
    {
        var normalized = Normalize(prompt ?? string.Empty);
        return new[] { "hom nay", "hom qua", "ngay", "tuan", "thang", "ky truoc", "tu ", "den " }
            .Any(normalized.Contains);
    }

    private static bool ContainsStoreExpression(string? prompt)
    {
        var normalized = Normalize(prompt ?? string.Empty);
        return normalized.Contains("cua hang") || normalized.Contains("chi nhanh") || normalized.Contains("store ");
    }

    private static DashboardChartAnalysisDto BuildChartAnalysis(
        DashboardAnalyticsWidget widget,
        DashboardAnalyticsResponse current,
        DashboardAnalyticsResponse? baseline,
        DashboardComparisonResultDto comparison,
        DashboardEvidenceDto evidence)
    {
        var definition = DashboardWidgetCatalog.Get(widget);
        var points = JsonRows(current.Rows)
            .Select(row => new ChartPoint(
                Text(row, definition.LabelField),
                Number(row, definition.ValueField)))
            .Where(point => point.Value.HasValue)
            .ToList();
        var first = points.FirstOrDefault();
        var last = points.LastOrDefault();
        var deltas = points.Zip(points.Skip(1), (left, right) => right.Value - left.Value).ToList();
        var trend = points.Count < 2
            ? "Insufficient"
            : deltas.All(delta => delta > 0) ? "Increasing"
            : deltas.All(delta => delta < 0) ? "Decreasing"
            : deltas.All(delta => delta == 0) ? "Stable"
            : last.Value > first.Value ? "MixedIncreasing"
            : last.Value < first.Value ? "MixedDecreasing"
            : "Mixed";
        var highest = points.OrderByDescending(point => point.Value).FirstOrDefault();
        var lowest = points.OrderBy(point => point.Value).FirstOrDefault();
        var facts = new List<string>();
        var chartAnomalies = new List<string>();
        if (EvaluateRowsStatus(current.Rows, current.DataStatus) is "NO_DATA" or "ERROR" || points.Count == 0)
            facts.Add("Chưa có đủ dữ liệu để kết luận cho chỉ số này.");
        else
        {
            facts.Add($"Giá trị tổng hợp trong kỳ: {comparison.CurrentValue:N2}.");
            if (highest.Value.HasValue)
                facts.Add($"Điểm cao nhất: {highest.Label} ({highest.Value:N2}).");
            if (lowest.Value.HasValue)
                facts.Add($"Điểm thấp nhất: {lowest.Label} ({lowest.Value:N2}).");
        }
        if (comparison.PercentageDifference.HasValue)
        {
            if (Math.Abs(comparison.PercentageDifference.Value) >= 20)
                chartAnomalies.Add("Biến động lớn cần theo dõi.");
            facts.Add($"So với kỳ trước: {comparison.PercentageDifference.Value:+0.##;-0.##;0}%.");
        }
        var total = points.Where(point => point.Value.HasValue).Sum(point => point.Value!.Value);
        return new DashboardChartAnalysisDto
        {
            Widget = widget,
            Section = definition.Section,
            Title = definition.Title,
            ChartType = definition.ChartType,
            DataStatus = evidence.DataStatus,
            Summary = string.Join(" ", facts),
            Trend = trend,
            CurrentValue = comparison.CurrentValue,
            BaselineValue = comparison.BaselineValue,
            PercentageDifference = comparison.PercentageDifference,
            ComparisonAvailable = baseline != null && comparison.BaselineValue.HasValue,
            HighestPoint = highest.Label,
            LowestPoint = lowest.Label,
            Facts = facts,
            Anomalies = chartAnomalies,
            Highlights = points.OrderByDescending(point => point.Value).Take(3)
                .Where(point => point.Value.HasValue)
                .Select(point => $"{point.Label}: {point.Value:N2}")
                .ToList(),
            TopEntities = points.OrderByDescending(point => point.Value).Take(5)
                .Where(point => point.Value.HasValue)
                .Select(point => new DashboardEntityContributionDto
                {
                    Entity = point.Label,
                    Value = point.Value!.Value,
                    ContributionPercent = total == 0 ? null : point.Value.Value / total * 100
                }).ToList(),
            Evidence = [evidence],
            Chart = CreateChart(widget, current.Rows)
        };
    }

    private static DashboardChartDto CreateChart(DashboardAnalyticsWidget widget, object rows)
    {
        var definition = DashboardWidgetCatalog.Get(widget);
        return new DashboardChartDto
        {
            Type = definition.ChartType,
            WidgetKey = widget.ToString(),
            Section = definition.Section,
            Title = definition.Title,
            XField = string.IsNullOrWhiteSpace(definition.XField) ? definition.LabelField : definition.XField,
            YField = string.IsNullOrWhiteSpace(definition.YField) ? definition.ValueField : definition.YField,
            ValueField = definition.ValueField,
            SeriesField = definition.SeriesField,
            XUnit = definition.XUnit,
            YUnit = string.IsNullOrWhiteSpace(definition.YUnit) ? definition.Unit : definition.YUnit,
            MinimumRows = definition.MinimumRows,
            FieldLabels = DashboardWidgetCatalog.FieldLabels,
            Rows = rows
        };
    }

    private static DashboardEvidencePackDto BuildEvidencePack(
        DashboardQuestionUnderstandingDto understanding,
        DashboardDataPlanDto plan,
        string dataStatus,
        IReadOnlyList<DashboardEvidenceDto> evidence,
        IReadOnlyList<string> limitations)
    {
        var primary = evidence
            .Where(x => x.SourceWidget == plan.PrimaryWidget)
            .ToList();
        var supporting = evidence
            .Where(x => x.SourceWidget != plan.PrimaryWidget)
            .ToList();
        var table = evidence
            .Where(x => !string.IsNullOrWhiteSpace(x.EntityName))
            .Take(Math.Max(1, plan.Limit))
            .ToList();
        return new DashboardEvidencePackDto
        {
            OriginalQuestion = understanding.OriginalQuestion,
            AnalysisGoal = plan.AnalysisGoal,
            AppliedFilters = new Dictionary<string, string>(
                plan.Filters,
                StringComparer.OrdinalIgnoreCase),
            PrimaryFacts = primary,
            SupportingFacts = supporting,
            ChartEvidence = evidence
                .Where(x => x.Kind.Equals("STATISTIC", StringComparison.OrdinalIgnoreCase))
                .ToList(),
            TableEvidence = table.Count > 0 ? table : primary.Take(plan.Limit).ToList(),
            DataStatus = dataStatus,
            MissingFields = dataStatus is "NO_DATA" or "ERROR"
                ? plan.RequiredFields.ToList()
                : [],
            Limitations = limitations.ToList()
        };
    }

    private static List<string> BuildLimitations(
        DashboardQuestionUnderstandingDto understanding,
        DashboardDataPlanDto plan,
        string dataStatus,
        bool missingBaseline,
        IReadOnlyList<DashboardEvidenceDto> evidence)
    {
        var result = new List<string>();
        if (dataStatus is "NO_DATA" or "ERROR")
            result.Add("Không có đủ dữ liệu trong phạm vi bộ lọc để kết luận.");
        else if (!dataStatus.Equals("OK", StringComparison.OrdinalIgnoreCase))
            result.Add($"Chất lượng dữ liệu hiện tại là {dataStatus}; chỉ nên dùng kết quả để tham khảo.");
        if (understanding.RequiresComparison && missingBaseline)
            result.Add("Thiếu hoặc không đầy đủ dữ liệu kỳ so sánh.");
        if (understanding.AnswerFocus == DashboardAnswerFocus.LowMarginProducts
            && evidence.Any(x => !x.DataStatus.Equals("OK", StringComparison.OrdinalIgnoreCase)))
            result.Add("Không kết luận sản phẩm biên lợi nhuận thấp khi COGS chưa đầy đủ.");
        if (understanding.AnswerFocus == DashboardAnswerFocus.ReorderPriority
            && evidence.Any(x => x.DataStatus is not "OK"))
            result.Add("Không đưa ra hành động nhập hàng khi supplier, quy cách, giá, conversion hoặc lead time chưa hợp lệ.");
        if (understanding.FocusType == DashboardFocusType.Dynamic)
            result.Add($"Trọng tâm động được ánh xạ vào widget {plan.PrimaryWidget}; hệ thống không sinh SQL động.");
        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    private static string BuildAnalysisContext(
        DashboardQuestionUnderstandingDto understanding,
        DashboardDataPlanDto plan,
        DashboardAnalysisContextDto context,
        string dataStatus)
    {
        var stores = context.Stores.Count == 0
            ? "không có cửa hàng"
            : string.Join(", ", context.Stores.Select(x => x.StoreName));
        return $"Phân tích trọng tâm {understanding.AnswerFocus} bằng {plan.PrimaryWidget}"
            + (plan.SupportingWidgets.Count == 0
                ? string.Empty
                : $" và dữ liệu hỗ trợ {string.Join(", ", plan.SupportingWidgets)}")
            + $", trong kỳ {plan.FromDate:dd/MM/yyyy}–{plan.ToDate:dd/MM/yyyy}, phạm vi {stores}. "
            + $"Trạng thái dữ liệu: {dataStatus}.";
    }

    private static string BuildKeyConclusion(
        DashboardQuestionUnderstandingDto understanding,
        IReadOnlyList<DashboardChartAnalysisDto> charts,
        IReadOnlyList<DashboardEvidenceDto> evidence,
        string fallback,
        string dataStatus)
    {
        if (dataStatus is "NO_DATA" or "ERROR")
            return fallback;
        var entity = evidence.FirstOrDefault(x =>
            x.SourceWidget == charts.FirstOrDefault()?.Widget
            && !string.IsNullOrWhiteSpace(x.EntityName));
        if (entity != null)
        {
            var direction = understanding.RankingDirection.Equals("ASC", StringComparison.OrdinalIgnoreCase)
                ? "cần chú ý đầu tiên"
                : "đứng đầu";
            return $"{entity.EntityName} {direction} theo {entity.MetricName}, "
                + $"giá trị {entity.CurrentValue:N2} {entity.Unit} (EvidenceId: {entity.EvidenceId}).";
        }
        var primary = charts.FirstOrDefault();
        if (primary != null && !string.IsNullOrWhiteSpace(primary.Summary))
            return primary.Summary;
        return fallback;
    }

    private static bool CanRecommend(
        DashboardQuestionUnderstandingDto understanding,
        string dataStatus,
        IReadOnlyList<DashboardEvidenceDto> evidence)
    {
        if (!understanding.RequiresRecommendation
            || dataStatus is "NO_DATA" or "ERROR")
            return false;
        if (understanding.AnswerFocus != DashboardAnswerFocus.ReorderPriority)
            return true;
        return evidence.Count > 0
            && evidence.All(x => x.DataStatus.Equals("OK", StringComparison.OrdinalIgnoreCase));
    }

    private static List<DashboardNarrativeItemDto> BuildOverview(
        IReadOnlyList<DashboardChartAnalysisDto> charts,
        string fallback)
    {
        var result = charts.Where(chart => chart.DataStatus is not ("NO_DATA" or "ERROR"))
            .Take(5)
            .Select(chart => new DashboardNarrativeItemDto
            {
                Text = $"{chart.Title}: {chart.Summary}",
                EvidenceIds = chart.Evidence.Select(evidence => evidence.EvidenceId).Distinct().ToList()
            }).ToList();
        if (result.Count == 0)
        {
            var cited = charts.SelectMany(chart => chart.Evidence)
                .Select(item => item.EvidenceId)
                .FirstOrDefault();
            result.Add(new DashboardNarrativeItemDto
            {
                Text = fallback,
                EvidenceIds = cited == null ? [] : [cited]
            });
        }
        return result;
    }

    private static List<DashboardNarrativeItemDto> BuildConclusions(
        IReadOnlyList<DashboardChartAnalysisDto> charts,
        string fallback)
    {
        var result = charts.Where(chart => chart.PercentageDifference.HasValue)
            .OrderByDescending(chart => Math.Abs(chart.PercentageDifference!.Value))
            .Take(3)
            .Select(chart => new DashboardNarrativeItemDto
            {
                Text = $"{chart.Title}: biến động {chart.PercentageDifference:+0.##;-0.##;0}% so với kỳ trước.",
                EvidenceIds = chart.Evidence.Select(evidence => evidence.EvidenceId).ToList()
            }).ToList();
        if (result.Count == 0)
        {
            var cited = charts.SelectMany(chart => chart.Evidence)
                .Select(item => item.EvidenceId)
                .FirstOrDefault();
            result.Add(new DashboardNarrativeItemDto
            {
                Text = fallback,
                EvidenceIds = cited == null ? [] : [cited]
            });
        }
        return result;
    }

    private static List<DashboardNarrativeItemDto> BuildRecommendations(
        IReadOnlyList<DashboardChartAnalysisDto> charts,
        IReadOnlyList<DashboardEvidenceDto> evidence,
        IReadOnlyList<DashboardAnomalyResultDto> anomalies)
    {
        var byId = evidence.ToDictionary(item => item.EvidenceId, StringComparer.Ordinal);
        var result = new List<DashboardNarrativeItemDto>();
        foreach (var anomaly in anomalies.Take(5))
        {
            var cited = anomaly.EvidenceIds.FirstOrDefault(id => byId.ContainsKey(id));
            if (cited == null)
                continue;
            var item = byId[cited];
            result.Add(new DashboardNarrativeItemDto
            {
                Text = item.EntityName == null
                    ? $"Kiểm tra nguyên nhân và phạm vi ảnh hưởng của cảnh báo: {anomaly.Message}"
                    : $"Ưu tiên kiểm tra {item.EntityName}{(item.StoreName == null ? string.Empty : $" tại {item.StoreName}")}.",
                EvidenceIds = [cited],
                Priority = anomaly.Severity.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase) ? "Critical" : "High",
                VerifyCondition = "Xác nhận dữ liệu thực tế và các giao dịch/đơn đang mở trước khi thực hiện nghiệp vụ."
            });
        }
        foreach (var chart in charts.Where(chart => chart.PercentageDifference < 0).Take(3))
        {
            result.Add(new DashboardNarrativeItemDto
            {
                Text = $"Kiểm tra nguyên nhân {chart.Title} giảm {Math.Abs(chart.PercentageDifference!.Value):0.##}% so với kỳ trước.",
                EvidenceIds = chart.Evidence.Select(item => item.EvidenceId).ToList(),
                Priority = "Medium",
                VerifyCondition = "Đối chiếu theo cửa hàng, ngày và khung giờ trước khi điều chỉnh vận hành."
            });
        }
        if (result.Count == 0 && evidence.Count > 0)
        {
            result.Add(new DashboardNarrativeItemDto
            {
                Text = "Tiếp tục theo dõi kỳ kế tiếp; chưa có evidence đủ mạnh để đề xuất thay đổi vận hành.",
                EvidenceIds = [evidence[0].EvidenceId],
                Priority = "Low",
                VerifyCondition = "Chỉ hành động khi có thêm dữ liệu hoặc cảnh báo backend."
            });
        }
        return result;
    }

    private static IReadOnlyList<DashboardAnalyticsWidget> DataPlan(
        DashboardBusinessIntent intent,
        IReadOnlyCollection<string> focusMetrics) => intent switch
    {
        DashboardBusinessIntent.RevenueAnalysis or DashboardBusinessIntent.SalesTrend =>
        [
            DashboardAnalyticsWidget.NetSalesTrend, DashboardAnalyticsWidget.StoreRanking,
            DashboardAnalyticsWidget.OrderHeatmap, DashboardAnalyticsWidget.CategoryPerformance,
            DashboardAnalyticsWidget.ProductPeriodPerformance
        ],
        DashboardBusinessIntent.OrderAnalysis =>
        [
            DashboardAnalyticsWidget.OrderStatusSummary, DashboardAnalyticsWidget.HourlyOrders,
            DashboardAnalyticsWidget.PaymentMethodMix, DashboardAnalyticsWidget.OrderHeatmap
        ],
        DashboardBusinessIntent.ProductPerformance =>
        [
            DashboardAnalyticsWidget.TopProducts, DashboardAnalyticsWidget.ProductPeriodPerformance,
            DashboardAnalyticsWidget.CategoryPerformance, DashboardAnalyticsWidget.VolumeMarginMatrix,
            DashboardAnalyticsWidget.SizeMargin, DashboardAnalyticsWidget.TopToppings,
            DashboardAnalyticsWidget.BomHealth
        ],
        DashboardBusinessIntent.StoreComparison =>
        [
            DashboardAnalyticsWidget.StoreRanking, DashboardAnalyticsWidget.InventoryShortageRisk,
            DashboardAnalyticsWidget.OperationalAlerts
        ],
        DashboardBusinessIntent.InventoryAnalysis =>
        [
            DashboardAnalyticsWidget.InventoryShortageRisk, DashboardAnalyticsWidget.InventoryThresholdRisk,
            DashboardAnalyticsWidget.InventoryMovementByType, DashboardAnalyticsWidget.IngredientConsumptionTrend,
            DashboardAnalyticsWidget.InventoryWasteByStoreIngredient
        ],
        DashboardBusinessIntent.ReorderAnalysis =>
        [
            DashboardAnalyticsWidget.InventoryReorderSuggestions, DashboardAnalyticsWidget.InventoryShortageRisk,
            DashboardAnalyticsWidget.IngredientConsumptionTrend
        ],
        DashboardBusinessIntent.SupplierAnalysis =>
        [
            DashboardAnalyticsWidget.SupplierQuality, DashboardAnalyticsWidget.PurchasePriceTrend,
            DashboardAnalyticsWidget.ProcurementSpendBreakdown, DashboardAnalyticsWidget.SupplierIssueMix,
            DashboardAnalyticsWidget.OverduePurchaseOrders, DashboardAnalyticsWidget.PurchaseOrderPipeline
        ],
        DashboardBusinessIntent.AnomalyDetection =>
        [
            DashboardAnalyticsWidget.OperationalAlerts, DashboardAnalyticsWidget.NetSalesTrend,
            DashboardAnalyticsWidget.InventoryShortageRisk, DashboardAnalyticsWidget.InventoryWasteByStoreIngredient,
            DashboardAnalyticsWidget.SupplierQuality, DashboardAnalyticsWidget.SupplierIssueMix
        ],
        DashboardBusinessIntent.GeneralBusinessSummary =>
        [
            DashboardAnalyticsWidget.NetSalesTrend, DashboardAnalyticsWidget.StoreRanking,
            DashboardAnalyticsWidget.TopProducts, DashboardAnalyticsWidget.CategoryPerformance,
            DashboardAnalyticsWidget.InventoryShortageRisk, DashboardAnalyticsWidget.SupplierQuality,
            DashboardAnalyticsWidget.SupplierIssueMix, DashboardAnalyticsWidget.OverduePurchaseOrders,
            DashboardAnalyticsWidget.PurchaseOrderPipeline, DashboardAnalyticsWidget.OperationalAlerts,
            DashboardAnalyticsWidget.WorkShiftCashDiscrepancy, DashboardAnalyticsWidget.WorkShiftSales,
            DashboardAnalyticsWidget.WorkforceShiftStatus, DashboardAnalyticsWidget.WorkforceHourlyDemand,
            DashboardAnalyticsWidget.WorkforceStaffPerformance
        ],
        _ => FocusPlan(focusMetrics)
    };

    private static IReadOnlyList<DashboardAnalyticsWidget> FocusPlan(
        IReadOnlyCollection<string> focusMetrics)
    {
        var normalized = focusMetrics.Select(item => item.Trim().ToUpperInvariant()).ToHashSet();
        if (normalized.Contains("INVENTORY"))
            return [DashboardAnalyticsWidget.InventoryShortageRisk, DashboardAnalyticsWidget.IngredientConsumptionTrend];
        if (normalized.Contains("PRODUCT"))
            return [DashboardAnalyticsWidget.TopProducts, DashboardAnalyticsWidget.VolumeMarginMatrix];
        if (normalized.Contains("SUPPLIER"))
            return [DashboardAnalyticsWidget.SupplierQuality, DashboardAnalyticsWidget.PurchasePriceTrend];
        if (normalized.Contains("ORDER"))
            return [DashboardAnalyticsWidget.OrderStatusSummary, DashboardAnalyticsWidget.HourlyOrders];
        return [DashboardAnalyticsWidget.NetSalesTrend, DashboardAnalyticsWidget.StoreRanking];
    }

    private static EvidenceBundle BuildWidgetEvidence(
        DashboardAnalyticsWidget widget,
        object rows,
        DashboardComparisonResultDto comparison,
        string dataStatus,
        int top)
    {
        var definition = DashboardWidgetCatalog.Get(widget);
        var total = new DashboardEvidenceDto
        {
            EvidenceId = $"E-{widget}-TOTAL",
            Kind = IsStatisticWidget(widget) ? "STATISTIC" : "FACT",
            SourceWidget = widget,
            WidgetKey = widget.ToString(),
            SectionKey = definition.Section.ToString(),
            Title = definition.Title,
            Description = $"Chỉ số backend của widget {widget}.",
            MetricName = definition.Metric!.Name,
            Statement = dataStatus is "NO_DATA" or "ERROR"
                ? $"{definition.Title}: không đủ dữ liệu trong kỳ."
                : comparison.BaselineValue.HasValue
                    ? $"{definition.Title}: kỳ hiện tại {comparison.CurrentValue:N2}, kỳ so sánh {comparison.BaselineValue:N2}."
                    : $"{definition.Title}: kỳ hiện tại {comparison.CurrentValue:N2}.",
            CurrentValue = comparison.CurrentValue,
            BaselineValue = comparison.BaselineValue,
            Delta = comparison.AbsoluteDifference,
            DeviationPercent = comparison.PercentageDifference,
            SampleSize = comparison.CurrentSampleSize,
            Unit = definition.Metric.Unit,
            DataStatus = dataStatus,
            Baseline = comparison.BaselineValue.HasValue ? "PreviousPeriod" : null
        };
        var facts = new List<DashboardEvidenceDto>();
        var statistics = new List<DashboardEvidenceDto>();
        (IsStatisticWidget(widget) ? statistics : facts).Add(total);

        if (comparison.PercentageDifference.HasValue && !IsStatisticWidget(widget))
        {
            statistics.Add(new DashboardEvidenceDto
            {
                EvidenceId = $"E-{widget}-DELTA",
                Kind = "STATISTIC",
                SourceWidget = widget,
                WidgetKey = widget.ToString(),
                SectionKey = definition.Section.ToString(),
                Title = $"{definition.Title} so với kỳ trước",
                MetricName = "DeltaPercent",
                Statement = $"{definition.Title} biến động {comparison.PercentageDifference:+0.##;-0.##;0}% so với kỳ trước.",
                CurrentValue = comparison.PercentageDifference.Value / 100m,
                BaselineValue = 0,
                Delta = comparison.PercentageDifference.Value / 100m,
                SampleSize = comparison.CurrentSampleSize,
                Unit = "PERCENT",
                DataStatus = dataStatus,
                Baseline = "PreviousPeriod"
            });
        }
        if (widget == DashboardAnalyticsWidget.NetSalesTrend && comparison.CurrentSampleSize > 0)
        {
            var aov = comparison.CurrentValue / comparison.CurrentSampleSize;
            statistics.Add(new DashboardEvidenceDto
            {
                EvidenceId = $"E-{widget}-AOV",
                Kind = "STATISTIC",
                SourceWidget = widget,
                WidgetKey = widget.ToString(),
                SectionKey = definition.Section.ToString(),
                Title = "Giá trị đơn hàng trung bình",
                MetricName = "AverageOrderValue",
                Statement = $"Giá trị đơn hàng trung bình: {aov:N0} VND.",
                CurrentValue = aov,
                SampleSize = comparison.CurrentSampleSize,
                Unit = "VND",
                DataStatus = dataStatus
            });
        }

        var entityIndex = 0;
        foreach (var row in JsonRows(rows).Take(Math.Clamp(top, 1, 20)))
        {
            var entity = BuildEntityEvidence(widget, row, dataStatus, ++entityIndex);
            if (entity != null)
                facts.Add(entity);
        }
        return new EvidenceBundle(facts, statistics);
    }

    private static DashboardEvidenceDto? BuildEntityEvidence(
        DashboardAnalyticsWidget widget,
        JsonElement row,
        string dataStatus,
        int index)
    {
        var definition = DashboardWidgetCatalog.Get(widget);
        string? entityType = null;
        string? entityId = null;
        string? entityCode = null;
        string? entityName = null;
        int? storeId = null;
        string? storeName = null;
        decimal value;
        string unit = definition.Unit;
        string? priority = null;
        string? risk = null;

        switch (widget)
        {
            case DashboardAnalyticsWidget.StoreRanking:
                entityType = "STORE"; entityId = Text(row, "storeId"); entityName = Text(row, "storeName");
                storeId = NullableInt(row, "storeId"); storeName = entityName; value = Number(row, "netSales") ?? 0; break;
            case DashboardAnalyticsWidget.TopProducts:
            case DashboardAnalyticsWidget.ProductPeriodPerformance:
            case DashboardAnalyticsWidget.LowVolumeProducts:
            case DashboardAnalyticsWidget.LowMarginProducts:
            case DashboardAnalyticsWidget.VolumeMarginMatrix:
                entityType = "PRODUCT"; entityId = Text(row, "drinkId"); entityName = Text(row, "drinkName");
                value = Number(row, definition.ValueField) ?? 0; break;
            case DashboardAnalyticsWidget.CategoryPerformance:
                entityType = "CATEGORY"; entityId = Text(row, "categoryId"); entityName = Text(row, "categoryName");
                value = Number(row, "totalSold") ?? 0; break;
            case DashboardAnalyticsWidget.SizeMargin:
                entityType = "SIZE"; entityId = Text(row, "sizeId"); entityName = Text(row, "sizeName");
                value = Number(row, "confirmedGrossProfit") ?? 0; break;
            case DashboardAnalyticsWidget.TopToppings:
                entityType = "TOPPING"; entityId = Text(row, "toppingId"); entityName = Text(row, "toppingName");
                value = Number(row, "revenue") ?? 0; break;
            case DashboardAnalyticsWidget.BomHealth:
                entityType = "PRODUCT"; entityId = Text(row, "drinkId"); entityCode = Text(row, "drinkCode");
                entityName = Text(row, "drinkName"); value = Number(row, "bomIssueCount") ?? 0; break;
            case DashboardAnalyticsWidget.PaymentMethodMix:
                entityType = "PAYMENT_METHOD"; entityId = Text(row, "paymentMethodId");
                entityCode = Text(row, "paymentMethodCode"); entityName = Text(row, "paymentMethodName");
                value = Number(row, "totalTransactions") ?? 0; break;
            case DashboardAnalyticsWidget.InventoryShortageRisk:
                value = Number(row, "shortageQuantity") ?? 0;
                if (value <= 0) return null;
                entityType = "INGREDIENT"; entityId = Text(row, "ingredientId");
                entityCode = Text(row, "ingredientCode"); entityName = Text(row, "ingredientName");
                storeId = NullableInt(row, "storeId"); storeName = Text(row, "storeName");
                unit = Text(row, "unit"); risk = Text(row, "riskLevel");
                priority = risk.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase) ? "Critical" : "High";
                break;
            case DashboardAnalyticsWidget.InventoryReorderSuggestions:
                entityType = "INGREDIENT"; entityId = Text(row, "ingredientId");
                entityCode = Text(row, "ingredientCode"); entityName = Text(row, "ingredientName");
                storeId = NullableInt(row, "storeId"); storeName = Text(row, "storeName");
                value = Number(row, "finalSuggestedQuantity")
                    ?? Number(row, "suggestedQuantity")
                    ?? Number(row, "requestedQuantity")
                    ?? 0;
                unit = Text(row, "unit"); priority = NormalizePriority(Text(row, "priority")); break;
            case DashboardAnalyticsWidget.SupplierQuality:
                entityType = "SUPPLIER"; entityId = Text(row, "supplierId"); entityName = Text(row, "supplierName");
                value = Number(row, "rejectionRate") ?? 0; break;
            case DashboardAnalyticsWidget.SupplierIssueMix:
                entityType = "SUPPLIER"; entityId = Text(row, "supplierId"); entityName = Text(row, "supplierName");
                storeId = NullableInt(row, "storeId"); storeName = Text(row, "storeName");
                value = Number(row, "issueCount") ?? 0; priority = "High"; break;
            case DashboardAnalyticsWidget.OperationalAlerts:
                entityType = Text(row, "entityType"); entityId = Text(row, "entityId");
                entityCode = Text(row, "entityCode"); entityName = Text(row, "entityName");
                storeId = NullableInt(row, "storeId"); storeName = Text(row, "storeName");
                value = Number(row, "alertValue") ?? 0; unit = Text(row, "unit");
                priority = NormalizePriority(Text(row, "severity")); risk = Text(row, "severity"); break;
            case DashboardAnalyticsWidget.OverduePurchaseOrders:
                entityType = "PURCHASE_ORDER"; entityId = Text(row, "purchaseOrderId");
                entityCode = Text(row, "code"); entityName = Text(row, "code");
                storeId = NullableInt(row, "storeId"); storeName = Text(row, "storeName");
                value = Number(row, "overdueDays") ?? 0;
                priority = value >= 7 ? "High" : "Medium"; break;
            default:
                return null;
        }

        if (string.IsNullOrWhiteSpace(entityName))
            return null;
        var evidence = new DashboardEvidenceDto
        {
            EvidenceId = $"E-{widget}-{index:000}",
            Kind = IsStatisticWidget(widget) ? "STATISTIC" : "FACT",
            SourceWidget = widget,
            WidgetKey = widget.ToString(),
            SectionKey = definition.Section.ToString(),
            Title = $"{definition.Title}: {entityName}",
            Description = "Evidence cấp thực thể từ backend.",
            MetricName = definition.Metric!.Name,
            Statement = $"{entityName}: {value:N2} {unit}.",
            CurrentValue = value,
            SampleSize = EntitySample(widget, row),
            Unit = string.IsNullOrWhiteSpace(unit) ? definition.Unit : unit,
            DataStatus = dataStatus,
            EntityType = entityType,
            EntityId = entityId,
            EntityCode = entityCode,
            EntityName = entityName,
            StoreId = storeId,
            StoreName = storeName,
            Priority = priority,
            RiskLevel = risk
        };
        foreach (var key in EntityMetadataFields(widget))
            if (row.TryGetProperty(key, out var metadata))
                evidence.Metadata[key] = JsonValue(metadata);
        return evidence;
    }

    private static IEnumerable<string> EntityMetadataFields(DashboardAnalyticsWidget widget) => widget switch
    {
        DashboardAnalyticsWidget.StoreRanking =>
            ["totalOrders", "averageOrderValue", "rank", "contributionPercent", "dataStatus"],
        DashboardAnalyticsWidget.TopProducts =>
            ["categoryName", "totalSold", "productRevenue", "confirmedCogs", "confirmedGrossProfit", "confirmedMarginRate", "contributionPercent", "dataStatus"],
        DashboardAnalyticsWidget.ProductPeriodPerformance =>
            ["totalSold", "revenue", "confirmedCogs", "confirmedGrossProfit", "confirmedMarginRate", "contributionPercent", "dataStatus"],
        DashboardAnalyticsWidget.LowVolumeProducts or DashboardAnalyticsWidget.LowMarginProducts =>
            ["totalSold", "revenue", "confirmedCogs", "confirmedGrossProfit", "confirmedMarginRate", "contributionPercent", "dataStatus"],
        DashboardAnalyticsWidget.CategoryPerformance =>
            ["totalSold", "revenue", "confirmedCogs", "confirmedGrossProfit", "confirmedMarginRate", "contributionPercent", "dataStatus"],
        DashboardAnalyticsWidget.VolumeMarginMatrix =>
            ["volume", "revenue", "confirmedCogs", "confirmedMarginRate", "dataStatus"],
        DashboardAnalyticsWidget.SizeMargin =>
            ["totalSold", "revenue", "confirmedCogs", "confirmedGrossProfit", "dataStatus"],
        DashboardAnalyticsWidget.TopToppings =>
            ["totalUsed", "revenue", "confirmedCogs", "dataStatus"],
        DashboardAnalyticsWidget.BomHealth =>
            ["recipeCount", "recipeLineCount", "invalidLineCount", "bomIssueCount", "dataStatus"],
        DashboardAnalyticsWidget.PaymentMethodMix =>
            ["totalTransactions", "amount", "transactionShare", "revenueShare", "dataStatus"],
        DashboardAnalyticsWidget.InventoryShortageRisk =>
            ["onHandQuantity", "reservedQuantity", "availableQuantity", "minimumStock", "shortageQuantity", "suggestedReorderQuantity", "dataStatus"],
        DashboardAnalyticsWidget.InventoryReorderSuggestions =>
            ["onHandQuantity", "reservedQuantity", "availableQuantity", "minimumStock", "shortageQuantity", "requestedQuantity", "suggestedQuantity", "status", "dataStatus"],
        DashboardAnalyticsWidget.SupplierQuality =>
            ["acceptedBaseQuantity", "rejectedBaseQuantity", "rejectionRate", "receiptCount", "dataStatus"],
        DashboardAnalyticsWidget.SupplierIssueMix =>
            ["issueType", "status", "issueCount", "affectedBaseQuantity", "dataStatus"],
        DashboardAnalyticsWidget.OperationalAlerts =>
            ["alertType", "severity", "message", "dataStatus"],
        DashboardAnalyticsWidget.OverduePurchaseOrders =>
            ["supplierId", "supplierName", "expectedDeliveryAtUtc", "overdueDays", "orderedValue", "status", "dataStatus"],
        _ => ["dataStatus"]
    };

    private static void AddBackendAnomalies(
        DashboardAnalyticsWidget widget,
        EvidenceBundle bundle,
        ICollection<DashboardAnomalyResultDto> anomalies)
    {
        foreach (var evidence in bundle.Facts.Concat(bundle.Statistics))
        {
            if (widget == DashboardAnalyticsWidget.OperationalAlerts && evidence.EntityName != null)
            {
                anomalies.Add(new DashboardAnomalyResultDto
                {
                    Code = evidence.Metadata.TryGetValue("alertType", out var type) ? type?.ToString() ?? "OPERATIONAL_ALERT" : "OPERATIONAL_ALERT",
                    Severity = evidence.RiskLevel ?? "WARNING",
                    Message = evidence.Metadata.TryGetValue("message", out var message) ? message?.ToString() ?? evidence.Statement : evidence.Statement,
                    EvidenceIds = [evidence.EvidenceId]
                });
            }
            else if (widget == DashboardAnalyticsWidget.InventoryShortageRisk && evidence.EntityName != null)
            {
                anomalies.Add(new DashboardAnomalyResultDto
                {
                    Code = "LOW_STOCK",
                    Severity = evidence.RiskLevel ?? "WARNING",
                    Message = $"{evidence.EntityName} tại {evidence.StoreName} thiếu {evidence.CurrentValue:N2} {evidence.Unit}.",
                    EvidenceIds = [evidence.EvidenceId]
                });
            }
            else if (widget == DashboardAnalyticsWidget.SupplierIssueMix && evidence.EntityName != null)
            {
                anomalies.Add(new DashboardAnomalyResultDto
                {
                    Code = "SUPPLIER_ISSUE",
                    Severity = "WARNING",
                    Message = $"Nhà cung cấp {evidence.EntityName} có sự cố cần kiểm tra.",
                    EvidenceIds = [evidence.EvidenceId]
                });
            }
            else if (widget == DashboardAnalyticsWidget.OverduePurchaseOrders && evidence.EntityName != null)
            {
                anomalies.Add(new DashboardAnomalyResultDto
                {
                    Code = "OVERDUE_PO",
                    Severity = evidence.Priority == "High" ? "WARNING" : "INFO",
                    Message = $"PO {evidence.EntityName} quá hạn {evidence.CurrentValue:N0} ngày.",
                    EvidenceIds = [evidence.EvidenceId]
                });
            }
        }
        if ((widget is DashboardAnalyticsWidget.TopProducts
                or DashboardAnalyticsWidget.VolumeMarginMatrix
                or DashboardAnalyticsWidget.ProductPeriodPerformance
                or DashboardAnalyticsWidget.CategoryPerformance
                or DashboardAnalyticsWidget.SizeMargin
                or DashboardAnalyticsWidget.TopToppings)
            && bundle.Facts.Concat(bundle.Statistics).Any(item => item.DataStatus == "PARTIAL_COGS"))
        {
            var cited = bundle.Facts.Concat(bundle.Statistics).First().EvidenceId;
            anomalies.Add(new DashboardAnomalyResultDto
            {
                Code = "PARTIAL_COGS",
                Severity = "WARNING",
                Message = "COGS chưa đầy đủ; phân tích lợi nhuận chỉ mang tính tham khảo.",
                EvidenceIds = [cited]
            });
        }
    }

    private static string EvaluateWidgetStatus(
        DashboardAnalyticsResponse current,
        DashboardAnalyticsResponse? baseline,
        bool comparisonRequested)
    {
        var currentStatus = EvaluateRowsStatus(current.Rows, current.DataStatus);
        if (currentStatus is "NO_DATA" or "ERROR")
            return currentStatus;
        if (comparisonRequested
            && DashboardWidgetCatalog.Get(current.Widget).SupportsComparison
            && (baseline == null || EvaluateRowsStatus(baseline.Rows, baseline.DataStatus) != "OK"))
            return currentStatus == "OK" ? "PARTIAL" : currentStatus;
        return currentStatus;
    }

    private static string EvaluateRowsStatus(object rows, string responseStatus)
    {
        if (responseStatus.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
            return "ERROR";
        var data = JsonRows(rows);
        if (data.Count == 0)
            return "NO_DATA";
        var statuses = data
            .Select(row => Text(row, "dataStatus").Trim().ToUpperInvariant())
            .Select(status => status switch
            {
                "" or "NO_DATA" => "NO_DATA",
                "AVAILABLE" or "OK" => "OK",
                "PARTIAL" => "PARTIAL",
                "PARTIAL_COGS" => "PARTIAL_COGS",
                "THRESHOLD_NOT_CONFIGURED" or "UNCONFIGURED" or "MISSING_BOM" or "MISSING_CONFIG"
                    => "MISSING_CONFIG",
                "ERROR" => "ERROR",
                _ => "PARTIAL"
            })
            .ToList();
        if (statuses.All(status => status is "" or "NO_DATA"))
            return "NO_DATA";
        if (statuses.All(status => status == "ERROR"))
            return "ERROR";
        if (statuses.Any(status => status == "PARTIAL_COGS"))
            return "PARTIAL_COGS";
        if (statuses.Any(status => status == "MISSING_CONFIG"))
            return "MISSING_CONFIG";
        if (statuses.Any(status => status is "PARTIAL" or "ERROR" or "NO_DATA"))
            return "PARTIAL";
        return "OK";
    }

    private static string AggregateDataStatus(IReadOnlyCollection<string> statuses)
    {
        if (statuses.Count == 0 || statuses.All(status => status == "NO_DATA"))
            return "NO_DATA";
        if (statuses.All(status => status is "NO_DATA" or "ERROR")
            && statuses.Any(status => status == "ERROR"))
            return "ERROR";
        if (statuses.All(status => status == "OK"))
            return "OK";
        var degraded = statuses.Where(status => status != "OK").Distinct().ToList();
        if (degraded.Count == 1 && degraded[0] == "PARTIAL_COGS")
            return "PARTIAL_COGS";
        if (degraded.Count == 1 && degraded[0] == "MISSING_CONFIG")
            return "MISSING_CONFIG";
        return "PARTIAL";
    }

    private static decimal CalculateConfidence(
        string dataStatus,
        long sampleSize,
        bool missingBaseline,
        bool missingEntityEvidence,
        int failedWidgets)
    {
        if (dataStatus is "NO_DATA" or "ERROR")
            return 0m;
        var confidence = dataStatus == "OK" ? 0.85m : 0.60m;
        if (sampleSize < 10) confidence -= 0.08m;
        if (missingBaseline) confidence -= 0.07m;
        if (missingEntityEvidence) confidence -= 0.08m;
        confidence -= Math.Min(0.20m, failedWidgets * 0.05m);
        return Math.Clamp(confidence, 0m, 0.90m);
    }

    private static bool IsStatisticWidget(DashboardAnalyticsWidget widget) =>
        widget is DashboardAnalyticsWidget.OrderStatusSummary
            or DashboardAnalyticsWidget.SupplierQuality
            or DashboardAnalyticsWidget.VolumeMarginMatrix;

    private static bool RequiresEntityEvidence(DashboardAnalyticsWidget widget) =>
        widget is DashboardAnalyticsWidget.StoreRanking
            or DashboardAnalyticsWidget.TopProducts
            or DashboardAnalyticsWidget.ProductPeriodPerformance
            or DashboardAnalyticsWidget.LowVolumeProducts
            or DashboardAnalyticsWidget.LowMarginProducts
            or DashboardAnalyticsWidget.CategoryPerformance
            or DashboardAnalyticsWidget.SizeMargin
            or DashboardAnalyticsWidget.TopToppings
            or DashboardAnalyticsWidget.BomHealth
            or DashboardAnalyticsWidget.InventoryShortageRisk
            or DashboardAnalyticsWidget.InventoryReorderSuggestions
            or DashboardAnalyticsWidget.SupplierQuality
            or DashboardAnalyticsWidget.SupplierIssueMix
            or DashboardAnalyticsWidget.PaymentMethodMix
            or DashboardAnalyticsWidget.OperationalAlerts
            or DashboardAnalyticsWidget.OverduePurchaseOrders;

    private static long EntitySample(DashboardAnalyticsWidget widget, JsonElement row) => widget switch
    {
        DashboardAnalyticsWidget.StoreRanking => Long(row, "totalOrders"),
        DashboardAnalyticsWidget.TopProducts or DashboardAnalyticsWidget.ProductPeriodPerformance
            or DashboardAnalyticsWidget.LowVolumeProducts or DashboardAnalyticsWidget.LowMarginProducts
            or DashboardAnalyticsWidget.CategoryPerformance => Long(row, "totalSold"),
        DashboardAnalyticsWidget.SupplierQuality => Long(row, "receiptCount"),
        DashboardAnalyticsWidget.PaymentMethodMix => Long(row, "totalTransactions"),
        DashboardAnalyticsWidget.SupplierIssueMix => Long(row, "issueCount"),
        DashboardAnalyticsWidget.SizeMargin => Long(row, "totalSold"),
        DashboardAnalyticsWidget.TopToppings => Long(row, "totalUsed"),
        DashboardAnalyticsWidget.OperationalAlerts => Long(row, "alertCount"),
        DashboardAnalyticsWidget.OverduePurchaseOrders => 1,
        _ => 0
    };

    private static string NormalizePriority(string value) => value.Trim().ToUpperInvariant() switch
    {
        "CRITICAL" or "URGENT" => "Critical",
        "HIGH" or "WARNING" => "High",
        "MEDIUM" => "Medium",
        _ => "Low"
    };

    private static IReadOnlyList<JsonElement> JsonRows(object rows)
    {
        var json = JsonSerializer.SerializeToElement(rows, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return json.ValueKind == JsonValueKind.Array ? json.EnumerateArray().ToList() : [];
    }

    private static decimal? Number(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.TryGetDecimal(out var number) ? number : null;

    private static int? NullableInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;

    private static string Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind is not JsonValueKind.Null
            ? value.ToString()
            : string.Empty;

    private static object? JsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => value.ToString()
    };

    private static List<DashboardChartAnalysisDto> MergeChartAnalyses(
        IReadOnlyList<DashboardChartAnalysisDto> deterministic,
        IReadOnlyList<DashboardChartAnalysisDto> ai,
        IReadOnlySet<string> evidenceIds)
    {
        var byWidget = ai
            .Where(item => item.Evidence.All(evidence => evidenceIds.Contains(evidence.EvidenceId)))
            .GroupBy(item => item.Widget)
            .ToDictionary(group => group.Key, group => group.First());
        return deterministic.Select(item =>
        {
            if (byWidget.TryGetValue(item.Widget, out var narrative)
                && !string.IsNullOrWhiteSpace(narrative.Summary))
                item.Summary = narrative.Summary.Trim();
            return item;
        }).ToList();
    }

    private static List<DashboardNarrativeItemDto> ValidateNarratives(
        IEnumerable<DashboardNarrativeItemDto> items,
        IReadOnlySet<string> evidenceIds)
    {
        var priorities = new HashSet<string>(["Critical", "High", "Medium", "Low"], StringComparer.OrdinalIgnoreCase);
        return items
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.Text)
                && item.EvidenceIds.Count > 0
                && item.EvidenceIds.All(evidenceIds.Contains)
                && (item.Priority == null || priorities.Contains(item.Priority)))
            .Select(item => new DashboardNarrativeItemDto
            {
                Text = item.Text.Trim(),
                EvidenceIds = item.EvidenceIds.Distinct(StringComparer.Ordinal).ToList(),
                Priority = item.Priority == null ? null : NormalizePriority(item.Priority),
                VerifyCondition = item.VerifyCondition?.Trim()
            })
            .ToList();
    }

    private readonly record struct ChartPoint(string Label, decimal? Value);
    private sealed record EvidenceBundle(
        IReadOnlyList<DashboardEvidenceDto> Facts,
        IReadOnlyList<DashboardEvidenceDto> Statistics);
}
