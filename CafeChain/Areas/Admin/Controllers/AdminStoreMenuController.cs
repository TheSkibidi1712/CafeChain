using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Data;
using CafeChain.ViewModels.Admin.StoreMenu;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers
{
    [RequirePermission(PermissionConstants.StoreMenuView)]
    public sealed class AdminStoreMenuController : AdminBaseController
    {
        private readonly AppDbContext _context;
        private readonly IAdminActorContextAccessor _actor;
        private readonly IAdminPermissionService _permissions;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly IStoreMenuWorkspaceService _workspace;
        private readonly IStoreMenuPricingService _pricing;

        public AdminStoreMenuController(
            AppDbContext context,
            IAdminActorContextAccessor actor,
            IAdminPermissionService permissions,
            IScopeAuthorizationService scopeAuthorization,
            IStoreMenuWorkspaceService workspace,
            IStoreMenuPricingService pricing)
        {
            _context = context;
            _actor = actor;
            _permissions = permissions;
            _scopeAuthorization = scopeAuthorization;
            _workspace = workspace;
            _pricing = pricing;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var actor = _actor.Get(User);
            var permissions = await _permissions.GetEffectivePermissionCodesAsync(actor.AccountId);
            HashSet<string> effective = permissions.IsSuccess && permissions.Data != null
                ? permissions.Data
                : [];
            return View(new StoreMenuPageVM
            {
                Stores = await ResolveAllowedStoresAsync(actor.StaffId, actor.StoreId, cancellationToken),
                CanPublish = effective.Contains(PermissionConstants.StoreMenuUpdate)
                    && (User.IsInRole(RoleConstants.BusinessOwner)
                        || User.IsInRole(RoleConstants.SystemAdmin)),
                CanOperate = effective.Contains(PermissionConstants.StoreMenuUpdate),
                CanOverridePrice = effective.Contains(PermissionConstants.StoreMenuOverridePrice)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Rows(int storeId, CancellationToken cancellationToken)
        {
            var actor = _actor.Get(User);
            return ServiceJson(await _workspace.GetRowsAsync(
                storeId, actor.StaffId, DateTime.UtcNow, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.StoreMenuUpdate)]
        public async Task<IActionResult> UpdateLifecycle(
            [FromBody] UpdateStoreMenuLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var actor = _actor.Get(User);
            return ServiceJson(await _workspace.UpdateLifecycleAsync(request, actor.StaffId, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.StoreMenuOverridePrice)]
        public async Task<IActionResult> UpdatePriceOverride(
            [FromBody] UpdateStoreMenuPriceOverrideRequest request,
            CancellationToken cancellationToken)
        {
            var actor = _actor.Get(User);
            return ServiceJson(await _pricing.UpdateOverrideAsync(request, actor.StaffId, cancellationToken));
        }

        private async Task<IReadOnlyList<StoreMenuStoreOptionVM>> ResolveAllowedStoresAsync(
            int staffId,
            int ownStoreId,
            CancellationToken cancellationToken)
        {
            if (staffId <= 0)
                return [];

            var allowed = await _scopeAuthorization.GetAllowedStoresAsync(
                staffId,
                StoreScopePurpose.Default);
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
