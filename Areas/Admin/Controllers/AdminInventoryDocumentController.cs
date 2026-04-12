using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.ViewModels.Admin.InventoryDocuments;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminInventoryDocumentController : Controller
    {
        private readonly IAdminInventoryDocumentService _service;

        public AdminInventoryDocumentController(IAdminInventoryDocumentService service)
        {
            _service = service;
        }

        // ================= INDEX =================
        public async Task<IActionResult> Index(InventoryDocumentFilterDTO filter)
        {
            if (filter.Page <= 0) filter.Page = 1;
            if (filter.PageSize <= 0) filter.PageSize = 10;

            var result = await _service.GetPagedAsync(filter);
            return View(result); // vẫn giữ view list
        }

        // ================= GET DATA CREATE =================
        [HttpGet]
        public async Task<IActionResult> GetCreateData()
        {
            var vm = await _service.GetCreateDataAsync();

            return Json(new
            {
                stores = vm.Stores.Select(x => new { x.StoreId, x.Name }),
                staffs = vm.Staffs.Select(x => new { x.StaffId, x.FullName }),
                suppliers = vm.Suppliers.Select(x => new { x.SupplierId, x.Name })
            });
        }

        // ================= CREATE =================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] InventoryDocumentVM model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return Json(new
                {
                    success = false,
                    message = "ModelState Invalid",
                    errors
                });
            }

            if (model == null || !model.Details.Any())
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
            }

            try
            {
                await _service.CreateAsync(model);

                return Json(new
                {
                    success = true,
                    message = "Tạo phiếu thành công"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.InnerException?.Message ?? ex.Message
                });
            }
        }

        // ================= GET DETAIL =================
        [HttpGet]
        public async Task<IActionResult> GetDetail(int id)
        {
            var data = await _service.GetDetailAsync(id);

            if (data == null)
                return Json(new { success = false });

            return Json(new
            {
                success = true,
                data = new
                {
                    data.Id,
                    data.Code,
                    data.StoreName,
                    data.StaffName,
                    data.SupplierName,
                    data.Type,
                    data.Status,
                    data.Date,
                    data.Note,
                    details = data.Details.Select(d => new
                    {
                        d.IngredientName,
                        d.Quantity,
                        d.UnitName,
                        d.BaseQuantity,
                        d.UnitPrice,
                        d.Note
                    })
                }
            });
        }

        // ================= NHẬP KHO =================

        [HttpGet]
        public async Task<IActionResult> GetStock(int storeId, int ingredientId)
        {
            var stock = await _service.GetStockAsync(storeId, ingredientId);
            return Json(stock);
        }

        [HttpGet]
        public async Task<IActionResult> GetUnits(int ingredientId)
        {
            var units = await _service.GetUnitsByIngredientAsync(ingredientId);

            return Json(units.Select(u => new
            {
                unitId = u.UnitId,
                name = u.Name
            }));
        }

        // =============== Lấy thông tin đơn vị và giá nhập của nguyên liệu từ nhà cung cấp ===============
        [HttpGet]
        public async Task<IActionResult> GetImportInfo(int ingredientId, int supplierId)
        {
            var data = await _service.GetImportInfoAsync(ingredientId, supplierId);

            return Json(new
            {
                unitId = data.unitId,
                unitName = data.unitName,
                price = data.price
            });
        }

        // =============== Lấy danh sách nguyên liệu mà nhà cung cấp đang cung cấp ===============
        [HttpGet]
        public async Task<IActionResult> GetIngredientSuppliersBySupplier(int supplierId)
        {
            var data = await _service.GetIngredientSuppliersBySupplierAsync(supplierId);

            return Json(data.Select(x => new
            {
                ingredientId = x.IngredientId,
                ingredient = new { x.Ingredient.Name }
            }));
        }

        // ======================== XUẤT KHO ========================
        [HttpGet]
        public async Task<IActionResult> GetIngredientsByStore(int storeId)
        {
            var data = await _service.GetIngredientsByStoreAsync(storeId);

            return Json(data.Select(x => new
            {
                ingredientId = x.IngredientId,
                name = x.Ingredient.Name,
                stock = x.AvailableQty
            }));
        }


    }
}
