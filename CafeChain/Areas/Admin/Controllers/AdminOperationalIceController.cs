using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.ViewModels.Admin.OperationalIce;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers;

public sealed class AdminOperationalIceController : AdminBaseController
{
    private readonly AppDbContext _context;
    private readonly IOperationalIceService _service;
    private readonly IAdminActorContextAccessor _actorAccessor;
    private readonly IAdminStoreScopeResolver _storeScopeResolver;
    private readonly IAdminPermissionService _permissionService;

    public AdminOperationalIceController(
        AppDbContext context,
        IOperationalIceService service,
        IAdminActorContextAccessor actorAccessor,
        IAdminStoreScopeResolver storeScopeResolver,
        IAdminPermissionService permissionService)
    {
        _context = context;
        _service = service;
        _actorAccessor = actorAccessor;
        _storeScopeResolver = storeScopeResolver;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? storeId = null,
        DateTime? businessDate = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var actor = _actorAccessor.Get(User);
        var scope = await _storeScopeResolver.ResolveAsync(actor, storeId, cancellationToken);
        if (!scope.IsResolved)
            return StoreScopeFailure(scope);
        var selectedStoreId = scope.StoreId!.Value;
        if (!await HasPermissionAsync(OperationalIcePermissions.View, selectedStoreId))
            return Forbid();

        var date = (businessDate ?? DateTime.Today).Date;
        var policy = await _context.IcePolicies.AsNoTracking()
            .Include(x => x.Ingredient)
            .Include(x => x.DisplayUnit)
            .SingleOrDefaultAsync(x => x.StoreId == selectedStoreId && x.Active, cancellationToken);
        var shifts = await _context.OperationalShifts.AsNoTracking()
            .Where(x => x.StoreId == selectedStoreId && x.BusinessDate == date)
            .Where(x => string.IsNullOrWhiteSpace(status) || x.Status == status)
            .OrderBy(x => x.StartAtUtc)
            .Select(x => new OperationalIceListRowVM
            {
                OperationalShiftId = x.OperationalShiftId,
                IceAllocationId = x.IceAllocations.Select(a => (int?)a.IceAllocationId).FirstOrDefault(),
                ShiftName = x.Name,
                StartAtUtc = x.StartAtUtc,
                EndAtUtc = x.EndAtUtc,
                SuggestedQuantity = policy == null ? 0 : policy.SuggestedShiftQuantity,
                TotalIssuedQuantity = x.IceAllocations.Select(a => a.InitialIssuedQuantity + a.SupplementalIssuedQuantity).FirstOrDefault(),
                TheoreticalUsageQuantity = x.IceAllocations.Select(a => a.TheoreticalUsageQuantity).FirstOrDefault(),
                VarianceQuantity = x.IceAllocations.Select(a => a.VarianceQuantity).FirstOrDefault(),
                Status = x.Status
            })
            .ToListAsync(cancellationToken);

        var canManage = await HasPermissionAsync(OperationalIcePermissions.Manage, selectedStoreId);
        var canApprove = await HasPermissionAsync(OperationalIcePermissions.Approve, selectedStoreId);
        var canPolicy = await HasPermissionAsync(OperationalIcePermissions.Policy, selectedStoreId);
        SetStoreScopeViewData(scope);
        return View(new OperationalIceIndexVM
        {
            StoreId = selectedStoreId,
            BusinessDate = date,
            Status = status,
            Stores = scope.AccessibleStores,
            Rows = shifts,
            Policy = policy == null ? null : new IcePolicyVM
            {
                StoreId = policy.StoreId,
                IngredientId = policy.IngredientId,
                DisplayUnitId = policy.DisplayUnitId,
                IngredientName = policy.Ingredient.Name,
                UnitName = policy.DisplayUnit.Name,
                SuggestedDailyQuantity = policy.SuggestedDailyQuantity,
                SuggestedShiftQuantity = policy.SuggestedShiftQuantity,
                AllowSupplementalIssue = policy.AllowSupplementalIssue,
                AllowSameDayCarryOver = policy.AllowSameDayCarryOver,
                RequireVarianceApproval = policy.RequireVarianceApproval,
                VarianceApprovalQuantityThreshold = policy.VarianceApprovalQuantityThreshold,
                VarianceApprovalPercentThreshold = policy.VarianceApprovalPercentThreshold
            },
            Ingredients = canPolicy ? await _context.Ingredients.AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .Select(x => new OperationalIceOptionVM { Id = x.IngredientId, Label = x.Code + " · " + x.Name })
                .ToListAsync(cancellationToken) : [],
            Units = canPolicy ? await _context.Units.AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .Select(x => new OperationalIceOptionVM { Id = x.UnitId, Label = x.Name })
                .ToListAsync(cancellationToken) : [],
            ShiftLeads = canManage ? await GetShiftLeadOptionsAsync(selectedStoreId, cancellationToken) : [],
            CanManage = canManage,
            CanApprove = canApprove,
            CanConfigurePolicy = canPolicy
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        var allocation = await _context.IceAllocations.AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.OperationalShift).ThenInclude(x => x.Store)
            .Include(x => x.OperationalShift).ThenInclude(x => x.WorkShiftLinks).ThenInclude(x => x.WorkShift).ThenInclude(x => x.User)
            .Include(x => x.IcePolicy).ThenInclude(x => x.DisplayUnit)
            .Include(x => x.Ingredient)
            .Include(x => x.StoreInventory)
            .Include(x => x.SupplementalIssues).ThenInclude(x => x.RequestedByStaff)
            .Include(x => x.OutgoingCarryOvers).ThenInclude(x => x.ToOperationalShift)
            .Include(x => x.OutgoingCarryOvers).ThenInclude(x => x.HandedOverByStaff)
            .Include(x => x.OutgoingCarryOvers).ThenInclude(x => x.ReceivedByStaff)
            .Include(x => x.IncomingCarryOvers).ThenInclude(x => x.FromOperationalShift)
            .Include(x => x.IncomingCarryOvers).ThenInclude(x => x.HandedOverByStaff)
            .Include(x => x.IncomingCarryOvers).ThenInclude(x => x.ReceivedByStaff)
            .Include(x => x.InventoryPostings).ThenInclude(x => x.ApprovedByStaff)
            .SingleOrDefaultAsync(x => x.IceAllocationId == id, cancellationToken);
        if (allocation == null)
            return NotFound();

        var actor = _actorAccessor.Get(User);
        var scope = await _storeScopeResolver.ResolveAsync(actor, allocation.OperationalShift.StoreId, cancellationToken);
        if (!scope.IsResolved)
            return StoreScopeFailure(scope);
        if (!await HasPermissionAsync(OperationalIcePermissions.View, allocation.OperationalShift.StoreId))
            return Forbid();
        var canManage = await HasPermissionAsync(OperationalIcePermissions.Manage, allocation.OperationalShift.StoreId);
        var canApprove = await HasPermissionAsync(OperationalIcePermissions.Approve, allocation.OperationalShift.StoreId);

        var linkedIds = allocation.OperationalShift.WorkShiftLinks.Select(x => x.WorkShiftId).ToArray();
        var availableWorkShifts = canManage ? await _context.WorkShifts.AsNoTracking()
            .Where(x => x.StoreId == allocation.OperationalShift.StoreId
                        && !linkedIds.Contains(x.ShiftId)
                        && !_context.OperationalShiftWorkShifts.Any(link => link.WorkShiftId == x.ShiftId))
            .OrderByDescending(x => x.StartTime)
            .Take(30)
            .Select(x => new OperationalIceOptionVM
            {
                Id = x.ShiftId,
                Label = "POS #" + x.ShiftId + " · " + x.User.FullName + " · " + x.Status
            })
            .ToListAsync(cancellationToken) : [];
        var carryTargets = canManage ? await _context.IceAllocations.AsNoTracking()
            .Where(x => x.IceAllocationId != allocation.IceAllocationId
                        && x.OperationalShift.StoreId == allocation.OperationalShift.StoreId
                        && x.OperationalShift.BusinessDate == allocation.OperationalShift.BusinessDate
                        && x.OperationalShift.StartAtUtc >= allocation.OperationalShift.EndAtUtc
                        && x.IngredientId == allocation.IngredientId
                        && x.Status == OperationalIceStatuses.Open)
            .OrderBy(x => x.OperationalShift.StartAtUtc)
            .Select(x => new OperationalIceOptionVM { Id = x.IceAllocationId, Label = x.OperationalShift.Name })
            .ToListAsync(cancellationToken) : [];

        return View(new OperationalIceDetailVM
        {
            StoreId = allocation.OperationalShift.StoreId,
            StoreName = allocation.OperationalShift.Store.Name,
            OperationalShiftId = allocation.OperationalShiftId,
            IceAllocationId = allocation.IceAllocationId,
            ShiftName = allocation.OperationalShift.Name,
            BusinessDate = allocation.OperationalShift.BusinessDate,
            StartAtUtc = allocation.OperationalShift.StartAtUtc,
            EndAtUtc = allocation.OperationalShift.EndAtUtc,
            Status = allocation.Status,
            IngredientName = allocation.Ingredient.Name,
            UnitName = allocation.IcePolicy.DisplayUnit.Name,
            AvailableQuantity = allocation.StoreInventory.AvailableQty - allocation.StoreInventory.ReservedQty,
            ReservedStoreQuantity = allocation.StoreInventory.ReservedQty,
            ReservedOutstandingQuantity = allocation.ReservedOutstandingQuantity,
            OpeningCarryQuantity = allocation.OpeningCarryQuantity,
            InitialIssuedQuantity = allocation.InitialIssuedQuantity,
            SupplementalIssuedQuantity = allocation.SupplementalIssuedQuantity,
            ReturnedQuantity = allocation.ReturnedQuantity,
            ClosingCarryQuantity = allocation.ClosingCarryQuantity,
            TheoreticalUsageQuantity = allocation.TheoreticalUsageQuantity,
            ActualUsageQuantity = allocation.ActualUsageQuantity,
            VarianceQuantity = allocation.VarianceQuantity,
            CloseReason = allocation.CloseReason,
            ReconciliationReason = allocation.ReconciliationReason,
            CostSnapshotStatus = allocation.CostSnapshotStatus,
            UnitCostSnapshot = allocation.UnitCostSnapshot,
            WorkShifts = allocation.OperationalShift.WorkShiftLinks.OrderBy(x => x.WorkShift.StartTime)
                .Select(x => new OperationalIceWorkShiftVM
                {
                    WorkShiftId = x.WorkShiftId,
                    StaffName = x.WorkShift.User.FullName,
                    Status = x.WorkShift.Status,
                    StartTime = x.WorkShift.StartTime,
                    EndTime = x.WorkShift.EndTime
                }).ToList(),
            Supplements = allocation.SupplementalIssues.OrderByDescending(x => x.RequestedAtUtc)
                .Select(x => new OperationalIceSupplementVM
                {
                    PublicId = x.PublicId,
                    Quantity = x.Quantity,
                    Reason = x.Reason,
                    Status = x.Status,
                    RequestedBy = x.RequestedByStaff.FullName,
                    RequestedAtUtc = x.RequestedAtUtc
                }).ToList(),
            CarryOvers = allocation.OutgoingCarryOvers.Select(x => new OperationalIceCarryVM
                {
                    PublicId = x.PublicId,
                    Direction = "Giao",
                    OtherShiftName = x.ToOperationalShift.Name,
                    Quantity = x.Quantity,
                    Status = x.Status,
                    HandedOverBy = x.HandedOverByStaff.FullName,
                    ReceivedBy = x.ReceivedByStaff == null ? null : x.ReceivedByStaff.FullName,
                    ConfirmedAtUtc = x.ConfirmedAtUtc
                })
                .Concat(allocation.IncomingCarryOvers.Select(x => new OperationalIceCarryVM
                {
                    PublicId = x.PublicId,
                    Direction = "Nhận",
                    OtherShiftName = x.FromOperationalShift.Name,
                    Quantity = x.Quantity,
                    Status = x.Status,
                    HandedOverBy = x.HandedOverByStaff.FullName,
                    ReceivedBy = x.ReceivedByStaff == null ? null : x.ReceivedByStaff.FullName,
                    ConfirmedAtUtc = x.ConfirmedAtUtc
                })).OrderByDescending(x => x.ConfirmedAtUtc).ToList(),
            Postings = allocation.InventoryPostings.OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new OperationalIcePostingVM
                {
                    IceInventoryPostingId = x.IceInventoryPostingId,
                    PostingType = x.PostingType,
                    IdempotencyKey = x.IdempotencyKey,
                    Quantity = x.Quantity,
                    UnitCost = x.UnitCost,
                    TotalCost = x.TotalCost,
                    InventoryTransactionId = x.InventoryTransactionId,
                    ApprovedBy = x.ApprovedByStaff.FullName,
                    Reason = x.Reason,
                    CreatedAtUtc = x.CreatedAtUtc
                }).ToList(),
            AvailableWorkShifts = availableWorkShifts,
            CarryTargets = carryTargets,
            StaffOptions = canManage ? await _context.Staffs.AsNoTracking()
                .Where(x => x.StoreId == allocation.OperationalShift.StoreId && x.Active)
                .OrderBy(x => x.FullName)
                .Select(x => new OperationalIceOptionVM { Id = x.StaffId, Label = x.FullName })
                .ToListAsync(cancellationToken) : [],
            CanManage = canManage,
            CanApprove = canApprove
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePolicy(SaveIcePolicyRequest request, CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(OperationalIcePermissions.Policy, request.StoreId))
            return Forbid();
        return RedirectWithResult(await _service.SavePolicyAsync(request, _actorAccessor.Get(User), cancellationToken), nameof(Index), new { storeId = request.StoreId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateShift(CreateOperationalShiftRequest request, CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(OperationalIcePermissions.Manage, request.StoreId))
            return Forbid();
        var normalized = new CreateOperationalShiftRequest
        {
            StoreId = request.StoreId,
            BusinessDate = request.BusinessDate.Date,
            Name = request.Name,
            StartAtUtc = NormalizeLocalToUtc(request.StartAtUtc),
            EndAtUtc = NormalizeLocalToUtc(request.EndAtUtc),
            ShiftLeadId = request.ShiftLeadId
        };
        return RedirectWithResult(await _service.CreateShiftAsync(normalized, _actorAccessor.Get(User), cancellationToken), nameof(Index), new { storeId = request.StoreId, businessDate = request.BusinessDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenAllocation(OpenIceAllocationRequest request, CancellationToken cancellationToken)
    {
        var storeId = await StoreIdForShiftAsync(request.OperationalShiftId, cancellationToken);
        if (storeId == 0) return NotFound();
        if (!await HasPermissionAsync(OperationalIcePermissions.Manage, storeId)) return Forbid();
        var result = await _service.OpenAllocationAsync(request, _actorAccessor.Get(User), cancellationToken);
        if (result.IsSuccess && result.Data != null)
            return RedirectWithResult(result, nameof(Details), new { id = result.Data.IceAllocationId });
        return RedirectWithResult(result, nameof(Index), new { storeId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkWorkShift(LinkOperationalWorkShiftRequest request, int allocationId, CancellationToken cancellationToken)
    {
        var storeId = await StoreIdForShiftAsync(request.OperationalShiftId, cancellationToken);
        if (!await HasPermissionAsync(OperationalIcePermissions.Manage, storeId)) return Forbid();
        return RedirectWithResult(await _service.LinkWorkShiftAsync(request, _actorAccessor.Get(User), cancellationToken), nameof(Details), new { id = allocationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestSupplemental(RequestSupplementalIceRequest request, CancellationToken cancellationToken) =>
        await RunAllocationActionAsync(request.IceAllocationId, OperationalIcePermissions.Manage,
            () => _service.RequestSupplementalAsync(request, _actorAccessor.Get(User), cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideSupplemental(DecideSupplementalIceRequest request, int allocationId, CancellationToken cancellationToken) =>
        await RunAllocationActionAsync(allocationId, OperationalIcePermissions.Approve,
            () => _service.DecideSupplementalAsync(request, _actorAccessor.Get(User), cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmCarryOver(ConfirmIceCarryOverRequest request, CancellationToken cancellationToken) =>
        await RunAllocationActionAsync(request.FromIceAllocationId, OperationalIcePermissions.Manage,
            () => _service.ConfirmCarryOverAsync(request, _actorAccessor.Get(User), cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseAllocation(CloseIceAllocationRequest request, CancellationToken cancellationToken) =>
        await RunAllocationActionAsync(request.IceAllocationId, OperationalIcePermissions.Manage,
            () => _service.CloseAllocationAsync(request, _actorAccessor.Get(User), cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveVariance(ApproveIceVarianceRequest request, CancellationToken cancellationToken) =>
        await RunAllocationActionAsync(request.IceAllocationId, OperationalIcePermissions.Approve,
            () => _service.ApproveVarianceAsync(request, _actorAccessor.Get(User), cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReconcileVariance(ReconcileIceVarianceRequest request, CancellationToken cancellationToken) =>
        await RunAllocationActionAsync(request.IceAllocationId, OperationalIcePermissions.Approve,
            () => _service.ReconcileVarianceAsync(request, _actorAccessor.Get(User), cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAllocation(CancelIceAllocationRequest request, CancellationToken cancellationToken) =>
        await RunAllocationActionAsync(request.IceAllocationId, OperationalIcePermissions.Approve,
            () => _service.CancelAllocationAsync(request, _actorAccessor.Get(User), cancellationToken));

    private async Task<IActionResult> RunAllocationActionAsync<T>(
        int allocationId,
        string permission,
        Func<Task<ServiceResult<T>>> action)
    {
        var storeId = await StoreIdForAllocationAsync(allocationId, HttpContext.RequestAborted);
        if (storeId == 0) return NotFound();
        if (!await HasPermissionAsync(permission, storeId)) return Forbid();
        return RedirectWithResult(await action(), nameof(Details), new { id = allocationId });
    }

    private async Task<IActionResult> RunAllocationActionAsync(
        int allocationId,
        string permission,
        Func<Task<ServiceResult>> action)
    {
        var storeId = await StoreIdForAllocationAsync(allocationId, HttpContext.RequestAborted);
        if (storeId == 0) return NotFound();
        if (!await HasPermissionAsync(permission, storeId)) return Forbid();
        return RedirectWithResult(await action(), nameof(Details), new { id = allocationId });
    }

    private IActionResult RedirectWithResult(ServiceResult result, string action, object? routeValues = null)
    {
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(action, routeValues);
    }

    private async Task<bool> HasPermissionAsync(string permissionCode, int storeId)
    {
        var accountId = GetAccountId();
        if (accountId <= 0 || storeId <= 0) return false;
        var decision = await _permissionService.HasPermissionAsync(accountId, permissionCode, storeId);
        return decision.IsSuccess && decision.Data?.Allowed == true;
    }

    private int GetAccountId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("AccountId")
                    ?? User.FindFirstValue("sub");
        return int.TryParse(value, out var accountId) ? accountId : 0;
    }

    private Task<int> StoreIdForShiftAsync(int shiftId, CancellationToken cancellationToken) =>
        _context.OperationalShifts.AsNoTracking().Where(x => x.OperationalShiftId == shiftId)
            .Select(x => x.StoreId).SingleOrDefaultAsync(cancellationToken);

    private Task<int> StoreIdForAllocationAsync(int allocationId, CancellationToken cancellationToken) =>
        _context.IceAllocations.AsNoTracking().Where(x => x.IceAllocationId == allocationId)
            .Select(x => x.OperationalShift.StoreId).SingleOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<OperationalIceOptionVM>> GetShiftLeadOptionsAsync(int storeId, CancellationToken cancellationToken) =>
        await _context.Staffs.AsNoTracking()
            .Where(x => x.StoreId == storeId && x.Active && x.Account.Active
                        && x.Account.AccountRoles.Any(role => role.Role.Active
                            && (role.Role.Name == RoleConstants.ShiftSupervisor || role.Role.Name == RoleConstants.StoreManager)))
            .OrderBy(x => x.FullName)
            .Select(x => new OperationalIceOptionVM { Id = x.StaffId, Label = x.FullName })
            .ToListAsync(cancellationToken);

    private static DateTime NormalizeLocalToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
}
