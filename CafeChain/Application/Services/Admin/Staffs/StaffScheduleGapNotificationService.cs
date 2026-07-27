using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Admin.Staffs;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Staffs;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.Admin.Staffs;

public sealed class StaffScheduleGapNotificationService : IStaffScheduleGapNotificationService
{
    private readonly IShiftOptimizationRepository _repository;
    private readonly IInventoryNotificationDeliveryService _delivery;
    private readonly IInventoryReorderNotificationRepository _notificationRepository;
    private readonly IScopeAuthorizationService _scopeAuthorization;
    private readonly IAdminPermissionService _permissions;
    private readonly StaffScheduleGapNotificationOptions _options;

    public StaffScheduleGapNotificationService(
        IShiftOptimizationRepository repository,
        IInventoryNotificationDeliveryService delivery,
        IInventoryReorderNotificationRepository notificationRepository,
        IScopeAuthorizationService scopeAuthorization,
        IAdminPermissionService permissions,
        IOptions<StaffScheduleGapNotificationOptions> options)
    {
        _repository = repository;
        _delivery = delivery;
        _notificationRepository = notificationRepository;
        _scopeAuthorization = scopeAuthorization;
        _permissions = permissions;
        _options = options.Value;
    }

    public async Task<StaffScheduleGapScanResult> ScanStoreAsync(
        int storeId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        if (storeId <= 0 || fromDate.Date > toDate.Date)
            return new StaffScheduleGapScanResult(storeId, 0, 0, 0, 0);

        var recipients = await ResolveRecipientsAsync(storeId, cancellationToken);
        if (recipients.Count == 0)
        {
            var resolvedWithoutRecipients = 0;
            var staleNotifications = await _notificationRepository.GetActiveForStoreAsync(
                storeId, StaffScheduleNotificationTypes.Gap);
            foreach (var notification in staleNotifications)
            {
                if (string.IsNullOrWhiteSpace(notification.DeduplicationKey))
                    continue;
                var result = await _delivery.ResolveByDeduplicationKeyAsync(
                    notification.DeduplicationKey, cancellationToken);
                resolvedWithoutRecipients += result.ResolvedCount;
            }
            return new StaffScheduleGapScanResult(
                storeId, 0, 0, resolvedWithoutRecipients, 0);
        }

        var staffs = await _repository.GetStaffsAsync(storeId, cancellationToken);
        var shifts = await _repository.GetShiftsAsync(storeId, cancellationToken);
        var schedules = await _repository.GetSchedulesAsync(storeId, fromDate, toDate, cancellationToken);
        var availability = await _repository.GetAvailabilityAsync(storeId, fromDate, toDate, cancellationToken);
        var exceptions = await _repository.GetExceptionsAsync(storeId, fromDate, toDate, cancellationToken);
        var timeOffs = await _repository.GetTimeOffsAsync(
            storeId, fromDate, toDate.AddDays(1), cancellationToken);
        var constraints = await _repository.GetConstraintsAsync(
            storeId, fromDate, toDate, cancellationToken);
        var requirements = await _repository.GetRequirementsAsync(
            storeId, fromDate, toDate, cancellationToken);
        var storeName = await _repository.GetStoreNameAsync(storeId, cancellationToken)
            ?? $"Cửa hàng #{storeId}";

        var currentKeys = new HashSet<string>(StringComparer.Ordinal);
        var created = 0;
        var updated = 0;
        var missing = 0;

        foreach (var date in Dates(fromDate.Date, toDate.Date))
        {
            foreach (var requirement in requirements.Where(x =>
                x.DayOfWeek == date.DayOfWeek
                && x.EffectiveFrom.Date <= date
                && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= date)))
            {
                var shift = shifts.FirstOrDefault(x => x.ShiftId == requirement.ShiftId);
                if (shift == null) continue;

                var interval = Interval(date, shift.StartTime, shift.EndTime, shift.IsOvernight);
                var scheduledCount = schedules.Count(x =>
                    x.ShiftId == shift.ShiftId && x.WorkDate.Date == date.Date);
                var shortage = Math.Max(0, requirement.TargetStaff - scheduledCount);
                if (shortage <= 0) continue;

                missing++;
                var candidates = staffs
                    .Where(staff => EligibleCandidate(
                        staff,
                        requirement,
                        interval,
                        date,
                        availability,
                        exceptions,
                        timeOffs,
                        constraints,
                        schedules))
                    .OrderBy(staff => WeeklyHours(staff.StaffId, date, schedules))
                    .ThenBy(staff => staff.StaffId)
                    .Take(Math.Clamp(_options.MaximumCandidatesPerAlert, 1, 50))
                    .ToList();

                var requirementId = Convert.ToInt32(Math.Clamp(
                    Convert.ToInt64(requirement.StoreStaffingRequirementId),
                    1L,
                    int.MaxValue));
                var baseKey =
                    $"{StaffScheduleNotificationTypes.Gap}:{storeId}:{requirementId}:{date:yyyyMMdd}";
                var title = $"Thiếu lịch nhân sự tại {storeName}";
                var names = candidates.Count == 0
                    ? "Chưa có nhân viên đủ điều kiện để đề xuất."
                    : string.Join(", ", candidates.Select(x => x.FullName));
                var body =
                    $"{date:dd/MM/yyyy} — ca {shift.Name} đang thiếu {shortage} người " +
                    $"(đã xếp {scheduledCount}, mục tiêu {requirement.TargetStaff}). " +
                    $"Nhân viên phù hợp chưa có lịch: {names}";

                var result = await _delivery.DeliverAsync(
                    new InventoryNotificationDeliveryRequest(
                        storeId,
                        StaffScheduleNotificationTypes.Gap,
                        title,
                        body,
                        shortage > 1 ? "URGENT" : "WARNING",
                        StaffScheduleNotificationEntityTypes.Gap,
                        requirementId,
                        InventoryNotificationChangeKinds.Updated,
                        baseKey,
                        Math.Clamp(_options.ReminderCooldownHours, 1, 168) * 60,
                        recipients),
                    cancellationToken);
                created += result.CreatedCount;
                updated += result.UpdatedCount;

                foreach (var recipientStaffId in recipients)
                    currentKeys.Add($"{recipientStaffId}:{baseKey}");
            }
        }

        var active = await _notificationRepository.GetActiveForStoreAsync(
            storeId, StaffScheduleNotificationTypes.Gap);
        var resolved = 0;
        foreach (var notification in active)
        {
            if (string.IsNullOrWhiteSpace(notification.DeduplicationKey)
                || currentKeys.Contains(notification.DeduplicationKey))
            {
                continue;
            }

            var result = await _delivery.ResolveByDeduplicationKeyAsync(
                notification.DeduplicationKey, cancellationToken);
            resolved += result.ResolvedCount;
        }

        return new StaffScheduleGapScanResult(storeId, created, updated, resolved, missing);
    }

    private async Task<IReadOnlyCollection<int>> ResolveRecipientsAsync(
        int storeId,
        CancellationToken cancellationToken)
    {
        var recipients = new List<int>();
        foreach (var candidate in await _notificationRepository.GetRecipientCandidatesAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await _scopeAuthorization.CanAccessStoreAsync(candidate.StaffId, storeId))
                continue;

            var isManager = candidate.RoleNames.Any(role =>
                string.Equals(role, RoleConstants.StoreManager, StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, RoleConstants.AreaManager, StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, RoleConstants.BusinessOwner, StringComparison.OrdinalIgnoreCase));
            if (!isManager)
                continue;

            var notificationPermission = await _permissions.HasPermissionAsync(
                candidate.AccountId, PermissionConstants.NotificationView, storeId);
            var shiftPermission = await _permissions.HasPermissionAsync(
                candidate.AccountId, PermissionConstants.ShiftView, storeId);
            if (notificationPermission.IsSuccess
                && notificationPermission.Data?.Allowed == true
                && shiftPermission.IsSuccess
                && shiftPermission.Data?.Allowed == true)
            {
                recipients.Add(candidate.StaffId);
            }
        }

        return recipients.Distinct().ToList();
    }

    private static bool EligibleCandidate(
        Staff staff,
        StoreStaffingRequirement requirement,
        IntervalValue interval,
        DateTime date,
        IReadOnlyCollection<StaffAvailabilityRule> availability,
        IReadOnlyCollection<StaffAvailabilityException> exceptions,
        IReadOnlyCollection<StaffTimeOff> timeOffs,
        IReadOnlyCollection<StaffWorkConstraint> constraints,
        IReadOnlyCollection<StaffShift> schedules)
    {
        if (requirement.RequiredRoleId.HasValue
            && !staff.Account.AccountRoles.Any(x => x.RoleId == requirement.RequiredRoleId.Value))
        {
            return false;
        }

        var hasAvailability = availability.Any(x =>
            x.StaffId == staff.StaffId
            && x.DayOfWeek == date.DayOfWeek
            && x.EffectiveFrom.Date <= date.Date
            && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= date.Date)
            && Covers(date, x.StartTime, x.EndTime, interval));
        if (!hasAvailability) return false;

        if (exceptions.Any(x =>
            x.StaffId == staff.StaffId
            && x.Date.Date == date.Date
            && !x.IsAvailable))
        {
            return false;
        }

        if (timeOffs.Any(x =>
            x.StaffId == staff.StaffId
            && x.FromUtc < interval.End
            && x.ToUtc > interval.Start))
        {
            return false;
        }

        var constraint = constraints.FirstOrDefault(x =>
            x.StaffId == staff.StaffId
            && x.EffectiveFrom.Date <= date.Date
            && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= date.Date));
        if (constraint == null) return false;

        var duration = (decimal)(interval.End - interval.Start).TotalHours;
        if (duration > constraint.MaxDailyHours
            || WeeklyHours(staff.StaffId, date, schedules) + duration > constraint.MaxWeeklyHours)
        {
            return false;
        }

        var existingIntervals = schedules
            .Where(x => x.StaffId == staff.StaffId)
            .Select(ScheduleInterval)
            .ToList();
        if (existingIntervals.Any(existing => Overlaps(interval, existing)))
            return false;

        return existingIntervals.All(existing =>
            RestMinutes(interval, existing) >= constraint.MinimumRestMinutes);
    }

    private static decimal WeeklyHours(
        int staffId,
        DateTime date,
        IReadOnlyCollection<StaffShift> schedules)
    {
        var weekStart = date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        var weekEnd = weekStart.AddDays(6);
        return schedules
            .Where(x =>
                x.StaffId == staffId
                && x.WorkDate.Date >= weekStart
                && x.WorkDate.Date <= weekEnd)
            .Sum(x => (decimal)(ScheduleInterval(x).End - ScheduleInterval(x).Start).TotalHours);
    }

    private static IntervalValue ScheduleInterval(StaffShift shift) =>
        Interval(
            shift.WorkDate,
            shift.CustomStartTime ?? shift.Shift.StartTime,
            shift.CustomEndTime ?? shift.Shift.EndTime,
            shift.CustomEndTime.HasValue
                ? shift.CustomEndTime <= shift.CustomStartTime
                : shift.Shift.IsOvernight);

    private static bool Covers(
        DateTime date,
        TimeSpan start,
        TimeSpan end,
        IntervalValue target)
    {
        var available = Interval(date, start, end, end <= start);
        return available.Start <= target.Start && available.End >= target.End;
    }

    private static IntervalValue Interval(
        DateTime date,
        TimeSpan start,
        TimeSpan end,
        bool overnight) =>
        new(
            date.Date + start,
            date.Date + end + (overnight || end <= start
                ? TimeSpan.FromDays(1)
                : TimeSpan.Zero));

    private static bool Overlaps(IntervalValue left, IntervalValue right) =>
        left.Start < right.End && left.End > right.Start;

    private static double RestMinutes(IntervalValue left, IntervalValue right)
    {
        if (Overlaps(left, right)) return 0;
        return left.Start >= right.End
            ? (left.Start - right.End).TotalMinutes
            : (right.Start - left.End).TotalMinutes;
    }

    private static IEnumerable<DateTime> Dates(DateTime from, DateTime to)
    {
        for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
            yield return date;
    }

    private readonly record struct IntervalValue(DateTime Start, DateTime End);
}
