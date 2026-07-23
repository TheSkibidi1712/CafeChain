using System.Text.Json;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Staffs;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Admin.Staffs;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.Admin.Staffs;

public sealed class ShiftOptimizationService : IShiftOptimizationService
{
    private readonly IShiftOptimizationRepository _repository;
    private readonly IScopeAuthorizationService _scope;
    private readonly bool _enabled;
    public ShiftOptimizationService(IShiftOptimizationRepository repository, IScopeAuthorizationService scope, IConfiguration configuration)
    { _repository = repository; _scope = scope; _enabled = configuration.GetValue<bool>("ShiftOptimization:ProposalEnabled"); }

    public async Task<ShiftOptimizationSetupDto> GetSetupAsync(AdminActorContext actor, int storeId, CancellationToken ct = default)
    {
        await RequireStoreAsync(actor, storeId);
        var from = DateTime.Today.AddDays(-7); var to = DateTime.Today.AddYears(1);
        var staffs = await _repository.GetStaffsAsync(storeId, ct);
        var shifts = await _repository.GetShiftsAsync(storeId, ct);
        var availability = await _repository.GetAvailabilityAsync(storeId, from, to, ct);
        var constraints = await _repository.GetConstraintsAsync(storeId, from, to, ct);
        var requirements = await _repository.GetRequirementsAsync(storeId, from, to, ct);
        var timeOffs = await _repository.GetTimeOffsAsync(storeId, from, to, ct);
        return new ShiftOptimizationSetupDto
        {
            StoreId = storeId,
            Staffs = staffs.OrderBy(x => x.FullName).Select(x => new ShiftOptimizationOptionDto(x.StaffId, x.FullName)).ToList(),
            Shifts = shifts.OrderBy(x => x.StartTime).Select(x => new ShiftOptimizationOptionDto(x.ShiftId, x.Name)).ToList(),
            Availability = availability.Select(x => (object)new { x.StaffAvailabilityRuleId, x.StaffId, x.DayOfWeek, x.StartTime, x.EndTime, x.EffectiveFrom, x.EffectiveTo }).ToList(),
            Constraints = constraints.Select(x => (object)new { x.StaffWorkConstraintId, x.StaffId, x.TargetWeeklyHours, x.MaxWeeklyHours, x.MaxDailyHours, x.MinimumRestMinutes, x.EffectiveFrom, x.EffectiveTo }).ToList(),
            Requirements = requirements.Select(x => (object)new { x.StoreStaffingRequirementId, x.ShiftId, x.DayOfWeek, x.MinimumStaff, x.TargetStaff, x.MaximumStaff, x.RequiredRoleId, x.EffectiveFrom, x.EffectiveTo }).ToList(),
            TimeOffs = timeOffs.Select(x => (object)new { x.StaffTimeOffId, x.StaffId, x.FromUtc, x.ToUtc, x.Status, x.Reason }).ToList()
        };
    }

    public async Task<ShiftOptimizationProposalDto> GenerateAsync(AdminActorContext actor, ShiftOptimizationInputDto input, CancellationToken ct = default)
    {
        if (!_enabled) throw new InvalidOperationException("Shift Optimization đang tắt.");
        await RequireStoreAsync(actor, input.StoreId);
        var from = input.FromDate.Date; var to = input.ToDate.Date;
        if (from > to || (to - from).TotalDays > 31) throw new ArgumentException("Khoảng đề xuất phải từ 1 đến 31 ngày.");
        var staffs = await _repository.GetStaffsAsync(input.StoreId, ct);
        var shifts = await _repository.GetShiftsAsync(input.StoreId, ct);
        var schedules = await _repository.GetSchedulesAsync(input.StoreId, from, to, ct);
        var availability = await _repository.GetAvailabilityAsync(input.StoreId, from, to, ct);
        var exceptions = await _repository.GetExceptionsAsync(input.StoreId, from, to, ct);
        var timeOffs = await _repository.GetTimeOffsAsync(input.StoreId, from, to.AddDays(1), ct);
        var constraints = await _repository.GetConstraintsAsync(input.StoreId, from, to, ct);
        var requirements = await _repository.GetRequirementsAsync(input.StoreId, from, to, ct);
        var violations = new List<string>(); var proposed = new List<Proposed>();

        foreach (var date in Dates(from, to))
        foreach (var requirement in requirements.Where(x => x.DayOfWeek == date.DayOfWeek && x.EffectiveFrom.Date <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= date)))
        {
            var shift = shifts.FirstOrDefault(x => x.ShiftId == requirement.ShiftId); if (shift == null) continue;
            var interval = Interval(date, shift.StartTime, shift.EndTime, shift.IsOvernight);
            var existingCount = schedules.Count(x => x.ShiftId == shift.ShiftId && x.WorkDate.Date == date);
            var need = Math.Max(0, requirement.TargetStaff - existingCount);
            for (var slot = 0; slot < need; slot++)
            {
                var candidates = staffs.Where(staff => Eligible(staff, requirement, interval, date, availability, exceptions,
                        timeOffs, constraints, schedules, proposed))
                    .Select(staff => new
                    {
                        Staff = staff,
                        Hours = WeeklyHours(staff.StaffId, from, to, schedules, proposed),
                        Target = constraints.First(x => x.StaffId == staff.StaffId && x.EffectiveFrom.Date <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= date)).TargetWeeklyHours
                    })
                    .OrderByDescending(x => x.Target - x.Hours).ThenBy(x => x.Staff.StaffId).ToList();
                if (candidates.Count == 0)
                {
                    violations.Add($"Thiếu nhân sự hợp lệ cho {date:dd/MM/yyyy} - {shift.Name}."); break;
                }
                proposed.Add(new Proposed(candidates[0].Staff, shift, date, interval.Start, interval.End));
            }
            var finalCount = existingCount + proposed.Count(x => x.Shift.ShiftId == shift.ShiftId && x.Date == date);
            if (finalCount < requirement.MinimumStaff)
                violations.Add($"Không đạt minimum staffing {requirement.MinimumStaff} cho {date:dd/MM/yyyy} - {shift.Name}.");
        }
        if (requirements.Count == 0) violations.Add("Chưa cấu hình định mức nhân sự trong khoảng được chọn.");
        foreach (var staff in staffs)
        {
            if (!availability.Any(x => x.StaffId == staff.StaffId)) violations.Add($"{staff.FullName}: thiếu lịch khả dụng.");
            if (!constraints.Any(x => x.StaffId == staff.StaffId)) violations.Add($"{staff.FullName}: thiếu giới hạn giờ làm.");
        }
        var status = violations.Count == 0 ? "FEASIBLE" : proposed.Count > 0 ? "PARTIALLY_FEASIBLE" : "INFEASIBLE";
        var entity = new ScheduleOptimizationProposal
        {
            ScheduleOptimizationProposalId = Guid.NewGuid(), StoreId = input.StoreId, FromDate = from, ToDate = to,
            Status = status, ViolationsJson = JsonSerializer.Serialize(violations),
            ScoreBreakdownJson = JsonSerializer.Serialize(new { coverageAssignments = proposed.Count, violationCount = violations.Count }),
            CreatedByStaffId = actor.StaffId, CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddHours(24),
            Assignments = proposed.Select(x => new ScheduleOptimizationAssignment
            {
                StaffId = x.Staff.StaffId, ShiftId = x.Shift.ShiftId, WorkDate = x.Date,
                StartTime = x.Shift.StartTime, EndTime = x.Shift.EndTime,
                ReasonCodesJson = "[\"DEMAND_COVERAGE\",\"FAIR_HOURS\"]"
            }).ToList()
        };
        _repository.Add(entity); await _repository.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ShiftOptimizationProposalDto> GetAsync(AdminActorContext actor, Guid proposalId, CancellationToken ct = default)
    {
        var entity = await _repository.GetProposalAsync(proposalId, false, ct) ?? throw new KeyNotFoundException("Không tìm thấy đề xuất.");
        await RequireStoreAsync(actor, entity.StoreId); return Map(entity);
    }

    public async Task ApplyAsync(AdminActorContext actor, ApplyScheduleProposalDto input, CancellationToken ct = default)
    {
        var entity = await _repository.GetProposalAsync(input.ProposalId, true, ct) ?? throw new KeyNotFoundException("Không tìm thấy đề xuất.");
        await RequireStoreAsync(actor, entity.StoreId);
        if (entity.Status != "FEASIBLE") throw new InvalidOperationException("Chỉ đề xuất FEASIBLE mới được áp dụng.");
        if (entity.ExpiresAtUtc <= DateTime.UtcNow || entity.AppliedAtUtc.HasValue) throw new InvalidOperationException("Đề xuất đã hết hạn hoặc đã áp dụng.");
        if (!VersionMatches(entity.RowVersion, input.RowVersion)) throw new DbUpdateConcurrencyException("Đề xuất đã thay đổi.");
        var latest = await _repository.GetSchedulesAsync(entity.StoreId, entity.FromDate, entity.ToDate, ct);
        var staffs = await _repository.GetStaffsAsync(entity.StoreId, ct);
        var shifts = await _repository.GetShiftsAsync(entity.StoreId, ct);
        var availability = await _repository.GetAvailabilityAsync(entity.StoreId, entity.FromDate, entity.ToDate, ct);
        var exceptions = await _repository.GetExceptionsAsync(entity.StoreId, entity.FromDate, entity.ToDate, ct);
        var timeOffs = await _repository.GetTimeOffsAsync(entity.StoreId, entity.FromDate, entity.ToDate.AddDays(1), ct);
        var constraints = await _repository.GetConstraintsAsync(entity.StoreId, entity.FromDate, entity.ToDate, ct);
        var requirements = await _repository.GetRequirementsAsync(entity.StoreId, entity.FromDate, entity.ToDate, ct);
        var revalidated = new List<Proposed>();
        foreach (var assignment in entity.Assignments)
        {
            var candidate = Interval(assignment.WorkDate, assignment.StartTime, assignment.EndTime, assignment.EndTime <= assignment.StartTime);
            var staff = staffs.FirstOrDefault(x => x.StaffId == assignment.StaffId);
            var shift = shifts.FirstOrDefault(x => x.ShiftId == assignment.ShiftId);
            var requirement = requirements.FirstOrDefault(x => x.ShiftId == assignment.ShiftId && x.DayOfWeek == assignment.WorkDate.DayOfWeek
                && x.EffectiveFrom.Date <= assignment.WorkDate.Date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= assignment.WorkDate.Date));
            if (staff == null || shift == null || requirement == null || !Eligible(staff, requirement, candidate, assignment.WorkDate.Date,
                    availability, exceptions, timeOffs, constraints, latest, revalidated))
                throw new DbUpdateConcurrencyException("Dữ liệu khả dụng, nghỉ phép, giới hạn giờ hoặc lịch hiện tại đã thay đổi. Vui lòng tạo lại đề xuất.");
            revalidated.Add(new Proposed(staff, shift, assignment.WorkDate.Date, candidate.Start, candidate.End));
        }
        foreach (var date in Dates(entity.FromDate, entity.ToDate))
        foreach (var requirement in requirements.Where(x => x.DayOfWeek == date.DayOfWeek && x.EffectiveFrom.Date <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= date)))
        {
            var count = latest.Count(x => x.ShiftId == requirement.ShiftId && x.WorkDate.Date == date)
                + revalidated.Count(x => x.Shift.ShiftId == requirement.ShiftId && x.Date == date);
            if (count < requirement.MinimumStaff || count > requirement.MaximumStaff)
                throw new DbUpdateConcurrencyException("Định mức nhân sự đã thay đổi. Vui lòng tạo lại đề xuất.");
        }
        var scheduled = await _repository.GetScheduledStatusAsync(ct) ?? throw new InvalidOperationException("Thiếu trạng thái SCHEDULED.");
        await _repository.BeginTransactionAsync(ct);
        try
        {
            foreach (var assignment in entity.Assignments)
            {
                var schedule = new StaffShift { StaffId = assignment.StaffId, ShiftId = assignment.ShiftId, WorkDate = assignment.WorkDate, StatusId = scheduled.StaffShiftStatusId };
                _repository.Add(schedule);
            }
            entity.Status = "APPLIED"; entity.AppliedAtUtc = DateTime.UtcNow;
            _repository.Add(new AuditLog { TableName = "ScheduleOptimizationProposals", RecordId = 0, Action = "APPLY", NewData = JsonSerializer.Serialize(new { entity.ScheduleOptimizationProposalId, Count = entity.Assignments.Count }), UserId = actor.StaffId, CreatedAt = DateTime.UtcNow });
            await _repository.SaveChangesAsync(ct); await _repository.CommitAsync(ct);
        }
        catch { await _repository.RollbackAsync(ct); throw; }
    }

    public async Task SaveAvailabilityAsync(AdminActorContext actor, SaveAvailabilityRuleDto input, CancellationToken ct = default)
    {
        var storeId = await StaffStoreAsync(input.StaffId, ct); await RequireStoreAsync(actor, storeId);
        if (input.EndTime == input.StartTime) throw new ArgumentException("Khoảng availability phải có thời lượng khác 0.");
        _repository.Add(new StaffAvailabilityRule { StaffId = input.StaffId, DayOfWeek = input.DayOfWeek, StartTime = input.StartTime, EndTime = input.EndTime, EffectiveFrom = input.EffectiveFrom.Date, EffectiveTo = input.EffectiveTo?.Date, CreatedByStaffId = actor.StaffId, CreatedAtUtc = DateTime.UtcNow });
        await _repository.SaveChangesAsync(ct);
    }
    public async Task SaveConstraintAsync(AdminActorContext actor, SaveWorkConstraintDto input, CancellationToken ct = default)
    {
        var storeId = await StaffStoreAsync(input.StaffId, ct); await RequireStoreAsync(actor, storeId);
        if (input.TargetWeeklyHours > input.MaxWeeklyHours) throw new ArgumentException("Target hours không được vượt max weekly hours.");
        _repository.Add(new StaffWorkConstraint { StaffId = input.StaffId, EffectiveFrom = input.EffectiveFrom.Date, EffectiveTo = input.EffectiveTo?.Date, TargetWeeklyHours = input.TargetWeeklyHours, MaxWeeklyHours = input.MaxWeeklyHours, MaxDailyHours = input.MaxDailyHours, MinimumRestMinutes = input.MinimumRestMinutes, CreatedByStaffId = actor.StaffId, CreatedAtUtc = DateTime.UtcNow }); await _repository.SaveChangesAsync(ct);
    }
    public async Task SaveRequirementAsync(AdminActorContext actor, SaveStaffingRequirementDto input, CancellationToken ct = default)
    {
        await RequireStoreAsync(actor, input.StoreId);
        if (input.MinimumStaff > input.TargetStaff || input.TargetStaff > input.MaximumStaff) throw new ArgumentException("Phải thỏa Minimum ≤ Target ≤ Maximum.");
        var shifts = await _repository.GetShiftsAsync(input.StoreId, ct); if (!shifts.Any(x => x.ShiftId == input.ShiftId)) throw new ArgumentException("Mẫu ca không thuộc cửa hàng.");
        _repository.Add(new StoreStaffingRequirement { StoreId = input.StoreId, ShiftId = input.ShiftId, DayOfWeek = input.DayOfWeek, MinimumStaff = input.MinimumStaff, TargetStaff = input.TargetStaff, MaximumStaff = input.MaximumStaff, RequiredRoleId = input.RequiredRoleId, EffectiveFrom = input.EffectiveFrom.Date, EffectiveTo = input.EffectiveTo?.Date, CreatedByStaffId = actor.StaffId, CreatedAtUtc = DateTime.UtcNow }); await _repository.SaveChangesAsync(ct);
    }
    public async Task SaveTimeOffAsync(AdminActorContext actor, SaveTimeOffDto input, CancellationToken ct = default)
    {
        var storeId = await StaffStoreAsync(input.StaffId, ct); await RequireStoreAsync(actor, storeId);
        if (input.ToUtc <= input.FromUtc) throw new ArgumentException("Khoảng nghỉ không hợp lệ.");
        _repository.Add(new StaffTimeOff { StaffId = input.StaffId, FromUtc = input.FromUtc, ToUtc = input.ToUtc, Reason = input.Reason.Trim(), Status = "APPROVED", RequestedByStaffId = actor.StaffId, ReviewedByStaffId = actor.StaffId, CreatedAtUtc = DateTime.UtcNow, ReviewedAtUtc = DateTime.UtcNow }); await _repository.SaveChangesAsync(ct);
    }

    private static bool Eligible(Staff staff, StoreStaffingRequirement requirement, IntervalValue interval, DateTime date,
        List<StaffAvailabilityRule> rules, List<StaffAvailabilityException> exceptions, List<StaffTimeOff> timeOffs,
        List<StaffWorkConstraint> constraints, List<StaffShift> schedules, List<Proposed> proposed)
    {
        if (requirement.RequiredRoleId.HasValue && !staff.Account.AccountRoles.Any(x => x.RoleId == requirement.RequiredRoleId)) return false;
        var rule = rules.Any(x => x.StaffId == staff.StaffId && x.DayOfWeek == date.DayOfWeek && x.EffectiveFrom.Date <= date
            && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= date) && CoversAvailability(date, x.StartTime, x.EndTime, interval));
        if (!rule) return false;
        if (exceptions.Any(x => x.StaffId == staff.StaffId && x.Date.Date == date && !x.IsAvailable)) return false;
        if (timeOffs.Any(x => x.StaffId == staff.StaffId && x.FromUtc < interval.End && x.ToUtc > interval.Start)) return false;
        var constraint = constraints.FirstOrDefault(x => x.StaffId == staff.StaffId && x.EffectiveFrom.Date <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= date));
        if (constraint == null) return false;
        var duration = (decimal)(interval.End - interval.Start).TotalHours;
        var weekStart = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        if (duration > constraint.MaxDailyHours || WeeklyHours(staff.StaffId, weekStart, weekStart.AddDays(6), schedules, proposed) + duration > constraint.MaxWeeklyHours) return false;
        var all = schedules.Where(x => x.StaffId == staff.StaffId).Select(ScheduleInterval).Concat(proposed.Where(x => x.Staff.StaffId == staff.StaffId).Select(x => new IntervalValue(x.Start, x.End))).ToList();
        if (all.Any(x => Overlaps(interval, x))) return false;
        return all.All(x => RestMinutes(interval, x) >= constraint.MinimumRestMinutes);
    }

    private static decimal WeeklyHours(int staffId, DateTime from, DateTime to, IEnumerable<StaffShift> schedules, IEnumerable<Proposed> proposed) =>
        schedules.Where(x => x.StaffId == staffId && x.WorkDate.Date >= from.Date && x.WorkDate.Date <= to.Date).Sum(x => (decimal)(ScheduleInterval(x).End - ScheduleInterval(x).Start).TotalHours)
        + proposed.Where(x => x.Staff.StaffId == staffId && x.Date >= from.Date && x.Date <= to.Date).Sum(x => (decimal)(x.End - x.Start).TotalHours);
    private async Task<int> StaffStoreAsync(int staffId, CancellationToken ct) =>
        (await _repository.GetStaffAsync(staffId, ct))?.StoreId ?? throw new KeyNotFoundException("Không tìm thấy nhân viên.");
    private async Task RequireStoreAsync(AdminActorContext actor, int storeId) { if (actor.StaffId <= 0 || !(await _scope.GetAllowedStoresAsync(actor.StaffId)).Any(x => x.StoreId == storeId)) throw new UnauthorizedAccessException("Cửa hàng nằm ngoài phạm vi được cấp."); }
    private static IEnumerable<DateTime> Dates(DateTime from, DateTime to) { for (var date = from; date <= to; date = date.AddDays(1)) yield return date; }
    private static IntervalValue Interval(DateTime date, TimeSpan start, TimeSpan end, bool overnight) => new(date.Date + start, date.Date + end + (overnight || end <= start ? TimeSpan.FromDays(1) : TimeSpan.Zero));
    private static IntervalValue ScheduleInterval(StaffShift x) => Interval(x.WorkDate, x.CustomStartTime ?? x.Shift.StartTime, x.CustomEndTime ?? x.Shift.EndTime, x.CustomEndTime.HasValue ? x.CustomEndTime <= x.CustomStartTime : x.Shift.IsOvernight);
    private static bool Overlaps(IntervalValue a, IntervalValue b) => a.Start < b.End && a.End > b.Start;
    private static bool CoversAvailability(DateTime date, TimeSpan start, TimeSpan end, IntervalValue interval)
    {
        var available = Interval(date, start, end, end <= start);
        return available.Start <= interval.Start && available.End >= interval.End;
    }
    private static double RestMinutes(IntervalValue a, IntervalValue b)
    {
        if (Overlaps(a, b)) return 0;
        return a.Start >= b.End ? (a.Start - b.End).TotalMinutes : (b.Start - a.End).TotalMinutes;
    }
    private static bool VersionMatches(byte[] value, string encoded) { try { return value.SequenceEqual(Convert.FromBase64String(encoded)); } catch { return false; } }
    private static ShiftOptimizationProposalDto Map(ScheduleOptimizationProposal x) => new() { ProposalId = x.ScheduleOptimizationProposalId, StoreId = x.StoreId, FromDate = x.FromDate, ToDate = x.ToDate, Status = x.Status, RowVersion = Convert.ToBase64String(x.RowVersion), Violations = JsonSerializer.Deserialize<List<string>>(x.ViolationsJson) ?? [], Assignments = x.Assignments.Select(a => new ShiftOptimizationAssignmentDto { AssignmentId = a.ScheduleOptimizationAssignmentId, StaffId = a.StaffId, StaffName = a.Staff?.FullName ?? $"#{a.StaffId}", ShiftId = a.ShiftId, ShiftName = a.Shift?.Name ?? $"#{a.ShiftId}", WorkDate = a.WorkDate, StartTime = a.StartTime, EndTime = a.EndTime, ReasonCodes = JsonSerializer.Deserialize<List<string>>(a.ReasonCodesJson) ?? [] }).ToList() };
    private readonly record struct IntervalValue(DateTime Start, DateTime End);
    private sealed record Proposed(Staff Staff, Shift Shift, DateTime Date, DateTime Start, DateTime End);
}
