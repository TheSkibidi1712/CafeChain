using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles =
        RoleConstants.BusinessOwner + "," +
        RoleConstants.AreaManager + "," +
        RoleConstants.StoreManager + "," +
        RoleConstants.AccountantWarehouse)]
    public sealed class AdminPurchaseAdvicesController : Controller
    {
        private readonly IPurchaseAdviceService _service;
        private readonly IAdminActorContextAccessor _actorAccessor;

        public AdminPurchaseAdvicesController(
            IPurchaseAdviceService service,
            IAdminActorContextAccessor actorAccessor)
        {
            _service = service;
            _actorAccessor = actorAccessor;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            int? storeId = null,
            string? status = null,
            string? priority = null,
            int? ingredientId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var result = await _service.GetPageAsync(new PurchaseAdviceFilterDto
            {
                StoreId = storeId,
                Status = status,
                Priority = priority,
                IngredientId = ingredientId,
                FromDate = fromDate,
                ToDate = toDate
            }, _actorAccessor.Get(User));
            if (!result.IsSuccess) return Failure(result.ErrorCode, result.Message);
            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? storeId = null, int? restockRequestId = null)
        {
            var actor = _actorAccessor.Get(User);
            var targetStoreId = storeId ?? actor.StoreId;
            var sources = await _service.GetAvailableSourcesAsync(targetStoreId, actor);
            if (!sources.IsSuccess) return Failure(sources.ErrorCode, sources.Message);
            ViewBag.Sources = sources.Data;
            ViewBag.SelectedRestockRequestId = restockRequestId;
            return View(new CreatePurchaseAdviceRequest
            {
                StoreId = targetStoreId,
                RequestKey = Guid.NewGuid().ToString("N"),
                NeededByDate = DateTime.Today.AddDays(2),
                Priority = PurchaseAdvicePriorities.Normal,
                Lines = sources.Data!.Select(x => new CreatePurchaseAdviceLineRequest
                {
                    RestockRequestId = x.RestockRequestId,
                    RequestedPurchaseBaseQuantity = x.RestockRequestId == restockRequestId
                        ? (x.RestockRequestedProcurementQuantity.HasValue ? 0m : x.RemainingToPurchaseQuantity)
                        : 0m,
                    RequestedPurchaseProcurementQuantity = x.RestockRequestId == restockRequestId
                        ? x.RemainingToPurchaseProcurementQuantity
                        : null,
                    NeededByDate = DateTime.Today.AddDays(2),
                    RestockRowVersion = x.RestockRowVersion
                }).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePurchaseAdviceRequest model, int[] selectedRestockIds)
        {
            model.Lines = model.Lines
                .Where(x => x.RestockRequestId.HasValue && selectedRestockIds.Contains(x.RestockRequestId.Value))
                .ToList();
            var result = await _service.CreateAsync(model, _actorAccessor.Get(User));
            if (result.IsSuccess)
            {
                TempData["Success"] = "Đã tạo đề nghị mua hàng ở trạng thái nháp.";
                return RedirectToAction(nameof(Details), new { id = result.Data!.PurchaseAdviceId });
            }
            ModelState.AddModelError(string.Empty, result.Message);
            var sources = await _service.GetAvailableSourcesAsync(model.StoreId, _actorAccessor.Get(User));
            ViewBag.Sources = sources.Data ?? Array.Empty<PurchaseAdviceSourceDto>();
            ViewBag.SelectedRestockIds = selectedRestockIds;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDirect(CreatePurchaseAdviceRequest model)
        {
            model.IsDirectProposal = true;
            var result = await _service.CreateDirectAsync(model, _actorAccessor.Get(User));
            if (result.IsSuccess)
            {
                TempData["Success"] = "Đã tạo đề nghị mua trực tiếp và ghi nhận nhu cầu bổ sung.";
                return RedirectToAction(nameof(Details), new { id = result.Data!.PurchaseAdviceId });
            }

            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Index), new { storeId = model.StoreId });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var result = await _service.GetDetailAsync(id, _actorAccessor.Get(User));
            if (!result.IsSuccess) return Failure(result.ErrorCode, result.Message);
            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var result = await _service.GetDetailAsync(id, _actorAccessor.Get(User));
            if (!result.IsSuccess) return Failure(result.ErrorCode, result.Message);
            if (!result.Data!.CanEdit) return Forbid();
            ViewBag.Detail = result.Data;
            return View(new UpdatePurchaseAdviceRequest
            {
                PurchaseAdviceId = result.Data.PurchaseAdviceId,
                NeededByDate = result.Data.NeededByDate,
                Priority = result.Data.Priority,
                Note = result.Data.Note,
                RowVersion = result.Data.RowVersion,
                Lines = result.Data.Lines.Select(x => new UpdatePurchaseAdviceLineRequest
                {
                    PurchaseAdviceLineId = x.PurchaseAdviceLineId,
                    RequestedPurchaseBaseQuantity = x.RequestedPurchaseBaseQuantity,
                    RequestedPurchaseProcurementQuantity = x.RequestedProcurementQuantity,
                    NeededByDate = x.NeededByDate,
                    Note = x.Note,
                    RowVersion = x.RowVersion
                }).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UpdatePurchaseAdviceRequest model)
        {
            var result = await _service.UpdateAsync(model, _actorAccessor.Get(User));
            if (result.IsSuccess)
            {
                TempData["Success"] = "Đã cập nhật đề nghị mua hàng.";
                return RedirectToAction(nameof(Details), new { id = model.PurchaseAdviceId });
            }
            ModelState.AddModelError(string.Empty, result.Message);
            var detail = await _service.GetDetailAsync(model.PurchaseAdviceId, _actorAccessor.Get(User));
            if (!detail.IsSuccess) return Failure(detail.ErrorCode, detail.Message);
            ViewBag.Detail = detail.Data;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Submit(int id, PurchaseAdviceTransitionRequest request) =>
            RunTransition(() => _service.SubmitAsync(id, request, _actorAccessor.Get(User)), id, "Đã gửi đề nghị mua để duyệt.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> StartReview(int id, PurchaseAdviceTransitionRequest request) =>
            RunTransition(() => _service.StartReviewAsync(id, request, _actorAccessor.Get(User)), id, "Đề nghị mua đang được xem xét.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Reject(int id, PurchaseAdviceTransitionRequest request) =>
            RunTransition(() => _service.RejectAsync(id, request, _actorAccessor.Get(User)), id, "Đã từ chối đề nghị mua.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Cancel(int id, PurchaseAdviceTransitionRequest request) =>
            RunTransition(() => _service.CancelAsync(id, request, _actorAccessor.Get(User)), id, "Đã hủy đề nghị mua.");

        private async Task<IActionResult> RunTransition(
            Func<Task<CafeChain.Application.Results.ServiceResult<PurchaseAdviceDetailDto>>> action,
            int id,
            string successMessage)
        {
            var result = await action();
            TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
                ? successMessage
                : result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        private IActionResult Failure(string code, string message)
        {
            if (code == PurchaseAdviceErrorCodes.Forbidden || code == PurchaseAdviceErrorCodes.StoreScopeMismatch)
                return Forbid();
            if (code == PurchaseAdviceErrorCodes.NotFound) return NotFound(message);
            TempData["Error"] = message;
            return RedirectToAction(nameof(Index));
        }
    }
}
