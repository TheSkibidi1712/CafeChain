using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CafeChain.Application.DTOs.Admin.StoreScope;
using CafeChain.ViewModels.Admin.StoreScope;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public abstract class AdminStoreScopedController : Controller
    {
        protected IActionResult StoreScopeFailure(AdminStoreScopeResolution resolution)
        {
            Response.StatusCode = resolution.Status == AdminStoreScopeResolutionStatus.StoreNotFound
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status403Forbidden;
            return View("~/Areas/Admin/Views/Shared/StoreScopeError.cshtml", new AdminStoreScopeErrorVM
            {
                ErrorCode = resolution.ErrorCode ?? AdminStoreScopeErrorCodes.StoreScopeNotConfigured,
                Message = resolution.Message
                          ?? "Tài khoản chưa được cấu hình phạm vi cửa hàng. Vui lòng liên hệ quản trị viên."
            });
        }

        protected void SetStoreScopeViewData(AdminStoreScopeResolution resolution)
        {
            ViewBag.StoreOptions = resolution.AccessibleStores;
            ViewBag.SelectedStoreId = resolution.StoreId;
            if (resolution.WarningCode == AdminStoreScopeErrorCodes.SelectedStoreNoLongerAccessible)
            {
                TempData["WarningMessage"] =
                    "Cửa hàng đã chọn trước đó không còn trong phạm vi được cấp. Hệ thống đã chuyển sang cửa hàng hợp lệ.";
            }
        }
    }

    [Authorize(Policy = "RequireAdminPanelAccess")]
    public abstract class AdminBaseController : AdminStoreScopedController
    {
    }
}
