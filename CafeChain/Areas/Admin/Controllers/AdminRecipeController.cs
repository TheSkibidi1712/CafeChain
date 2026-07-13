using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminRecipeController : AdminBaseController
    {
        private readonly IAdminRecipeService _recipeService;
        private readonly IAdminRecipeQueryService _queryService;
        private readonly IRecipeBomTreeQueryService _bomTreeQuery;
        private readonly IRecipeOutputNormalizer _outputNormalizer;
        private readonly IEstimatedBomCostService _estimatedBomCost;

        public AdminRecipeController(
            IAdminRecipeService recipeService,
            IAdminRecipeQueryService queryService,
            IRecipeBomTreeQueryService bomTreeQuery,
            IRecipeOutputNormalizer outputNormalizer,
            IEstimatedBomCostService estimatedBomCost)
        {
            _recipeService = recipeService;
            _queryService = queryService;
            _bomTreeQuery = bomTreeQuery;
            _outputNormalizer = outputNormalizer;
            _estimatedBomCost = estimatedBomCost;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? type = null)
        {
            var page = await _queryService.GetIndexPageAsync(type);
            return View(page);
        }

        [HttpGet]
        public async Task<IActionResult> DataHealth()
        {
            return View(await _queryService.GetDataHealthPageAsync());
        }

        [HttpGet]
        public async Task<IActionResult> GetRecipeTree(int id)
        {
            var tree = await _bomTreeQuery.BuildTreeAsync(id);
            if (tree.RootNotFound)
                return NotFound();

            return PartialView("Partials/_BomTree", tree);
        }

        [HttpGet]
        public async Task<IActionResult> Visualize(int recipeId)
        {
            var page = await _queryService.GetVisualizePageAsync(recipeId);
            if (page == null)
                return NotFound("Không tìm thấy công thức.");

            return View(page);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var page = await _queryService.GetCreatePageAsync();
            return View(page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] RecipeCreateVM model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = "Dữ liệu không hợp lệ.", errors = errors });
            }

            var result = await _recipeService.CreateRecipeAsync(model);
            if (result.IsSuccess)
                return Json(new { success = true, message = result.Message });

            return Json(new { success = false, message = result.Message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewNormalizedOutput(
            [FromBody] RecipeOutputPreviewRequest request)
        {
            if (request == null
                || request.PreparedItemId <= 0
                || request.OutputQuantity <= 0
                || request.OutputUnitId <= 0)
            {
                return Json(new { success = false, message = "Thiếu BTP, sản lượng hoặc đơn vị đầu ra." });
            }

            var result = await _outputNormalizer.NormalizeAsync(
                request.PreparedItemId,
                request.OutputQuantity,
                request.OutputUnitId);

            if (!result.IsSuccess)
                return Json(new { success = false, message = result.Message });

            var d = result.Data;
            return Json(new
            {
                success = true,
                preview = d.PreviewText,
                outputQuantity = d.OutputQuantity,
                outputUnitCode = d.OutputUnitCode,
                normalizedQuantityInBase = d.NormalizedQuantityInBase,
                baseUnitCode = d.BaseUnitCode,
                preparedItemCode = d.PreparedItemCode,
                preparedItemName = d.PreparedItemName
            });
        }

        [HttpGet]
        public async Task<IActionResult> EstimateBomCost(int recipeId)
        {
            if (recipeId <= 0)
                return Json(new { success = false, message = "RecipeId không hợp lệ." });

            var result = await _estimatedBomCost.CalculateRecipeEstimatedCostAsync(recipeId);
            return Json(new
            {
                success = true,
                status = result.Status.ToString(),
                isComplete = result.IsComplete,
                totalCost = result.TotalCost,
                label = result.IsComplete
                    ? "Giá vốn ước tính"
                    : "Giá vốn ước tính — chưa đủ dữ liệu",
                issues = result.Issues.Select(i => new { i.Code, i.Message, i.IngredientId, i.RecipeId, i.RecipeDetailId }),
                lines = result.Lines.Select(l => new
                {
                    l.RecipeDetailId,
                    componentKind = l.ComponentKind.ToString(),
                    l.IngredientId,
                    l.ChildRecipeId,
                    l.PreparedItemId,
                    l.Quantity,
                    l.UnitId,
                    l.UnitCode,
                    l.QuantityInBase,
                    l.BaseUnitCode,
                    l.BaseUnitCost,
                    l.LineCost,
                    status = l.Status.ToString(),
                    l.PackagePrice,
                    l.PackageQuantity,
                    l.PackageUnitCode,
                    l.DisplaySummary
                })
            });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var page = await _queryService.GetEditPageAsync(id);
            if (page == null)
            {
                TempData["ErrorMsg"] = "Không tìm thấy công thức hoặc đã bị lưu trữ.";
                return RedirectToAction(nameof(Index));
            }

            return View(page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RecipeCreateVM model)
        {
            if (!ModelState.IsValid)
            {
                var page = await _queryService.GetEditPageAsync(id) ?? new AdminRecipeFormPageVM
                {
                    Form = model,
                    Options = await _queryService.GetFormOptionsAsync(),
                    SourceRecipeId = id,
                    IsEdit = true
                };
                page.Form = model;
                return View(page);
            }

            var result = await _recipeService.UpdateRecipeAsync(id, model);
            if (result.IsSuccess)
            {
                TempData["SuccessMsg"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            var failPage = await _queryService.GetEditPageAsync(id) ?? new AdminRecipeFormPageVM
            {
                Form = model,
                Options = await _queryService.GetFormOptionsAsync(),
                SourceRecipeId = id,
                IsEdit = true
            };
            failPage.Form = model;
            ModelState.AddModelError("", result.Message);
            return View(failPage);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _recipeService.DeleteRecipeAsync(id);
            return Json(new { success = result.IsSuccess, message = result.Message });
        }

        [HttpGet]
        public async Task<IActionResult> GetSizesByDrink(int drinkId)
        {
            var sizes = await _queryService.GetSizesByDrinkAsync(drinkId);
            return Json(sizes.Select(s => new
            {
                s.SizeId,
                s.SizeName,
                s.Price
            }));
        }
    }

    public class RecipeOutputPreviewRequest
    {
        public int PreparedItemId { get; set; }
        public decimal OutputQuantity { get; set; }
        public int OutputUnitId { get; set; }
    }
}
