using System.Text.Json;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Analytics;
using CafeChain.Models.Analytics;
using CafeChain.Models.Operations;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Application.Interfaces.Systems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CafeChain.Data;
using CafeChain.Models.Inventories.Auditing;

namespace CafeChain.Application.Services.AI;

public sealed class AnomalyDetectionService : IAnomalyDetectionService
{
    private readonly IAnomalyDetectionRepository _repository;
    private readonly IInventoryReorderNotificationRepository _notifications;
    private readonly IAdminPermissionService _permissions;
    private readonly IScopeAuthorizationService _scope;
    private readonly AnomalyDetectionOptions _options;
    private readonly IBusinessDateService _businessDate;
    private readonly AppDbContext? _context;
    public AnomalyDetectionService(IAnomalyDetectionRepository repository, IInventoryReorderNotificationRepository notifications,
        IAdminPermissionService permissions, IScopeAuthorizationService scope, IOptions<AnomalyDetectionOptions> options,
        IBusinessDateService businessDate,
        AppDbContext? context = null)
    { _repository = repository; _notifications = notifications; _permissions = permissions; _scope = scope; _options = options.Value; _businessDate = businessDate; _context = context; }

    public async Task AnalyzeStoreAsync(int storeId, CancellationToken ct = default)
    {
        if (!_options.IsEnabledForStore(storeId)) return;
        var targetDate = _businessDate.Today.AddDays(-1);
        var from = targetDate.AddDays(-_options.AnalysisWindowDays);
        var metrics = await _repository.GetDailyOperationalMetricsAsync(storeId, from, targetDate.AddDays(1), ct);
        foreach (var metric in metrics)
            await AnalyzeMetricAsync(storeId, metric.Key, metric.Value, from, targetDate, ct);
    }

    private async Task AnalyzeMetricAsync(int storeId, string metricCode, IReadOnlyList<DailyMetricPoint> data, DateTime from, DateTime targetDate, CancellationToken ct)
    {
        var byDate = data.GroupBy(x => x.Date.Date).ToDictionary(x => x.Key, x => x.Sum(y => y.Value));
        var historyFrom = targetDate.AddDays(-_options.AnalysisWindowDays);
        if (!byDate.TryGetValue(targetDate, out var current)) return;
        var history = data.Where(x => x.Date.Date >= historyFrom && x.Date.Date < targetDate)
            .Select(x => x.Value).ToList();
        if (history.Count < _options.MinimumSampleCount) return;
        var sameWeekday = data.Where(x => x.Date.Date >= historyFrom && x.Date.Date < targetDate
            && x.Date.DayOfWeek == targetDate.DayOfWeek).Select(x => x.Value).ToList();
        var baselineValues = sameWeekday.Count >= 4 ? sameWeekday : history.TakeLast(14).ToList();
        var median = Median(baselineValues); var mad = Median(baselineValues.Select(x => Math.Abs(x - median)).ToList());
        var robust = mad <= 0 ? 0 : .6745m * (current - median) / mad;
        var absolute = current - median; var percentage = median == 0 ? (current == 0 ? 0 : 1) : absolute / median;
        var minimumAbsolute = metricCode == "REVENUE" ? _options.MinimumAbsoluteRevenueDeviation
            : metricCode == "CASH_DISCREPANCY" ? _options.MinimumAbsoluteCashDeviation : 1m;
        var isDropOnly = metricCode.StartsWith("PRODUCT_VOLUME:", StringComparison.Ordinal);
        var isAnomaly = (!isDropOnly || absolute < 0) && Math.Abs(absolute) >= minimumAbsolute
            && Math.Abs(percentage) >= _options.MinimumPercentageDeviation && Math.Abs(robust) >= _options.RobustScoreThreshold;
        var key = $"{targetDate:yyyyMMdd}:{_options.DetectionVersion}"; var row = await _repository.GetByKeyAsync(storeId, metricCode, key, ct);
        if (!isAnomaly)
        {
            return;
        }
        var severity = Math.Abs(robust) >= _options.CriticalRobustScoreThreshold || Math.Abs(percentage) >= _options.CriticalPercentageDeviation ? "CRITICAL" : "HIGH";
        if (row == null) { row = new OperationalAnomaly { StoreId = storeId, MetricCode = metricCode, PeriodKey = key, BusinessDate = targetDate, DetectionVersion = _options.DetectionVersion, CreatedAtUtc = DateTime.UtcNow }; await _repository.AddAsync(row, ct); }
        row.CurrentValue = current; row.BaselineValue = median; row.AbsoluteDeviation = absolute; row.PercentageDeviation = percentage;
        row.RobustScore = robust; row.WindowFromUtc = historyFrom; row.WindowToExclusiveUtc = targetDate.AddDays(1); row.SampleCount = history.Count;
        row.Severity = severity; row.Confidence = sameWeekday.Count >= 4 ? "HIGH" : "MEDIUM";
        row.ReasonCodesJson = JsonSerializer.Serialize(new[] { absolute < 0 ? "BELOW_SEASONAL_BASELINE" : "ABOVE_SEASONAL_BASELINE", "MATERIAL_DEVIATION", "ROBUST_SCORE_EXCEEDED" });
        row.UpdatedAtUtc = DateTime.UtcNow; await _repository.SaveChangesAsync(ct);
        if (!_options.ShadowMode) await SyncNotificationsAsync(storeId, metricCode, row, ct);
    }

    public async Task<IReadOnlyList<OperationalAnomalyDto>> GetOpenAsync(AdminActorContext actor, int storeId, CancellationToken ct = default)
    {
        await EnsurePermission(actor, storeId, PermissionConstants.OperationalAnomalyView);
        await EnsureScope(actor, storeId); var rows = await _repository.GetOpenAsync(storeId, ct); return rows.Select(Map).ToList();
    }

    public async Task<AnomalyExplanationContextDto> GetExplanationContextAsync(AdminActorContext actor, int anomalyId, CancellationToken ct = default)
    {
        var authorizedStoreId = await _repository.GetStoreIdAsync(anomalyId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tín hiệu bất thường.");
        await EnsurePermission(actor, authorizedStoreId, PermissionConstants.OperationalAnomalyView);
        await EnsureScope(actor, authorizedStoreId);
        var row = await _repository.GetByIdAsync(anomalyId, ct) ?? throw new KeyNotFoundException("Không tìm thấy tín hiệu bất thường.");
        await EnsureScope(actor, row.StoreId);
        var reasonCodes = JsonSerializer.Deserialize<string[]>(row.ReasonCodesJson) ?? [];
        var presentation = OperationalAnomalyPresentation.Build(
            row.MetricCode,
            row.CurrentValue,
            row.BaselineValue,
            row.PercentageDeviation,
            row.Severity,
            row.Status,
            row.Confidence,
            reasonCodes);
        return new AnomalyExplanationContextDto
        {
            AnomalyId = row.OperationalAnomalyId,
            MetricCode = row.MetricCode,
            CurrentValue = row.CurrentValue,
            BaselineValue = row.BaselineValue,
            RobustScore = row.RobustScore,
            ReasonCodes = reasonCodes,
            MetricDisplayName = presentation.MetricDisplayName,
            CurrentValueDisplay = presentation.CurrentValueDisplay,
            BaselineValueDisplay = presentation.BaselineValueDisplay,
            DirectionDescription = presentation.DirectionDescription,
            ReasonSummaries = presentation.ReasonSummaries,
            SuggestedChecks = presentation.SuggestedChecks,
            PercentageDeviation = row.PercentageDeviation,
            AbsolutePercentageDeviation = Math.Abs(row.PercentageDeviation),
            PercentageDeviationDisplay = presentation.DeviationDisplay,
            ImpactSummary = presentation.ImpactSummary,
            WhyDetected = presentation.WhyDetected,
            ImmediateActions = presentation.ImmediateActions,
            PreparationChecklist = presentation.PreparationChecklist
        };
    }

    public async Task<AnomalyFeedbackResultDto> RecordFeedbackAsync(AdminActorContext actor, AnomalyFeedbackDto input, CancellationToken ct = default)
    {
        var requestedAction = input.Action.Trim().ToUpperInvariant();
        var requiredPermission = requestedAction switch
        {
            "ACKNOWLEDGE" => PermissionConstants.OperationalAnomalyAcknowledge,
            "RESOLVE" => PermissionConstants.OperationalAnomalyResolve,
            "FEEDBACK" => PermissionConstants.OperationalAnomalyFeedback,
            _ => throw new ArgumentException("Hành động không hợp lệ.")
        };
        var authorizedStoreId = await _repository.GetStoreIdAsync(input.Id, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tín hiệu bất thường.");
        await EnsurePermission(actor, authorizedStoreId, requiredPermission);
        await EnsureScope(actor, authorizedStoreId);
        var row = await _repository.GetByIdAsync(input.Id, ct) ?? throw new KeyNotFoundException("Không tìm thấy tín hiệu bất thường.");
        await EnsureScope(actor, row.StoreId);
        if (!Convert.ToBase64String(row.RowVersion).Equals(input.RowVersion, StringComparison.Ordinal)) throw new DbUpdateConcurrencyException("Tín hiệu đã được cập nhật. Vui lòng tải lại.");
        var action = requestedAction;
        if (action == "ACKNOWLEDGE" && row.Status != "OPEN")
            throw new ArgumentException("Chỉ anomaly OPEN mới có thể acknowledge.");
        if (action == "RESOLVE" && row.Status is not "OPEN" and not "ACKNOWLEDGED")
            throw new ArgumentException("Anomaly không còn ở trạng thái có thể resolve.");
        if (action == "FEEDBACK" && input.Feedback?.Trim() is not ("Useful" or "NotUseful" or "FalsePositive"))
            throw new ArgumentException("Feedback phải là Useful, NotUseful hoặc FalsePositive.");
        var previousStatus = row.Status;
        if (action == "ACKNOWLEDGE") { row.Status = "ACKNOWLEDGED"; row.AcknowledgedAtUtc = DateTime.UtcNow; row.AcknowledgedByStaffId = actor.StaffId; }
        else if (action == "RESOLVE") { row.Status = "RESOLVED"; row.ResolvedAtUtc = DateTime.UtcNow; row.ResolvedByStaffId = actor.StaffId; row.ResolutionNote = input.Note?.Trim(); }
        else if (action != "FEEDBACK") throw new ArgumentException("Hành động không hợp lệ.");
        row.Feedback = string.IsNullOrWhiteSpace(input.Feedback) ? row.Feedback : input.Feedback.Trim(); row.FeedbackByStaffId = actor.StaffId; row.UpdatedAtUtc = DateTime.UtcNow;
        if (action == "FEEDBACK") row.FeedbackNote = input.Note?.Trim();
        _context?.AuditLogs.Add(new AuditLog
        {
            TableName = "OperationalAnomaly",
            RecordId = row.OperationalAnomalyId,
            Action = action,
            UserId = actor.StaffId,
            CreatedAt = DateTime.UtcNow,
            OldData = JsonSerializer.Serialize(new { Status = previousStatus }),
            NewData = JsonSerializer.Serialize(new { row.Status, row.Feedback, Note = input.Note })
        });
        await _repository.SaveChangesAsync(ct);
        return new AnomalyFeedbackResultDto
        {
            Id = row.OperationalAnomalyId,
            Feedback = row.Feedback ?? string.Empty,
            Note = row.FeedbackNote,
            RowVersion = Convert.ToBase64String(row.RowVersion),
            UpdatedAtUtc = row.UpdatedAtUtc,
            FeedbackDisplay = row.Feedback switch
            {
                "Useful" => "Đã ghi nhận: Hữu ích",
                "NotUseful" => "Đã ghi nhận: Chưa hữu ích",
                "FalsePositive" => "Đã ghi nhận: Cảnh báo không phù hợp",
                _ => "Đã ghi nhận phản hồi"
            }
        };
    }

    private async Task EnsureScope(AdminActorContext actor, int storeId) { if (actor.StaffId <= 0 || !await _scope.CanAccessStoreAsync(actor.StaffId, storeId)) throw new UnauthorizedAccessException("Bạn không có quyền truy cập cửa hàng này."); }
    private async Task EnsurePermission(AdminActorContext actor, int storeId, string code)
    {
        if (actor.AccountId <= 0) throw new UnauthorizedAccessException("Thiếu account context.");
        var permission = await _permissions.HasPermissionAsync(actor.AccountId, code, storeId);
        if (!permission.IsSuccess || permission.Data?.Allowed != true)
            throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thao tác anomaly này.");
    }

    private async Task SyncNotificationsAsync(int storeId, string metricCode, OperationalAnomaly? anomaly, CancellationToken ct)
    {
        var entityType = $"{StaffNotificationEntityTypes.OperationalAnomaly}:{metricCode}";
        var active = await _notifications.GetActiveForStoreAsync(storeId, StaffNotificationTypes.OperationalAnomaly);
        if (anomaly == null)
        {
            var changed = false;
            foreach (var notification in active.Where(x => x.EntityType == entityType)) { notification.ResolvedAt = DateTime.UtcNow; notification.UpdatedAt = DateTime.UtcNow; changed = true; }
            if (changed) await _notifications.SaveChangesAsync();
            return;
        }
        var recipients = await _notifications.GetRecipientCandidatesAsync(); var changedCount = 0;
        foreach (var recipient in recipients)
        {
            ct.ThrowIfCancellationRequested();
            if (!await _scope.CanAccessStoreAsync(recipient.StaffId, storeId)) continue;
            var permission = await _permissions.HasPermissionAsync(recipient.AccountId, PermissionConstants.OperationalAnomalyView, storeId);
            if (!permission.IsSuccess || permission.Data?.Allowed != true) continue;
            var key = $"{recipient.StaffId}:{storeId}:{metricCode}:{anomaly.PeriodKey}:{StaffNotificationTypes.OperationalAnomaly}";
            var existing = await _notifications.GetByDeduplicationKeyAsync(key);
            var reasonCodes = JsonSerializer.Deserialize<string[]>(anomaly.ReasonCodesJson) ?? [];
            var presentation = OperationalAnomalyPresentation.Build(
                anomaly.MetricCode,
                anomaly.CurrentValue,
                anomaly.BaselineValue,
                anomaly.PercentageDeviation,
                anomaly.Severity,
                anomaly.Status,
                anomaly.Confidence,
                reasonCodes);
            var title = $"Tín hiệu vận hành: {presentation.MetricDisplayName}";
            var body = $"{presentation.DirectionDescription} Mức hiện tại {presentation.CurrentValueDisplay}; mức thông thường trước đây {presentation.BaselineValueDisplay}. Đây là tín hiệu cần kiểm tra, không phải kết luận sai phạm.";
            if (existing == null)
            {
                _notifications.Add(new StaffNotification { StoreId = storeId, RecipientStaffId = recipient.StaffId, Type = StaffNotificationTypes.OperationalAnomaly, Title = title, Body = body, Severity = anomaly.Severity, DeduplicationKey = key, EntityType = entityType, EntityId = anomaly.OperationalAnomalyId, CreatedAt = DateTime.UtcNow }); changedCount++;
            }
            else if (existing.Body != body || existing.Severity != anomaly.Severity || existing.ResolvedAt.HasValue)
            {
                var escalated = existing.Severity != anomaly.Severity; existing.Body = body; existing.Severity = anomaly.Severity; existing.ResolvedAt = null; existing.UpdatedAt = DateTime.UtcNow;
                if (escalated) { existing.IsRead = false; existing.ReadAt = null; } changedCount++;
            }
        }
        if (changedCount > 0) await _notifications.SaveChangesAsync();
    }
    private static decimal Median(IReadOnlyList<decimal> values) { var s = values.OrderBy(x => x).ToList(); if (s.Count == 0) return 0; return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2; }
    private static OperationalAnomalyDto Map(OperationalAnomaly x)
    {
        var reasonCodes = JsonSerializer.Deserialize<string[]>(x.ReasonCodesJson) ?? [];
        var presentation = OperationalAnomalyPresentation.Build(
            x.MetricCode,
            x.CurrentValue,
            x.BaselineValue,
            x.PercentageDeviation,
            x.Severity,
            x.Status,
            x.Confidence,
            reasonCodes);
        return new OperationalAnomalyDto
        {
            Id = x.OperationalAnomalyId,
            StoreId = x.StoreId,
            MetricCode = x.MetricCode,
            PeriodKey = x.PeriodKey,
            BusinessDate = x.BusinessDate,
            DetectionVersion = x.DetectionVersion,
            CurrentValue = x.CurrentValue,
            BaselineValue = x.BaselineValue,
            PercentageDeviation = x.PercentageDeviation,
            RobustScore = x.RobustScore,
            Severity = x.Severity,
            Confidence = x.Confidence,
            Status = x.Status,
            ReasonCodes = reasonCodes,
            MetricDisplayName = presentation.MetricDisplayName,
            CurrentValueDisplay = presentation.CurrentValueDisplay,
            BaselineValueDisplay = presentation.BaselineValueDisplay,
            DeviationDisplay = presentation.DeviationDisplay,
            SeverityDisplay = presentation.SeverityDisplay,
            StatusDisplay = presentation.StatusDisplay,
            ConfidenceDisplay = presentation.ConfidenceDisplay,
            ReasonSummaries = presentation.ReasonSummaries,
            SuggestedChecks = presentation.SuggestedChecks,
            FeedbackDisplay = x.Feedback switch
            {
                "Useful" => "Hữu ích",
                "NotUseful" => "Chưa hữu ích",
                "FalsePositive" => "Cảnh báo không phù hợp",
                _ => string.Empty
            },
            CreatedAtUtc = x.CreatedAtUtc,
            RowVersion = Convert.ToBase64String(x.RowVersion)
        };
    }
}
