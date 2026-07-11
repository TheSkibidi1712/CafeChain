using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using CafeChain.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using CafeChain.Models.Enums.Unit;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminRecipeController : AdminBaseController
    {
        private readonly IAdminRecipeService _recipeService;
        private readonly IRecipeOutputNormalizer _outputNormalizer;
        private readonly IEstimatedBomCostService _estimatedBomCost;
        private readonly AppDbContext _context;

        public AdminRecipeController(
            IAdminRecipeService recipeService,
            IRecipeOutputNormalizer outputNormalizer,
            IEstimatedBomCostService estimatedBomCost,
            AppDbContext context)
        {
            _recipeService = recipeService;
            _outputNormalizer = outputNormalizer;
            _estimatedBomCost = estimatedBomCost;
            _context = context;
        }

        // ============================================================
        // INDEX: Danh sách Công Thức (Chỉ hiển thị Active)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var recipes = await _context.Recipes
                .Include(r => r.ChildRecipeDetails)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Ingredient)
                .Include(r => r.PreparedItem)
                .Include(r => r.OutputUnit)
                .Where(r => r.Status == "Active")
                .OrderByDescending(r => r.RecipeId)
                .ToListAsync();

            return View(recipes);
        }

        // ============================================================
        // API: Lấy cấu trúc cây (BOM Tree) cho 1 Recipe
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetRecipeTree(int id)
        {
            var recipe = await _context.Recipes
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Ingredient)
                        .ThenInclude(i => i.BaseUnit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Unit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.ChildRecipe)
                .FirstOrDefaultAsync(r => r.RecipeId == id);

            if (recipe == null) return NotFound();

            var treeHtml = await BuildTreeHtml(recipe, 0);
            return Content(treeHtml, "text/html");
        }

        // ============================================================
        // VISUALIZE: Giao diện trực quan BOM Tree
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Visualize(int recipeId)
        {
            var recipe = await _context.Recipes
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Ingredient)
                        .ThenInclude(i => i.BaseUnit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Unit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.ChildRecipe)
                .Include(r => r.PreparedItem)
                    .ThenInclude(p => p!.BaseUnit)
                .Include(r => r.OutputUnit)
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

            if (recipe == null)
            {
                return NotFound("Không tìm thấy công thức.");
            }

            return View(recipe);
        }

        private const int MAX_TREE_DEPTH = 5;

        private async Task<string> BuildTreeHtml(Models.Drinks.Recipe recipe, int currentDepth)
        {
            if (currentDepth > MAX_TREE_DEPTH)
            {
                return "<ul class='list-group'><li class='list-group-item text-danger'>" +
                       $"<i class='fas fa-exclamation-triangle me-2'></i>Đã đạt giới hạn {MAX_TREE_DEPTH} tầng hiển thị.</li></ul>";
            }

            var html = $"<ul class='list-group list-group-flush mb-0'>";
            foreach (var detail in recipe.RecipeDetails)
            {
                if (detail.IngredientId.HasValue)
                {
                    var ingName = detail.Ingredient?.Name ?? "N/A";
                    var unitName = detail.Unit?.Name ?? detail.Ingredient?.BaseUnit?.Name ?? "";

                    html += $@"<li class='list-group-item d-flex justify-content-between align-items-center bg-transparent px-3 py-2 border-bottom-dashed'>
                                <div><i class='fas fa-leaf text-success me-2'></i> {ingName}</div>
                                <span class='badge bg-light text-dark border'>{detail.Quantity} {unitName}</span>
                               </li>";
                }
                else if (detail.ChildRecipeId.HasValue)
                {
                    var childRecipe = await _context.Recipes
                        .Include(r => r.RecipeDetails).ThenInclude(rd => rd.Ingredient).ThenInclude(i => i.BaseUnit)
                        .Include(r => r.RecipeDetails).ThenInclude(rd => rd.Unit)
                        .Include(r => r.RecipeDetails).ThenInclude(rd => rd.ChildRecipe)
                        .FirstOrDefaultAsync(r => r.RecipeId == detail.ChildRecipeId.Value);

                    var childName = childRecipe?.Name ?? "N/A";
                    var unitName = detail.Unit?.Name ?? "Phần";

                    html += $@"<li class='list-group-item bg-transparent px-3 py-2 border-bottom-dashed'>
                                <div class='d-flex justify-content-between align-items-center mb-2'>
                                    <div class='fw-bold text-primary'><i class='fas fa-flask text-warning me-2'></i> {childName}</div>
                                    <span class='badge bg-warning text-dark'>{detail.Quantity} {unitName}</span>
                                </div>";

                    if (childRecipe != null)
                    {
                        var childHtml = await BuildTreeHtml(childRecipe, currentDepth + 1);
                        html += $"<div class='ms-4 ps-2 border-start border-warning'>{childHtml}</div>";
                    }
                    html += "</li>";
                }
            }
            html += "</ul>";
            return html;
        }

        // ============================================================
        // CREATE
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateViewBagDataAsync();
            return View(new RecipeCreateVM());
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
            {
                return Json(new { success = true, message = result.Message });
            }

            return Json(new { success = false, message = result.Message });
        }

        /// <summary>
        /// Backend-authoritative normalized output preview (no JS conversion factors).
        /// </summary>
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

        /// <summary>
        /// EstimatedBomCost for a saved Recipe (#117). Authoritative COMPLETE/INCOMPLETE.
        /// </summary>
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

        // ============================================================
        // EDIT: Chỉnh sửa BOM (Versioning — Insert mới, Archive cũ)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var recipe = await _context.Recipes
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Ingredient)
                        .ThenInclude(i => i.BaseUnit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.ChildRecipe)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Unit)
                .Include(r => r.PreparedItem)
                .FirstOrDefaultAsync(r => r.RecipeId == id && r.Status == "Active");

            if (recipe == null)
            {
                TempData["ErrorMsg"] = "Không tìm thấy công thức hoặc đã bị lưu trữ.";
                return RedirectToAction(nameof(Index));
            }

            bool isLegacyUnmapped =
                !recipe.DrinkId.HasValue
                && !recipe.ToppingId.HasValue
                && !recipe.PreparedItemId.HasValue;

            string recipeType = recipe.ToppingId.HasValue
                ? "TOPPING"
                : recipe.DrinkId.HasValue
                    ? "POS"
                    : "SUBRECIPE";

            var vm = new RecipeCreateVM
            {
                RecipeType = recipeType,
                DrinkId = recipe.DrinkId,
                SizeId = recipe.SizeId,
                ToppingId = recipe.ToppingId,
                PreparedItemId = recipe.PreparedItemId,
                ExpectedYield = recipe.OutputQuantity,
                OutputUnitId = recipe.OutputUnitId,
                SubRecipeName = recipe.DrinkId.HasValue || recipe.ToppingId.HasValue
                    ? null
                    : recipe.Name,
                IsLegacyUnmappedSubRecipe = isLegacyUnmapped,
                PreparedItemLocked = recipe.PreparedItemId.HasValue,
                Active = recipe.Active,
                EffectiveDate = recipe.EffectiveDate ?? System.DateTime.Today,
                Details = recipe.RecipeDetails.Select(rd => new RecipeDetailVM
                {
                    ItemCode = rd.IngredientId.HasValue
                        ? $"ING_{rd.IngredientId}"
                        : $"REC_{rd.ChildRecipeId}",
                    Quantity = rd.Quantity,
                    UnitId = rd.UnitId,
                    UnitName = rd.Unit?.Name ?? ""
                }).ToList()
            };

            ViewBag.RecipeId = id;
            ViewBag.RecipeName = recipe.Name;
            await PopulateViewBagDataAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RecipeCreateVM model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.RecipeId = id;
                await PopulateViewBagDataAsync();
                return View(model);
            }

            var result = await _recipeService.UpdateRecipeAsync(id, model);
            if (result.IsSuccess)
            {
                TempData["SuccessMsg"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            ViewBag.RecipeId = id;
            await PopulateViewBagDataAsync();
            ModelState.AddModelError("", result.Message);
            return View(model);
        }

        // ============================================================
        // DELETE: Xóa BOM (AJAX endpoint)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _recipeService.DeleteRecipeAsync(id);
            return Json(new { success = result.IsSuccess, message = result.Message });
        }

        // ============================================================
        // API: Cascaded Size Dropdown (AJAX endpoint)
        // ============================================================
        [HttpGet]
        public IActionResult GetSizesByDrink(int drinkId)
        {
            var sizes = _context.DrinkSizes
                .Include(ds => ds.Size)
                .Where(ds => ds.DrinkId == drinkId && ds.Active)
                .Select(ds => new
                {
                    ds.SizeId,
                    SizeName = ds.Size.Name,
                    ds.Price
                })
                .ToList();

            return Json(sizes);
        }

        private async Task PopulateViewBagDataAsync()
        {
            // Package-normalized base-unit costs (#117) — never map CurrentPrice as ₫/Gram.
            var ingredients = await _context.Ingredients
                .AsNoTracking()
                .Include(i => i.BaseUnit)
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .ToListAsync();

            var ingredientDtos = new List<object>();
            foreach (var x in ingredients)
            {
                var cost = await _estimatedBomCost.ResolveIngredientBaseUnitCostAsync(x.IngredientId);
                ingredientDtos.Add(new
                {
                    Id = x.IngredientId,
                    Name = x.Name,
                    // BaseCost only when COMPLETE package-normalized unit cost exists
                    BaseCost = cost.IsComplete ? cost.BaseUnitCost!.Value : 0m,
                    CostComplete = cost.IsComplete,
                    PackagePrice = cost.PackagePrice,
                    PackageQuantity = cost.PackageQuantity,
                    PackageUnitCode = cost.PackageUnitCode,
                    BaseUnitCode = cost.BaseUnitCode ?? x.BaseUnit?.UnitCode,
                    CostMessage = cost.IsComplete
                        ? null
                        : (cost.Issues.FirstOrDefault()?.Message ?? "Chưa đủ dữ liệu giá vốn"),
                    UnitId = x.BaseUnitId,
                    UnitName = x.BaseUnit?.Name ?? ""
                });
            }

            ViewBag.Ingredients = ingredientDtos;

            // Child recipes: no silent package base cost; estimate when output contract exists later via server
            ViewBag.SubRecipes = _context.Recipes
                .Where(x => x.Active && x.Status == "Active")
                .Select(x => new
                {
                    Id = x.RecipeId,
                    Name = x.Name,
                    BaseCost = 0m,
                    CostComplete = false,
                    UnitId = 0,
                    UnitName = "Phần",
                    CostMessage = "BTP: dùng EstimateBomCost / sản lượng đầu ra"
                }).ToList<object>();

            ViewBag.Drinks = _context.Drinks
                .Where(x => x.Active)
                .Select(x => new { x.DrinkId, x.Name })
                .ToList<object>();

            ViewBag.Toppings = _context.Toppings
                .Where(x => x.Active)
                .Select(x => new { x.ToppingId, x.Name })
                .ToList<object>();

            // Active PreparedItems for BTP output identity (#112 / #116)
            ViewBag.PreparedItems = _context.PreparedItems
                .AsNoTracking()
                .Where(p => p.Active)
                .OrderBy(p => p.Code)
                .Select(p => new
                {
                    p.PreparedItemId,
                    p.Code,
                    p.Name,
                    p.BaseUnitId,
                    BaseUnitCode = p.BaseUnit.UnitCode,
                    BaseUnitName = p.BaseUnit.Name
                })
                .ToList<object>();

            // Output units: active mass/volume/count, exclude commercial packaging
            var units = _context.Units
                .AsNoTracking()
                .Where(x => x.Active
                    && (x.Type == UnitType.KhoiLuong
                        || x.Type == UnitType.TheTich
                        || x.Type == UnitType.Dem))
                .OrderBy(x => x.UnitCode)
                .Select(x => new { x.UnitId, x.Name, x.UnitCode })
                .ToList()
                .Where(u => !PackageUnitCodes.IsRejectedCommercialPackaging(u.UnitCode))
                .Select(u => new { u.UnitId, u.Name, u.UnitCode })
                .ToList<object>();

            ViewBag.Units = units;
        }
    }

    public class RecipeOutputPreviewRequest
    {
        public int PreparedItemId { get; set; }
        public decimal OutputQuantity { get; set; }
        public int OutputUnitId { get; set; }
    }
}
