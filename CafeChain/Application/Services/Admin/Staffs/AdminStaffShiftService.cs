using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.Staffs;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Staffs;
using CafeChain.ViewModels.Admin.Staffs;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Staffs;

public sealed class AdminStaffShiftService : IAdminStaffShiftService
{
    private const string Scheduled = "SCHEDULED";
    private const string Cancelled = "CANCELLED";
    private readonly IAdminStaffShiftRepository _repository;

    public AdminStaffShiftService(IAdminStaffShiftRepository repository) => _repository = repository;

    public async Task<StaffShiftManagementVM> GetPageAsync(
        int storeId,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<StaffShiftStoreOptionVM> stores,
        IReadOnlySet<string> permissions,
        CancellationToken ct = default)
    {
        var store = await _repository.GetStoreAsync(storeId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy cửa hàng.");
        var staffs = await _repository.GetStaffsAsync(storeId, startDate, endDate, ct);
        var schedules = await _repository.GetSchedulesAsync(storeId, startDate, endDate, ct);
        var templates = await _repository.GetTemplatesAsync(storeId, true, ct);

        return new StaffShiftManagementVM
        {
            StoreId = storeId,
            StoreName = store.Name,
            StartDate = startDate.Date,
            Stores = stores,
            CanCreate = permissions.Contains(PermissionConstants.ShiftCreate),
            CanUpdate = permissions.Contains(PermissionConstants.ShiftUpdate),
            CanCancel = permissions.Contains(PermissionConstants.ShiftCancel),
            Templates = templates.Select(MapTemplate).ToList(),
            StaffRows = staffs.Select(staff => new StaffScheduleRowVM
            {
                StaffId = staff.StaffId,
                StaffName = staff.FullName,
                AvatarUrl = staff.AvatarUrl,
                RoleNames = string.Join(", ", staff.Account.AccountRoles.Select(x => x.Role.Name)),
                Schedules = schedules.Where(x => x.StaffId == staff.StaffId).Select(MapSchedule).ToList()
            }).ToList()
        };
    }

    public async Task<ServiceResult> AssignAsync(int storeId, int actorStaffId, AssignStaffShiftRequest request, CancellationToken ct = default)
    {
        var validation = ValidateCustomTime(request.UseCustomTime, request.CustomStartTime, request.CustomEndTime);
        if (validation != null) return validation;

        var staff = await _repository.GetStaffAsync(request.StaffId, ct);
        var template = await _repository.GetTemplateAsync(request.ShiftId, ct);
        if (staff == null || !staff.Active || staff.EmployeeStatus == 3)
            return Fail("Nhân viên không tồn tại hoặc đã ngưng làm việc.", "INVALID_STAFF");
        if (staff.StoreId != storeId) return Forbidden();
        if (template == null || template.StoreId != storeId) return Forbidden();
        if (!template.Active) return Fail("Mẫu ca đã ngưng hoạt động.", "INACTIVE_SHIFT");

        var customStart = request.UseCustomTime ? request.CustomStartTime : null;
        var customEnd = request.UseCustomTime ? request.CustomEndTime : null;
        var overlap = await HasOverlapAsync(staff.StaffId, request.WorkDate, template, customStart, customEnd, null, ct);
        if (overlap) return Fail("Thời gian làm việc bị trùng với một lịch đã xếp.", "SHIFT_OVERLAP");

        var scheduledStatus = await RequireStatusAsync(Scheduled, ct);
        var existing = await _repository.GetScheduleAsync(staff.StaffId, template.ShiftId, request.WorkDate, ct);
        if (existing != null && existing.Status.Code == Scheduled)
            return Fail("Lịch này đã được xếp trước đó.", "DUPLICATE_SHIFT");

        await _repository.BeginTransactionAsync(ct);
        try
        {
            if (existing != null)
            {
                var before = Snapshot(existing);
                existing.StatusId = scheduledStatus.StaffShiftStatusId;
                existing.CustomStartTime = customStart;
                existing.CustomEndTime = customEnd;
                AddAudit("StaffShifts", existing.StaffShiftId, "RESTORE", before, Snapshot(existing), actorStaffId);
                await _repository.SaveChangesAsync(ct);
                await _repository.CommitTransactionAsync(ct);
                return Success("Đã khôi phục và cập nhật lịch làm việc.", existing.StaffShiftId);
            }

            var staffShift = new StaffShift
            {
                StaffId = staff.StaffId,
                ShiftId = template.ShiftId,
                WorkDate = request.WorkDate.Date,
                CustomStartTime = customStart,
                CustomEndTime = customEnd,
                StatusId = scheduledStatus.StaffShiftStatusId
            };
            _repository.Add(staffShift);
            await _repository.SaveChangesAsync(ct);
            AddAudit("StaffShifts", staffShift.StaffShiftId, "CREATE", null, Snapshot(staffShift), actorStaffId);
            await _repository.SaveChangesAsync(ct);
            await _repository.CommitTransactionAsync(ct);
            return Success("Đã phân lịch làm việc.", staffShift.StaffShiftId);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _repository.RollbackTransactionAsync(ct);
            return Conflict();
        }
        catch
        {
            await _repository.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> UpdateAssignmentAsync(int storeId, int actorStaffId, UpdateStaffShiftRequest request, CancellationToken ct = default)
    {
        var validation = ValidateCustomTime(request.UseCustomTime, request.CustomStartTime, request.CustomEndTime);
        if (validation != null) return validation;
        var schedule = await _repository.GetScheduleAsync(request.StaffShiftId, ct);
        var template = await _repository.GetTemplateAsync(request.ShiftId, ct);
        if (schedule == null) return Fail("Không tìm thấy lịch làm việc.", "NOT_FOUND");
        if (request.StaffId != schedule.StaffId) return Forbidden();
        if (schedule.Staff.StoreId != storeId || schedule.Shift.StoreId != storeId || template?.StoreId != storeId) return Forbidden();
        if (schedule.Status.Code != Scheduled) return Fail("Chỉ lịch đang hiệu lực mới được sửa.", "INVALID_STATUS");
        if (template == null || !template.Active) return Fail("Mẫu ca không còn hoạt động.", "INACTIVE_SHIFT");
        if (!VersionMatches(schedule.RowVersion, request.RowVersion)) return Conflict();

        var customStart = request.UseCustomTime ? request.CustomStartTime : null;
        var customEnd = request.UseCustomTime ? request.CustomEndTime : null;
        if (await HasOverlapAsync(schedule.StaffId, request.WorkDate, template, customStart, customEnd, schedule.StaffShiftId, ct))
            return Fail("Thời gian làm việc bị trùng với một lịch đã xếp.", "SHIFT_OVERLAP");

        var before = Snapshot(schedule);
        schedule.ShiftId = template.ShiftId;
        schedule.WorkDate = request.WorkDate.Date;
        schedule.CustomStartTime = customStart;
        schedule.CustomEndTime = customEnd;
        AddAudit("StaffShifts", schedule.StaffShiftId, "UPDATE", before, Snapshot(schedule), actorStaffId);
        return await SaveMutationAsync("Đã cập nhật lịch làm việc.", schedule.StaffShiftId, ct);
    }

    public async Task<ServiceResult> CancelAsync(int storeId, int actorStaffId, CancelStaffShiftRequest request, CancellationToken ct = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason)) return Fail("Vui lòng nhập lý do hủy lịch.", "REASON_REQUIRED");
        var schedule = await _repository.GetScheduleAsync(request.StaffShiftId, ct);
        if (schedule == null) return Fail("Không tìm thấy lịch làm việc.", "NOT_FOUND");
        if (schedule.Staff.StoreId != storeId || schedule.Shift.StoreId != storeId) return Forbidden();
        if (schedule.Status.Code == Cancelled) return Success("Lịch đã được hủy trước đó.", schedule.StaffShiftId);
        if (!VersionMatches(schedule.RowVersion, request.RowVersion)) return Conflict();

        var before = Snapshot(schedule);
        schedule.StatusId = (await RequireStatusAsync(Cancelled, ct)).StaffShiftStatusId;
        AddAudit("StaffShifts", schedule.StaffShiftId, "CANCEL", before,
            new { Schedule = Snapshot(schedule), Reason = reason }, actorStaffId);
        return await SaveMutationAsync("Đã hủy lịch và giữ lại lịch sử.", schedule.StaffShiftId, ct);
    }

    public async Task<ServiceResult> CreateTemplateAsync(int storeId, int actorStaffId, CreateShiftTemplateRequest request, CancellationToken ct = default)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return Fail("Tên mẫu ca là bắt buộc.", "NAME_REQUIRED");
        var template = new Shift
        {
            Name = name,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsOvernight = request.EndTime <= request.StartTime,
            Duration = Duration(request.StartTime, request.EndTime),
            Notes = request.Notes?.Trim(),
            Active = true,
            StoreId = storeId
        };
        await _repository.BeginTransactionAsync(ct);
        try
        {
            _repository.Add(template);
            await _repository.SaveChangesAsync(ct);
            AddAudit("Shifts", template.ShiftId, "CREATE", null, Snapshot(template), actorStaffId);
            await _repository.SaveChangesAsync(ct);
            await _repository.CommitTransactionAsync(ct);
            return Success("Đã tạo mẫu ca.", template.ShiftId);
        }
        catch
        {
            await _repository.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> UpdateTemplateAsync(int storeId, int actorStaffId, UpdateShiftTemplateRequest request, CancellationToken ct = default)
    {
        var template = await _repository.GetTemplateAsync(request.ShiftId, ct);
        if (template == null) return Fail("Không tìm thấy mẫu ca.", "NOT_FOUND");
        if (template.StoreId != storeId) return Forbidden();
        if (!VersionMatches(template.RowVersion, request.RowVersion)) return Conflict();
        if (string.IsNullOrWhiteSpace(request.Name)) return Fail("Tên mẫu ca là bắt buộc.", "NAME_REQUIRED");

        if (template.StartTime != request.StartTime || template.EndTime != request.EndTime)
        {
            var assigned = await _repository.GetTemplateSchedulesAsync(template.ShiftId, ct);
            foreach (var schedule in assigned.Where(x => x.Status.Code == Scheduled && !x.CustomStartTime.HasValue))
            {
                var proposed = new Shift { StartTime = request.StartTime, EndTime = request.EndTime, IsOvernight = request.EndTime <= request.StartTime };
                if (await HasOverlapAsync(schedule.StaffId, schedule.WorkDate, proposed, null, null, schedule.StaffShiftId, ct))
                    return Fail("Giờ mới làm trùng lịch của nhân viên đã được phân. Hãy điều chỉnh lịch trước.", "TEMPLATE_IMPACT_CONFLICT");
            }
        }

        var before = Snapshot(template);
        template.Name = request.Name.Trim();
        template.StartTime = request.StartTime;
        template.EndTime = request.EndTime;
        template.IsOvernight = request.EndTime <= request.StartTime;
        template.Duration = Duration(request.StartTime, request.EndTime);
        template.Notes = request.Notes?.Trim();
        AddAudit("Shifts", template.ShiftId, "UPDATE", before, Snapshot(template), actorStaffId);
        return await SaveMutationAsync("Đã cập nhật mẫu ca.", template.ShiftId, ct);
    }

    public async Task<ServiceResult> ToggleTemplateAsync(int storeId, int actorStaffId, ToggleShiftTemplateRequest request, CancellationToken ct = default)
    {
        var template = await _repository.GetTemplateAsync(request.ShiftId, ct);
        if (template == null) return Fail("Không tìm thấy mẫu ca.", "NOT_FOUND");
        if (template.StoreId != storeId) return Forbidden();
        if (!VersionMatches(template.RowVersion, request.RowVersion)) return Conflict();
        var before = Snapshot(template);
        template.Active = !template.Active;
        AddAudit("Shifts", template.ShiftId, "TOGGLE_STATUS", before, Snapshot(template), actorStaffId);
        return await SaveMutationAsync(template.Active ? "Đã kích hoạt mẫu ca." : "Đã ngưng mẫu ca.", template.ShiftId, ct);
    }

    private async Task<bool> HasOverlapAsync(int staffId, DateTime date, Shift template, TimeSpan? customStart, TimeSpan? customEnd, int? excludeId, CancellationToken ct)
    {
        var candidate = Interval(date, customStart ?? template.StartTime, customEnd ?? template.EndTime, template.IsOvernight);
        var existing = await _repository.GetPotentialOverlapsAsync(staffId, date.AddDays(-1), date.AddDays(1), excludeId, ct);
        return existing.Where(x => x.Status.Code == Scheduled).Any(x =>
        {
            var interval = Interval(x.WorkDate, x.CustomStartTime ?? x.Shift.StartTime,
                x.CustomEndTime ?? x.Shift.EndTime, x.Shift.IsOvernight);
            return candidate.Start < interval.End && candidate.End > interval.Start;
        });
    }

    private async Task<StaffShiftStatus> RequireStatusAsync(string code, CancellationToken ct) =>
        await _repository.GetStatusAsync(code, ct) ?? throw new InvalidOperationException($"Thiếu trạng thái {code}.");

    private async Task<ServiceResult> SaveMutationAsync(string message, int id, CancellationToken ct)
    {
        try
        {
            await _repository.SaveChangesAsync(ct);
            return Success(message, id);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
    }

    private void AddAudit(string table, int id, string action, object? before, object? after, int actorStaffId) =>
        _repository.Add(new AuditLog
        {
            TableName = table,
            RecordId = id,
            Action = action,
            OldData = before == null ? null : JsonSerializer.Serialize(before),
            NewData = after == null ? null : JsonSerializer.Serialize(after),
            UserId = actorStaffId,
            CreatedAt = DateTime.UtcNow
        });

    private static ServiceResult? ValidateCustomTime(bool useCustom, TimeSpan? start, TimeSpan? end)
    {
        if (!useCustom && (start.HasValue || end.HasValue)) return Fail("Tắt giờ riêng thì không được gửi giờ tùy chỉnh.", "INVALID_CUSTOM_TIME");
        if (useCustom && (!start.HasValue || !end.HasValue)) return Fail("Giờ bắt đầu và kết thúc tùy chỉnh đều bắt buộc.", "INVALID_CUSTOM_TIME");
        return null;
    }

    private static (DateTime Start, DateTime End) Interval(DateTime date, TimeSpan start, TimeSpan end, bool overnight)
    {
        var from = date.Date.Add(start);
        var to = date.Date.Add(end);
        if (overnight || end <= start) to = to.AddDays(1);
        return (from, to);
    }

    private static TimeSpan Duration(TimeSpan start, TimeSpan end) => end <= start
        ? TimeSpan.FromHours(24) - start + end
        : end - start;

    private static bool VersionMatches(byte[] current, string supplied)
    {
        try { return current.SequenceEqual(Convert.FromBase64String(supplied)); }
        catch { return false; }
    }

    private static object Snapshot(Shift x) => new { x.ShiftId, x.Name, x.StartTime, x.EndTime, x.IsOvernight, x.Active, x.StoreId, x.Notes };
    private static object Snapshot(StaffShift x) => new { x.StaffShiftId, x.StaffId, x.ShiftId, x.WorkDate, x.CustomStartTime, x.CustomEndTime, x.StatusId };

    private static ShiftTemplateVM MapTemplate(Shift x) => new()
    {
        ShiftId = x.ShiftId, Name = x.Name, StartTime = x.StartTime, EndTime = x.EndTime,
        IsOvernight = x.IsOvernight, Active = x.Active, Notes = x.Notes,
        RowVersion = Convert.ToBase64String(x.RowVersion ?? Array.Empty<byte>())
    };

    private static StaffScheduleItemVM MapSchedule(StaffShift x)
    {
        var start = x.CustomStartTime ?? x.Shift.StartTime;
        var end = x.CustomEndTime ?? x.Shift.EndTime;
        return new StaffScheduleItemVM
        {
            StaffShiftId = x.StaffShiftId, ShiftId = x.ShiftId, ShiftName = x.Shift.Name,
            WorkDate = x.WorkDate, EffectiveStart = start, EffectiveEnd = end,
            CustomStartTime = x.CustomStartTime, CustomEndTime = x.CustomEndTime,
            IsOvernight = x.Shift.IsOvernight || end <= start, StatusCode = x.Status.Code,
            RowVersion = Convert.ToBase64String(x.RowVersion ?? Array.Empty<byte>())
        };
    }

    private static ServiceResult Success(string message, int id)
    {
        var result = ServiceResult.Success(message); result.EntityId = id; return result;
    }
    private static ServiceResult Fail(string message, string code) => ServiceResult.Failure(message, errorCode: code);
    private static ServiceResult Forbidden() => Fail("Bạn không có quyền thao tác dữ liệu của cửa hàng này.", "FORBIDDEN");
    private static ServiceResult Conflict() => Fail("Dữ liệu đã thay đổi. Vui lòng tải lại trang.", "CONCURRENCY_CONFLICT");
}
