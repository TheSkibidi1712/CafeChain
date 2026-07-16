using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers
{
    public sealed class AdminReorderSuggestionsController : AdminBaseController
    {
        private readonly IReorderSuggestionService _suggestions;
        private readonly IRestockRequestService _restockRequests;
        private readonly IAdminActorContextAccessor _actorAccessor;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly AppDbContext _context;

        public AdminReorderSuggestionsController(
            IReorderSuggestionService suggestions,
            IRestockRequestService restockRequests,
            IAdminActorContextAccessor actorAccessor,
            IScopeAuthorizationService scopeAuthorization,
            AppDbContext context)
        {
            _suggestions = suggestions;
            _restockRequests = restockRequests;
            _actorAccessor = actorAccessor;
            _scopeAuthorization = scopeAuthorization;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? storeId, int analysisWindowDays = 30)
        {
            var actor = _actorAccessor.Get(User);
            var stores = await GetAccessibleStoresAsync(actor.StaffId, actor.StoreId, actor.RoleNames);
            var selectedStoreId = storeId.GetValueOrDefault();
            if (selectedStoreId <= 0)
                selectedStoreId = actor.StoreId;
            if (selectedStoreId <= 0 && int.TryParse(stores.FirstOrDefault()?.Value, out var firstStoreId))
                selectedStoreId = firstStoreId;
            ViewBag.Stores = stores;
            ViewBag.SelectedStoreId = selectedStoreId;
            ViewBag.CanCreateDraft = actor.RoleNames.Any(x =>
                x.Equals(RoleConstants.StoreManager, StringComparison.OrdinalIgnoreCase)
                || x.Equals(RoleConstants.BusinessOwner, StringComparison.OrdinalIgnoreCase)
                || x.Equals(RoleConstants.AreaManager, StringComparison.OrdinalIgnoreCase));

            if (selectedStoreId <= 0)
                return View(new ReorderSuggestionListDto { AnalysisWindowDays = analysisWindowDays });

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
        public async Task<IActionResult> CreateDraft(int storeId, int ingredientId, int analysisWindowDays = 30)
        {
            var actor = _actorAccessor.Get(User);
            var suggestions = await _suggestions.GetForStoreAsync(
                storeId, actor.StaffId, actor.RoleNames, analysisWindowDays);
            var item = suggestions.Data?.Items.FirstOrDefault(x => x.IngredientId == ingredientId);
            if (!suggestions.IsSuccess || item == null || item.Status != ReorderSuggestionStatuses.Ready
                || !item.SuggestedBaseQuantity.HasValue || item.SuggestedBaseQuantity <= 0
                || !item.MinLevel.HasValue || !item.AverageDailyUsage.HasValue || !item.LeadTimeDays.HasValue)
            {
                TempData["ErrorMessage"] = suggestions.Message ?? "Gợi ý không còn đủ điều kiện để tạo yêu cầu nhập.";
                return RedirectToAction(nameof(Index), new { storeId, analysisWindowDays });
            }

            var created = await _restockRequests.CreateDraftFromSuggestionAsync(
                new CreateRestockDraftFromSuggestionDto
                {
                    StoreId = storeId,
                    IngredientId = ingredientId,
                    RequestedQuantity = item.SuggestedBaseQuantity.Value,
                    SuggestedQuantity = item.SuggestedBaseQuantity.Value,
                    AnalysisWindowDays = analysisWindowDays,
                    AvailableSnapshot = item.AvailableQuantity,
                    MinLevelSnapshot = item.MinLevel.Value,
                    AverageDailyUsageSnapshot = item.AverageDailyUsage.Value,
                    LeadTimeDaysSnapshot = item.LeadTimeDays.Value,
                    IncomingQuantitySnapshot = item.IncomingApprovedPoQuantity,
                    SuggestionReason = item.Reason
                },
                actor.StaffId);

            TempData[created.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                created.Message ?? (created.IsSuccess ? "Đã tạo yêu cầu nhập nháp." : "Không tạo được yêu cầu nhập.");
            if (created.IsSuccess && created.Data != null)
                return RedirectToAction("Details", "AdminRestockRequests", new { id = created.Data.RestockRequestId });
            return RedirectToAction(nameof(Index), new { storeId, analysisWindowDays });
        }

        private async Task<List<SelectListItem>> GetAccessibleStoresAsync(
            int staffId,
            int actorStoreId,
            IReadOnlyCollection<string> roles)
        {
            var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var stores = await _context.Stores.AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new { x.StoreId, x.Name })
                .ToListAsync();
            if (roleSet.Contains(RoleConstants.StoreManager))
                stores = stores.Where(x => x.StoreId == actorStoreId).ToList();
            else if (roleSet.Contains(RoleConstants.AreaManager))
            {
                var allowed = new List<int>();
                foreach (var store in stores)
                {
                    if (await _scopeAuthorization.CanAccessStoreAsync(staffId, store.StoreId))
                        allowed.Add(store.StoreId);
                }
                stores = stores.Where(x => allowed.Contains(x.StoreId)).ToList();
            }
            else if (!roleSet.Contains(RoleConstants.BusinessOwner)
                     && !roleSet.Contains(RoleConstants.AccountantWarehouse))
            {
                stores.Clear();
            }

            return stores.Select(x => new SelectListItem(x.Name, x.StoreId.ToString())).ToList();
        }
    }
}
