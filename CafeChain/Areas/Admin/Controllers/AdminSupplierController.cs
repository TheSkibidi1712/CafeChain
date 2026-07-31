using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Exceptions;
using CafeChain.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(PermissionConstants.SupplierView)]
    public class AdminSupplierController : Controller
    {
        private readonly IAdminSupplierService _service;
        private readonly IAdminActorContextAccessor _actorContext;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly IAdminPermissionService? _permissions;

        public AdminSupplierController(
            IAdminSupplierService service,
            IAdminActorContextAccessor actorContext,
            IScopeAuthorizationService scopeAuthorization,
            IAdminPermissionService? permissions = null)
        {
            _service = service;
            _actorContext = actorContext;
            _scopeAuthorization = scopeAuthorization;
            _permissions = permissions;
        }

        // ===== INDEX =====
        public async Task<IActionResult> Index(string? search, bool? status)
        {
            var storeScope = await ResolveStoreScopeAsync();
            var data = await _service.GetAllAsync(search, status, storeScope);
            var actor = _actorContext.Get(User);
            ViewBag.CanMutateSupplier = await HasPermissionAsync(
                actor.AccountId,
                PermissionConstants.SupplierUpdate);
            return View(data);
        }

        // ===== GET DETAIL (for detail modal / page) =====
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _service.GetByIdAsync(id, await ResolveStoreScopeAsync());
            if (data == null)
                return Json(new { success = false, message = "Không tìm thấy nhà cung cấp" });

            return Json(new { success = true, data });
        }

        // ===== GET NEXT CODE (auto-generate NCC code) =====
        [HttpGet]
        public async Task<IActionResult> GetNextCode()
        {
            var code = await _service.GenerateNextCodeAsync();
            return Json(new { success = true, code });
        }

        // ===== CREATE =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierCreate)]
        public async Task<IActionResult> Create([FromBody] AdminSupplierCreateDTO dto)
        {
            if (!ModelState.IsValid)
                return SupplierValidationError();

            try
            {
                var actor = _actorContext.Get(User);
                var id = await _service.CreateAsync(dto, actor.StaffId);
                return Json(new { success = true, message = "Thêm nhà cung cấp thành công", data = id });
            }
            catch (SupplierDomainException ex)
            {
                return Json(new { success = false, code = ex.Code, message = ex.Message, data = ex.DataPayload });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, code = "SUPPLIER_OPERATION_FAILED", message = ex.Message });
            }
        }

        // ===== UPDATE =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierUpdate)]
        public async Task<IActionResult> Update([FromBody] AdminSupplierUpdateDTO dto)
        {
            if (!ModelState.IsValid)
                return SupplierValidationError();

            try
            {
                var actor = _actorContext.Get(User);
                await _service.UpdateAsync(dto, actor.StaffId);
                return Json(new { success = true, message = "Cập nhật thành công" });
            }
            catch (SupplierDomainException ex)
            {
                return Json(new { success = false, code = ex.Code, message = ex.Message, data = ex.DataPayload });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, code = "SUPPLIER_OPERATION_FAILED", message = ex.Message });
            }
        }

        // ===== TOGGLE STATUS =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierToggleStatus)]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                await _service.ToggleStatusAsync(id);
                return Json(new { success = true, message = "Đã cập nhật trạng thái" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===================== PHONES =====================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierUpdate)]
        public async Task<IActionResult> AddPhone([FromBody] AdminSupplierPhoneCreateDTO dto)
        {
            try
            {
                await _service.AddPhoneAsync(dto);
                return Json(new { success = true, message = "Thêm số điện thoại thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierUpdate)]
        public async Task<IActionResult> DeletePhone(int supplierPhoneId)
        {
            try
            {
                await _service.DeletePhoneAsync(supplierPhoneId);
                return Json(new { success = true, message = "Đã xoá số điện thoại" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===================== CONTACTS =====================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierUpdate)]
        public async Task<IActionResult> AddContact([FromBody] AdminSupplierContactCreateDTO dto)
        {
            try
            {
                await _service.AddContactAsync(dto);
                return Json(new { success = true, message = "Thêm liên hệ thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierUpdate)]
        public async Task<IActionResult> UpdateContact([FromBody] AdminSupplierContactUpdateDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu liên hệ không hợp lệ" });

            try
            {
                await _service.UpdateContactAsync(dto);
                return Json(new { success = true, message = "Đã cập nhật người liên hệ" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierUpdate)]
        public async Task<IActionResult> DeleteContact(int supplierContactId)
        {
            try
            {
                await _service.DeleteContactAsync(supplierContactId);
                return Json(new { success = true, message = "Đã xoá liên hệ" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierUpdate)]
        public async Task<IActionResult> SetPrimaryContact(int supplierContactId)
        {
            try
            {
                await _service.SetPrimaryContactAsync(supplierContactId);
                return Json(new { success = true, message = "Đã cập nhật người liên hệ chính" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===================== INGREDIENT SUPPLIER OFFERS (#111) =====================

        [HttpGet]
        public async Task<IActionResult> GetIngredientOffers(int supplierId)
        {
            if (!await CanReadSupplierAsync(supplierId))
                return SupplierScopeDenied();
            var data = await _service.GetIngredientOffersAsync(supplierId);
            return Json(new { success = true, data });
        }

        [HttpGet]
        public async Task<IActionResult> GetIngredientOffer(int id)
        {
            var data = await _service.GetIngredientOfferByIdAsync(id);
            if (data == null)
                return Json(new { success = false, message = "Không tìm thấy bảng giá gói mua" });
            if (!await CanReadSupplierAsync(data.SupplierId))
                return SupplierScopeDenied();
            return Json(new { success = true, data });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierUpdate)]
        public async Task<IActionResult> CreateIngredientOffer([FromBody] AdminIngredientSupplierSaveDTO dto)
        {
            try
            {
                var id = await _service.CreateIngredientOfferAsync(dto);
                return Json(new { success = true, message = "Thêm gói mua nguyên liệu thành công", data = id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierUpdate)]
        public async Task<IActionResult> UpdateIngredientOffer([FromBody] AdminIngredientSupplierSaveDTO dto)
        {
            try
            {
                await _service.UpdateIngredientOfferAsync(dto);
                return Json(new { success = true, message = "Cập nhật gói mua nguyên liệu thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierToggleStatus)]
        public async Task<IActionResult> ToggleIngredientOffer([FromBody] AdminIngredientSupplierToggleDTO dto)
        {
            try
            {
                await _service.ToggleIngredientOfferActiveAsync(
                    dto.IngredientSupplierId,
                    dto.Active,
                    dto.RowVersion);
                return Json(new { success = true, message = "Đã cập nhật trạng thái gói mua" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierUpdate)]
        public async Task<IActionResult> ChangeIngredientOfferPrice(
            [FromBody] AdminIngredientSupplierPriceChangeDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu đổi giá không hợp lệ" });

            try
            {
                var actor = _actorContext.Get(User);
                await _service.ChangeIngredientOfferPriceAsync(dto, actor.StaffId);
                return Json(new { success = true, message = "Đã cập nhật giá và lưu lịch sử" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPriceHistory(int ingredientSupplierId)
        {
            var offer = await _service.GetIngredientOfferByIdAsync(ingredientSupplierId);
            if (offer == null)
                return Json(new { success = false, message = "Không tìm thấy bảng giá gói mua" });
            if (!await CanReadSupplierAsync(offer.SupplierId))
                return SupplierScopeDenied();
            var data = await _service.GetIngredientOfferPriceHistoryAsync(ingredientSupplierId);
            return Json(new { success = true, data });
        }

        [HttpGet]
        public async Task<IActionResult> GetIngredientOptions()
        {
            var data = await _service.GetIngredientDropdownAsync();
            return Json(new { success = true, data });
        }

        [HttpGet]
        public async Task<IActionResult> GetContentUnitOptions()
        {
            var data = await _service.GetContentUnitDropdownAsync();
            return Json(new { success = true, data });
        }

        // ===================== STORE SCOPE =====================

        [HttpGet]
        public async Task<IActionResult> GetSupplierStores(int supplierId)
        {
            if (!await CanReadSupplierAsync(supplierId))
                return SupplierScopeDenied();

            var data = await _service.GetSupplierStoresAsync(
                supplierId,
                await ResolveStoreScopeAsync());
            return Json(new { success = true, data });
        }

        [HttpGet]
        public async Task<IActionResult> GetStoreOptions()
        {
            var data = await _service.GetStoreDropdownAsync(await ResolveStoreScopeAsync());
            return Json(new { success = true, data });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SupplierUpdate)]
        public async Task<IActionResult> SaveSupplierStore([FromBody] AdminSupplierStoreSaveDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu phạm vi cửa hàng không hợp lệ" });
            var allowedStoreIds = await ResolveStoreScopeAsync();
            if (!allowedStoreIds.Contains(dto.StoreId))
                return SupplierScopeDenied();
            if (!await CanReadSupplierAsync(dto.SupplierId))
                return SupplierScopeDenied();

            try
            {
                await _service.SaveSupplierStoreAsync(dto);
                return Json(new { success = true, message = "Đã cập nhật phạm vi cửa hàng" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private async Task<bool> CanReadSupplierAsync(int supplierId)
        {
            var supplier = await _service.GetByIdAsync(supplierId, await ResolveStoreScopeAsync());
            return supplier != null;
        }

        private async Task<IReadOnlyCollection<int>> ResolveStoreScopeAsync()
        {
            var actor = _actorContext.Get(User);
            var allowed = await _scopeAuthorization.GetAllowedStoresAsync(actor.StaffId);
            return allowed
                .Where(x => x.Active)
                .Select(x => x.StoreId)
                .Distinct()
                .ToArray();
        }

        private async Task<bool> HasPermissionAsync(int accountId, string permissionCode)
        {
            if (_permissions == null || accountId <= 0)
                return false;
            var result = await _permissions.HasPermissionAsync(accountId, permissionCode);
            return result.IsSuccess && result.Data?.Allowed == true;
        }

        private JsonResult SupplierScopeDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Json(new
            {
                success = false,
                message = "Nhà cung cấp không thuộc phạm vi cửa hàng bạn được phép xem."
            });
        }

        private JsonResult SupplierValidationError() => Json(new
        {
            success = false,
            code = "SUPPLIER_VALIDATION_FAILED",
            message = "Dữ liệu nhà cung cấp không hợp lệ.",
            errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    x => x.Key,
                    x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray())
        });

    }
}
