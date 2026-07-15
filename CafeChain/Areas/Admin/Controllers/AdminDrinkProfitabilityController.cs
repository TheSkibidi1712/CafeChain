using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Data;
using CafeChain.ViewModels.Admin.Profitability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers
{
    public sealed class AdminDrinkProfitabilityController : AdminBaseController
    {
        private readonly AppDbContext _context;
        private readonly IAdminActorContextAccessor _actor;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly IDrinkSizeProfitabilityQueryService _profitability;
        private readonly IDrinkSizeRecipeResolver _recipeResolver;
        private readonly IDrinkSizeToppingPolicyService _toppingPolicies;
        private readonly IPriceSuggestionService _suggestions;
        private readonly IDrinkSizePricingService _pricing;

        public AdminDrinkProfitabilityController(
            AppDbContext context,
            IAdminActorContextAccessor actor,
            IScopeAuthorizationService scopeAuthorization,
            IDrinkSizeProfitabilityQueryService profitability,
            IDrinkSizeRecipeResolver recipeResolver,
            IDrinkSizeToppingPolicyService toppingPolicies,
            IPriceSuggestionService suggestions,
            IDrinkSizePricingService pricing)
        {
            _context = context;
            _actor = actor;
            _scopeAuthorization = scopeAuthorization;
            _profitability = profitability;
            _recipeResolver = recipeResolver;
            _toppingPolicies = toppingPolicies;
            _suggestions = suggestions;
            _pricing = pricing;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            if (!CanView()) return Forbid();

            var actor = _actor.Get(User);
            var stores = await ResolveAllowedStoresAsync(actor.StaffId, actor.StoreId, cancellationToken);
            var drinks = await _context.Drinks.AsNoTracking()
                .Where(x => x.Active && x.DrinkSizes.Any(ds => ds.Active))
                .OrderBy(x => x.Name)
                .Select(x => new ProfitabilitySelectOptionVM { Id = x.DrinkId, Name = x.Name })
                .ToListAsync(cancellationToken);

            return View(new DrinkProfitabilityPageVM
            {
                Stores = stores,
                Drinks = drinks,
                CanUpdateGlobalPrice = User.IsInRole(RoleConstants.BusinessOwner),
                CanManageToppingPolicy = User.IsInRole(RoleConstants.BusinessOwner)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Preview(int storeId, int drinkId, CancellationToken cancellationToken)
        {
            if (!CanView()) return ApiFailure("Bạn không có quyền xem giá vốn.", StatusCodes.Status403Forbidden);
            var actor = _actor.Get(User);
            var result = await _profitability.PreviewAsync(storeId, drinkId, DateTime.UtcNow, actor.StaffId, cancellationToken);
            return ServiceJson(result);
        }

        [HttpGet]
        public async Task<IActionResult> DataHealth(int? drinkId, CancellationToken cancellationToken)
        {
            if (!CanView()) return ApiFailure("Bạn không có quyền xem tình trạng BOM.", StatusCodes.Status403Forbidden);
            var rows = await _recipeResolver.GetDataHealthAsync(DateTime.UtcNow, cancellationToken);
            if (drinkId.HasValue) rows = rows.Where(x => x.DrinkId == drinkId.Value).ToList();
            return Json(new { success = true, data = rows });
        }

        [HttpGet]
        public async Task<IActionResult> ToppingPolicies(int drinkSizeId, CancellationToken cancellationToken)
        {
            if (!CanView()) return ApiFailure("Bạn không có quyền xem chính sách topping.", StatusCodes.Status403Forbidden);

            var drinkSize = await _context.DrinkSizes.AsNoTracking()
                .Where(x => x.DrinkSizeId == drinkSizeId && x.Active)
                .Select(x => new { x.DrinkId })
                .FirstOrDefaultAsync(cancellationToken);
            if (drinkSize == null) return ApiFailure("Không tìm thấy DrinkSize.", StatusCodes.Status404NotFound);

            var permittedIds = await _context.DrinkToppings.AsNoTracking()
                .Where(x => x.DrinkId == drinkSize.DrinkId && x.Active && x.Topping.Active)
                .Select(x => x.ToppingId)
                .Union(_context.DrinkDefaultToppings.AsNoTracking()
                    .Where(x => x.DrinkId == drinkSize.DrinkId && x.Topping.Active)
                    .Select(x => x.ToppingId))
                .Distinct()
                .ToListAsync(cancellationToken);
            var options = await _context.Toppings.AsNoTracking()
                .Where(x => permittedIds.Contains(x.ToppingId) && x.Active)
                .OrderBy(x => x.Name)
                .Select(x => new { x.ToppingId, x.Name, x.Price })
                .ToListAsync(cancellationToken);
            var policies = await _toppingPolicies.GetActiveAsync(drinkSizeId, cancellationToken);
            return Json(new { success = true, data = new { policies, options } });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Suggest([FromBody] PriceSuggestionRequest request)
        {
            if (!CanView()) return ApiFailure("Bạn không có quyền tính giá đề xuất.", StatusCodes.Status403Forbidden);
            var result = _suggestions.Calculate(request);
            if (!result.IsValid) Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = result.IsValid, message = result.Message, data = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePrice(int storeId, [FromBody] UpdateDrinkSizePriceRequest request, CancellationToken cancellationToken)
        {
            var actor = _actor.Get(User);
            var result = await _pricing.UpdatePriceAsync(request, storeId, actor.StaffId, cancellationToken);
            return ServiceJson(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpsertToppingPolicy([FromBody] UpsertDrinkSizeToppingPolicyRequest request, CancellationToken cancellationToken)
        {
            var actor = _actor.Get(User);
            var result = await _toppingPolicies.UpsertAsync(request, actor.StaffId, cancellationToken);
            return ServiceJson(result);
        }

        private bool CanView() =>
            User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.AreaManager)
            || User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.SystemAdmin);

        private async Task<IReadOnlyList<ProfitabilitySelectOptionVM>> ResolveAllowedStoresAsync(int staffId, int ownStoreId, CancellationToken cancellationToken)
        {
            if (User.IsInRole(RoleConstants.BusinessOwner)
                || User.IsInRole(RoleConstants.AccountantWarehouse)
                || User.IsInRole(RoleConstants.SystemAdmin))
            {
                return await _context.Stores.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Name)
                    .Select(x => new ProfitabilitySelectOptionVM { Id = x.StoreId, Name = x.Name })
                    .ToListAsync(cancellationToken);
            }

            var allowed = await _scopeAuthorization.GetAllowedStoresAsync(staffId);
            if (User.IsInRole(RoleConstants.StoreManager) && ownStoreId > 0 && allowed.All(x => x.StoreId != ownStoreId))
            {
                var ownStore = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(x => x.StoreId == ownStoreId && x.Active, cancellationToken);
                if (ownStore != null) allowed.Add(ownStore);
            }
            return allowed.OrderBy(x => x.Name)
                .Select(x => new ProfitabilitySelectOptionVM { Id = x.StoreId, Name = x.Name })
                .ToList();
        }

        private JsonResult ServiceJson<T>(Application.Results.ServiceResult<T> result)
        {
            if (!result.IsSuccess)
            {
                Response.StatusCode = result.ErrorCode?.Contains("FORBIDDEN", StringComparison.Ordinal) == true
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status400BadRequest;
            }
            return Json(new { success = result.IsSuccess, message = result.Message, errorCode = result.ErrorCode, data = result.Data });
        }

        private JsonResult ApiFailure(string message, int statusCode)
        {
            Response.StatusCode = statusCode;
            return Json(new { success = false, message });
        }
    }
}
