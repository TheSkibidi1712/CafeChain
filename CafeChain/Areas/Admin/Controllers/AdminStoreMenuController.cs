using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Data;
using CafeChain.ViewModels.Admin.StoreMenu;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers
{
    public sealed class AdminStoreMenuController : AdminBaseController
    {
        private readonly AppDbContext _context;
        private readonly IAdminActorContextAccessor _actor;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly IStoreMenuWorkspaceService _workspace;
        private readonly IStoreMenuPricingService _pricing;

        public AdminStoreMenuController(
            AppDbContext context,
            IAdminActorContextAccessor actor,
            IScopeAuthorizationService scopeAuthorization,
            IStoreMenuWorkspaceService workspace,
            IStoreMenuPricingService pricing)
        {
            _context = context;
            _actor = actor;
            _scopeAuthorization = scopeAuthorization;
            _workspace = workspace;
            _pricing = pricing;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            if (!CanView()) return Forbid();
            var actor = _actor.Get(User);
            return View(new StoreMenuPageVM
            {
                Stores = await ResolveAllowedStoresAsync(actor.StaffId, actor.StoreId, cancellationToken),
                CanPublish = User.IsInRole(RoleConstants.BusinessOwner),
                CanOperate = User.IsInRole(RoleConstants.BusinessOwner)
                    || User.IsInRole(RoleConstants.StoreManager),
                CanOverridePrice = User.IsInRole(RoleConstants.BusinessOwner)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Rows(int storeId, CancellationToken cancellationToken)
        {
            if (!CanView()) return ApiFailure("Bạn không có quyền xem menu cửa hàng.", StatusCodes.Status403Forbidden);
            var actor = _actor.Get(User);
            return ServiceJson(await _workspace.GetRowsAsync(
                storeId, actor.StaffId, DateTime.UtcNow, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLifecycle(
            [FromBody] UpdateStoreMenuLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var actor = _actor.Get(User);
            return ServiceJson(await _workspace.UpdateLifecycleAsync(request, actor.StaffId, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePriceOverride(
            [FromBody] UpdateStoreMenuPriceOverrideRequest request,
            CancellationToken cancellationToken)
        {
            var actor = _actor.Get(User);
            return ServiceJson(await _pricing.UpdateOverrideAsync(request, actor.StaffId, cancellationToken));
        }

        private bool CanView() =>
            User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.AreaManager)
            || User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.SystemAdmin);

        private async Task<IReadOnlyList<StoreMenuStoreOptionVM>> ResolveAllowedStoresAsync(
            int staffId,
            int ownStoreId,
            CancellationToken cancellationToken)
        {
            if (User.IsInRole(RoleConstants.BusinessOwner)
                || User.IsInRole(RoleConstants.AccountantWarehouse)
                || User.IsInRole(RoleConstants.SystemAdmin))
            {
                return await _context.Stores.AsNoTracking()
                    .Where(x => x.Active)
                    .OrderBy(x => x.Name)
                    .Select(x => new StoreMenuStoreOptionVM { Id = x.StoreId, Name = x.Name })
                    .ToListAsync(cancellationToken);
            }

            var allowed = await _scopeAuthorization.GetAllowedStoresAsync(staffId);
            if (User.IsInRole(RoleConstants.StoreManager)
                && ownStoreId > 0
                && allowed.All(x => x.StoreId != ownStoreId))
            {
                var ownStore = await _context.Stores.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.StoreId == ownStoreId && x.Active, cancellationToken);
                if (ownStore != null) allowed.Add(ownStore);
            }
            return allowed.OrderBy(x => x.Name)
                .Select(x => new StoreMenuStoreOptionVM { Id = x.StoreId, Name = x.Name })
                .ToList();
        }

        private JsonResult ServiceJson<T>(Application.Results.ServiceResult<T> result)
        {
            if (!result.IsSuccess)
            {
                Response.StatusCode = result.ErrorCode switch
                {
                    "STORE_MENU_CHANGED_BY_ANOTHER_USER" => StatusCodes.Status409Conflict,
                    var code when code?.Contains("FORBIDDEN", StringComparison.Ordinal) == true => StatusCodes.Status403Forbidden,
                    _ => StatusCodes.Status400BadRequest
                };
            }
            return Json(new
            {
                success = result.IsSuccess,
                message = result.Message,
                errorCode = result.ErrorCode,
                data = result.Data
            });
        }

        private JsonResult ApiFailure(string message, int statusCode)
        {
            Response.StatusCode = statusCode;
            return Json(new { success = false, message });
        }
    }
}
