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
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly IStoreMenuWorkspaceService _workspace;
        private readonly IStoreMenuPricingService _pricing;
        private readonly IStoreMenuProvisioningService _provisioning;
        private readonly IAdminPermissionService _permissions;

        public AdminStoreMenuController(
            AppDbContext context,
            IAdminActorContextAccessor actor,
            IScopeAuthorizationService scopeAuthorization,
            IStoreMenuWorkspaceService workspace,
            IStoreMenuPricingService pricing,
            IStoreMenuProvisioningService provisioning,
            IAdminPermissionService permissions)
        {
            _context = context;
            _actor = actor;
            _scopeAuthorization = scopeAuthorization;
            _workspace = workspace;
            _pricing = pricing;
            _provisioning = provisioning;
            _permissions = permissions;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var actor = _actor.Get(User);
            var effective = await _permissions.GetEffectivePermissionCodesAsync(actor.AccountId);
            var codes = effective.IsSuccess && effective.Data != null
                ? effective.Data
                : new HashSet<string>(StringComparer.Ordinal);
            if (!codes.Contains(PermissionConstants.StoreMenuView))
                return Forbid();

            return View(new StoreMenuPageVM
            {
                Stores = await ResolveAllowedStoresAsync(actor.StaffId, actor.StoreId, cancellationToken),
                CanPublish = codes.Contains(PermissionConstants.StoreMenuUpdate),
                CanOperate = codes.Contains(PermissionConstants.StoreMenuUpdate),
                CanProvision = codes.Contains(PermissionConstants.StoreMenuUpdate),
                CanOverridePrice = codes.Contains(PermissionConstants.StoreMenuOverridePrice)
            });
        }

        [HttpGet]
        [RequirePermission(PermissionConstants.StoreMenuView)]
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.StoreMenuUpdate)]
        public async Task<IActionResult> ProvisionMissing(int storeId, CancellationToken cancellationToken)
        {
            var actor = _actor.Get(User);
            return ServiceJson(await _provisioning.ProvisionMissingAsync(
                storeId,
                actor.AccountId,
                actor.StaffId,
                cancellationToken));
        }

        private async Task<IReadOnlyList<StoreMenuStoreOptionVM>> ResolveAllowedStoresAsync(
            int staffId,
            int ownStoreId,
            CancellationToken cancellationToken)
        {
            if (User.IsInRole(RoleConstants.BusinessOwner)
                || User.IsInRole(RoleConstants.AccountantWarehouse))
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
