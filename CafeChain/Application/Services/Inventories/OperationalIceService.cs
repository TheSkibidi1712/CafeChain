using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Inventories.Ice;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;

namespace CafeChain.Application.Services.Inventories;

public sealed class OperationalIceService : IOperationalIceService
{
    // Ingredient.Code is the stable catalog identity. Do not infer ice eligibility from display names.
    private const string OperationalIceIngredientCode = "ING00007";
    private const string LinkWorkShiftAuditAction = "LINK_WORKSHIFT";

    // Quy tắc đá được dùng chung cho UI và backend validation.
    private static readonly string[] ManageRoles =
    [
        RoleConstants.BusinessOwner,
        RoleConstants.StoreManager,
        RoleConstants.AccountantWarehouse,
        RoleConstants.SystemAdmin,
        RoleConstants.ShiftSupervisor
    ];

    private static readonly string[] ApproveRoles =
    [
        RoleConstants.BusinessOwner,
        RoleConstants.StoreManager,
        RoleConstants.AccountantWarehouse,
        RoleConstants.SystemAdmin
    ];

    private static readonly string[] HighVarianceApproveRoles =
    [
        RoleConstants.BusinessOwner,
        RoleConstants.AccountantWarehouse,
        RoleConstants.SystemAdmin
    ];

    private readonly AppDbContext _context;
    private readonly IScopeAuthorizationService _scopeAuthorization;
    private readonly IInventoryCostLayerConsumptionService? _costLayerConsumption;
    private readonly IUnitConversionService? _unitConversionService;

    public OperationalIceService(
        AppDbContext context,
        IScopeAuthorizationService scopeAuthorization,
        IInventoryCostLayerConsumptionService? costLayerConsumption = null,
        IUnitConversionService? unitConversionService = null)
    {
        _context = context;
        _scopeAuthorization = scopeAuthorization;
        _costLayerConsumption = costLayerConsumption;
        _unitConversionService = unitConversionService;
    }

    public Task<ServiceResult<OperationalIcePolicySetupDto>> GetPolicySetupAsync(
        int storeId,
        CancellationToken cancellationToken = default) =>
        BuildPolicySetupAsync(storeId, cancellationToken);

    public async Task<ServiceResult<IReadOnlyList<OperationalIceScheduleOptionDto>>> GetScheduleOptionsAsync(
        int storeId,
        DateTime businessDate,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync(actor, storeId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<IReadOnlyList<OperationalIceScheduleOptionDto>>(authorization);

        var date = businessDate.Date;
        var snapshots = await LoadScheduleSnapshotsAsync(storeId, date, cancellationToken);
        if (snapshots.Count == 0)
            return ServiceResult<IReadOnlyList<OperationalIceScheduleOptionDto>>.Success([]);

        var existingSourceIds = await _context.OperationalShifts.AsNoTracking()
            .Where(x => x.StoreId == storeId
                        && x.BusinessDate == date
                        && x.SourceScheduleShiftId != null
                        && x.Status != OperationalIceStatuses.Cancelled)
            .Select(x => x.SourceScheduleShiftId!.Value)
            .ToListAsync(cancellationToken);

        var options = snapshots
            .Where(snapshot => !existingSourceIds.Contains(snapshot.ScheduleShiftId))
            .OrderBy(snapshot => snapshot.StartAtUtc)
            .ToArray();

        return ServiceResult<IReadOnlyList<OperationalIceScheduleOptionDto>>.Success(options);
    }

    public async Task<ServiceResult<IReadOnlyList<OperationalIceScheduleReviewDto>>> GetScheduleReviewsAsync(
        int storeId,
        DateTime businessDate,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync(actor, storeId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<IReadOnlyList<OperationalIceScheduleReviewDto>>(authorization);

        var date = businessDate.Date;
        var shifts = await _context.OperationalShifts.AsNoTracking()
            .Where(x => x.StoreId == storeId
                        && x.BusinessDate == date
                        && x.CreationSource == OperationalIceCreationSources.StaffSchedule
                        && x.SourceScheduleShiftId != null
                        && x.Status != OperationalIceStatuses.Cancelled)
            .Select(x => new
            {
                x.OperationalShiftId,
                SourceScheduleShiftId = x.SourceScheduleShiftId!.Value,
                x.Name,
                x.StartAtUtc,
                x.EndAtUtc,
                x.ShiftLeadId,
                x.Status
            })
            .ToListAsync(cancellationToken);
        if (shifts.Count == 0)
            return ServiceResult<IReadOnlyList<OperationalIceScheduleReviewDto>>.Success([]);

        var sourceIds = shifts.Select(x => x.SourceScheduleShiftId).Distinct().ToArray();
        var cancelledAssignments = await _context.StaffShifts.AsNoTracking()
            .Where(x => sourceIds.Contains(x.ShiftId)
                        && x.WorkDate == date
                        && x.Shift.StoreId == storeId
                        && x.Status.Code == "CANCELLED")
            .Select(x => new { x.ShiftId, x.StaffId })
            .ToListAsync(cancellationToken);
        var cancelledBySource = cancelledAssignments
            .GroupBy(x => x.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.StaffId).ToHashSet());
        var snapshots = (await LoadScheduleSnapshotsAsync(storeId, date, cancellationToken))
            .ToDictionary(x => x.ScheduleShiftId);
        var reviews = shifts.Select(shift =>
        {
            snapshots.TryGetValue(shift.SourceScheduleShiftId, out var current);
            cancelledBySource.TryGetValue(shift.SourceScheduleShiftId, out var cancelledStaffIds);
            var hasCancelledAssignments = cancelledStaffIds is { Count: > 0 };
            var requiresLeadReplacement = shift.ShiftLeadId.HasValue
                                          && cancelledStaffIds?.Contains(shift.ShiftLeadId.Value) == true;
            var currentLeadId = current?.SuggestedShiftLeadId;
            var hasChanges = current == null
                             || hasCancelledAssignments
                             || !string.Equals(shift.Name, current.Name, StringComparison.Ordinal)
                             || shift.StartAtUtc != current.StartAtUtc
                             || shift.EndAtUtc != current.EndAtUtc
                             || shift.ShiftLeadId != currentLeadId;
            return new OperationalIceScheduleReviewDto
            {
                OperationalShiftId = shift.OperationalShiftId,
                IsScheduleAvailable = current != null,
                HasChanges = hasChanges,
                CanSync = current != null
                          && hasChanges
                          && shift.Status == OperationalIceStatuses.Draft
                          && currentLeadId.HasValue,
                HasCancelledAssignments = hasCancelledAssignments,
                RequiresLeadReplacement = requiresLeadReplacement,
                BlocksOpening = current == null || requiresLeadReplacement,
                SavedName = shift.Name,
                SavedStartAtUtc = shift.StartAtUtc,
                SavedEndAtUtc = shift.EndAtUtc,
                SavedShiftLeadId = shift.ShiftLeadId,
                CurrentName = current?.Name,
                CurrentStartAtUtc = current?.StartAtUtc,
                CurrentEndAtUtc = current?.EndAtUtc,
                CurrentShiftLeadId = currentLeadId,
                StaffCount = current?.StaffCount ?? 0,
                CancelledStaffCount = cancelledStaffIds?.Count ?? 0
            };
        }).ToArray();

        return ServiceResult<IReadOnlyList<OperationalIceScheduleReviewDto>>.Success(reviews);
    }

    public async Task<ServiceResult<IReadOnlyList<OperationalIceWorkShiftSuggestionDto>>> GetWorkShiftSuggestionsAsync(
        int operationalShiftId,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var shift = await _context.OperationalShifts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationalShiftId == operationalShiftId, cancellationToken);
        if (shift == null)
            return NotFound<IReadOnlyList<OperationalIceWorkShiftSuggestionDto>>("Không tìm thấy ca vận hành.");
        var authorization = await AuthorizeAsync(actor, shift.StoreId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<IReadOnlyList<OperationalIceWorkShiftSuggestionDto>>(authorization);
        if (shift.Status != OperationalIceStatuses.Open)
            return ServiceResult<IReadOnlyList<OperationalIceWorkShiftSuggestionDto>>.Success([]);

        var startLocal = shift.StartAtUtc.ToLocalTime();
        var endLocal = shift.EndAtUtc.ToLocalTime();
        var (businessWindowStart, businessWindowEnd) = LocalBusinessWindow(shift, endLocal);
        var suggestions = await _context.WorkShifts.AsNoTracking()
            .Where(x => x.StoreId == shift.StoreId
                        && (x.Status == "Open" || x.Status == "Closed")
                        && x.StartTime >= businessWindowStart
                        && x.StartTime < businessWindowEnd
                        && x.StartTime < endLocal
                        && (x.EndTime == null || x.EndTime > startLocal)
                        && !_context.OperationalShiftWorkShifts.Any(link => link.WorkShiftId == x.ShiftId))
            .OrderBy(x => x.StartTime)
            .Take(30)
            .Select(x => new OperationalIceWorkShiftSuggestionDto
            {
                WorkShiftId = x.ShiftId,
                StaffName = x.User.FullName,
                StartTime = x.StartTime,
                EndTime = x.EndTime
            })
            .ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<OperationalIceWorkShiftSuggestionDto>>.Success(suggestions);
    }

    public async Task<ServiceResult> SavePolicyAsync(
        SaveIcePolicyRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync(actor, request.StoreId, ApproveRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return authorization;
        var quantityError = ValidatePolicyQuantities(
            request.SuggestedDailyQuantity,
            request.SuggestedShiftQuantity,
            request.VarianceApprovalQuantityThreshold,
            request.VarianceApprovalPercentThreshold,
            request.RequireVarianceApproval);
        if (request.IngredientId <= 0 || request.DisplayUnitId <= 0 || quantityError != null)
            return Invalid(quantityError ?? "Nguyên liệu đá và đơn vị hiển thị là bắt buộc.");

        var setup = await GetPolicySetupAsync(request.StoreId, cancellationToken);
        if (!setup.IsSuccess || setup.Data == null)
            return ServiceResult.Failure(setup.Message, setup.Errors, setup.ErrorCode);
        if (!setup.Data.Ingredients.Any(x => x.Id == request.IngredientId))
            return Invalid("Chỉ được chọn nguyên liệu đã được định danh cho nghiệp vụ quản lý đá, đang hoạt động, có tồn tại chi nhánh và dùng gram làm đơn vị cơ sở.");
        if (!setup.Data.Units.Any(x => x.Id == request.DisplayUnitId))
            return Invalid("Đơn vị hiển thị của đá chỉ được là g hoặc kg đang hoạt động.");

        if (_unitConversionService == null)
            return Invalid("Không thể kiểm tra quy đổi đơn vị đá lúc này.");
        var conversion = await _unitConversionService.ConvertAsync(
            request.IngredientId,
            1m,
            request.DisplayUnitId);
        if (!conversion.IsSuccess || conversion.Data <= 0)
        {
            return ServiceResult.Failure(
                "Chưa cấu hình quy đổi từ đơn vị hiển thị sang đơn vị tồn kho của nguyên liệu đá.",
                errorCode: OperationalIceErrorCodes.InvalidRequest);
        }

        var policy = await _context.IcePolicies
            .SingleOrDefaultAsync(x => x.StoreId == request.StoreId, cancellationToken);
        if (policy == null)
        {
            policy = new IcePolicy { StoreId = request.StoreId };
            _context.IcePolicies.Add(policy);
        }
        else if (policy.IngredientId != request.IngredientId
                 && await _context.IceAllocations.AnyAsync(x => x.IcePolicyId == policy.IcePolicyId, cancellationToken))
        {
            return ServiceResult.Failure(
                "Không thể đổi nguyên liệu đá khi chính sách đã có lịch sử phân bổ.",
                errorCode: OperationalIceErrorCodes.InvalidState);
        }

        policy.IngredientId = request.IngredientId;
        policy.DisplayUnitId = request.DisplayUnitId;
        policy.SuggestedDailyQuantity = request.SuggestedDailyQuantity;
        policy.SuggestedShiftQuantity = request.SuggestedShiftQuantity;
        policy.AllowSupplementalIssue = request.AllowSupplementalIssue;
        policy.AllowSameDayCarryOver = request.AllowSameDayCarryOver;
        policy.RequireVarianceApproval = request.RequireVarianceApproval;
        policy.VarianceApprovalQuantityThreshold = request.VarianceApprovalQuantityThreshold;
        policy.VarianceApprovalPercentThreshold = request.VarianceApprovalPercentThreshold;
        policy.Active = true;
        policy.UpdatedByStaffId = actor.StaffId;
        policy.UpdatedAtUtc = DateTime.UtcNow;

        return await SaveAsync("Đã lưu chính sách đá vận hành.", cancellationToken);
    }

    public async Task<ServiceResult<OperationalShiftSummaryDto>> CreateShiftAsync(
        CreateOperationalShiftRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync(actor, request.StoreId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<OperationalShiftSummaryDto>(authorization);
        if (string.IsNullOrWhiteSpace(request.Name)
            || request.EndAtUtc <= request.StartAtUtc
            || request.EndAtUtc - request.StartAtUtc > TimeSpan.FromHours(24))
            return Invalid<OperationalShiftSummaryDto>("Tên ca và khoảng thời gian vận hành không hợp lệ.");

        var businessDate = request.BusinessDate.Date;
        if (request.StartAtUtc.ToLocalTime().Date != businessDate)
            return Invalid<OperationalShiftSummaryDto>("Thời gian bắt đầu phải thuộc đúng ngày kinh doanh đã chọn.");
        if (!OperationalIceCreationSources.All.Contains(request.CreationSource, StringComparer.Ordinal))
            return Invalid<OperationalShiftSummaryDto>("Nguồn tạo ca vận hành không hợp lệ.");
        var isScheduleSource = request.CreationSource == OperationalIceCreationSources.StaffSchedule;
        if (isScheduleSource != request.SourceScheduleShiftId.HasValue)
            return Invalid<OperationalShiftSummaryDto>("Nguồn lịch làm việc của ca vận hành không nhất quán.");

        if (isScheduleSource)
        {
            var sourceIsValid = await _context.Shifts.AsNoTracking()
                .AnyAsync(x => x.ShiftId == request.SourceScheduleShiftId
                               && x.StoreId == request.StoreId
                               && x.Active
                               && x.StaffShifts.Any(schedule =>
                                   schedule.WorkDate == businessDate
                                   && schedule.Status.Code != "CANCELLED"),
                    cancellationToken);
            if (!sourceIsValid)
                return Invalid<OperationalShiftSummaryDto>("Ca lịch không thuộc chi nhánh hoặc ngày kinh doanh đã chọn.");
        }

        var setup = await GetPolicySetupAsync(request.StoreId, cancellationToken);
        if (!setup.IsSuccess || setup.Data == null || !setup.Data.IsValid)
            return InvalidState<OperationalShiftSummaryDto>(setup.Data?.StatusMessage ?? setup.Message);

        if (!request.ShiftLeadId.HasValue)
            return Invalid<OperationalShiftSummaryDto>("Phải phân công ca trưởng trước khi tạo ca vận hành đá.");
        var leadValid = await _context.Staffs.AsNoTracking()
            .AnyAsync(x => x.StaffId == request.ShiftLeadId
                           && x.StoreId == request.StoreId
                           && x.Active
                           && x.Account.Active
                           && x.Account.AccountRoles.Any(role => role.Role.Active
                               && (role.Role.Name == RoleConstants.ShiftSupervisor
                                   || role.Role.Name == RoleConstants.StoreManager)),
                cancellationToken);
        if (!leadValid)
            return Invalid<OperationalShiftSummaryDto>("Ca trưởng phải đang hoạt động, đúng chi nhánh và có vai trò Ca trưởng hoặc Cửa hàng trưởng.");

        var duplicate = isScheduleSource
            ? await _context.OperationalShifts.AsNoTracking()
                .AnyAsync(x => x.StoreId == request.StoreId
                               && x.BusinessDate == businessDate
                               && x.SourceScheduleShiftId == request.SourceScheduleShiftId
                               && x.Status != OperationalIceStatuses.Cancelled,
                    cancellationToken)
            : await _context.OperationalShifts.AsNoTracking()
                .AnyAsync(x => x.StoreId == request.StoreId
                               && x.BusinessDate == businessDate
                               && x.Name == request.Name.Trim(),
                    cancellationToken);
        if (duplicate)
            return isScheduleSource
                ? ScheduleShiftConflict<OperationalShiftSummaryDto>()
                : ServiceResult<OperationalShiftSummaryDto>.Failure("Tên ca vận hành đã tồn tại trong ngày.", errorCode: OperationalIceErrorCodes.InvalidState);

        var shift = new OperationalShift
        {
            StoreId = request.StoreId,
            BusinessDate = businessDate,
            Name = request.Name.Trim(),
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            CreationSource = request.CreationSource,
            SourceScheduleShiftId = request.SourceScheduleShiftId,
            ShiftLeadId = request.ShiftLeadId,
            Status = OperationalIceStatuses.Draft,
            CreatedByStaffId = actor.StaffId,
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.OperationalShifts.Add(shift);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return ServiceResult<OperationalShiftSummaryDto>.Success(Map(shift), "Đã tạo ca vận hành.");
        }
        catch (DbUpdateException exception) when (isScheduleSource && IsUniqueConstraintViolation(exception))
        {
            _context.Entry(shift).State = EntityState.Detached;
            return ScheduleShiftConflict<OperationalShiftSummaryDto>();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<OperationalShiftSummaryDto>();
        }
        catch (DbUpdateException)
        {
            return InvalidState<OperationalShiftSummaryDto>("Dữ liệu ca vận hành vừa thay đổi. Vui lòng tải lại.");
        }
    }

    public async Task<ServiceResult<OperationalShiftSummaryDto>> SyncDraftWithScheduleAsync(
        SyncOperationalShiftScheduleRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var shift = await _context.OperationalShifts
            .SingleOrDefaultAsync(
                x => x.OperationalShiftId == request.OperationalShiftId,
                cancellationToken);
        if (shift == null)
            return NotFound<OperationalShiftSummaryDto>("Không tìm thấy ca vận hành.");

        var authorization = await AuthorizeAsync(actor, shift.StoreId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<OperationalShiftSummaryDto>(authorization);
        if (shift.CreationSource != OperationalIceCreationSources.StaffSchedule
            || !shift.SourceScheduleShiftId.HasValue)
        {
            return InvalidState<OperationalShiftSummaryDto>(
                "Chỉ ca được tạo từ lịch làm việc mới có thể đồng bộ lại lịch.");
        }
        if (shift.Status != OperationalIceStatuses.Draft)
        {
            return InvalidState<OperationalShiftSummaryDto>(
                "Chỉ ca nháp mới được đồng bộ lại lịch làm việc. Ca đã mở không được tự động thay đổi.");
        }

        var current = (await LoadScheduleSnapshotsAsync(
                shift.StoreId,
                shift.BusinessDate,
                cancellationToken))
            .SingleOrDefault(x => x.ScheduleShiftId == shift.SourceScheduleShiftId.Value);
        if (current == null)
        {
            return InvalidState<OperationalShiftSummaryDto>(
                "Lịch nguồn không còn hoạt động trong ngày kinh doanh đã chọn.");
        }

        var synchronizedLeadId = current.SuggestedShiftLeadId ?? shift.ShiftLeadId;
        if (!synchronizedLeadId.HasValue)
        {
            return InvalidState<OperationalShiftSummaryDto>(
                "Lịch hiện tại chưa có ca trưởng hợp lệ để đồng bộ.");
        }

        var before = ShiftAuditSnapshot(shift);
        shift.Name = current.Name;
        shift.StartAtUtc = current.StartAtUtc;
        shift.EndAtUtc = current.EndAtUtc;
        shift.ShiftLeadId = synchronizedLeadId;
        AddOperationalShiftAudit(
            shift,
            "SYNC_STAFF_SCHEDULE",
            before,
            ShiftAuditSnapshot(shift),
            actor.StaffId);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return ServiceResult<OperationalShiftSummaryDto>.Success(
                Map(shift),
                "Đã đồng bộ ca nháp với lịch làm việc hiện tại.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<OperationalShiftSummaryDto>();
        }
        catch (DbUpdateException)
        {
            return InvalidState<OperationalShiftSummaryDto>(
                "Dữ liệu ca hoặc lịch làm việc vừa thay đổi. Vui lòng tải lại.");
        }
    }

    public async Task<ServiceResult<OperationalShiftSummaryDto>> ConvertDraftToManualAsync(
        ConvertOperationalShiftToManualRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return Invalid<OperationalShiftSummaryDto>("Vui lòng nhập lý do chuyển ca sang tạo thủ công.");

        var shift = await _context.OperationalShifts
            .SingleOrDefaultAsync(
                x => x.OperationalShiftId == request.OperationalShiftId,
                cancellationToken);
        if (shift == null)
            return NotFound<OperationalShiftSummaryDto>("Không tìm thấy ca vận hành.");
        var authorization = await AuthorizeAsync(actor, shift.StoreId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<OperationalShiftSummaryDto>(authorization);
        if (shift.Status != OperationalIceStatuses.Draft)
            return InvalidState<OperationalShiftSummaryDto>("Chỉ ca nháp mới được chuyển sang ca thủ công.");
        if (shift.CreationSource != OperationalIceCreationSources.StaffSchedule
            || !shift.SourceScheduleShiftId.HasValue)
        {
            return InvalidState<OperationalShiftSummaryDto>("Ca vận hành đã là ca thủ công.");
        }

        var before = ShiftAuditSnapshot(shift);
        shift.CreationSource = OperationalIceCreationSources.Manual;
        shift.SourceScheduleShiftId = null;
        AddOperationalShiftAudit(
            shift,
            "CONVERT_TO_MANUAL",
            new { Shift = before, Reason = reason },
            ShiftAuditSnapshot(shift),
            actor.StaffId);
        return await SaveShiftMutationAsync(
            shift,
            "Đã chuyển ca nháp sang tạo thủ công và giữ nguyên thông tin ca.",
            cancellationToken);
    }

    public async Task<ServiceResult<OperationalShiftSummaryDto>> UpdateDraftShiftLeadAsync(
        UpdateOperationalShiftLeadRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return Invalid<OperationalShiftSummaryDto>("Vui lòng nhập lý do thay ca trưởng.");

        var shift = await _context.OperationalShifts
            .SingleOrDefaultAsync(
                x => x.OperationalShiftId == request.OperationalShiftId,
                cancellationToken);
        if (shift == null)
            return NotFound<OperationalShiftSummaryDto>("Không tìm thấy ca vận hành.");
        var authorization = await AuthorizeAsync(actor, shift.StoreId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<OperationalShiftSummaryDto>(authorization);
        if (shift.Status != OperationalIceStatuses.Draft)
            return InvalidState<OperationalShiftSummaryDto>("Chỉ ca nháp mới được thay ca trưởng.");
        if (!await IsEligibleShiftLeadAsync(
                request.ShiftLeadId,
                shift.StoreId,
                cancellationToken))
        {
            return Invalid<OperationalShiftSummaryDto>(
                "Ca trưởng thay thế phải đang hoạt động, đúng cửa hàng và có vai trò Ca trưởng hoặc Quản lý chi nhánh.");
        }

        var before = ShiftAuditSnapshot(shift);
        shift.ShiftLeadId = request.ShiftLeadId;
        AddOperationalShiftAudit(
            shift,
            "UPDATE_SHIFT_LEAD",
            new { Shift = before, Reason = reason },
            ShiftAuditSnapshot(shift),
            actor.StaffId);
        return await SaveShiftMutationAsync(
            shift,
            "Đã cập nhật ca trưởng cho ca nháp.",
            cancellationToken);
    }

    public async Task<ServiceResult<OperationalShiftSummaryDto>> CancelDraftShiftAsync(
        CancelDraftOperationalShiftRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return Invalid<OperationalShiftSummaryDto>("Vui lòng nhập lý do hủy ca vận hành.");

        var shift = await _context.OperationalShifts
            .SingleOrDefaultAsync(
                x => x.OperationalShiftId == request.OperationalShiftId,
                cancellationToken);
        if (shift == null)
            return NotFound<OperationalShiftSummaryDto>("Không tìm thấy ca vận hành.");
        var authorization = await AuthorizeAsync(actor, shift.StoreId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<OperationalShiftSummaryDto>(authorization);
        if (shift.Status != OperationalIceStatuses.Draft)
            return InvalidState<OperationalShiftSummaryDto>("Chỉ ca nháp chưa cấp đá mới được hủy tại đây.");
        if (await _context.IceAllocations.AnyAsync(
                x => x.OperationalShiftId == shift.OperationalShiftId,
                cancellationToken))
        {
            return InvalidState<OperationalShiftSummaryDto>(
                "Ca đã có phân bổ đá. Vui lòng xử lý phân bổ thay vì hủy ca nháp.");
        }

        var before = ShiftAuditSnapshot(shift);
        shift.Status = OperationalIceStatuses.Cancelled;
        AddOperationalShiftAudit(
            shift,
            "CANCEL_DRAFT",
            new { Shift = before, Reason = reason },
            ShiftAuditSnapshot(shift),
            actor.StaffId);
        return await SaveShiftMutationAsync(
            shift,
            "Đã hủy ca vận hành nháp và giữ lại lịch sử.",
            cancellationToken);
    }

    public async Task<ServiceResult<IceAllocationDto>> OpenAllocationAsync(
        OpenIceAllocationRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (request.InitialIssuedQuantity <= 0)
            return Invalid<IceAllocationDto>("Lượng đá cấp đầu ca phải lớn hơn 0.");

        var shift = await _context.OperationalShifts
            .SingleOrDefaultAsync(x => x.OperationalShiftId == request.OperationalShiftId, cancellationToken);
        if (shift == null)
            return NotFound<IceAllocationDto>("Không tìm thấy ca vận hành.");
        var authorization = await AuthorizeAsync(actor, shift.StoreId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<IceAllocationDto>(authorization);
        if (shift.Status is not (OperationalIceStatuses.Draft or OperationalIceStatuses.Open))
            return InvalidState<IceAllocationDto>("Chỉ ca nháp hoặc đang mở mới được cấp đá.");
        if (await _context.IceAllocations.AnyAsync(x => x.OperationalShiftId == shift.OperationalShiftId, cancellationToken))
            return InvalidState<IceAllocationDto>("Ca vận hành đã có phân bổ đá.");
        var openingValidation = await ValidateShiftCanOpenAsync(shift, cancellationToken);
        if (!openingValidation.IsSuccess)
            return Fail<IceAllocationDto>(openingValidation);

        var policy = await _context.IcePolicies
            .SingleOrDefaultAsync(x => x.StoreId == shift.StoreId && x.Active, cancellationToken);
        if (policy == null)
            return InvalidState<IceAllocationDto>("Cửa hàng chưa cấu hình chính sách đá vận hành.");
        var inventory = await _context.StoreInventories
            .SingleOrDefaultAsync(x => x.StoreId == shift.StoreId && x.IngredientId == policy.IngredientId && x.SupersededByStoreInventoryId == null, cancellationToken);
        if (inventory == null)
            return NotFound<IceAllocationDto>("Cửa hàng chưa có tồn kho cho nguyên liệu đá.");

        var setup = await GetPolicySetupAsync(shift.StoreId, cancellationToken);
        if (!setup.IsSuccess || setup.Data == null || !setup.Data.IsValid)
            return InvalidState<IceAllocationDto>(setup.Data?.StatusMessage ?? setup.Message);
        if (!shift.ShiftLeadId.HasValue)
            return InvalidState<IceAllocationDto>("Ca vận hành chưa có ca trưởng nên chưa thể mở cấp đá.");
        if (inventory.AvailableQty - inventory.ReservedQty < request.InitialIssuedQuantity)
            return Insufficient<IceAllocationDto>(inventory.AvailableQty - inventory.ReservedQty);

        var linkValidation = await ValidateWorkShiftsAsync(shift, request.WorkShiftIds, cancellationToken);
        if (!linkValidation.IsSuccess)
            return Fail<IceAllocationDto>(linkValidation);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var publicId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var allocation = new IceAllocation
            {
                PublicId = publicId,
                OperationalShiftId = shift.OperationalShiftId,
                IcePolicyId = policy.IcePolicyId,
                StoreInventoryId = inventory.StoreInventoryId,
                IngredientId = policy.IngredientId,
                InitialIssuedQuantity = request.InitialIssuedQuantity,
                ReservedOutstandingQuantity = request.InitialIssuedQuantity,
                ReservationReference = $"ICE:{publicId:N}",
                Status = OperationalIceStatuses.Open,
                CreatedByStaffId = actor.StaffId,
                OpenedByStaffId = actor.StaffId,
                CreatedAtUtc = now,
                OpenedAtUtc = now,
                Revision = 1
            };
            _context.IceAllocations.Add(allocation);
            inventory.ReservedQty += request.InitialIssuedQuantity;
            inventory.LastUpdated = now;
            shift.Status = OperationalIceStatuses.Open;
            shift.OpenedByStaffId ??= actor.StaffId;
            shift.OpenedAtUtc ??= now;
            foreach (var workShiftId in request.WorkShiftIds.Distinct())
            {
                _context.OperationalShiftWorkShifts.Add(new OperationalShiftWorkShift
                {
                    OperationalShiftId = shift.OperationalShiftId,
                    WorkShiftId = workShiftId,
                    LinkedByStaffId = actor.StaffId,
                    LinkedAtUtc = now
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult<IceAllocationDto>.Success(Map(allocation, shift.StoreId), "Đã mở phân bổ đá đầu ca.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict<IceAllocationDto>();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return InvalidState<IceAllocationDto>("Dữ liệu ca hoặc liên kết WorkShift vừa được cập nhật. Vui lòng tải lại.");
        }
    }

    public async Task<ServiceResult> LinkWorkShiftAsync(
        LinkOperationalWorkShiftRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default) =>
        await LinkWorkShiftsAsync(
            new LinkOperationalWorkShiftsRequest
            {
                OperationalShiftId = request.OperationalShiftId,
                WorkShiftIds = [request.WorkShiftId]
            },
            actor,
            cancellationToken);

    public async Task<ServiceResult> LinkWorkShiftsAsync(
        LinkOperationalWorkShiftsRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var shift = await _context.OperationalShifts.SingleOrDefaultAsync(x => x.OperationalShiftId == request.OperationalShiftId, cancellationToken);
        if (shift == null)
            return NotFound("Không tìm thấy ca vận hành.");
        var authorization = await AuthorizeAsync(actor, shift.StoreId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return authorization;
        if (shift.Status != OperationalIceStatuses.Open)
            return InvalidState("Chỉ ca vận hành đang mở mới nhận thêm WorkShift POS.");
        var requestedIds = request.WorkShiftIds.Where(x => x > 0).Distinct().ToArray();
        if (requestedIds.Length == 0)
            return Invalid("Vui lòng chọn ít nhất một WorkShift POS để liên kết.");
        var validation = await ValidateWorkShiftsAsync(shift, requestedIds, cancellationToken);
        if (!validation.IsSuccess)
            return validation;

        var existingIds = await _context.OperationalShiftWorkShifts.AsNoTracking()
            .Where(x => x.OperationalShiftId == shift.OperationalShiftId
                        && requestedIds.Contains(x.WorkShiftId))
            .Select(x => x.WorkShiftId)
            .ToListAsync(cancellationToken);
        var missingIds = requestedIds.Except(existingIds).ToArray();
        if (missingIds.Length == 0)
            return ServiceResult.Success("Các WorkShift POS đã được liên kết trước đó.");

        var now = DateTime.UtcNow;
        _context.OperationalShiftWorkShifts.AddRange(missingIds.Select(workShiftId => new OperationalShiftWorkShift
        {
            OperationalShiftId = shift.OperationalShiftId,
            WorkShiftId = workShiftId,
            LinkedByStaffId = actor.StaffId,
            LinkedAtUtc = now
        }));
        _context.AuditLogs.AddRange(missingIds.Select(workShiftId => new AuditLog
        {
            TableName = nameof(OperationalShiftWorkShift),
            RecordId = shift.OperationalShiftId,
            Action = LinkWorkShiftAuditAction,
            OldData = null,
            NewData = JsonSerializer.Serialize(new
            {
                shift.OperationalShiftId,
                WorkShiftId = workShiftId
            }),
            UserId = actor.StaffId,
            CreatedAt = now
        }));
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult.Success($"Đã liên kết {missingIds.Length} WorkShift POS với ca vận hành.");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            _context.ChangeTracker.Clear();
            var links = await _context.OperationalShiftWorkShifts.AsNoTracking()
                .Where(x => requestedIds.Contains(x.WorkShiftId))
                .ToListAsync(cancellationToken);
            return links.Count == requestedIds.Length
                   && links.All(x => x.OperationalShiftId == shift.OperationalShiftId)
                ? ServiceResult.Success("Các WorkShift POS đã được liên kết trước đó.")
                : ServiceResult.Failure(
                    "Có WorkShift POS vừa được liên kết với ca vận hành khác. Vui lòng tải lại.",
                     errorCode: OperationalIceErrorCodes.WorkShiftAlreadyLinked);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult.Failure(
                "Dữ liệu ca vừa được người khác cập nhật. Vui lòng tải lại.",
                errorCode: OperationalIceErrorCodes.ConcurrencyConflict);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult.Failure(
                "Không thể lưu liên kết WorkShift POS. Vui lòng tải lại và thử lại.",
                errorCode: OperationalIceErrorCodes.InvalidState);
        }
    }

    public async Task<ServiceResult<IceSupplementalIssueDto>> RequestSupplementalAsync(
        RequestSupplementalIceRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.Reason))
            return Invalid<IceSupplementalIssueDto>("Lượng cấp bổ sung và lý do là bắt buộc.");
        var allocation = await _context.IceAllocations
            .Include(x => x.OperationalShift)
            .Include(x => x.IcePolicy)
            .SingleOrDefaultAsync(x => x.IceAllocationId == request.IceAllocationId, cancellationToken);
        if (allocation == null)
            return NotFound<IceSupplementalIssueDto>("Không tìm thấy phân bổ đá.");
        var authorization = await AuthorizeAsync(actor, allocation.OperationalShift.StoreId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<IceSupplementalIssueDto>(authorization);
        if (allocation.Status != OperationalIceStatuses.Open || !allocation.IcePolicy.AllowSupplementalIssue)
            return InvalidState<IceSupplementalIssueDto>("Phân bổ này không cho phép cấp bổ sung.");

        var issue = new IceSupplementalIssue
        {
            PublicId = Guid.NewGuid(),
            IceAllocationId = allocation.IceAllocationId,
            Quantity = request.Quantity,
            Reason = request.Reason.Trim(),
            Status = IceSupplementalIssueStatuses.Pending,
            RequestedByStaffId = actor.StaffId,
            RequestedAtUtc = DateTime.UtcNow
        };
        _context.IceSupplementalIssues.Add(issue);
        var saved = await SaveAsync("Đã gửi yêu cầu cấp bổ sung đá.", cancellationToken);
        return saved.IsSuccess
            ? ServiceResult<IceSupplementalIssueDto>.Success(Map(issue), saved.Message)
            : Fail<IceSupplementalIssueDto>(saved);
    }

    public async Task<ServiceResult<IceSupplementalIssueDto>> DecideSupplementalAsync(
        DecideSupplementalIceRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var issue = await _context.IceSupplementalIssues
            .Include(x => x.IceAllocation).ThenInclude(x => x.OperationalShift)
            .Include(x => x.IceAllocation).ThenInclude(x => x.StoreInventory)
            .SingleOrDefaultAsync(x => x.PublicId == request.SupplementalIssuePublicId, cancellationToken);
        if (issue == null)
            return NotFound<IceSupplementalIssueDto>("Không tìm thấy yêu cầu cấp bổ sung.");
        var authorization = await AuthorizeAsync(actor, issue.IceAllocation.OperationalShift.StoreId, ApproveRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<IceSupplementalIssueDto>(authorization);
        if (issue.Status != IceSupplementalIssueStatuses.Pending)
            return InvalidState<IceSupplementalIssueDto>("Yêu cầu cấp bổ sung đã được xử lý.");
        if (!request.Approve && string.IsNullOrWhiteSpace(request.RejectionReason))
            return Invalid<IceSupplementalIssueDto>("Phải nhập lý do khi từ chối cấp bổ sung.");

        var now = DateTime.UtcNow;
        if (!request.Approve)
        {
            issue.Status = IceSupplementalIssueStatuses.Rejected;
            issue.RejectedByStaffId = actor.StaffId;
            issue.RejectedAtUtc = now;
            issue.RejectionReason = request.RejectionReason!.Trim();
        }
        else
        {
            var allocation = issue.IceAllocation;
            if (allocation.Status != OperationalIceStatuses.Open)
                return InvalidState<IceSupplementalIssueDto>("Phân bổ đá không còn ở trạng thái đang mở.");
            var inventory = allocation.StoreInventory;
            var usable = inventory.AvailableQty - inventory.ReservedQty;
            if (usable < issue.Quantity)
                return Insufficient<IceSupplementalIssueDto>(usable);

            inventory.ReservedQty += issue.Quantity;
            inventory.LastUpdated = now;
            allocation.SupplementalIssuedQuantity += issue.Quantity;
            allocation.ReservedOutstandingQuantity += issue.Quantity;
            allocation.Revision += 1;
            issue.Status = IceSupplementalIssueStatuses.Approved;
            issue.ApprovedByStaffId = actor.StaffId;
            issue.ApprovedAtUtc = now;
            issue.ReservationApplied = true;
        }

        var saved = await SaveAsync(request.Approve ? "Đã duyệt cấp bổ sung đá." : "Đã từ chối cấp bổ sung đá.", cancellationToken);
        return saved.IsSuccess
            ? ServiceResult<IceSupplementalIssueDto>.Success(Map(issue), saved.Message)
            : Fail<IceSupplementalIssueDto>(saved);
    }

    public async Task<ServiceResult<IceCarryOverDto>> ConfirmCarryOverAsync(
        ConfirmIceCarryOverRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0 || request.FromIceAllocationId == request.ToIceAllocationId
            || request.ReceivedByStaffId <= 0 || request.ReceivedByStaffId == actor.StaffId)
        {
            return Invalid<IceCarryOverDto>("Dữ liệu bàn giao đá không hợp lệ; người giao và người nhận phải khác nhau.");
        }

        var allocations = await _context.IceAllocations
            .Include(x => x.OperationalShift)
            .Include(x => x.IcePolicy)
            .Include(x => x.StoreInventory)
            .Where(x => x.IceAllocationId == request.FromIceAllocationId || x.IceAllocationId == request.ToIceAllocationId)
            .ToListAsync(cancellationToken);
        var source = allocations.SingleOrDefault(x => x.IceAllocationId == request.FromIceAllocationId);
        var target = allocations.SingleOrDefault(x => x.IceAllocationId == request.ToIceAllocationId);
        if (source == null || target == null)
            return NotFound<IceCarryOverDto>("Không tìm thấy phân bổ đá giao hoặc nhận.");

        var authorization = await AuthorizeAsync(actor, source.OperationalShift.StoreId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<IceCarryOverDto>(authorization);
        if (source.Status != OperationalIceStatuses.Open || target.Status != OperationalIceStatuses.Open)
            return InvalidState<IceCarryOverDto>("Chỉ phân bổ đang mở mới được bàn giao đá.");
        if (!source.IcePolicy.AllowSameDayCarryOver || !target.IcePolicy.AllowSameDayCarryOver)
            return InvalidState<IceCarryOverDto>("Chính sách cửa hàng không cho phép bàn giao đá giữa ca.");
        if (source.OperationalShift.StoreId != target.OperationalShift.StoreId
            || source.OperationalShift.BusinessDate.Date != target.OperationalShift.BusinessDate.Date
            || source.IngredientId != target.IngredientId
            || source.StoreInventoryId != target.StoreInventoryId)
        {
            return Invalid<IceCarryOverDto>("Chỉ được bàn giao cùng nguyên liệu, cùng cửa hàng và cùng ngày kinh doanh.");
        }
        if (source.OperationalShift.EndAtUtc > target.OperationalShift.StartAtUtc)
            return Invalid<IceCarryOverDto>("Ca nhận phải bắt đầu sau ca giao.");
        if (source.ReservedOutstandingQuantity < request.Quantity)
            return Invalid<IceCarryOverDto>("Lượng đá còn giữ chỗ của ca giao không đủ để bàn giao.");
        if (await _context.IceCarryOvers.AnyAsync(x =>
                x.FromIceAllocationId == source.IceAllocationId
                && x.ToIceAllocationId == target.IceAllocationId,
                cancellationToken))
        {
            return InvalidState<IceCarryOverDto>("Hai ca này đã có một lần bàn giao đá.");
        }

        var receiverValid = await _context.Staffs.AsNoTracking().AnyAsync(x =>
            x.StaffId == request.ReceivedByStaffId && x.StoreId == source.OperationalShift.StoreId && x.Active,
            cancellationToken);
        if (!receiverValid)
            return Invalid<IceCarryOverDto>("Người nhận không hoạt động tại cửa hàng này.");

        var now = DateTime.UtcNow;
        var carry = new IceCarryOver
        {
            PublicId = Guid.NewGuid(),
            FromOperationalShiftId = source.OperationalShiftId,
            ToOperationalShiftId = target.OperationalShiftId,
            FromIceAllocationId = source.IceAllocationId,
            ToIceAllocationId = target.IceAllocationId,
            Quantity = request.Quantity,
            Status = IceCarryOverStatuses.Confirmed,
            HandedOverByStaffId = actor.StaffId,
            ReceivedByStaffId = request.ReceivedByStaffId,
            CreatedAtUtc = now,
            ConfirmedAtUtc = now
        };
        _context.IceCarryOvers.Add(carry);
        source.ClosingCarryQuantity += request.Quantity;
        source.ReservedOutstandingQuantity -= request.Quantity;
        target.OpeningCarryQuantity += request.Quantity;
        target.ReservedOutstandingQuantity += request.Quantity;
        source.Revision += 1;
        target.Revision += 1;

        var saved = await SaveAsync("Đã xác nhận bàn giao đá giữa hai ca.", cancellationToken);
        return saved.IsSuccess
            ? ServiceResult<IceCarryOverDto>.Success(Map(carry), saved.Message)
            : Fail<IceCarryOverDto>(saved);
    }

    public async Task<ServiceResult<IceCloseResultDto>> CloseAllocationAsync(
        CloseIceAllocationRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (request.ReturnedQuantity < 0)
            return Invalid<IceCloseResultDto>("Lượng đá trả kho không được âm.");

        var allocation = await LoadAllocationForCloseAsync(request.IceAllocationId, cancellationToken);
        if (allocation == null)
            return NotFound<IceCloseResultDto>("Không tìm thấy phân bổ đá.");
        var authorization = await AuthorizeAsync(actor, allocation.OperationalShift.StoreId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<IceCloseResultDto>(authorization);
        if (allocation.Status != OperationalIceStatuses.Open)
            return InvalidState<IceCloseResultDto>("Chỉ phân bổ đang mở mới được gửi chốt.");
        if (allocation.SupplementalIssues.Any(x => x.Status == IceSupplementalIssueStatuses.Pending))
            return InvalidState<IceCloseResultDto>("Còn yêu cầu cấp bổ sung chưa được xử lý.");
        if (request.ReturnedQuantity > allocation.ReservedOutstandingQuantity)
            return Invalid<IceCloseResultDto>("Lượng trả vượt quá lượng đá còn giữ chỗ của ca.");

        if (request.ReturnedQuantity > 0)
        {
            if (!string.Equals(request.ReturnCondition, IceReturnConditions.SealedIntact, StringComparison.Ordinal)
                || !request.ReturnReceivedByStaffId.HasValue
                || request.ReturnReceivedByStaffId.Value <= 0
                || request.ReturnReceivedByStaffId.Value == actor.StaffId)
            {
                return Invalid<IceCloseResultDto>("Chỉ đá còn nguyên bao mới được trả và phải có hai nhân sự giao nhận khác nhau.");
            }
            var receiverValid = await _context.Staffs.AsNoTracking().AnyAsync(x =>
                x.StaffId == request.ReturnReceivedByStaffId.Value
                && x.StoreId == allocation.OperationalShift.StoreId
                && x.Active,
                cancellationToken);
            if (!receiverValid)
                return Invalid<IceCloseResultDto>("Người nhận trả kho không hoạt động tại cửa hàng này.");
        }

        var theoretical = await CalculateTheoreticalUsageAsync(allocation, cancellationToken);
        var totalIssued = allocation.InitialIssuedQuantity + allocation.SupplementalIssuedQuantity;
        var actual = allocation.OpeningCarryQuantity + totalIssued
                     - allocation.ClosingCarryQuantity - request.ReturnedQuantity;
        if (actual < 0)
            return Invalid<IceCloseResultDto>("Tồn bàn giao và lượng trả vượt tổng lượng đá ca đã nhận.");
        var variance = actual - theoretical;
        if (variance != 0 && string.IsNullOrWhiteSpace(request.CloseReason))
            return Invalid<IceCloseResultDto>("Phải nhập lý do khi lượng dùng thực tế lệch lượng dùng theo POS.");

        var now = DateTime.UtcNow;
        allocation.ReturnedQuantity = request.ReturnedQuantity;
        allocation.ReturnCondition = request.ReturnedQuantity > 0 ? IceReturnConditions.SealedIntact : null;
        allocation.ReturnedByStaffId = request.ReturnedQuantity > 0 ? actor.StaffId : null;
        allocation.ReturnReceivedByStaffId = request.ReturnedQuantity > 0 ? request.ReturnReceivedByStaffId : null;
        allocation.ReturnedAtUtc = request.ReturnedQuantity > 0 ? now : null;
        allocation.TheoreticalUsageQuantity = theoretical;
        allocation.ActualUsageQuantity = actual;
        allocation.VarianceQuantity = variance;
        allocation.CloseReason = string.IsNullOrWhiteSpace(request.CloseReason) ? null : request.CloseReason.Trim();
        allocation.Revision += 1;

        if (variance > 0)
        {
            allocation.Status = OperationalIceStatuses.PendingApproval;
            allocation.OperationalShift.Status = OperationalIceStatuses.PendingApproval;
        }
        else if (variance < 0)
        {
            if (!ReleaseOutstandingReservation(allocation))
                return InvalidState<IceCloseResultDto>("Dữ liệu giữ chỗ tồn kho không còn nhất quán. Vui lòng đối soát trước khi chốt.");
            allocation.Status = OperationalIceStatuses.ReconciliationRequired;
            allocation.OperationalShift.Status = OperationalIceStatuses.ReconciliationRequired;
        }
        else
        {
            if (!ReleaseOutstandingReservation(allocation))
                return InvalidState<IceCloseResultDto>("Dữ liệu giữ chỗ tồn kho không còn nhất quán. Vui lòng tải lại.");
            CloseAllocation(allocation, actor.StaffId, now);
        }

        var saved = await SaveAsync(
            variance > 0 ? "Chênh lệch dương đang chờ quản lý duyệt."
            : variance < 0 ? "Ca cần đối soát; hệ thống không tự tăng tồn kho."
            : "Đã chốt phân bổ đá, không phát sinh chênh lệch.",
            cancellationToken);
        return saved.IsSuccess
            ? ServiceResult<IceCloseResultDto>.Success(MapClose(allocation), saved.Message)
            : Fail<IceCloseResultDto>(saved);
    }

    public async Task<ServiceResult<IceCloseResultDto>> ApproveVarianceAsync(
        ApproveIceVarianceRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Invalid<IceCloseResultDto>("Lý do duyệt chênh lệch là bắt buộc.");
        var allocation = await LoadAllocationForCloseAsync(request.IceAllocationId, cancellationToken);
        if (allocation == null)
            return NotFound<IceCloseResultDto>("Không tìm thấy phân bổ đá.");

        var existingPosting = await _context.IceInventoryPostings.AsNoTracking()
            .AnyAsync(x => x.IceAllocationId == allocation.IceAllocationId
                           && x.PostingType == IcePostingTypes.VarianceOut,
                cancellationToken);
        if (allocation.Status == OperationalIceStatuses.Closed && existingPosting)
            return ServiceResult<IceCloseResultDto>.Success(MapClose(allocation), "Chênh lệch này đã được ghi nhận trước đó.");

        var authorization = await AuthorizeAsync(actor, allocation.OperationalShift.StoreId, ApproveRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<IceCloseResultDto>(authorization);
        if (allocation.Status != OperationalIceStatuses.PendingApproval || allocation.VarianceQuantity is not > 0)
            return InvalidState<IceCloseResultDto>("Phân bổ không có chênh lệch dương đang chờ duyệt.");

        var variance = allocation.VarianceQuantity.Value;
        var percent = allocation.TheoreticalUsageQuantity > 0
            ? variance / allocation.TheoreticalUsageQuantity * 100m
            : 100m;
        var overLimit = (allocation.IcePolicy.VarianceApprovalQuantityThreshold > 0
                         && variance > allocation.IcePolicy.VarianceApprovalQuantityThreshold)
                        || (allocation.IcePolicy.VarianceApprovalPercentThreshold > 0
                            && percent > allocation.IcePolicy.VarianceApprovalPercentThreshold);
        if (overLimit && !actor.RoleNames.Any(role => HighVarianceApproveRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            return ServiceResult<IceCloseResultDto>.Failure(
                "Chênh lệch vượt hạn mức của quản lý chi nhánh và cần Kế toán kho hoặc Chủ doanh nghiệp duyệt.",
                errorCode: OperationalIceErrorCodes.Forbidden);
        }
        if (allocation.StoreInventory.AvailableQty < variance)
            return Insufficient<IceCloseResultDto>(allocation.StoreInventory.AvailableQty - allocation.StoreInventory.ReservedQty);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            CostLayerConsumptionPlan? costPlan = null;
            if (_costLayerConsumption != null)
            {
                var costResult = await _costLayerConsumption.PlanConsumeAsync(
                    allocation.OperationalShift.StoreId,
                    allocation.IngredientId,
                    null,
                    variance,
                    requireFullCoverage: false,
                    cancellationToken);
                if (!costResult.IsSuccess)
                    return ServiceResult<IceCloseResultDto>.Failure(costResult.Message, costResult.Errors, costResult.ErrorCode);
                costPlan = costResult.Data;
                _costLayerConsumption.ApplyPlan(costPlan);
            }

            var now = DateTime.UtcNow;
            var before = allocation.StoreInventory.AvailableQty;
            allocation.StoreInventory.AvailableQty -= variance;
            allocation.StoreInventory.LastUpdated = now;
            if (!ReleaseOutstandingReservation(allocation))
                return InvalidState<IceCloseResultDto>("Dữ liệu giữ chỗ tồn kho không còn nhất quán. Vui lòng tải lại.");

            var hasCompleteCost = costPlan?.IsFullyCovered == true;
            var movement = new InventoryTransaction
            {
                StoreInventoryId = allocation.StoreInventoryId,
                Type = InventoryTransactionTypeEnum.ICE_VARIANCE_OUT,
                StockStatus = ResolveStockStatus(allocation.StoreInventory),
                Quantity = -variance,
                BeforeQty = before,
                AfterQty = allocation.StoreInventory.AvailableQty,
                UnitCost = hasCompleteCost ? costPlan!.WeightedUnitCost : null,
                TotalCost = hasCompleteCost ? costPlan!.TotalCost : null,
                CreatedAt = now
            };
            _context.InventoryTransactions.Add(movement);
            _context.IceInventoryPostings.Add(new IceInventoryPosting
            {
                IceAllocationId = allocation.IceAllocationId,
                Revision = allocation.Revision,
                PostingType = IcePostingTypes.VarianceOut,
                IdempotencyKey = $"IceVariancePosting:{allocation.IceAllocationId}:{allocation.Revision}",
                InventoryTransaction = movement,
                Quantity = variance,
                UnitCost = movement.UnitCost,
                TotalCost = movement.TotalCost,
                ApprovedByStaffId = actor.StaffId,
                Reason = request.Reason.Trim(),
                CreatedAtUtc = now
            });
            allocation.UnitCostSnapshot = movement.UnitCost;
            allocation.CostSnapshotStatus = hasCompleteCost ? IceCostSnapshotStatuses.Available : IceCostSnapshotStatuses.Missing;
            CloseAllocation(allocation, actor.StaffId, now);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult<IceCloseResultDto>.Success(MapClose(allocation), "Đã duyệt và ghi giảm đúng một lần phần đá chênh lệch.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict<IceCloseResultDto>();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return InvalidState<IceCloseResultDto>("Chênh lệch đã được xử lý hoặc dữ liệu vừa thay đổi. Vui lòng tải lại.");
        }
    }

    public async Task<ServiceResult<IceCloseResultDto>> ReconcileVarianceAsync(
        ReconcileIceVarianceRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Invalid<IceCloseResultDto>("Lý do đối soát là bắt buộc.");
        var allocation = await LoadAllocationForCloseAsync(request.IceAllocationId, cancellationToken);
        if (allocation == null)
            return NotFound<IceCloseResultDto>("Không tìm thấy phân bổ đá.");
        var authorization = await AuthorizeAsync(actor, allocation.OperationalShift.StoreId, ApproveRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return Fail<IceCloseResultDto>(authorization);
        if (allocation.Status != OperationalIceStatuses.ReconciliationRequired || allocation.VarianceQuantity is not < 0)
            return InvalidState<IceCloseResultDto>("Phân bổ không có chênh lệch âm cần đối soát.");

        allocation.ReconciliationReason = request.Reason.Trim();
        CloseAllocation(allocation, actor.StaffId, DateTime.UtcNow);
        var saved = await SaveAsync("Đã hoàn tất đối soát; không phát sinh bút toán tăng tồn tự động.", cancellationToken);
        return saved.IsSuccess
            ? ServiceResult<IceCloseResultDto>.Success(MapClose(allocation), saved.Message)
            : Fail<IceCloseResultDto>(saved);
    }

    public async Task<ServiceResult> CancelAllocationAsync(
        CancelIceAllocationRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Invalid("Lý do hủy phân bổ là bắt buộc.");
        var allocation = await LoadAllocationForCloseAsync(request.IceAllocationId, cancellationToken);
        if (allocation == null)
            return NotFound("Không tìm thấy phân bổ đá.");
        var authorization = await AuthorizeAsync(actor, allocation.OperationalShift.StoreId, ApproveRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return authorization;
        if (allocation.Status is OperationalIceStatuses.Closed or OperationalIceStatuses.Cancelled)
            return InvalidState("Phân bổ đã kết thúc.");
        if (allocation.TheoreticalUsageQuantity > 0 || allocation.ActualUsageQuantity.HasValue
            || allocation.OutgoingCarryOvers.Any(x => x.Status == IceCarryOverStatuses.Confirmed)
            || allocation.IncomingCarryOvers.Any(x => x.Status == IceCarryOverStatuses.Confirmed))
        {
            return InvalidState("Không thể hủy phân bổ đã phát sinh tiêu hao hoặc bàn giao; hãy thực hiện chốt và đối soát.");
        }
        if (!ReleaseOutstandingReservation(allocation))
            return InvalidState("Dữ liệu giữ chỗ tồn kho không còn nhất quán. Vui lòng tải lại.");

        var now = DateTime.UtcNow;
        foreach (var issue in allocation.SupplementalIssues.Where(x => x.Status == IceSupplementalIssueStatuses.Pending))
            issue.Status = IceSupplementalIssueStatuses.Cancelled;
        allocation.Status = OperationalIceStatuses.Cancelled;
        allocation.CloseReason = request.Reason.Trim();
        allocation.ClosedByStaffId = actor.StaffId;
        allocation.ClosedAtUtc = now;
        allocation.OperationalShift.Status = OperationalIceStatuses.Cancelled;
        allocation.OperationalShift.ClosedByStaffId = actor.StaffId;
        allocation.OperationalShift.ClosedAtUtc = now;
        allocation.Revision += 1;
        return await SaveAsync("Đã hủy phân bổ và giải phóng toàn bộ lượng giữ chỗ.", cancellationToken);
    }

    private async Task<IceAllocation?> LoadAllocationForCloseAsync(int allocationId, CancellationToken cancellationToken) =>
        await _context.IceAllocations
            .Include(x => x.OperationalShift)
            .Include(x => x.IcePolicy)
            .Include(x => x.StoreInventory)
            .Include(x => x.SupplementalIssues)
            .Include(x => x.OutgoingCarryOvers)
            .Include(x => x.IncomingCarryOvers)
            .SingleOrDefaultAsync(x => x.IceAllocationId == allocationId, cancellationToken);

    private async Task<decimal> CalculateTheoreticalUsageAsync(IceAllocation allocation, CancellationToken cancellationToken)
    {
        var movements = await (
            from movement in _context.InventoryTransactions.AsNoTracking()
            join order in _context.Orders.AsNoTracking()
                on movement.ReferenceOrderId equals (int?)order.OrderId
            join link in _context.OperationalShiftWorkShifts.AsNoTracking()
                on order.WorkShiftId equals (int?)link.WorkShiftId
            where link.OperationalShiftId == allocation.OperationalShiftId
                  && movement.StoreInventoryId == allocation.StoreInventoryId
                  && (movement.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION
                      || movement.Type == InventoryTransactionTypeEnum.SALES_RETURN)
            select new { movement.Type, movement.Quantity })
            .ToListAsync(cancellationToken);

        var total = movements
            .Where(x => x.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION)
            .Sum(x => x.Quantity)
            - movements
                .Where(x => x.Type == InventoryTransactionTypeEnum.SALES_RETURN)
                .Sum(x => x.Quantity);
        return Math.Max(0m, total);
    }

    private static bool ReleaseOutstandingReservation(IceAllocation allocation)
    {
        var release = allocation.ReservedOutstandingQuantity;
        if (release <= 0)
            return true;
        if (allocation.StoreInventory.ReservedQty < release)
            return false;
        allocation.StoreInventory.ReservedQty -= release;
        allocation.StoreInventory.LastUpdated = DateTime.UtcNow;
        allocation.ReservedOutstandingQuantity = 0;
        return true;
    }

    private static void CloseAllocation(IceAllocation allocation, int staffId, DateTime now)
    {
        allocation.Status = OperationalIceStatuses.Closed;
        allocation.ClosedByStaffId = staffId;
        allocation.ClosedAtUtc = now;
        allocation.OperationalShift.Status = OperationalIceStatuses.Closed;
        allocation.OperationalShift.ClosedByStaffId = staffId;
        allocation.OperationalShift.ClosedAtUtc = now;
        allocation.Revision += 1;
    }

    private static InventoryStockStatus ResolveStockStatus(CafeChain.Models.Stores.StoreInventory inventory)
    {
        if (inventory.AvailableQty < 0)
            return InventoryStockStatus.NEGATIVE_CONFIRMED;
        if (inventory.MinStockLevel.HasValue && inventory.AvailableQty <= inventory.MinStockLevel.Value)
            return InventoryStockStatus.LOW_STOCK;
        return InventoryStockStatus.NORMAL;
    }

    private async Task<IReadOnlyList<OperationalIceScheduleOptionDto>> LoadScheduleSnapshotsAsync(
        int storeId,
        DateTime businessDate,
        CancellationToken cancellationToken)
    {
        var date = businessDate.Date;
        var scheduled = await _context.StaffShifts.AsNoTracking()
            .Where(x => x.WorkDate == date
                        && x.Shift.StoreId == storeId
                        && x.Shift.Active
                        && x.Status.Code != "CANCELLED"
                        && x.Staff.Active
                        && x.Staff.Account.Active)
            .Select(x => new
            {
                x.ShiftId,
                x.Shift.Name,
                x.Shift.StartTime,
                x.Shift.EndTime,
                x.Shift.IsOvernight,
                x.StaffId,
                IsShiftSupervisor = x.Staff.Account.AccountRoles.Any(role =>
                    role.Role.Active && role.Role.Name == RoleConstants.ShiftSupervisor),
                IsStoreManager = x.Staff.Account.AccountRoles.Any(role =>
                    role.Role.Active && role.Role.Name == RoleConstants.StoreManager)
            })
            .ToListAsync(cancellationToken);
        if (scheduled.Count == 0)
            return [];

        var fallbackLeadIds = await _context.Staffs.AsNoTracking()
            .Where(x => x.StoreId == storeId
                        && x.Active
                        && x.Account.Active
                        && x.Account.AccountRoles.Any(role => role.Role.Active
                            && (role.Role.Name == RoleConstants.ShiftSupervisor
                                || role.Role.Name == RoleConstants.StoreManager)))
            .OrderBy(x => x.Account.AccountRoles.Any(role =>
                role.Role.Active && role.Role.Name == RoleConstants.ShiftSupervisor) ? 0 : 1)
            .ThenBy(x => x.StaffId)
            .Select(x => x.StaffId)
            .ToListAsync(cancellationToken);
        var cancelledAssignments = await _context.StaffShifts.AsNoTracking()
            .Where(x => x.WorkDate == date
                        && x.Shift.StoreId == storeId
                        && x.Status.Code == "CANCELLED")
            .Select(x => new { x.ShiftId, x.StaffId })
            .ToListAsync(cancellationToken);
        var cancelledByShift = cancelledAssignments
            .GroupBy(x => x.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.StaffId).ToHashSet());

        return scheduled
            .GroupBy(x => new { x.ShiftId, x.Name, x.StartTime, x.EndTime, x.IsOvernight })
            .OrderBy(group => group.Key.StartTime)
            .Select(group =>
            {
                var startLocal = DateTime.SpecifyKind(date.Add(group.Key.StartTime), DateTimeKind.Local);
                var endLocal = DateTime.SpecifyKind(date.Add(group.Key.EndTime), DateTimeKind.Local);
                if (group.Key.IsOvernight || endLocal <= startLocal)
                    endLocal = endLocal.AddDays(1);
                var leadId = group.Where(x => x.IsShiftSupervisor).OrderBy(x => x.StaffId)
                                 .Select(x => (int?)x.StaffId).FirstOrDefault()
                             ?? group.Where(x => x.IsStoreManager).OrderBy(x => x.StaffId)
                                 .Select(x => (int?)x.StaffId).FirstOrDefault()
                             ?? fallbackLeadIds
                                 .Where(staffId => !cancelledByShift.TryGetValue(group.Key.ShiftId, out var cancelled)
                                                   || !cancelled.Contains(staffId))
                                 .Select(staffId => (int?)staffId)
                                 .FirstOrDefault();
                return new OperationalIceScheduleOptionDto
                {
                    ScheduleShiftId = group.Key.ShiftId,
                    Name = group.Key.Name,
                    BusinessDate = date,
                    StartAtUtc = startLocal.ToUniversalTime(),
                    EndAtUtc = endLocal.ToUniversalTime(),
                    StaffCount = group.Count(),
                    SuggestedShiftLeadId = leadId
                };
            })
            .ToArray();
    }

    private async Task<ServiceResult<OperationalIcePolicySetupDto>> BuildPolicySetupAsync(
        int storeId,
        CancellationToken cancellationToken)
    {
        if (storeId <= 0)
            return Invalid<OperationalIcePolicySetupDto>("Cửa hàng không hợp lệ.");
        var units = await GetCompatibleIceUnitsAsync(cancellationToken);
        var ingredients = units.Count == 2
            ? await GetEligibleIceIngredientsAsync(storeId, cancellationToken)
            : [];
        var policy = await _context.IcePolicies.AsNoTracking()
            .SingleOrDefaultAsync(x => x.StoreId == storeId && x.Active, cancellationToken);
        var inventory = policy == null
            ? null
            : await GetInventorySnapshotAsync(storeId, policy.IngredientId, cancellationToken);
        return ServiceResult<OperationalIcePolicySetupDto>.Success(
            CreatePolicySetup(policy, ingredients, units, inventory));
    }

    private async Task<IReadOnlyList<OperationalIcePolicyOptionDto>> GetCompatibleIceUnitsAsync(
        CancellationToken cancellationToken)
    {
        var massUnits = await _context.Units.AsNoTracking()
            .Where(x => x.Active && x.Type == UnitType.KhoiLuong)
            .OrderBy(x => x.UnitId)
            .ToListAsync(cancellationToken);
        var gram = massUnits.FirstOrDefault(x =>
            PhysicalUnitConversionRegistry.NormalizeUnitCode(x.UnitCode) == PhysicalUnitConversionRegistry.CodeGram);
        var kilogram = massUnits.FirstOrDefault(x =>
            PhysicalUnitConversionRegistry.NormalizeUnitCode(x.UnitCode) == PhysicalUnitConversionRegistry.CodeKilogram);
        if (gram == null || kilogram == null
            || !PhysicalUnitConversionRegistry.TryGetPairFactor(
                gram.UnitCode, kilogram.UnitCode, gram.Type, kilogram.Type, out _)
            || !PhysicalUnitConversionRegistry.TryGetPairFactor(
                kilogram.UnitCode, gram.UnitCode, kilogram.Type, gram.Type, out _))
        {
            return [];
        }

        return [MapPolicyOption(kilogram), MapPolicyOption(gram)];
    }

    private async Task<IReadOnlyList<OperationalIcePolicyOptionDto>> GetEligibleIceIngredientsAsync(
        int storeId,
        CancellationToken cancellationToken)
    {
        var candidates = await _context.Ingredients.AsNoTracking()
            .Include(x => x.BaseUnit)
            .Where(x => x.Active
                        && x.Code == OperationalIceIngredientCode
                        && x.BaseUnit.Active
                        && x.BaseUnit.Type == UnitType.KhoiLuong
                        && x.StoreInventories.Any(inventory =>
                            inventory.StoreId == storeId
                            && inventory.SupersededByStoreInventoryId == null))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return candidates
            .Where(x => PhysicalUnitConversionRegistry.NormalizeUnitCode(x.BaseUnit.UnitCode)
                        == PhysicalUnitConversionRegistry.CodeGram)
            .Select(x => new OperationalIcePolicyOptionDto
            {
                Id = x.IngredientId,
                Code = x.Code,
                Label = $"{x.Code} · {x.Name}"
            })
            .ToArray();
    }

    private Task<OperationalIceInventorySnapshotDto?> GetInventorySnapshotAsync(
        int storeId,
        int ingredientId,
        CancellationToken cancellationToken) =>
        _context.StoreInventories.AsNoTracking()
            .Where(x => x.StoreId == storeId
                        && x.IngredientId == ingredientId
                        && x.SupersededByStoreInventoryId == null)
            .Select(x => new OperationalIceInventorySnapshotDto
            {
                PhysicalQuantity = x.AvailableQty,
                ReservedQuantity = x.ReservedQty,
                AvailableQuantity = x.AvailableQty - x.ReservedQty
            })
            .SingleOrDefaultAsync(cancellationToken);

    private static OperationalIcePolicySetupDto CreatePolicySetup(
        IcePolicy? policy,
        IReadOnlyList<OperationalIcePolicyOptionDto> ingredients,
        IReadOnlyList<OperationalIcePolicyOptionDto> units,
        OperationalIceInventorySnapshotDto? inventory)
    {
        var quantityError = policy == null ? null : ValidatePolicyQuantities(policy);
        var selectionValid = policy != null
                             && ingredients.Any(x => x.Id == policy.IngredientId)
                             && units.Any(x => x.Id == policy.DisplayUnitId);
        var isValid = policy != null && units.Count == 2 && selectionValid
                      && inventory != null && quantityError == null;
        return new OperationalIcePolicySetupDto
        {
            IsConfigured = policy != null,
            IsValid = isValid,
            StatusMessage = PolicyStatusMessage(policy != null, units.Count == 2,
                selectionValid, inventory != null, quantityError, isValid),
            Ingredients = ingredients,
            Units = units,
            Inventory = inventory
        };
    }

    private static OperationalIcePolicyOptionDto MapPolicyOption(CafeChain.Models.Inventories.Ingredients.Unit unit)
    {
        var code = PhysicalUnitConversionRegistry.NormalizeUnitCode(unit.UnitCode);
        return new OperationalIcePolicyOptionDto { Id = unit.UnitId, Code = code, Label = $"{unit.Name} ({code})" };
    }

    private static string PolicyStatusMessage(
        bool configured,
        bool hasUnits,
        bool selectionValid,
        bool hasInventory,
        string? quantityError,
        bool isValid)
    {
        if (!configured) return "Chi nhánh chưa cấu hình chính sách đá.";
        if (!hasUnits) return "Thiếu cặp đơn vị khối lượng g và kg đang hoạt động.";
        if (!selectionValid) return "Nguyên liệu phải được định danh cho nghiệp vụ quản lý đá, đang hoạt động, có tồn tại chi nhánh, base gram và hiển thị theo g hoặc kg.";
        if (!hasInventory) return "Nguyên liệu đá chưa có tồn kho tại chi nhánh.";
        if (quantityError != null) return quantityError;
        return isValid ? "Chính sách đá đã cấu hình hợp lệ." : "Chính sách đá chưa hợp lệ.";
    }

    private async Task<ServiceResult> ValidateShiftCanOpenAsync(
        OperationalShift shift,
        CancellationToken cancellationToken)
    {
        if (shift.CreationSource != OperationalIceCreationSources.StaffSchedule)
            return ServiceResult.Success();

        if (!shift.ShiftLeadId.HasValue
            || !await IsEligibleShiftLeadAsync(
                shift.ShiftLeadId.Value,
                shift.StoreId,
                cancellationToken))
        {
            return InvalidState(
                "Ca vận hành chưa có ca trưởng hợp lệ thuộc cửa hàng nên chưa thể mở cấp đá.");
        }

        if (!shift.SourceScheduleShiftId.HasValue)
            return InvalidState("Ca từ lịch làm việc đang thiếu nguồn lịch.");

        var sourceAvailable = await _context.StaffShifts.AsNoTracking()
            .AnyAsync(
                x => x.ShiftId == shift.SourceScheduleShiftId.Value
                     && x.WorkDate == shift.BusinessDate
                     && x.Shift.StoreId == shift.StoreId
                     && x.Shift.Active
                     && x.Status.Code != "CANCELLED"
                     && x.Staff.Active
                     && x.Staff.Account.Active,
                cancellationToken);
        if (!sourceAvailable)
        {
            return InvalidState(
                "Ca lịch nguồn đã bị hủy hoặc không còn nhân sự hiệu lực. Hãy chuyển ca nháp sang thủ công hoặc hủy ca.");
        }

        var assignedLeadWasCancelled = await _context.StaffShifts.AsNoTracking()
            .AnyAsync(
                x => x.ShiftId == shift.SourceScheduleShiftId.Value
                     && x.WorkDate == shift.BusinessDate
                     && x.Shift.StoreId == shift.StoreId
                     && x.StaffId == shift.ShiftLeadId.Value
                     && x.Status.Code == "CANCELLED",
                cancellationToken);
        return assignedLeadWasCancelled
            ? InvalidState(
                "Ca trưởng trong lịch đã bị hủy phân công. Hãy chọn ca trưởng thay thế trước khi mở ca.")
            : ServiceResult.Success();
    }

    private Task<bool> IsEligibleShiftLeadAsync(
        int staffId,
        int storeId,
        CancellationToken cancellationToken) =>
        _context.Staffs.AsNoTracking()
            .AnyAsync(
                x => x.StaffId == staffId
                     && x.StoreId == storeId
                     && x.Active
                     && x.Account.Active
                     && x.Account.AccountRoles.Any(role =>
                         role.Role.Active
                         && (role.Role.Name == RoleConstants.ShiftSupervisor
                             || role.Role.Name == RoleConstants.StoreManager)),
                cancellationToken);

    private async Task<ServiceResult> ValidateWorkShiftsAsync(
        OperationalShift operationalShift,
        IReadOnlyList<int> workShiftIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = workShiftIds.Where(id => id > 0).Distinct().ToArray();
        if (distinctIds.Length != workShiftIds.Count)
            return Invalid("Danh sách WorkShift POS không hợp lệ hoặc bị trùng.");
        if (distinctIds.Length == 0)
            return ServiceResult.Success();

        var operationalStartLocal = operationalShift.StartAtUtc.ToLocalTime();
        var operationalEndLocal = operationalShift.EndAtUtc.ToLocalTime();
        var (businessWindowStart, businessWindowEnd) =
            LocalBusinessWindow(operationalShift, operationalEndLocal);
        // Keep null EndTime semantics identical to the candidate query: an open
        // WorkShift extends through the operational window until it is closed.
        var currentLocalTime = DateTime.Now;
        var openWorkShiftEnd = currentLocalTime > operationalStartLocal
            ? currentLocalTime
            : operationalEndLocal;
        var workShifts = await _context.WorkShifts.AsNoTracking()
            .Where(x => distinctIds.Contains(x.ShiftId))
            .Select(x => new { x.ShiftId, x.StoreId, x.StartTime, x.EndTime, x.Status })
            .ToListAsync(cancellationToken);
        if (workShifts.Count != distinctIds.Length || workShifts.Any(x => x.StoreId != operationalShift.StoreId))
            return Invalid("WorkShift POS không thuộc cửa hàng của ca vận hành.");
        if (workShifts.Any(x => x.Status is not ("Open" or "Closed")))
            return Invalid("WorkShift POS không ở trạng thái hợp lệ để liên kết.");
        if (workShifts.Any(x => x.StartTime < businessWindowStart
                                || x.StartTime >= businessWindowEnd))
        {
            return Invalid(
                "WorkShift POS không thuộc ngày kinh doanh của ca vận hành.");
        }
        if (workShifts.Any(x => x.StartTime >= operationalEndLocal
                                || (x.EndTime ?? openWorkShiftEnd) <= operationalStartLocal))
            return Invalid("WorkShift POS không giao thời gian với ca vận hành.");
        var links = await _context.OperationalShiftWorkShifts.AsNoTracking()
            .Where(x => distinctIds.Contains(x.WorkShiftId))
            .ToListAsync(cancellationToken);
        return links.Any(x => x.OperationalShiftId != operationalShift.OperationalShiftId)
            ? ServiceResult.Failure("Có WorkShift POS đã liên kết với ca vận hành khác.", errorCode: OperationalIceErrorCodes.WorkShiftAlreadyLinked)
            : ServiceResult.Success();
    }

    private static (DateTime Start, DateTime End) LocalBusinessWindow(
        OperationalShift shift,
        DateTime operationalEndLocal)
    {
        var start = DateTime.SpecifyKind(shift.BusinessDate.Date, DateTimeKind.Local);
        var end = DateTime.SpecifyKind(
            operationalEndLocal.Date.AddDays(1),
            DateTimeKind.Local);
        return (start, end > start ? end : start.AddDays(1));
    }

    private async Task<ServiceResult<OperationalShiftSummaryDto>> SaveShiftMutationAsync(
        OperationalShift shift,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return ServiceResult<OperationalShiftSummaryDto>.Success(Map(shift), message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<OperationalShiftSummaryDto>();
        }
        catch (DbUpdateException)
        {
            return InvalidState<OperationalShiftSummaryDto>(
                "Dữ liệu ca vừa được cập nhật. Vui lòng tải lại.");
        }
    }

    private void AddOperationalShiftAudit(
        OperationalShift shift,
        string action,
        object? oldData,
        object? newData,
        int actorStaffId)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            TableName = nameof(OperationalShift),
            RecordId = shift.OperationalShiftId,
            Action = action,
            OldData = oldData == null ? null : JsonSerializer.Serialize(oldData),
            NewData = newData == null ? null : JsonSerializer.Serialize(newData),
            UserId = actorStaffId,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static OperationalShiftAuditSnapshot ShiftAuditSnapshot(OperationalShift shift) =>
        new(
            shift.CreationSource,
            shift.SourceScheduleShiftId,
            shift.Name,
            shift.StartAtUtc,
            shift.EndAtUtc,
            shift.ShiftLeadId,
            shift.Status);

    private sealed record OperationalShiftAuditSnapshot(
        string CreationSource,
        int? SourceScheduleShiftId,
        string Name,
        DateTime StartAtUtc,
        DateTime EndAtUtc,
        int? ShiftLeadId,
        string Status);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is Microsoft.Data.SqlClient.SqlException sqlException
        && sqlException.Number is 2601 or 2627;

    private static ServiceResult<T> ScheduleShiftConflict<T>() =>
        ServiceResult<T>.Failure(
            "Ca vận hành đá đã được tạo từ ca lịch này trong ngày kinh doanh đã chọn.",
            errorCode: OperationalIceErrorCodes.ScheduleShiftAlreadyUsed);

    private async Task<ServiceResult> AuthorizeAsync(
        AdminActorContext actor,
        int storeId,
        IReadOnlyCollection<string> allowedRoles,
        CancellationToken cancellationToken)
    {
        if (actor.StaffId <= 0 || !actor.RoleNames.Any(role => allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
            return ServiceResult.Failure("Bạn không có quyền thực hiện thao tác quản lý đá này.", errorCode: OperationalIceErrorCodes.Forbidden);
        if (!await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, storeId))
            return ServiceResult.Failure("Bạn không có quyền truy cập cửa hàng đã chọn.", errorCode: OperationalIceErrorCodes.StoreScopeForbidden);
        return ServiceResult.Success();
    }

    private async Task<ServiceResult> SaveAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success(message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Failure("Dữ liệu vừa được người khác cập nhật. Vui lòng tải lại.", errorCode: OperationalIceErrorCodes.ConcurrencyConflict);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Failure("Dữ liệu vừa thay đổi hoặc bị trùng. Vui lòng tải lại.", errorCode: OperationalIceErrorCodes.InvalidState);
        }
    }

    private static OperationalShiftSummaryDto Map(OperationalShift shift) => new()
    {
        OperationalShiftId = shift.OperationalShiftId,
        StoreId = shift.StoreId,
        BusinessDate = shift.BusinessDate,
        Name = shift.Name,
        Status = shift.Status,
        ShiftLeadId = shift.ShiftLeadId
    };

    private static IceAllocationDto Map(IceAllocation allocation, int storeId) => new()
    {
        IceAllocationId = allocation.IceAllocationId,
        PublicId = allocation.PublicId,
        OperationalShiftId = allocation.OperationalShiftId,
        StoreId = storeId,
        IngredientId = allocation.IngredientId,
        InitialIssuedQuantity = allocation.InitialIssuedQuantity,
        SupplementalIssuedQuantity = allocation.SupplementalIssuedQuantity,
        TheoreticalUsageQuantity = allocation.TheoreticalUsageQuantity,
        ReservedOutstandingQuantity = allocation.ReservedOutstandingQuantity,
        Status = allocation.Status
    };

    private static IceSupplementalIssueDto Map(IceSupplementalIssue issue) => new()
    {
        PublicId = issue.PublicId,
        IceAllocationId = issue.IceAllocationId,
        Quantity = issue.Quantity,
        Status = issue.Status,
        ReservationApplied = issue.ReservationApplied
    };

    private static IceCarryOverDto Map(IceCarryOver carry) => new()
    {
        PublicId = carry.PublicId,
        FromIceAllocationId = carry.FromIceAllocationId,
        ToIceAllocationId = carry.ToIceAllocationId,
        Quantity = carry.Quantity,
        Status = carry.Status
    };

    private static IceCloseResultDto MapClose(IceAllocation allocation) => new()
    {
        IceAllocationId = allocation.IceAllocationId,
        Status = allocation.Status,
        OpeningCarryQuantity = allocation.OpeningCarryQuantity,
        TotalIssuedQuantity = allocation.InitialIssuedQuantity + allocation.SupplementalIssuedQuantity,
        ReturnedQuantity = allocation.ReturnedQuantity,
        ClosingCarryQuantity = allocation.ClosingCarryQuantity,
        ActualUsageQuantity = allocation.ActualUsageQuantity ?? 0,
        TheoreticalUsageQuantity = allocation.TheoreticalUsageQuantity,
        VarianceQuantity = allocation.VarianceQuantity ?? 0,
        RequiresApproval = allocation.Status == OperationalIceStatuses.PendingApproval
    };

    private static ServiceResult Invalid(string message) => ServiceResult.Failure(message, errorCode: OperationalIceErrorCodes.InvalidRequest);
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Failure(message, errorCode: OperationalIceErrorCodes.InvalidRequest);
    private static ServiceResult InvalidState(string message) => ServiceResult.Failure(message, errorCode: OperationalIceErrorCodes.InvalidState);
    private static ServiceResult<T> InvalidState<T>(string message) => ServiceResult<T>.Failure(message, errorCode: OperationalIceErrorCodes.InvalidState);
    private static ServiceResult NotFound(string message) => ServiceResult.Failure(message, errorCode: OperationalIceErrorCodes.NotFound);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Failure(message, errorCode: OperationalIceErrorCodes.NotFound);
    private static ServiceResult<T> Insufficient<T>(decimal usable) => ServiceResult<T>.Failure($"Tồn khả dụng của đá chỉ còn {usable:N3} đơn vị gốc.", errorCode: OperationalIceErrorCodes.InsufficientUsableStock);
    private static ServiceResult<T> Conflict<T>() => ServiceResult<T>.Failure("Dữ liệu tồn kho vừa thay đổi. Vui lòng tải lại.", errorCode: OperationalIceErrorCodes.ConcurrencyConflict);
    private static ServiceResult<T> Fail<T>(ServiceResult result) => ServiceResult<T>.Failure(result.Message, result.Errors, result.ErrorCode);

    private static string? ValidatePolicyQuantities(IcePolicy policy) =>
        ValidatePolicyQuantities(
            policy.SuggestedDailyQuantity,
            policy.SuggestedShiftQuantity,
            policy.VarianceApprovalQuantityThreshold,
            policy.VarianceApprovalPercentThreshold,
            policy.RequireVarianceApproval);

    private static string? ValidatePolicyQuantities(
        decimal dailyQuantity,
        decimal shiftQuantity,
        decimal quantityThreshold,
        decimal percentThreshold,
        bool requireVarianceApproval)
    {
        if (dailyQuantity <= 0) return "Định mức ngày phải lớn hơn 0.";
        if (shiftQuantity <= 0) return "Định mức mỗi ca phải lớn hơn 0.";
        if (shiftQuantity > dailyQuantity) return "Định mức mỗi ca không được vượt định mức ngày.";
        if (quantityThreshold < 0) return "Ngưỡng duyệt theo lượng không được âm.";
        if (percentThreshold is < 0 or > 100) return "Ngưỡng duyệt theo phần trăm phải từ 0 đến 100%.";
        if (!requireVarianceApproval)
            return "Quy trình hiện tại bắt buộc duyệt mọi chênh lệch dương; các ngưỡng chỉ xác định cấp phê duyệt.";
        return null;
    }
}

public sealed class OperationalIceReservationConsumptionService : IOperationalIceReservationConsumptionService
{
    private readonly AppDbContext _context;

    public OperationalIceReservationConsumptionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult> ConsumeForCommittedOrderAsync(
        CafeChain.Models.Orders.Order committedOrder,
        IReadOnlyDictionary<int, decimal> ingredientRequirements,
        CancellationToken cancellationToken = default)
    {
        if (!committedOrder.WorkShiftId.HasValue || ingredientRequirements.Count == 0)
            return ServiceResult.Success();

        var alreadyDeducted = await _context.InventoryTransactions.AsNoTracking()
            .AnyAsync(x => x.ReferenceOrderId == committedOrder.OrderId
                           && x.Type == Models.Enums.Inventory.InventoryTransactionTypeEnum.SALES_DEDUCTION,
                cancellationToken);
        if (alreadyDeducted)
            return ServiceResult.Success();

        var allocation = await _context.IceAllocations
            .Include(x => x.StoreInventory)
            .Include(x => x.OperationalShift).ThenInclude(x => x.WorkShiftLinks)
            .SingleOrDefaultAsync(x =>
                x.OperationalShift.WorkShiftLinks.Any(link => link.WorkShiftId == committedOrder.WorkShiftId.Value)
                && x.Status == OperationalIceStatuses.Open,
                cancellationToken);
        if (allocation == null || !ingredientRequirements.TryGetValue(allocation.IngredientId, out var requiredQuantity))
            return ServiceResult.Success();
        if (requiredQuantity <= 0)
            return ServiceResult.Success();
        if (allocation.StoreInventory.StoreId != committedOrder.StoreId)
            return ServiceResult.Failure("Phân bổ đá không thuộc cửa hàng của đơn hàng.", errorCode: OperationalIceErrorCodes.InvalidState);

        var reservedConsumption = Math.Min(requiredQuantity, allocation.ReservedOutstandingQuantity);
        if (reservedConsumption > 0)
        {
            allocation.ReservedOutstandingQuantity -= reservedConsumption;
            allocation.StoreInventory.ReservedQty = Math.Max(0m, allocation.StoreInventory.ReservedQty - reservedConsumption);
            allocation.StoreInventory.LastUpdated = DateTime.UtcNow;
        }
        allocation.TheoreticalUsageQuantity += requiredQuantity;
        allocation.Revision += 1;
        return ServiceResult.Success();
    }
}
