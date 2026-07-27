using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Staffs;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Infrastructure.Interfaces.Admin.Staffs;
using CafeChain.Models.Staffs;

namespace CafeChain.Application.Services.Admin.Staffs;

public sealed class ShiftOptimizationService : IShiftOptimizationService
{
    private readonly IShiftOptimizationRepository _repository;
    private readonly IScopeAuthorizationService _scope;

    public ShiftOptimizationService(
        IShiftOptimizationRepository repository,
        IScopeAuthorizationService scope)
    {
        _repository = repository;
        _scope = scope;
    }

    public async Task<ShiftOptimizationSetupDto> GetSetupAsync(
        AdminActorContext actor,
        int storeId,
        CancellationToken ct = default)
    {
        await RequireStoreAsync(actor, storeId);
        var from = DateTime.Today.AddDays(-7);
        var to = DateTime.Today.AddYears(1);
        var staffs = await _repository.GetStaffsAsync(storeId, ct);
        var shifts = await _repository.GetShiftsAsync(storeId, ct);
        var roles = await _repository.GetRolesAsync(ct);
        var availability = await _repository.GetAvailabilityAsync(storeId, from, to, ct);
        var constraints = await _repository.GetConstraintsAsync(storeId, from, to, ct);
        var requirements = await _repository.GetRequirementsAsync(storeId, from, to, ct);
        var timeOffs = await _repository.GetTimeOffsAsync(storeId, from, to, ct);
        var staffNames = staffs.ToDictionary(x => x.StaffId, x => x.FullName);
        var shiftNames = shifts.ToDictionary(x => x.ShiftId, x => x.Name);
        var roleNames = roles.ToDictionary(x => x.RoleId, x => x.Name);

        return new ShiftOptimizationSetupDto
        {
            StoreId = storeId,
            Staffs = staffs
                .OrderBy(x => x.FullName)
                .Select(x => new ShiftOptimizationOptionDto(x.StaffId, x.FullName))
                .ToList(),
            Shifts = shifts
                .OrderBy(x => x.StartTime)
                .Select(x => new ShiftOptimizationOptionDto(x.ShiftId, x.Name))
                .ToList(),
            Roles = roles
                .Select(x => new ShiftOptimizationOptionDto(x.RoleId, x.Name))
                .ToList(),
            Availability = availability.Select(x => (object)new
            {
                x.StaffAvailabilityRuleId,
                x.StaffId,
                StaffName = staffNames.GetValueOrDefault(x.StaffId, $"#{x.StaffId}"),
                x.DayOfWeek,
                x.StartTime,
                x.EndTime,
                x.EffectiveFrom,
                x.EffectiveTo
            }).ToList(),
            Constraints = constraints.Select(x => (object)new
            {
                x.StaffWorkConstraintId,
                x.StaffId,
                StaffName = staffNames.GetValueOrDefault(x.StaffId, $"#{x.StaffId}"),
                x.TargetWeeklyHours,
                x.MaxWeeklyHours,
                x.MaxDailyHours,
                x.MinimumRestMinutes,
                x.EffectiveFrom,
                x.EffectiveTo
            }).ToList(),
            Requirements = requirements.Select(x => (object)new
            {
                x.StoreStaffingRequirementId,
                x.ShiftId,
                ShiftName = shiftNames.GetValueOrDefault(x.ShiftId, $"#{x.ShiftId}"),
                x.DayOfWeek,
                x.MinimumStaff,
                x.TargetStaff,
                x.MaximumStaff,
                x.RequiredRoleId,
                RequiredRoleName = x.RequiredRoleId.HasValue
                    ? roleNames.GetValueOrDefault(
                        x.RequiredRoleId.Value,
                        $"#{x.RequiredRoleId}")
                    : "Không yêu cầu",
                x.EffectiveFrom,
                x.EffectiveTo
            }).ToList(),
            TimeOffs = timeOffs.Select(x => (object)new
            {
                x.StaffTimeOffId,
                x.StaffId,
                StaffName = staffNames.GetValueOrDefault(x.StaffId, $"#{x.StaffId}"),
                x.FromUtc,
                x.ToUtc,
                x.Status,
                x.Reason
            }).ToList()
        };
    }

    public async Task SaveAvailabilityAsync(
        AdminActorContext actor,
        SaveAvailabilityRuleDto input,
        CancellationToken ct = default)
    {
        var storeId = await StaffStoreAsync(input.StaffId, ct);
        await RequireStoreAsync(actor, storeId);
        if (input.EndTime == input.StartTime)
            throw new ArgumentException("Khoảng khả dụng phải có thời lượng khác 0.");

        _repository.Add(new StaffAvailabilityRule
        {
            StaffId = input.StaffId,
            DayOfWeek = input.DayOfWeek,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            EffectiveFrom = input.EffectiveFrom.Date,
            EffectiveTo = input.EffectiveTo?.Date,
            CreatedByStaffId = actor.StaffId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync(ct);
    }

    public async Task SaveConstraintAsync(
        AdminActorContext actor,
        SaveWorkConstraintDto input,
        CancellationToken ct = default)
    {
        var storeId = await StaffStoreAsync(input.StaffId, ct);
        await RequireStoreAsync(actor, storeId);
        if (input.TargetWeeklyHours > input.MaxWeeklyHours)
            throw new ArgumentException(
                "Giờ mục tiêu không được vượt giờ tối đa mỗi tuần.");

        _repository.Add(new StaffWorkConstraint
        {
            StaffId = input.StaffId,
            EffectiveFrom = input.EffectiveFrom.Date,
            EffectiveTo = input.EffectiveTo?.Date,
            TargetWeeklyHours = input.TargetWeeklyHours,
            MaxWeeklyHours = input.MaxWeeklyHours,
            MaxDailyHours = input.MaxDailyHours,
            MinimumRestMinutes = input.MinimumRestMinutes,
            CreatedByStaffId = actor.StaffId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync(ct);
    }

    public async Task SaveRequirementAsync(
        AdminActorContext actor,
        SaveStaffingRequirementDto input,
        CancellationToken ct = default)
    {
        await RequireStoreAsync(actor, input.StoreId);
        if (input.MinimumStaff > input.TargetStaff
            || input.TargetStaff > input.MaximumStaff)
        {
            throw new ArgumentException(
                "Phải thỏa số người tối thiểu ≤ mục tiêu ≤ tối đa.");
        }

        var shifts = await _repository.GetShiftsAsync(input.StoreId, ct);
        if (!shifts.Any(x => x.ShiftId == input.ShiftId))
            throw new ArgumentException("Mẫu ca không thuộc cửa hàng.");

        _repository.Add(new StoreStaffingRequirement
        {
            StoreId = input.StoreId,
            ShiftId = input.ShiftId,
            DayOfWeek = input.DayOfWeek,
            MinimumStaff = input.MinimumStaff,
            TargetStaff = input.TargetStaff,
            MaximumStaff = input.MaximumStaff,
            RequiredRoleId = input.RequiredRoleId,
            EffectiveFrom = input.EffectiveFrom.Date,
            EffectiveTo = input.EffectiveTo?.Date,
            CreatedByStaffId = actor.StaffId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync(ct);
    }

    public async Task SaveTimeOffAsync(
        AdminActorContext actor,
        SaveTimeOffDto input,
        CancellationToken ct = default)
    {
        var storeId = await StaffStoreAsync(input.StaffId, ct);
        await RequireStoreAsync(actor, storeId);
        if (input.ToUtc <= input.FromUtc)
            throw new ArgumentException("Khoảng nghỉ không hợp lệ.");

        _repository.Add(new StaffTimeOff
        {
            StaffId = input.StaffId,
            FromUtc = input.FromUtc,
            ToUtc = input.ToUtc,
            Reason = input.Reason.Trim(),
            Status = "APPROVED",
            RequestedByStaffId = actor.StaffId,
            ReviewedByStaffId = actor.StaffId,
            CreatedAtUtc = DateTime.UtcNow,
            ReviewedAtUtc = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync(ct);
    }

    private async Task<int> StaffStoreAsync(int staffId, CancellationToken ct) =>
        (await _repository.GetStaffAsync(staffId, ct))?.StoreId
        ?? throw new KeyNotFoundException("Không tìm thấy nhân viên.");

    private async Task RequireStoreAsync(AdminActorContext actor, int storeId)
    {
        if (actor.StaffId <= 0
            || !(await _scope.GetAllowedStoresAsync(actor.StaffId))
                .Any(x => x.StoreId == storeId))
        {
            throw new UnauthorizedAccessException(
                "Cửa hàng nằm ngoài phạm vi được cấp.");
        }
    }
}
