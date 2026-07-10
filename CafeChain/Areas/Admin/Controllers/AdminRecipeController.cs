using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Recipes;
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
        private readonly AppDbContext _context;

        public AdminRecipeController(
            IAdminRecipeService recipeService,
            IRecipeOutputNormalizer outputNormalizer,
            AppDbContext context)
        {
            _recipeService = recipeService;
            _outputNormalizer = outputNormalizer;
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
        public IActionResult Create()
        {
            PopulateViewBagData();
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
            PopulateViewBagData();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RecipeCreateVM model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.RecipeId = id;
                PopulateViewBagData();
                return View(model);
            }

            var result = await _recipeService.UpdateRecipeAsync(id, model);
            if (result.IsSuccess)
            {
                TempData["SuccessMsg"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            ViewBag.RecipeId = id;
            PopulateViewBagData();
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

        private void PopulateViewBagData()
        {
            ViewBag.Ingredients = _context.Ingredients
                .Include(i => i.BaseUnit)
                .Include(i => i.IngredientSuppliers)
                .Where(x => x.Active)
                .Select(x => new
                {
                    Id = x.IngredientId,
                    Name = x.Name,
                    BaseCost = x.IngredientSuppliers.Any(s => s.IsPrimary)
                        ? x.IngredientSuppliers.First(s => s.IsPrimary).CurrentPrice
                        : x.IngredientSuppliers.Any()
                            ? x.IngredientSuppliers.First().CurrentPrice
                            : 0m,
                    UnitId = x.BaseUnitId,
                    UnitName = x.BaseUnit.Name
                }).ToList<object>();

            ViewBag.SubRecipes = _context.Recipes
                .Where(x => x.Active && x.Status == "Active")
                .Select(x => new
                {
                    Id = x.RecipeId,
                    Name = x.Name,
                    BaseCost = 0m,
                    UnitId = 0,
                    UnitName = "Phần"
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
