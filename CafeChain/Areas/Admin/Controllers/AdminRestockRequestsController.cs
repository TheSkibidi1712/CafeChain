using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.Mvc;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Issue #100 list/detail + Issue #128 workflow actions (intent-only; no inventory mutation).
    /// </summary>
    [RequirePermission(PermissionConstants.RestockView)]
    public class AdminRestockRequestsController : AdminBaseController
    {
        private readonly IRestockRequestService _service;
        private readonly IRestockRequestWorkflowService _workflow;
        private readonly IAdminActorContextAccessor _actor;
        private readonly IAdminStoreScopeResolver _storeScopeResolver;
        private readonly AppDbContext? _context;

        public AdminRestockRequestsController(
            IRestockRequestService service,
            IRestockRequestWorkflowService workflow,
            IAdminActorContextAccessor actor,
            IAdminStoreScopeResolver storeScopeResolver,
            AppDbContext? context = null)
        {
            _service = service;
            _workflow = workflow;
            _actor = actor;
            _storeScopeResolver = storeScopeResolver;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? status = "SUBMITTED",
            int page = 1,
            int? storeId = null)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockView))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem yêu cầu nhập hàng.";
                return RedirectToAction("Index", "AdminNotifications");
            }

            var ctx = _actor.Get(User);
            if (ctx.StaffId <= 0)
                return Unauthorized();
            var storeScope = await _storeScopeResolver.ResolveAsync(ctx, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var targetStoreId = storeScope.StoreId!.Value;

            var result = await _service.ListForStoreAsync(targetStoreId, status, page, 20);
            SetStoreScopeViewData(storeScope);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tải được danh sách yêu cầu.";
                return View(result.Data ?? new RestockRequestListResultDto());
            }

            ViewBag.StatusFilter = status;
            ViewBag.CanWarehouse = await HasEffectivePermissionAsync(PermissionConstants.RestockUpdate);
            ViewBag.CanCreateReceipt = await HasEffectivePermissionAsync(PermissionConstants.ReceiptCreate);
            ViewBag.CanCreateDemand = await HasEffectivePermissionAsync(PermissionConstants.RestockCreate);
            return View(result.Data);
        }

        [HttpGet]
        [RequirePermission(PermissionConstants.RestockCreate)]
        public async Task<IActionResult> CreateManual(int? storeId = null)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockCreate)) return Forbid();
            var ctx = _actor.Get(User);
            var scope = await _storeScopeResolver.ResolveAsync(ctx, storeId);
            if (!scope.IsResolved) return StoreScopeFailure(scope);
            await PopulateDemandOptionsAsync(scope.StoreId!.Value);
            return View(new CreateProcurementDemandRequest
            {
                StoreId = scope.StoreId.Value,
                SourceType = RestockRequestSourceTypes.ManualByStore,
                SourceReferenceId = Guid.NewGuid().ToString("N"),
                NeedByDate = DateTime.Today.AddDays(2)
            });
        }

        [HttpGet]
        [RequirePermission(PermissionConstants.RestockCreate)]
        public async Task<IActionResult> CreateCentralPlanner(int? storeId = null)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockCreate)) return Forbid();
            var ctx = _actor.Get(User);
            var scope = await _storeScopeResolver.ResolveAsync(ctx, storeId);
            if (!scope.IsResolved) return StoreScopeFailure(scope);
            await PopulateDemandOptionsAsync(scope.StoreId!.Value);
            return View(new CreateProcurementDemandRequest
            {
                StoreId = scope.StoreId.Value,
                SourceType = RestockRequestSourceTypes.CentralPlanner,
                SourceReferenceId = Guid.NewGuid().ToString("N"),
                NeedByDate = DateTime.Today.AddDays(2)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockView))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem yêu cầu nhập hàng.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.GetWorkflowDetailAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames);

            if (!result.IsSuccess || result.Data == null)
            {
                if (result.ErrorCode == BranchReceiptErrorCodes.StoreMismatch)
                    return NotFound();
                if (result.ErrorCode == BranchReceiptErrorCodes.Unauthorized)
                    return Forbid();
                return NotFound(result.Message ?? "Không tìm thấy yêu cầu.");
            }

            ViewBag.CanWarehouse = await HasEffectivePermissionAsync(PermissionConstants.RestockUpdate);
            ViewBag.CanCreateReceipt = await HasEffectivePermissionAsync(PermissionConstants.ReceiptCreate);
            ViewBag.CanCancel = await HasEffectivePermissionAsync(PermissionConstants.RestockCancel);
            ViewBag.CanSubmit = await HasEffectivePermissionAsync(PermissionConstants.RestockSubmit);
            ViewBag.CanAddDemand = await HasEffectivePermissionAsync(PermissionConstants.RestockCreate)
                && RestockRequestStatuses.ActiveValues.Contains(result.Data.Status);
            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> CheckActive(int storeId, int ingredientId)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockCreate)) return Forbid();
            var ctx = _actor.Get(User);
            var result = await _service.GetActiveForStoreIngredientAsync(
                storeId,
                ingredientId,
                ctx.StaffId);
            if (!result.IsSuccess)
            {
                if (result.ErrorCode == RestockRequestErrorCodes.Unauthorized)
                    return Forbid();
                return BadRequest(new
                {
                    success = false,
                    code = result.ErrorCode,
                    message = result.Message
                });
            }

            if (result.Data == null)
                return Ok(new { success = true, exists = false });

            return ActiveRequestConflict(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.RestockSubmit)]
        public async Task<IActionResult> Submit(int id, string? rowVersion)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockSubmit))
            {
                TempData["ErrorMessage"] = "Không có quyền gửi yêu cầu nhập hàng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.SubmitAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "Thao tác thành công." : "Thao tác thất bại.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.RestockApprove)]
        public async Task<IActionResult> StartProcessing(int id, string? reason, string? rowVersion)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockApprove))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền tiếp nhận xử lý yêu cầu.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.StartProcessingAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, reason, rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "Thao tác thành công." : "Thao tác thất bại.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.RestockReject)]
        public async Task<IActionResult> Reject(int id, string reason, string? rowVersion)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockReject))
            {
                TempData["ErrorMessage"] = "Không có quyền từ chối.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.RejectAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, reason, rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "Thao tác thành công." : "Thao tác thất bại.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.RestockCancel)]
        public async Task<IActionResult> Cancel(int id, string? reason, string? rowVersion)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockCancel))
            {
                TempData["ErrorMessage"] = "Không có quyền hủy.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.CancelAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, reason, rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "Thao tác thành công." : "Thao tác thất bại.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.RestockCloseRemaining)]
        public async Task<IActionResult> CloseRemaining(int id, string reason, string? rowVersion)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockCloseRemaining))
            {
                TempData["ErrorMessage"] = "Không có quyền đóng phần còn lại.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.CloseRemainingAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, reason, rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "Thao tác thành công." : "Thao tác thất bại.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.RestockUpdate)]
        public async Task<IActionResult> LinkFulfillment(int id, LinkRestockFulfillmentRequest model, string? rowVersion)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockUpdate))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền gắn nguồn thực hiện.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.LinkFulfillmentAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, model, rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "Thao tác thành công." : "Thao tác thất bại.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.RestockCreate)]
        public async Task<IActionResult> CreateManual(CreateProcurementDemandRequest model)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockCreate))
                return Forbid();

            if (!ModelState.IsValid)
            {
                await PopulateDemandOptionsAsync(model.StoreId);
                return View(model);
            }

            var ctx = _actor.Get(User);
            var result = await _service.CreateManualAsync(model, ctx.StaffId);
            if (result.IsSuccess && result.Data != null)
            {
                TempData["SuccessMessage"] = result.Message ?? "Đã tạo nhu cầu bổ sung.";
                return RedirectToAction(nameof(Details), new { id = result.Data.RestockRequestId });
            }

            if (result.ErrorCode == RestockRequestErrorCodes.ActiveRequestExists
                && result.Data?.ExistingActiveRequest != null)
            {
                if (WantsJsonResponse())
                    return ActiveRequestConflict(result.Data.ExistingActiveRequest);

                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(Details), new
                {
                    id = result.Data.ExistingActiveRequest.RestockRequestId
                });
            }

            ModelState.AddModelError(string.Empty,
                result.Message ?? "Không thể tạo nhu cầu bổ sung.");
            await PopulateDemandOptionsAsync(model.StoreId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.RestockUpdate)]
        public async Task<IActionResult> AddDemand(AddRestockDemandAdjustmentRequest model)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockUpdate)) return Forbid();
            var ctx = _actor.Get(User);
            var result = await _service.AddDemandAdjustmentAsync(model, ctx.StaffId);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess
                    ? "Đã bổ sung nhu cầu."
                    : "Không thể bổ sung nhu cầu.");
            return RedirectToAction(nameof(Details), new { id = model.RestockRequestId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.RestockCreate)]
        public async Task<IActionResult> CreateCentralPlanner(CreateProcurementDemandRequest model)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockCreate))
                return Forbid();

            var ctx = _actor.Get(User);
            var result = await _service.CreateCentralPlannerAsync(model, ctx.StaffId);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "Đã tạo kế hoạch bổ sung." : "Không thể tạo kế hoạch bổ sung.");
            return result.IsSuccess && result.Data != null
                ? RedirectToAction(nameof(Details), new { id = result.Data.RestockRequestId })
                : RedirectToAction(nameof(Index), new { storeId = model.StoreId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.RestockUpdate)]
        public async Task<IActionResult> SetSourcingDecision(SourcingDecisionRequest model)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.RestockUpdate))
                return Forbid();

            var ctx = _actor.Get(User);
            var result = await _service.SetSourcingDecisionAsync(model, ctx.StaffId);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "Đã ghi nhận quyết định nguồn cung." : "Không thể ghi nhận quyết định nguồn cung.");
            return RedirectToAction(nameof(Details), new { id = model.RestockRequestId });
        }

        private async Task PopulateDemandOptionsAsync(int storeId)
        {
            if (_context == null)
                throw new InvalidOperationException("AppDbContext chưa được cấu hình cho form nhu cầu mua hàng.");
            ViewBag.Ingredients = await _context.Ingredients
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .Select(x => new { x.IngredientId, x.Name })
                .ToListAsync();
            ViewBag.Units = await _context.Units
                .AsNoTracking()
                .Where(x => x.Active && (x.UnitCode == ProcurementUnitCodes.Kilogram
                    || x.UnitCode == ProcurementUnitCodes.Liter
                    || x.UnitCode == ProcurementUnitCodes.Piece))
                .OrderBy(x => x.UnitCode)
                .Select(x => new { x.UnitId, x.UnitCode, x.Name })
                .ToListAsync();
            ViewBag.SelectedStoreId = storeId;
        }

        private ObjectResult ActiveRequestConflict(ActiveRestockRequestDto existing) =>
            Conflict(new
            {
                success = false,
                code = RestockRequestErrorCodes.ActiveRequestExists,
                message = "Chi nhánh đã có một yêu cầu bổ sung đang xử lý cho nguyên liệu này. Hãy mở yêu cầu hiện tại hoặc bổ sung thêm nhu cầu.",
                existingRequestId = existing.RestockRequestId,
                existingRequestCode = $"#{existing.RestockRequestId}",
                existingStatus = existing.Status,
                existingRequestedQty = existing.RequestedProcurementQuantity,
                existingAllocatedQty = existing.AllocatedProcurementQuantity,
                existingRemainingQty = existing.RemainingUnallocatedProcurementQuantity,
                procurementUnit = existing.ProcurementUnitName,
                needByDate = existing.NeedByDate,
                detailUrl = Url.Action(nameof(Details), new { id = existing.RestockRequestId })
            });

        private bool WantsJsonResponse() =>
            Request.Headers.Accept.Any(x =>
                x?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    }
}
