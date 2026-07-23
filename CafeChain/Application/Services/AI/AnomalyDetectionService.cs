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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AI;

public sealed class AnomalyDetectionService : IAnomalyDetectionService
{
    private static readonly HashSet<string> RecipientRoles = new(StringComparer.OrdinalIgnoreCase)
    { RoleConstants.BusinessOwner, RoleConstants.AreaManager, RoleConstants.StoreManager, RoleConstants.AccountantWarehouse };
    private readonly IAnomalyDetectionRepository _repository;
    private readonly IInventoryReorderNotificationRepository _notifications;
    private readonly IAdminPermissionService _permissions;
    private readonly IScopeAuthorizationService _scope;
    private readonly AnomalyDetectionOptions _options;
    public AnomalyDetectionService(IAnomalyDetectionRepository repository, IInventoryReorderNotificationRepository notifications,
        IAdminPermissionService permissions, IScopeAuthorizationService scope, IOptions<AnomalyDetectionOptions> options)
    { _repository = repository; _notifications = notifications; _permissions = permissions; _scope = scope; _options = options.Value; }

    public async Task AnalyzeStoreAsync(int storeId, CancellationToken ct = default)
    {
        if (!_options.Enabled) return;
        var targetDate = DateTime.UtcNow.Date.AddDays(-1);
        var from = targetDate.AddDays(-Math.Max(84, _options.AnalysisWindowDays));
        var metrics = await _repository.GetDailyOperationalMetricsAsync(storeId, from, targetDate.AddDays(1), ct);
        foreach (var metric in metrics)
            await AnalyzeMetricAsync(storeId, metric.Key, metric.Value, from, targetDate, ct);
    }

    private async Task AnalyzeMetricAsync(int storeId, string metricCode, IReadOnlyList<DailyMetricPoint> data, DateTime from, DateTime targetDate, CancellationToken ct)
    {
        var byDate = data.ToDictionary(x => x.Date.Date, x => x.Value);
        var historyFrom = targetDate.AddDays(-_options.AnalysisWindowDays);
        var history = Enumerable.Range(0, _options.AnalysisWindowDays).Select(i => byDate.GetValueOrDefault(historyFrom.AddDays(i))).ToList();
        if (history.Count < _options.MinimumSampleCount) return;
        var current = byDate.GetValueOrDefault(targetDate); var sameWeekday = history.Where((_, i) => historyFrom.AddDays(i).DayOfWeek == targetDate.DayOfWeek).ToList();
        var baselineValues = sameWeekday.Count >= 4 ? sameWeekday : history.TakeLast(14).ToList();
        var median = Median(baselineValues); var mad = Median(baselineValues.Select(x => Math.Abs(x - median)).ToList());
        var robust = mad <= 0 ? 0 : .6745m * (current - median) / mad;
        var absolute = current - median; var percentage = median == 0 ? (current == 0 ? 0 : 1) : absolute / median;
        var minimumAbsolute = metricCode == "REVENUE" ? _options.MinimumAbsoluteRevenueDeviation
            : metricCode == "CASH_DISCREPANCY" ? 100000m : 1m;
        var isDropOnly = metricCode.StartsWith("PRODUCT_VOLUME:", StringComparison.Ordinal);
        var isAnomaly = (!isDropOnly || absolute < 0) && Math.Abs(absolute) >= minimumAbsolute
            && Math.Abs(percentage) >= _options.MinimumPercentageDeviation && Math.Abs(robust) >= _options.RobustScoreThreshold;
        var key = targetDate.ToString("yyyyMMdd"); var row = await _repository.GetByKeyAsync(storeId, metricCode, key, ct);
        if (!isAnomaly)
        {
            if (row != null && row.Status != "RESOLVED") { row.Status = "RESOLVED"; row.ResolvedAtUtc = DateTime.UtcNow; row.UpdatedAtUtc = DateTime.UtcNow; await _repository.SaveChangesAsync(ct); }
            await SyncNotificationsAsync(storeId, metricCode, null, ct);
            return;
        }
        var severity = Math.Abs(robust) >= 5 || Math.Abs(percentage) >= .5m ? "CRITICAL" : "HIGH";
        if (row == null) { row = new OperationalAnomaly { StoreId = storeId, MetricCode = metricCode, PeriodKey = key, CreatedAtUtc = DateTime.UtcNow }; await _repository.AddAsync(row, ct); }
        row.CurrentValue = current; row.BaselineValue = median; row.AbsoluteDeviation = absolute; row.PercentageDeviation = percentage;
        row.RobustScore = robust; row.WindowFromUtc = historyFrom; row.WindowToExclusiveUtc = targetDate.AddDays(1); row.SampleCount = history.Count;
        row.Severity = severity; row.Confidence = sameWeekday.Count >= 4 ? "HIGH" : "MEDIUM"; row.Status = "OPEN";
        row.ReasonCodesJson = JsonSerializer.Serialize(new[] { absolute < 0 ? "BELOW_SEASONAL_BASELINE" : "ABOVE_SEASONAL_BASELINE", "MATERIAL_DEVIATION", "ROBUST_SCORE_EXCEEDED" });
        row.UpdatedAtUtc = DateTime.UtcNow; row.ResolvedAtUtc = null; await _repository.SaveChangesAsync(ct);
        await SyncNotificationsAsync(storeId, metricCode, row, ct);
    }

    public async Task<IReadOnlyList<OperationalAnomalyDto>> GetOpenAsync(AdminActorContext actor, int storeId, CancellationToken ct = default)
    {
        await EnsureScope(actor, storeId); var rows = await _repository.GetOpenAsync(storeId, ct); return rows.Select(Map).ToList();
    }

    public async Task<AnomalyExplanationContextDto> GetExplanationContextAsync(AdminActorContext actor, int anomalyId, CancellationToken ct = default)
    {
        var row = await _repository.GetByIdAsync(anomalyId, ct) ?? throw new KeyNotFoundException("Không tìm thấy tín hiệu bất thường.");
        await EnsureScope(actor, row.StoreId);
        return new AnomalyExplanationContextDto { AnomalyId = row.OperationalAnomalyId, MetricCode = row.MetricCode, CurrentValue = row.CurrentValue, BaselineValue = row.BaselineValue, RobustScore = row.RobustScore, ReasonCodes = JsonSerializer.Deserialize<string[]>(row.ReasonCodesJson) ?? [] };
    }

    public async Task RecordFeedbackAsync(AdminActorContext actor, AnomalyFeedbackDto input, CancellationToken ct = default)
    {
        var row = await _repository.GetByIdAsync(input.Id, ct) ?? throw new KeyNotFoundException("Không tìm thấy tín hiệu bất thường.");
        await EnsureScope(actor, row.StoreId);
        if (!Convert.ToBase64String(row.RowVersion).Equals(input.RowVersion, StringComparison.Ordinal)) throw new DbUpdateConcurrencyException("Tín hiệu đã được cập nhật. Vui lòng tải lại.");
        var action = input.Action.Trim().ToUpperInvariant();
        if (action == "ACKNOWLEDGE") { row.Status = "ACKNOWLEDGED"; row.AcknowledgedAtUtc = DateTime.UtcNow; row.AcknowledgedByStaffId = actor.StaffId; }
        else if (action == "RESOLVE") { row.Status = "RESOLVED"; row.ResolvedAtUtc = DateTime.UtcNow; }
        else if (action != "FEEDBACK") throw new ArgumentException("Hành động không hợp lệ.");
        row.Feedback = string.IsNullOrWhiteSpace(input.Feedback) ? row.Feedback : input.Feedback.Trim(); row.FeedbackByStaffId = actor.StaffId; row.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.SaveChangesAsync(ct);
    }

    private async Task EnsureScope(AdminActorContext actor, int storeId) { if (actor.StaffId <= 0 || !await _scope.CanAccessStoreAsync(actor.StaffId, storeId)) throw new UnauthorizedAccessException("Bạn không có quyền truy cập cửa hàng này."); }
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
        foreach (var recipient in recipients.Where(x => x.RoleNames.Any(RecipientRoles.Contains)))
        {
            ct.ThrowIfCancellationRequested();
            if (!await _scope.CanAccessStoreAsync(recipient.StaffId, storeId)) continue;
            var permission = await _permissions.HasPermissionAsync(recipient.AccountId, PermissionConstants.AppAdminDashboard, storeId);
            if (!permission.IsSuccess || permission.Data?.Allowed != true) continue;
            var key = $"{recipient.StaffId}:{storeId}:{metricCode}:{anomaly.PeriodKey}:{StaffNotificationTypes.OperationalAnomaly}";
            var existing = await _notifications.GetByDeduplicationKeyAsync(key);
            var title = $"Tín hiệu vận hành: {metricCode}";
            var body = $"Giá trị {anomaly.CurrentValue:N0}, baseline {anomaly.BaselineValue:N0}, độ lệch {anomaly.PercentageDeviation:P1}. Cần kiểm tra nguyên nhân; đây không phải kết luận gian lận.";
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
    private static OperationalAnomalyDto Map(OperationalAnomaly x) => new() { Id = x.OperationalAnomalyId, StoreId = x.StoreId, MetricCode = x.MetricCode, PeriodKey = x.PeriodKey, CurrentValue = x.CurrentValue, BaselineValue = x.BaselineValue, PercentageDeviation = x.PercentageDeviation, RobustScore = x.RobustScore, Severity = x.Severity, Confidence = x.Confidence, Status = x.Status, ReasonCodes = JsonSerializer.Deserialize<string[]>(x.ReasonCodesJson) ?? [], CreatedAtUtc = x.CreatedAtUtc, RowVersion = Convert.ToBase64String(x.RowVersion) };
}
