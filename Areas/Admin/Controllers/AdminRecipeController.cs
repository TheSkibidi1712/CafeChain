using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using CafeChain.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminRecipeController : AdminBaseController
    {
        private readonly IAdminRecipeService _recipeService;
        private readonly AppDbContext _context;

        public AdminRecipeController(IAdminRecipeService recipeService, AppDbContext context)
        {
            _recipeService = recipeService;
            _context = context;
        }

        // ============================================================
        // INDEX: Danh sách Công Thức (Chỉ hiển thị Active)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var recipes = await _context.Recipes
                .Include(r => r.ChildRecipeDetails) // Để xác định nó có phải Bán thành phẩm không
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Ingredient)
                .Where(r => r.Status == "Active") // Chỉ hiển thị bản Active
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
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

            if (recipe == null)
            {
                return NotFound("Không tìm thấy công thức.");
            }

            return View(recipe);
        }

        // Giới hạn tối đa 5 tầng BOM cho BuildTreeHtml
        private const int MAX_TREE_DEPTH = 5;

        // Đệ quy dựng HTML Tree cho BOM (dành cho API) — có Depth Limit
        private async Task<string> BuildTreeHtml(Models.Drinks.Recipe recipe, int currentDepth)
        {
            // FIX #8: Depth Limit cho BuildTreeHtml
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
                    // Nguyên liệu thô (Leaf node)
                    var ingName = detail.Ingredient?.Name ?? "N/A";
                    var unitName = detail.Unit?.Name ?? detail.Ingredient?.BaseUnit?.Name ?? "";
                    
                    html += $@"<li class='list-group-item d-flex justify-content-between align-items-center bg-transparent px-3 py-2 border-bottom-dashed'>
                                <div><i class='fas fa-leaf text-success me-2'></i> {ingName}</div>
                                <span class='badge bg-light text-dark border'>{detail.Quantity} {unitName}</span>
                               </li>";
                }
                else if (detail.ChildRecipeId.HasValue)
                {
                    // Bán thành phẩm (Branch node - cần đệ quy tiếp)
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
                .FirstOrDefaultAsync(r => r.RecipeId == id && r.Status == "Active");

            if (recipe == null)
            {
                TempData["ErrorMsg"] = "Không tìm thấy công thức hoặc đã bị lưu trữ.";
                return RedirectToAction(nameof(Index));
            }

            // Map sang ViewModel
            var vm = new RecipeCreateVM
            {
                RecipeType = recipe.DrinkId.HasValue ? "POS" : "SUBRECIPE",
                DrinkId = recipe.DrinkId,
                SubRecipeName = recipe.DrinkId.HasValue ? null : recipe.Name,
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

        /// <summary>
        /// Tập trung tải toàn bộ dữ liệu Dropdown cho View (DRY)
        /// </summary>
        private void PopulateViewBagData()
        {
            // Nguyên liệu thô — lấy giá vốn từ NCC chính (IngredientSupplier)
            ViewBag.Ingredients = _context.Ingredients
                .Include(i => i.BaseUnit)
                .Include(i => i.IngredientSuppliers)
                .Where(x => x.Active)
                .Select(x => new
                {
                    Id = x.IngredientId,
                    Name = x.Name,
                    // Lấy giá NCC chính, fallback giá NCC đầu tiên, fallback 0
                    BaseCost = x.IngredientSuppliers.Any(s => s.IsPrimary) 
                        ? x.IngredientSuppliers.First(s => s.IsPrimary).Price
                        : x.IngredientSuppliers.Any() 
                            ? x.IngredientSuppliers.First().Price 
                            : 0m,
                    UnitId = x.BaseUnitId,
                    UnitName = x.BaseUnit.Name
                }).ToList<object>();

            // Sub-recipes — chỉ lấy bản Active
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

            // Drinks (POS Product dropdown)
            ViewBag.Drinks = _context.Drinks
                .Where(x => x.Active)
                .Select(x => new { x.DrinkId, x.Name })
                .ToList<object>();

            // Đơn vị tính (OutputUnit dropdown)
            ViewBag.Units = _context.Units
                .Where(x => x.Active)
                .Select(x => new { x.UnitId, x.Name })
                .ToList<object>();
        }
    }
}
