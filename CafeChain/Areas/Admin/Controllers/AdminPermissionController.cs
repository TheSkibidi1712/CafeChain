using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminPermissionController : AdminBaseController
    {
        private readonly IAdminPermissionService _permissionService;

        public AdminPermissionController(IAdminPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var guard = await EnsureCanManagePermissionsAsync(jsonResponse: false);
            if (guard != null) return guard;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Roles(int pageIndex = 1, int pageSize = 10, string? search = null)
        {
            var guard = await EnsureCanManagePermissionsAsync();
            if (guard != null) return guard;

            var result = await _permissionService.GetRolesAsync(pageIndex, pageSize, search);
            return ToJsonResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> Staff(int pageIndex = 1, int pageSize = 10, string? search = null)
        {
            var guard = await EnsureCanManagePermissionsAsync();
            if (guard != null) return guard;

            var result = await _permissionService.GetStaffAsync(pageIndex, pageSize, search);
            return ToJsonResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> ScopeReferences(int scopeTypeId, int? parentId = null)
        {
            var guard = await EnsureCanManagePermissionsAsync();
            if (guard != null) return guard;

            var result = await _permissionService.GetScopeReferencesAsync(scopeTypeId, parentId);
            return ToJsonResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> Permissions()
        {
            var guard = await EnsureCanManagePermissionsAsync();
            if (guard != null) return guard;

            var result = await _permissionService.GetPermissionCatalogAsync();
            return ToJsonResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> RolePermissions(int roleId)
        {
            var guard = await EnsureCanManagePermissionsAsync();
            if (guard != null) return guard;

            var result = await _permissionService.GetRolePermissionsAsync(roleId);
            return ToJsonResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> SaveRolePermissions(int roleId, [FromBody] SaveRolePermissionsRequest request)
        {
            var guard = await EnsureCanManagePermissionsAsync();
            if (guard != null) return guard;

            var result = await _permissionService.UpdateRolePermissionsAsync(
                roleId,
                request ?? new SaveRolePermissionsRequest());

            return ToJsonResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> StaffRoles(int staffId)
        {
            var guard = await EnsureCanManagePermissionsAsync();
            if (guard != null) return guard;

            var result = await _permissionService.GetStaffRolesAsync(staffId);
            return ToJsonResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> SaveStaffRoles(int staffId, [FromBody] SaveStaffRolesRequest request)
        {
            var guard = await EnsureCanManagePermissionsAsync();
            if (guard != null) return guard;

            var result = await _permissionService.UpdateStaffRolesAsync(
                staffId,
                request ?? new SaveStaffRolesRequest());

            return ToJsonResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> StaffScopes(int staffId)
        {
            var guard = await EnsureCanManagePermissionsAsync();
            if (guard != null) return guard;

            var result = await _permissionService.GetStaffScopesAsync(staffId);
            return ToJsonResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> SaveStaffScopes(int staffId, [FromBody] SaveStaffScopesRequest request)
        {
            var guard = await EnsureCanManagePermissionsAsync();
            if (guard != null) return guard;

            var result = await _permissionService.UpdateStaffScopesAsync(
                staffId,
                request ?? new SaveStaffScopesRequest());

            return ToJsonResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> StaffOverrides(int staffId)
        {
            var guard = await EnsureCanManagePermissionsAsync();
            if (guard != null) return guard;

            var result = await _permissionService.GetAccountOverridesAsync(staffId);
            return ToJsonResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> SaveStaffOverrides(int staffId, [FromBody] SaveAccountPermissionOverridesRequest request)
        {
            var guard = await EnsureCanManagePermissionsAsync();
            if (guard != null) return guard;

            var result = await _permissionService.UpdateAccountOverridesAsync(
                staffId,
                request ?? new SaveAccountPermissionOverridesRequest());

            return ToJsonResult(result);
        }


        // ===================================================
        // PRIVATE METHODS
        // ===================================================
        private async Task<IActionResult?> EnsureCanManagePermissionsAsync(bool jsonResponse = true)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return jsonResponse
                    ? StatusCode(StatusCodes.Status401Unauthorized, new
                    {
                        success = false,
                        message = "Bạn cần đăng nhập để truy cập chức năng này."
                    })
                    : Challenge();
            }

            var accountIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(accountIdValue, out var accountId))
            {
                return jsonResponse
                    ? StatusCode(StatusCodes.Status401Unauthorized, new
                    {
                        success = false,
                        message = "Bạn cần đăng nhập để truy cập chức năng này."
                    })
                    : RedirectToAction("AccessDenied", "Account", new { area = "" });
            }

            var decision = await _permissionService.HasPermissionAsync(
                accountId,
                PermissionConstants.SystemPermissionManage);

            if (!decision.IsSuccess || decision.Data == null || !decision.Data.Allowed)
            {
                return jsonResponse
                    ? StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        message = "Bạn không có quyền truy cập chức năng này. Vui lòng liên hệ cấp trên hoặc quản trị viên để được cấp quyền."
                    })
                    : RedirectToAction("AccessDenied", "Account", new { area = "" });
            }

            return null;
        }

        private IActionResult ToJsonResult<T>(Application.Results.ServiceResult<T> result)
        {
            if (result.IsSuccess)
            {
                return Json(new { success = true, message = result.Message, data = result.Data });
            }

            return BadRequest(new
            {
                success = false,
                message = result.Message,
                errorCode = result.ErrorCode,
                errors = result.Errors
            });
        }

        private IActionResult ToJsonResult(Application.Results.ServiceResult result)
        {
            if (result.IsSuccess)
            {
                return Json(new { success = true, message = result.Message });
            }

            return BadRequest(new
            {
                success = false,
                message = result.Message,
                errorCode = result.ErrorCode,
                errors = result.Errors
            });
        }
    }
}
