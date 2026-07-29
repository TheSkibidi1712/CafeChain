using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
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
    private readonly IUnitConversionService _unitConversionService;
    private readonly IOperationalIceReportService _reportService;
    private readonly IOperationalIceReportPdfRenderer _reportPdfRenderer;
    private readonly ILogger<AdminOperationalIceController> _logger;

    public AdminOperationalIceController(
        AppDbContext context,
        IOperationalIceService service,
        IAdminActorContextAccessor actorAccessor,
        IAdminStoreScopeResolver storeScopeResolver,
        IAdminPermissionService permissionService,
        IUnitConversionService unitConversionService,
        IOperationalIceReportService reportService,
        IOperationalIceReportPdfRenderer reportPdfRenderer,
        ILogger<AdminOperationalIceController> logger)
    {
        _context = context;
        _service = service;
        _actorAccessor = actorAccessor;
        _storeScopeResolver = storeScopeResolver;
        _permissionService = permissionService;
        _unitConversionService = unitConversionService;
        _reportService = reportService;
        _reportPdfRenderer = reportPdfRenderer;
        _logger = logger;
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
        var setupResult = await _service.GetPolicySetupAsync(selectedStoreId, cancellationToken);
        var setup = setupResult.Data ?? new OperationalIcePolicySetupDto
        {
            StatusMessage = setupResult.Message
        };

        var date = (businessDate ?? DateTime.Today).Date;
        var policy = await _context.IcePolicies.AsNoTracking()
            .Include(x => x.Ingredient)
            .Include(x => x.DisplayUnit)
            .SingleOrDefaultAsync(x => x.StoreId == selectedStoreId && x.Active, cancellationToken);
        var displayToBaseFactor = 1m;
        var displayUnitName = policy == null
            ? "đơn vị tồn kho"
            : DisplayUnitSymbol(policy.DisplayUnit.UnitCode);
        var policyConversionValid = true;
        if (policy != null)
        {
            var conversion = await _unitConversionService.ConvertAsync(
                policy.IngredientId,
                1m,
                policy.DisplayUnitId);
            if (conversion.IsSuccess && conversion.Data > 0)
            {
                displayToBaseFactor = conversion.Data;
            }
            else
            {
                policyConversionValid = false;
                displayUnitName = "đơn vị tồn kho";
                TempData["ErrorMessage"] = "Chính sách đá đang thiếu quy đổi đơn vị. Vui lòng cập nhật cấu hình trước khi cấp đá.";
            }
        }
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
                SuggestedQuantity = policy == null ? 0 : policy.SuggestedShiftQuantity / displayToBaseFactor,
                TotalIssuedQuantity = x.IceAllocations.Select(a => (a.InitialIssuedQuantity + a.SupplementalIssuedQuantity) / displayToBaseFactor).FirstOrDefault(),
                TheoreticalUsageQuantity = x.IceAllocations.Select(a => a.TheoreticalUsageQuantity / displayToBaseFactor).FirstOrDefault(),
                VarianceQuantity = x.IceAllocations.Select(a => a.VarianceQuantity / displayToBaseFactor).FirstOrDefault(),
                Status = x.Status,
                HasShiftLead = x.ShiftLeadId.HasValue,
                CreationSource = x.CreationSource,
                LinkedWorkShiftCount = x.WorkShiftLinks.Count
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
                UnitName = displayUnitName,
                SuggestedDailyQuantity = policy.SuggestedDailyQuantity / displayToBaseFactor,
                SuggestedShiftQuantity = policy.SuggestedShiftQuantity / displayToBaseFactor,
                AllowSupplementalIssue = policy.AllowSupplementalIssue,
                AllowSameDayCarryOver = policy.AllowSameDayCarryOver,
                RequireVarianceApproval = policy.RequireVarianceApproval,
                VarianceApprovalQuantityThreshold = policy.VarianceApprovalQuantityThreshold / displayToBaseFactor,
                VarianceApprovalPercentThreshold = policy.VarianceApprovalPercentThreshold
            },
            Ingredients = canPolicy ? setup.Ingredients.Select(x => new OperationalIceOptionVM
            {
                Id = x.Id,
                Code = x.Code,
                Label = x.Label
            }).ToList() : [],
            Units = canPolicy ? setup.Units.Select(x => new OperationalIceOptionVM
            {
                Id = x.Id,
                Code = x.Code,
                Label = x.Label
            }).ToList() : [],
            ShiftLeads = canManage ? await GetShiftLeadOptionsAsync(selectedStoreId, cancellationToken) : [],
            Inventory = setup.Inventory == null ? null : new OperationalIceInventoryVM
            {
                PhysicalQuantity = setup.Inventory.PhysicalQuantity / displayToBaseFactor,
                ReservedQuantity = setup.Inventory.ReservedQuantity / displayToBaseFactor,
                AvailableQuantity = setup.Inventory.AvailableQuantity / displayToBaseFactor,
                AvailableAfterSuggestedShiftQuantity = (setup.Inventory.AvailableQuantity
                    - (policy?.SuggestedShiftQuantity ?? 0)) / displayToBaseFactor,
                UnitName = displayUnitName
            },
            HasValidPolicy = setup.IsValid && policyConversionValid,
            PolicyStatusMessage = policyConversionValid
                ? setup.StatusMessage
                : "Chính sách đá thiếu quy đổi đơn vị hợp lệ.",
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
        var displayToBaseFactorResult = await _unitConversionService.ConvertAsync(
            allocation.IngredientId,
            1m,
            allocation.IcePolicy.DisplayUnitId);
        var detailDisplayToBaseFactor = displayToBaseFactorResult.IsSuccess && displayToBaseFactorResult.Data > 0
            ? displayToBaseFactorResult.Data
            : 1m;
        var detailUnitName = displayToBaseFactorResult.IsSuccess
            ? DisplayUnitSymbol(allocation.IcePolicy.DisplayUnit.UnitCode)
            : "đơn vị tồn kho";
        if (!displayToBaseFactorResult.IsSuccess)
            TempData["ErrorMessage"] = "Không thể quy đổi đơn vị đá. Dữ liệu đang hiển thị theo đơn vị tồn kho.";

        var availableWorkShiftRows = canManage
            ? (await _service.GetWorkShiftSuggestionsAsync(
                allocation.OperationalShiftId,
                actor,
                cancellationToken)).Data ?? []
            : [];
        var availableWorkShifts = availableWorkShiftRows
            .Select(x => new OperationalIceOptionVM
            {
                Id = x.WorkShiftId,
                Label = $"POS #{x.WorkShiftId} · {x.StaffName} · {x.StartTime:dd/MM/yyyy HH:mm}"
                        + (x.EndTime.HasValue ? $"–{x.EndTime:HH:mm}" : "–Đang mở")
            })
            .ToList();
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
            UnitName = detailUnitName,
            PhysicalQuantity = allocation.StoreInventory.AvailableQty / detailDisplayToBaseFactor,
            AvailableQuantity = (allocation.StoreInventory.AvailableQty - allocation.StoreInventory.ReservedQty) / detailDisplayToBaseFactor,
            ReservedStoreQuantity = allocation.StoreInventory.ReservedQty / detailDisplayToBaseFactor,
            ReservedOutstandingQuantity = allocation.ReservedOutstandingQuantity / detailDisplayToBaseFactor,
            OpeningCarryQuantity = allocation.OpeningCarryQuantity / detailDisplayToBaseFactor,
            InitialIssuedQuantity = allocation.InitialIssuedQuantity / detailDisplayToBaseFactor,
            SupplementalIssuedQuantity = allocation.SupplementalIssuedQuantity / detailDisplayToBaseFactor,
            ReturnedQuantity = allocation.ReturnedQuantity / detailDisplayToBaseFactor,
            ClosingCarryQuantity = allocation.ClosingCarryQuantity / detailDisplayToBaseFactor,
            TheoreticalUsageQuantity = allocation.TheoreticalUsageQuantity / detailDisplayToBaseFactor,
            ActualUsageQuantity = allocation.ActualUsageQuantity / detailDisplayToBaseFactor,
            VarianceQuantity = allocation.VarianceQuantity / detailDisplayToBaseFactor,
            CloseReason = allocation.CloseReason,
            ReconciliationReason = allocation.ReconciliationReason,
            CostSnapshotStatus = allocation.CostSnapshotStatus,
            UnitCostSnapshot = allocation.UnitCostSnapshot * detailDisplayToBaseFactor,
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
                    Quantity = x.Quantity / detailDisplayToBaseFactor,
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
                    Quantity = x.Quantity / detailDisplayToBaseFactor,
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
                    Quantity = x.Quantity / detailDisplayToBaseFactor,
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
                    Quantity = x.Quantity / detailDisplayToBaseFactor,
                    UnitCost = x.UnitCost * detailDisplayToBaseFactor,
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

    [HttpGet]
    public async Task<IActionResult> Report(int id, CancellationToken cancellationToken = default)
    {
        var storeId = await StoreIdForAllocationAsync(id, cancellationToken);
        if (storeId == 0)
            return NotFound();
        if (!await HasPermissionAsync(OperationalIcePermissions.View, storeId))
            return Forbid();

        var result = await _reportService.BuildAsync(id, cancellationToken);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadReport(int id, CancellationToken cancellationToken = default)
    {
        var storeId = await StoreIdForAllocationAsync(id, cancellationToken);
        if (storeId == 0)
            return NotFound();
        if (!await HasPermissionAsync(OperationalIcePermissions.View, storeId))
            return Forbid();

        var result = await _reportService.BuildAsync(id, cancellationToken);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        var content = _reportPdfRenderer.Render(result.Data, DateTime.UtcNow);
        var fileName = $"bao-cao-da-{result.Data.BusinessDate:yyyyMMdd}-ca-{result.Data.OperationalShiftId}.pdf";
        return File(content, "application/pdf", fileName);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePolicy(SaveIcePolicyRequest request, CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(OperationalIcePermissions.Policy, request.StoreId))
            return Forbid();
        var factor = await _unitConversionService.ConvertAsync(request.IngredientId, 1m, request.DisplayUnitId);
        if (!factor.IsSuccess || factor.Data <= 0)
            return RedirectConversionFailure(factor, nameof(Index), new { storeId = request.StoreId });
        var normalized = new SaveIcePolicyRequest
        {
            StoreId = request.StoreId,
            IngredientId = request.IngredientId,
            DisplayUnitId = request.DisplayUnitId,
            SuggestedDailyQuantity = request.SuggestedDailyQuantity * factor.Data,
            SuggestedShiftQuantity = request.SuggestedShiftQuantity * factor.Data,
            AllowSupplementalIssue = request.AllowSupplementalIssue,
            AllowSameDayCarryOver = request.AllowSameDayCarryOver,
            RequireVarianceApproval = request.RequireVarianceApproval,
            VarianceApprovalQuantityThreshold = request.VarianceApprovalQuantityThreshold * factor.Data,
            VarianceApprovalPercentThreshold = request.VarianceApprovalPercentThreshold
        };
        return RedirectWithResult(await _service.SavePolicyAsync(normalized, _actorAccessor.Get(User), cancellationToken), nameof(Index), new { storeId = request.StoreId });
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
            ShiftLeadId = request.ShiftLeadId,
            CreationSource = request.CreationSource,
            SourceScheduleShiftId = request.SourceScheduleShiftId
        };
        return RedirectWithResult(await _service.CreateShiftAsync(normalized, _actorAccessor.Get(User), cancellationToken), nameof(Index), new { storeId = request.StoreId, businessDate = request.BusinessDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenAllocation(OpenIceAllocationRequest request, CancellationToken cancellationToken)
    {
        var unitContext = await UnitContextForShiftAsync(request.OperationalShiftId, cancellationToken);
        if (unitContext == null) return NotFound();
        if (!await HasPermissionAsync(OperationalIcePermissions.Manage, unitContext.StoreId)) return Forbid();
        var converted = await _unitConversionService.ConvertAsync(
            unitContext.IngredientId,
            request.InitialIssuedQuantity,
            unitContext.DisplayUnitId);
        if (!converted.IsSuccess)
            return RedirectConversionFailure(converted, nameof(Index), new { storeId = unitContext.StoreId });
        var normalized = new OpenIceAllocationRequest
        {
            OperationalShiftId = request.OperationalShiftId,
            InitialIssuedQuantity = converted.Data,
            WorkShiftIds = request.WorkShiftIds
        };
        var result = await _service.OpenAllocationAsync(normalized, _actorAccessor.Get(User), cancellationToken);
        if (result.IsSuccess && result.Data != null)
            return RedirectWithResult(result, nameof(Details), new { id = result.Data.IceAllocationId });
        return RedirectWithResult(result, nameof(Index), new { storeId = unitContext.StoreId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkWorkShift(LinkOperationalWorkShiftRequest request, int allocationId, CancellationToken cancellationToken)
    {
        var storeId = await StoreIdForShiftAsync(request.OperationalShiftId, cancellationToken);
        if (!await HasPermissionAsync(OperationalIcePermissions.Manage, storeId)) return Forbid();
        return RedirectWithResult(await _service.LinkWorkShiftAsync(request, _actorAccessor.Get(User), cancellationToken), nameof(Details), new { id = allocationId });
    }

    [HttpGet]
    public async Task<IActionResult> ScheduleOptions(
        int storeId,
        DateTime businessDate,
        CancellationToken cancellationToken = default)
    {
        var actor = _actorAccessor.Get(User);
        var scope = await _storeScopeResolver.ResolveAsync(actor, storeId, cancellationToken);
        if (!scope.IsResolved || scope.StoreId != storeId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                message = "Bạn không có quyền xem lịch làm việc của chi nhánh này."
            });
        }

        if (!await HasPermissionAsync(OperationalIcePermissions.Manage, storeId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                message = "Bạn không có quyền tạo ca vận hành tại chi nhánh này."
            });
        }

        try
        {
            var result = await _service.GetScheduleOptionsAsync(
                storeId,
                businessDate.Date,
                actor,
                cancellationToken);
            if (!result.IsSuccess)
            {
                return BadRequest(new
                {
                    success = false,
                    message = string.IsNullOrWhiteSpace(result.Message)
                        ? "Không thể tải lịch làm việc. Vui lòng thử lại."
                        : result.Message
                });
            }

            return Ok(new
            {
                success = true,
                data = MapScheduleOptions(result.Data ?? [])
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Cannot load operational ice schedule options for StoreId={StoreId}, BusinessDate={BusinessDate}",
                storeId,
                businessDate.Date);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể tải lịch làm việc. Vui lòng thử lại."
            });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkWorkShifts(LinkOperationalWorkShiftsRequest request, int allocationId, CancellationToken cancellationToken)
    {
        var storeId = await StoreIdForShiftAsync(request.OperationalShiftId, cancellationToken);
        if (!await HasPermissionAsync(OperationalIcePermissions.Manage, storeId)) return Forbid();
        return RedirectWithResult(
            await _service.LinkWorkShiftsAsync(request, _actorAccessor.Get(User), cancellationToken),
            nameof(Details),
            new { id = allocationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestSupplemental(RequestSupplementalIceRequest request, CancellationToken cancellationToken)
    {
        var unitContext = await UnitContextForAllocationAsync(request.IceAllocationId, cancellationToken);
        if (unitContext == null) return NotFound();
        if (!await HasPermissionAsync(OperationalIcePermissions.Manage, unitContext.StoreId)) return Forbid();
        var converted = await _unitConversionService.ConvertAsync(
            unitContext.IngredientId,
            request.Quantity,
            unitContext.DisplayUnitId);
        if (!converted.IsSuccess)
            return RedirectConversionFailure(converted, nameof(Details), new { id = request.IceAllocationId });
        var normalized = new RequestSupplementalIceRequest
        {
            IceAllocationId = request.IceAllocationId,
            Quantity = converted.Data,
            Reason = request.Reason
        };
        return RedirectWithResult(
            await _service.RequestSupplementalAsync(normalized, _actorAccessor.Get(User), cancellationToken),
            nameof(Details),
            new { id = request.IceAllocationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideSupplemental(DecideSupplementalIceRequest request, int allocationId, CancellationToken cancellationToken) =>
        await RunAllocationActionAsync(allocationId, OperationalIcePermissions.Approve,
            () => _service.DecideSupplementalAsync(request, _actorAccessor.Get(User), cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmCarryOver(ConfirmIceCarryOverRequest request, CancellationToken cancellationToken)
    {
        var unitContext = await UnitContextForAllocationAsync(request.FromIceAllocationId, cancellationToken);
        if (unitContext == null) return NotFound();
        if (!await HasPermissionAsync(OperationalIcePermissions.Manage, unitContext.StoreId)) return Forbid();
        var converted = await _unitConversionService.ConvertAsync(
            unitContext.IngredientId,
            request.Quantity,
            unitContext.DisplayUnitId);
        if (!converted.IsSuccess)
            return RedirectConversionFailure(converted, nameof(Details), new { id = request.FromIceAllocationId });
        var normalized = new ConfirmIceCarryOverRequest
        {
            FromIceAllocationId = request.FromIceAllocationId,
            ToIceAllocationId = request.ToIceAllocationId,
            Quantity = converted.Data,
            ReceivedByStaffId = request.ReceivedByStaffId
        };
        return RedirectWithResult(
            await _service.ConfirmCarryOverAsync(normalized, _actorAccessor.Get(User), cancellationToken),
            nameof(Details),
            new { id = request.FromIceAllocationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseAllocation(CloseIceAllocationRequest request, CancellationToken cancellationToken)
    {
        var unitContext = await UnitContextForAllocationAsync(request.IceAllocationId, cancellationToken);
        if (unitContext == null) return NotFound();
        if (!await HasPermissionAsync(OperationalIcePermissions.Manage, unitContext.StoreId)) return Forbid();
        var converted = await _unitConversionService.ConvertAsync(
            unitContext.IngredientId,
            request.ReturnedQuantity,
            unitContext.DisplayUnitId);
        if (!converted.IsSuccess)
            return RedirectConversionFailure(converted, nameof(Details), new { id = request.IceAllocationId });
        var normalized = new CloseIceAllocationRequest
        {
            IceAllocationId = request.IceAllocationId,
            ReturnedQuantity = converted.Data,
            ReturnCondition = request.ReturnCondition,
            ReturnReceivedByStaffId = request.ReturnReceivedByStaffId,
            CloseReason = request.CloseReason
        };
        return RedirectWithResult(
            await _service.CloseAllocationAsync(normalized, _actorAccessor.Get(User), cancellationToken),
            nameof(Details),
            new { id = request.IceAllocationId });
    }

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

    private IActionResult RedirectConversionFailure(ServiceResult<decimal> result, string action, object routeValues)
    {
        TempData["ErrorMessage"] = result.IsSuccess
            ? "Số lượng đá không hợp lệ."
            : "Không thể quy đổi đơn vị đá. Vui lòng kiểm tra cấu hình kg và g.";
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

    private async Task<IceUnitContext?> UnitContextForShiftAsync(int shiftId, CancellationToken cancellationToken)
    {
        var storeId = await _context.OperationalShifts.AsNoTracking()
            .Where(x => x.OperationalShiftId == shiftId)
            .Select(x => x.StoreId)
            .SingleOrDefaultAsync(cancellationToken);
        if (storeId <= 0)
            return null;
        var policy = await _context.IcePolicies.AsNoTracking()
            .Where(x => x.StoreId == storeId && x.Active)
            .Select(x => new { x.IngredientId, x.DisplayUnitId })
            .SingleOrDefaultAsync(cancellationToken);
        return policy == null
            ? null
            : new IceUnitContext(storeId, policy.IngredientId, policy.DisplayUnitId);
    }

    private async Task<IceUnitContext?> UnitContextForAllocationAsync(int allocationId, CancellationToken cancellationToken)
    {
        var data = await _context.IceAllocations.AsNoTracking()
            .Where(x => x.IceAllocationId == allocationId)
            .Select(x => new
            {
                x.OperationalShift.StoreId,
                x.IngredientId,
                x.IcePolicy.DisplayUnitId
            })
            .SingleOrDefaultAsync(cancellationToken);
        return data == null
            ? null
            : new IceUnitContext(data.StoreId, data.IngredientId, data.DisplayUnitId);
    }

    private async Task<IReadOnlyList<OperationalIceOptionVM>> GetShiftLeadOptionsAsync(int storeId, CancellationToken cancellationToken) =>
        await _context.Staffs.AsNoTracking()
            .Where(x => x.StoreId == storeId && x.Active && x.Account.Active
                        && x.Account.AccountRoles.Any(role => role.Role.Active
                            && (role.Role.Name == RoleConstants.ShiftSupervisor || role.Role.Name == RoleConstants.StoreManager)))
            .OrderBy(x => x.FullName)
            .Select(x => new OperationalIceOptionVM { Id = x.StaffId, Label = x.FullName })
            .ToListAsync(cancellationToken);

    private static IReadOnlyList<OperationalIceScheduleOptionVM> MapScheduleOptions(
        IReadOnlyList<OperationalIceScheduleOptionDto> options) =>
        options.Select(option =>
        {
            var startLocal = option.StartAtUtc.ToLocalTime();
            var endLocal = option.EndAtUtc.ToLocalTime();
            return new OperationalIceScheduleOptionVM
            {
                ScheduleShiftId = option.ScheduleShiftId,
                Name = option.Name,
                Label = $"{option.Name} · {startLocal:HH:mm}–{endLocal:HH:mm} · {option.StaffCount} nhân viên",
                StartLocalValue = startLocal.ToString("yyyy-MM-ddTHH:mm"),
                EndLocalValue = endLocal.ToString("yyyy-MM-ddTHH:mm"),
                StaffCount = option.StaffCount,
                SuggestedShiftLeadId = option.SuggestedShiftLeadId
            };
        }).ToList();

    private static DateTime NormalizeLocalToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();

    private static string DisplayUnitSymbol(string? unitCode)
    {
        var normalized = PhysicalUnitConversionRegistry.NormalizeUnitCode(unitCode);
        return string.IsNullOrWhiteSpace(normalized) ? "đơn vị tồn kho" : normalized;
    }

    private sealed record IceUnitContext(int StoreId, int IngredientId, int DisplayUnitId);
}
