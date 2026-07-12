using CafeChain.Application.DTOs.Admin.UnitConversions;
using CafeChain.Application.Interfaces.Admin.UnitConversions;
using CafeChain.ViewModels.Admin.UnitConversions;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// #127 Admin unit conversion UX — physical / measuring / package separation.
    /// Authorization: RequireAdminPanelAccess via AdminBaseController (unchanged).
    /// </summary>
    [Area("Admin")]
    public class AdminUnitConversionController : AdminBaseController
    {
        private readonly IAdminUnitConversionService _service;

        public AdminUnitConversionController(IAdminUnitConversionService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search = null, string? status = null)
        {
            var model = await _service.GetIndexAsync(search, status);
            ViewBag.CanWrite = true; // same as before: any Admin panel role can mutate
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateFormLookupsAsync();
            return View(new UnitConversionVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UnitConversionVM model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateFormLookupsAsync();
                return View(model);
            }

            var request = ToRequest(model);
            var result = await _service.CreateAsync(request);
            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Message ?? "Không lưu được quy đổi.");
                ViewBag.EvalErrorCode = result.ErrorCode;
                await PopulateFormLookupsAsync();
                // Re-run evaluate for panel
                ViewBag.Eval = await _service.EvaluateAsync(request);
                return View(model);
            }

            TempData["SuccessMsg"] = "Đã tạo quy đổi đo lường theo nguyên liệu.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var data = await _service.GetForEditAsync(id);
            if (data == null) return NotFound();

            var vm = new UnitConversionVM
            {
                UnitConversionId = data.UnitConversionId ?? 0,
                IngredientId = data.IngredientId,
                FromUnitId = data.FromUnitId,
                FromQuantity = data.FromQuantity,
                ToUnitId = data.ToUnitId,
                ToQuantity = data.ToQuantity
            };
            await PopulateFormLookupsAsync();
            ViewBag.Eval = await _service.EvaluateAsync(data);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UnitConversionVM model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateFormLookupsAsync();
                return View(model);
            }

            var request = ToRequest(model);
            var result = await _service.UpdateAsync(request);
            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Message ?? "Không cập nhật được.");
                ViewBag.EvalErrorCode = result.ErrorCode;
                await PopulateFormLookupsAsync();
                ViewBag.Eval = await _service.EvaluateAsync(request);
                return View(model);
            }

            TempData["SuccessMsg"] = result.Message ?? "Cập nhật thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _service.DeleteAsync(id);
            TempData[result.IsSuccess ? "SuccessMsg" : "ErrorMsg"] =
                result.Message ?? (result.IsSuccess ? "Đã xóa." : "Không xóa được.");
            return RedirectToAction(nameof(Index));
        }

        /// <summary>#127 Server re-evaluation for form preview (JSON).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Evaluate([FromBody] AdminUnitConversionEvaluateRequest request)
        {
            if (request == null)
                return Json(new { success = false, message = "Thiếu dữ liệu." });

            var eval = await _service.EvaluateAsync(request);
            return Json(new
            {
                success = eval.IsValid,
                eval.ErrorCode,
                eval.Message,
                eval.IsPhysicalStandard,
                eval.HasPhysicalConflict,
                eval.PhysicalExpectedFactor,
                eval.IsCrossDimension,
                eval.IsMassVolumeCross,
                eval.HasPackageConflict,
                eval.RequiresPackageAcknowledgement,
                eval.PrimaryPackageQuantity,
                eval.PrimaryPackageUnitCode,
                eval.PrimaryPackageUnitName,
                eval.PrimaryPackagePrice,
                eval.ProposedPackageLikeQuantity,
                eval.PrimarySupplierId,
                eval.PrimarySupplierName,
                eval.Factor,
                eval.ReverseFactor,
                eval.FromUnitCode,
                eval.ToUnitCode,
                eval.FromDimension,
                eval.ToDimension,
                eval.FromIsPackagingCount,
                eval.ToIsPackagingCount,
                eval.Warnings,
                eval.Codes
            });
        }

        [HttpGet]
        public async Task<IActionResult> SearchIngredients(string? q = null)
        {
            var data = await _service.GetIngredientOptionsAsync(q);
            return Json(new { success = true, data });
        }

        private async Task PopulateFormLookupsAsync()
        {
            ViewBag.Ingredients = await _service.GetIngredientOptionsAsync(null);
            ViewBag.Units = await _service.GetUnitOptionsAsync();
            ViewBag.PhysicalStandards = _service.GetPhysicalStandards();
        }

        private static AdminUnitConversionEvaluateRequest ToRequest(UnitConversionVM model) => new()
        {
            UnitConversionId = model.UnitConversionId > 0 ? model.UnitConversionId : null,
            IngredientId = model.IngredientId,
            FromUnitId = model.FromUnitId,
            FromQuantity = model.FromQuantity,
            ToUnitId = model.ToUnitId,
            ToQuantity = model.ToQuantity,
            PackageConflictAcknowledged = model.PackageConflictAcknowledged
        };
    }
}
