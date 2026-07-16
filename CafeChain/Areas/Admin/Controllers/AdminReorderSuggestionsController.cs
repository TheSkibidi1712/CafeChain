using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CafeChain.Areas.Admin.Controllers
{
    public sealed class AdminReorderSuggestionsController : AdminBaseController
    {
        private readonly IReorderSuggestionService _suggestions;
        private readonly IStockAlertService _stockAlerts;
        private readonly IAdminActorContextAccessor _actorAccessor;
        private readonly IAdminStoreScopeResolver _storeScopeResolver;

        public AdminReorderSuggestionsController(
            IReorderSuggestionService suggestions,
            IStockAlertService stockAlerts,
            IAdminActorContextAccessor actorAccessor,
            IAdminStoreScopeResolver storeScopeResolver)
        {
            _suggestions = suggestions;
            _stockAlerts = stockAlerts;
            _actorAccessor = actorAccessor;
            _storeScopeResolver = storeScopeResolver;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? storeId, int analysisWindowDays = 30)
        {
            var actor = _actorAccessor.Get(User);
            if (actor.StaffId <= 0)
                return Unauthorized();
            var storeScope = await _storeScopeResolver.ResolveAsync(actor, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var selectedStoreId = storeScope.StoreId!.Value;
            var stores = storeScope.AccessibleStores
                .Select(x => new SelectListItem(x.StoreName, x.StoreId.ToString()))
                .ToList();
            SetStoreScopeViewData(storeScope);
            ViewBag.Stores = stores;
            ViewBag.SelectedStoreId = selectedStoreId;
            ViewBag.CanCreateDraft = actor.RoleNames.Any(x =>
                x.Equals(RoleConstants.StoreManager, StringComparison.OrdinalIgnoreCase)
                || x.Equals(RoleConstants.BusinessOwner, StringComparison.OrdinalIgnoreCase)
                || x.Equals(RoleConstants.AreaManager, StringComparison.OrdinalIgnoreCase));

            var result = await _suggestions.GetForStoreAsync(
                selectedStoreId, actor.StaffId, actor.RoleNames, analysisWindowDays);
            if (!result.IsSuccess || result.Data == null)
            {
                ViewBag.ErrorMessage = result.Message ?? "Không tải được gợi ý nhập hàng.";
                return View(new ReorderSuggestionListDto
                {
                    StoreId = selectedStoreId,
                    AnalysisWindowDays = analysisWindowDays
                });
            }

            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDraft(int storeId, int ingredientId)
        {
            var actor = _actorAccessor.Get(User);
            var storeScope = await _storeScopeResolver.ResolveAsync(actor, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var created = await _stockAlerts.CreateOrOpenFromReorderSuggestionAsync(
                storeScope.StoreId!.Value,
                ingredientId,
                "REORDER_SUGGESTION");

            TempData[created.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                created.Message ?? (created.IsSuccess ? "Đã mở cảnh báo tồn kho." : "Không mở được cảnh báo tồn kho.");
            if (created.IsSuccess && created.Data > 0)
                return RedirectToAction("Details", "AdminStockAlerts", new { id = created.Data, storeId = storeScope.StoreId });
            return RedirectToAction(nameof(Index), new { storeId = storeScope.StoreId });
        }
    }
}
