using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SupplierReadRoles)]
    public class AdminSupplierController : Controller
    {
        private const string SupplierReadRoles =
            RoleConstants.BusinessOwner + "," + RoleConstants.AccountantWarehouse + "," +
            RoleConstants.AreaManager + "," + RoleConstants.StoreManager;
        private const string SupplierMutationRoles =
            RoleConstants.BusinessOwner + "," + RoleConstants.AccountantWarehouse;
        private readonly IAdminSupplierService _service;
        private readonly IAdminActorContextAccessor _actorContext;
        private readonly IScopeAuthorizationService _scopeAuthorization;

        public AdminSupplierController(
            IAdminSupplierService service,
            IAdminActorContextAccessor actorContext,
            IScopeAuthorizationService scopeAuthorization)
        {
            _service = service;
            _actorContext = actorContext;
            _scopeAuthorization = scopeAuthorization;
        }

        // ===== INDEX =====
        public async Task<IActionResult> Index(string? search, bool? status)
        {
            var storeScope = await ResolveStoreScopeAsync();
            var data = await _service.GetAllAsync(search, status, storeScope);
            ViewBag.CanMutateSupplier = CanMutateSupplier();
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
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> Create([FromBody] AdminSupplierCreateDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            try
            {
                var id = await _service.CreateAsync(dto);
                return Json(new { success = true, message = "Thêm nhà cung cấp thành công", data = id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== UPDATE =====
        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> Update([FromBody] AdminSupplierUpdateDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            try
            {
                await _service.UpdateAsync(dto);
                return Json(new { success = true, message = "Cập nhật thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== TOGGLE STATUS =====
        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
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
        [Authorize(Roles = SupplierMutationRoles)]
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
        [Authorize(Roles = SupplierMutationRoles)]
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
        [Authorize(Roles = SupplierMutationRoles)]
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
        [Authorize(Roles = SupplierMutationRoles)]
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
        [Authorize(Roles = SupplierMutationRoles)]
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
        [Authorize(Roles = SupplierMutationRoles)]
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
        [Authorize(Roles = SupplierMutationRoles)]
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
        [Authorize(Roles = SupplierMutationRoles)]
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
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> ToggleIngredientOffer([FromBody] AdminIngredientSupplierToggleDTO dto)
        {
            try
            {
                await _service.ToggleIngredientOfferActiveAsync(dto.IngredientSupplierId, dto.Active);
                return Json(new { success = true, message = "Đã cập nhật trạng thái gói mua" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
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
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> SaveSupplierStore([FromBody] AdminSupplierStoreSaveDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu phạm vi cửa hàng không hợp lệ" });

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

        private bool CanMutateSupplier() =>
            User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AccountantWarehouse);

        private async Task<bool> CanReadSupplierAsync(int supplierId)
        {
            var supplier = await _service.GetByIdAsync(supplierId, await ResolveStoreScopeAsync());
            return supplier != null;
        }

        private async Task<IReadOnlyCollection<int>?> ResolveStoreScopeAsync()
        {
            if (CanMutateSupplier())
                return null;

            var actor = _actorContext.Get(User);
            var allowed = await _scopeAuthorization.GetAllowedStoresAsync(actor.StaffId);
            var ids = allowed.Select(x => x.StoreId).ToHashSet();

            if (User.IsInRole(RoleConstants.StoreManager) && actor.StoreId > 0)
                ids.Add(actor.StoreId);

            return ids.ToList();
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

    }
}
