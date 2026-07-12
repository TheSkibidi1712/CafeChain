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
            var data = await _service.GetIndexAsync(search, status);
            return View(new AdminUnitConversionIndexPageVM
            {
                Data = data,
                CanWrite = true,
                Search = search,
                Status = status
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            return View(await BuildFormPageAsync(new UnitConversionVM(), isEdit: false));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UnitConversionVM model)
        {
            if (!ModelState.IsValid)
                return View(await BuildFormPageAsync(model, isEdit: false));

            var request = ToRequest(model);
            var result = await _service.CreateAsync(request);
            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Message ?? "Không lưu được quy đổi.");
                var page = await BuildFormPageAsync(model, isEdit: false);
                page.EvalErrorCode = result.ErrorCode;
                page.Eval = await _service.EvaluateAsync(request);
                return View(page);
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
            var page = await BuildFormPageAsync(vm, isEdit: true);
            page.Eval = await _service.EvaluateAsync(data);
            return View(page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UnitConversionVM model)
        {
            if (!ModelState.IsValid)
                return View(await BuildFormPageAsync(model, isEdit: true));

            var request = ToRequest(model);
            var result = await _service.UpdateAsync(request);
            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Message ?? "Không cập nhật được.");
                var page = await BuildFormPageAsync(model, isEdit: true);
                page.EvalErrorCode = result.ErrorCode;
                page.Eval = await _service.EvaluateAsync(request);
                return View(page);
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

        private async Task<AdminUnitConversionFormPageVM> BuildFormPageAsync(UnitConversionVM form, bool isEdit)
        {
            return new AdminUnitConversionFormPageVM
            {
                Form = form,
                Ingredients = await _service.GetIngredientOptionsAsync(null),
                Units = await _service.GetUnitOptionsAsync(),
                PhysicalStandards = _service.GetPhysicalStandards(),
                IsEdit = isEdit
            };
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
