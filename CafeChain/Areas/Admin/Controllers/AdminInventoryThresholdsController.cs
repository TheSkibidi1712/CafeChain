using CafeChain.Application.DTOs.Admin.InventoryThresholds;
using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Services.Admin.StoreInventories;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Issue #104 — configure MinStockLevel for StoreInventory (Admin MVC).
    /// Read: any Admin panel role with store scope.
    /// Edit: StoreManager / AreaManager / BusinessOwner / SystemAdmin only.
    /// </summary>
    [RequirePermission(PermissionConstants.InventoryThresholdView)]
    public class AdminInventoryThresholdsController : AdminBaseController
    {
        private readonly IInventoryThresholdService _service;
        private readonly IAdminActorContextAccessor _actor;
        private readonly IAdminStoreScopeResolver _storeScopeResolver;
        private readonly IAdminPermissionService? _permissions;

        public AdminInventoryThresholdsController(
            IInventoryThresholdService service,
            IAdminActorContextAccessor actor,
            IAdminStoreScopeResolver storeScopeResolver,
            IAdminPermissionService? permissions = null)
        {
            _service = service;
            _actor = actor;
            _storeScopeResolver = storeScopeResolver;
            _permissions = permissions;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int storeId = 0, string? search = null, int page = 1)
        {
            var accountId = GetAccountId();
            if (accountId <= 0)
                return Unauthorized();

            var actor = _actor.Get(User);
            var storeScope = await _storeScopeResolver.ResolveAsync(
                actor,
                storeId > 0 ? storeId : null);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            storeId = storeScope.StoreId!.Value;
            var result = await _service.ListAsync(accountId, storeId, search, page, 20);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tải được danh sách ngưỡng tồn.";
                return View(new InventoryThresholdListResultDto());
            }

            ViewBag.Search = search;
            var permission = _permissions == null
                ? null
                : await _permissions.HasPermissionAsync(
                    accountId,
                    PermissionConstants.InventoryThresholdUpdate,
                    storeId);
            ViewBag.CanEditThreshold =
                permission?.IsSuccess == true && permission.Data?.Allowed == true;
            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.InventoryThresholdUpdate)]
        public async Task<IActionResult> Update(
            int storeInventoryId,
            string? minStockLevel,
            string? rowVersion,
            int storeId = 0,
            string? search = null,
            int page = 1)
        {
            var accountId = GetAccountId();
            if (accountId <= 0)
                return Unauthorized();

            var actor = _actor.Get(User);
            var storeScope = await _storeScopeResolver.ResolveAsync(actor, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            storeId = storeScope.StoreId!.Value;

            decimal? value = null;
            if (!string.IsNullOrWhiteSpace(minStockLevel))
            {
                if (!decimal.TryParse(
                        minStockLevel.Trim().Replace(',', '.'),
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var parsed) &&
                    !decimal.TryParse(
                        minStockLevel.Trim(),
                        NumberStyles.Number,
                        CultureInfo.GetCultureInfo("vi-VN"),
                        out parsed))
                {
                    TempData["ErrorMessage"] = "Ngưỡng tồn tối thiểu không hợp lệ.";
                    return RedirectToAction(nameof(Index), new { storeId, search, page });
                }

                value = parsed;
            }

            var result = await _service.UpdateMinStockLevelAsync(
                accountId,
                storeInventoryId,
                value,
                rowVersion);
            if (!result.IsSuccess)
                TempData["ErrorMessage"] = result.Message;
            else
                TempData["SuccessMessage"] = result.Message ?? "Cập nhật ngưỡng tồn kho thành công.";

            return RedirectToAction(nameof(Index), new { storeId, search, page });
        }

        private int GetAccountId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("AccountId")
                     ?? User.FindFirst("sub");

            if (claim == null)
                return 0;

            return int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
