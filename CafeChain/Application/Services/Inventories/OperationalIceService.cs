using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Ice;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

public sealed class OperationalIceService : IOperationalIceService
{
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

    private readonly AppDbContext _context;
    private readonly IScopeAuthorizationService _scopeAuthorization;

    public OperationalIceService(AppDbContext context, IScopeAuthorizationService scopeAuthorization)
    {
        _context = context;
        _scopeAuthorization = scopeAuthorization;
    }

    public async Task<ServiceResult> SavePolicyAsync(
        SaveIcePolicyRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync(actor, request.StoreId, ApproveRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return authorization;
        if (request.IngredientId <= 0 || request.DisplayUnitId <= 0
            || request.SuggestedDailyQuantity < 0 || request.SuggestedShiftQuantity < 0
            || request.VarianceApprovalQuantityThreshold < 0 || request.VarianceApprovalPercentThreshold < 0)
        {
            return Invalid("Dữ liệu chính sách đá không hợp lệ.");
        }

        var ingredientValid = await _context.Ingredients.AsNoTracking()
            .AnyAsync(x => x.IngredientId == request.IngredientId && x.Active, cancellationToken);
        var unitValid = await _context.Units.AsNoTracking()
            .AnyAsync(x => x.UnitId == request.DisplayUnitId && x.Active, cancellationToken);
        if (!ingredientValid || !unitValid)
            return Invalid("Nguyên liệu đá hoặc đơn vị hiển thị không còn hoạt động.");

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
        if (string.IsNullOrWhiteSpace(request.Name) || request.EndAtUtc <= request.StartAtUtc)
            return Invalid<OperationalShiftSummaryDto>("Tên ca và khoảng thời gian vận hành không hợp lệ.");

        if (request.ShiftLeadId.HasValue)
        {
            var leadValid = await _context.Staffs.AsNoTracking()
                .AnyAsync(x => x.StaffId == request.ShiftLeadId && x.StoreId == request.StoreId && x.Active, cancellationToken);
            if (!leadValid)
                return Invalid<OperationalShiftSummaryDto>("Ca trưởng không hoạt động tại cửa hàng đã chọn.");
        }

        var businessDate = request.BusinessDate.Date;
        var duplicate = await _context.OperationalShifts.AsNoTracking()
            .AnyAsync(x => x.StoreId == request.StoreId && x.BusinessDate == businessDate && x.Name == request.Name.Trim(), cancellationToken);
        if (duplicate)
            return ServiceResult<OperationalShiftSummaryDto>.Failure("Tên ca vận hành đã tồn tại trong ngày.", errorCode: OperationalIceErrorCodes.InvalidState);

        var shift = new OperationalShift
        {
            StoreId = request.StoreId,
            BusinessDate = businessDate,
            Name = request.Name.Trim(),
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            ShiftLeadId = request.ShiftLeadId,
            Status = OperationalIceStatuses.Draft,
            CreatedByStaffId = actor.StaffId,
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.OperationalShifts.Add(shift);
        var saved = await SaveAsync("Đã tạo ca vận hành.", cancellationToken);
        if (!saved.IsSuccess)
            return Fail<OperationalShiftSummaryDto>(saved);

        return ServiceResult<OperationalShiftSummaryDto>.Success(Map(shift), saved.Message);
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

        var policy = await _context.IcePolicies
            .SingleOrDefaultAsync(x => x.StoreId == shift.StoreId && x.Active, cancellationToken);
        if (policy == null)
            return InvalidState<IceAllocationDto>("Cửa hàng chưa cấu hình chính sách đá vận hành.");
        var inventory = await _context.StoreInventories
            .SingleOrDefaultAsync(x => x.StoreId == shift.StoreId && x.IngredientId == policy.IngredientId && x.SupersededByStoreInventoryId == null, cancellationToken);
        if (inventory == null)
            return NotFound<IceAllocationDto>("Cửa hàng chưa có tồn kho cho nguyên liệu đá.");
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
        CancellationToken cancellationToken = default)
    {
        var shift = await _context.OperationalShifts.SingleOrDefaultAsync(x => x.OperationalShiftId == request.OperationalShiftId, cancellationToken);
        if (shift == null)
            return NotFound("Không tìm thấy ca vận hành.");
        var authorization = await AuthorizeAsync(actor, shift.StoreId, ManageRoles, cancellationToken);
        if (!authorization.IsSuccess)
            return authorization;
        if (shift.Status != OperationalIceStatuses.Open)
            return InvalidState("Chỉ ca vận hành đang mở mới nhận thêm WorkShift POS.");
        var validation = await ValidateWorkShiftsAsync(shift, [request.WorkShiftId], cancellationToken);
        if (!validation.IsSuccess)
            return validation;

        _context.OperationalShiftWorkShifts.Add(new OperationalShiftWorkShift
        {
            OperationalShiftId = shift.OperationalShiftId,
            WorkShiftId = request.WorkShiftId,
            LinkedByStaffId = actor.StaffId,
            LinkedAtUtc = DateTime.UtcNow
        });
        return await SaveAsync("Đã liên kết ca POS với ca vận hành.", cancellationToken);
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

        var workShifts = await _context.WorkShifts.AsNoTracking()
            .Where(x => distinctIds.Contains(x.ShiftId))
            .Select(x => new { x.ShiftId, x.StoreId })
            .ToListAsync(cancellationToken);
        if (workShifts.Count != distinctIds.Length || workShifts.Any(x => x.StoreId != operationalShift.StoreId))
            return Invalid("WorkShift POS không thuộc cửa hàng của ca vận hành.");
        var linked = await _context.OperationalShiftWorkShifts.AsNoTracking()
            .AnyAsync(x => distinctIds.Contains(x.WorkShiftId), cancellationToken);
        return linked
            ? ServiceResult.Failure("Có WorkShift POS đã liên kết với ca vận hành khác.", errorCode: OperationalIceErrorCodes.WorkShiftAlreadyLinked)
            : ServiceResult.Success();
    }

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

    private static ServiceResult Invalid(string message) => ServiceResult.Failure(message, errorCode: OperationalIceErrorCodes.InvalidRequest);
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Failure(message, errorCode: OperationalIceErrorCodes.InvalidRequest);
    private static ServiceResult InvalidState(string message) => ServiceResult.Failure(message, errorCode: OperationalIceErrorCodes.InvalidState);
    private static ServiceResult<T> InvalidState<T>(string message) => ServiceResult<T>.Failure(message, errorCode: OperationalIceErrorCodes.InvalidState);
    private static ServiceResult NotFound(string message) => ServiceResult.Failure(message, errorCode: OperationalIceErrorCodes.NotFound);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Failure(message, errorCode: OperationalIceErrorCodes.NotFound);
    private static ServiceResult<T> Insufficient<T>(decimal usable) => ServiceResult<T>.Failure($"Tồn khả dụng của đá chỉ còn {usable:N3} đơn vị gốc.", errorCode: OperationalIceErrorCodes.InsufficientUsableStock);
    private static ServiceResult<T> Conflict<T>() => ServiceResult<T>.Failure("Dữ liệu tồn kho vừa thay đổi. Vui lòng tải lại.", errorCode: OperationalIceErrorCodes.ConcurrencyConflict);
    private static ServiceResult<T> Fail<T>(ServiceResult result) => ServiceResult<T>.Failure(result.Message, result.Errors, result.ErrorCode);
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
