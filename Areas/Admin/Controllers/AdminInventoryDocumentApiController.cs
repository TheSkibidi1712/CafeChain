using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.ViewModels.Admin.InventoryDocuments;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/admin/inventory-documents")]
    [ApiController]
    public class AdminInventoryDocumentApiController : Controller
    {
        private readonly IAdminInventoryDocumentService _service;

        public AdminInventoryDocumentApiController(IAdminInventoryDocumentService service)
        {
            _service = service;
        }

        // ================= GET CREATE DATA =================
        [HttpGet("create-data")]
        public async Task<IActionResult> GetCreateData()
        {
            var vm = await _service.GetCreateDataAsync();

            return Ok(new
            {
                stores = vm.Stores.Select(x => new { x.StoreId, x.Name }),
                suppliers = vm.Suppliers.Select(x => new { x.SupplierId, x.Name })
            });
        }

        // ================= CREATE (IMPORT / EXPORT / WASTE / STOCKTAKE) =================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] InventoryDocumentVM model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "ModelState Invalid",
                    errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                });
            }

            if (model == null || model.Details == null || !model.Details.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ"
                });
            }

            try
            {
                // 🔥 STOCKTAKE cũng đi qua đây (Type = STOCK_TAKE)
                await _service.CreateAsync(model);

                return Ok(new
                {
                    success = true,
                    message = "Tạo phiếu thành công",
                    type = model.Type
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.InnerException?.Message ?? ex.Message
                });
            }
        }

        // ================= GET DETAIL =================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetail(int id)
        {
            var data = await _service.GetDetailAsync(id);

            if (data == null)
                return NotFound(new { success = false });

            return Ok(new
            {
                success = true,
                data = new
                {
                    data.Id,
                    data.Code,
                    data.StoreName,
                    data.StaffName,
                    data.SupplierName,
                    data.PartnerName,
                    data.Purpose,
                    data.Type,
                    data.Status,
                    data.Date,
                    data.Note,
                    details = data.Details.Select(d => new
                    {
                        d.IngredientCode,
                        d.IngredientName,
                        d.BaseUnitName,
                        d.Quantity,
                        d.UnitName,
                        d.BaseQuantity,
                        d.UnitPrice,
                        d.Note
                    })
                }
            });
        }

        // ================= STOCK =================
        [HttpGet("stock")]
        public async Task<IActionResult> GetStock(int storeId, int ingredientId)
        {
            var stock = await _service.GetStockAsync(storeId, ingredientId);

            return Ok(new
            {
                storeId,
                ingredientId,
                stock
            });
        }

        // ================= UNITS =================
        [HttpGet("units")]
        public async Task<IActionResult> GetUnits(int ingredientId)
        {
            var units = await _service.GetUnitsByIngredientAsync(ingredientId);

            return Ok(units.Select(u => new
            {
                unitId = u.UnitId,
                name = u.Name
            }));
        }

        // ================= IMPORT INFO =================
        [HttpGet("import-info")]
        public async Task<IActionResult> GetImportInfo(int ingredientId, int supplierId)
        {
            var data = await _service.GetImportInfoAsync(ingredientId, supplierId);

            return Ok(new
            {
                unitId = data.UnitId,
                unitName = data.UnitName,
                price = data.Price
            });
        }

        // ================= INGREDIENT SUPPLIER =================
        [HttpGet("ingredient-suppliers")]
        public async Task<IActionResult> GetIngredientSuppliersBySupplier(int supplierId)
        {
            var data = await _service.GetIngredientSuppliersBySupplierAsync(supplierId);

            return Ok(data.Select(x => new
            {
                ingredientId = x.IngredientId,
                ingredientName = x.Ingredient.Name // ✅ FIX
            }));
        }

        // ================= STORE INVENTORIES (DÙNG CHO EXPORT + STOCKTAKE UI) =================
        [HttpGet("store-inventories")]
        public async Task<IActionResult> GetStoreInventories(int storeId, bool onlyAvailable = false)
        {
            var data = await _service.GetStoreInventoriesAsync(storeId, onlyAvailable);

            return Ok(data.Select(x => new
            {
                ingredientId = x.IngredientId,
                name = x.Ingredient.Name,
                stock = x.AvailableQty,
                baseUnitId = x.Ingredient.BaseUnitId,
                unitName = x.Ingredient.BaseUnit.Name
            }));
        }
    }
}