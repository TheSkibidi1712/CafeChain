using CafeChain.Data;
using CafeChain.ViewModels.Admin.Productions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminProductionOrderController : AdminBaseController
    {
        private readonly AppDbContext _context;

        public AdminProductionOrderController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // INDEX: Placeholder (chưa có DB table)
        // ============================================================
        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Create));
        }

        // ============================================================
        // CREATE (GET): Form tạo Lệnh Sơ Chế
        // ============================================================
        [HttpGet]
        public IActionResult Create()
        {
            PopulateDropdowns();
            return View(new ProductionOrderVM());
        }

        // ============================================================
        // API: Tính toán nguyên liệu tiêu hao (AJAX)
        // Input: RecipeId + PlannedBatches
        // Output: List<ProductionOrderDetailVM>
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> CalculateIngredients(int recipeId, decimal batches)
        {
            if (recipeId <= 0 || batches <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            var recipe = await _context.Recipes
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Ingredient)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Unit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.ChildRecipe)
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

            if (recipe == null)
                return Json(new { success = false, message = "Không tìm thấy công thức" });

            var details = recipe.RecipeDetails.Select(rd =>
            {
                var baseQty = rd.Quantity;
                var totalQty = baseQty * batches;
                // Tạm hardcode yield 100% vì RecipeDetail entity chưa có YieldPercentage column
                var yieldPct = 100m;
                var actualQty = yieldPct > 0 ? totalQty / (yieldPct / 100m) : totalQty;

                return new ProductionOrderDetailVM
                {
                    ItemName = rd.IngredientId.HasValue
                        ? (rd.Ingredient?.Name ?? $"ING_{rd.IngredientId}")
                        : (rd.ChildRecipe?.Name ?? $"REC_{rd.ChildRecipeId}"),
                    ItemType = rd.IngredientId.HasValue ? "Nguyên liệu" : "Bán thành phẩm",
                    BaseQuantity = baseQty,
                    TotalQuantity = totalQty,
                    UnitName = rd.Unit?.Name ?? "N/A",
                    YieldPercentage = yieldPct,
                    ActualQuantity = Math.Round(actualQty, 2)
                };
            }).ToList();

            return Json(new
            {
                success = true,
                recipeName = recipe.Name,
                data = details,
                estimatedOutput = batches // Placeholder: sau này tính từ ExpectedYield
            });
        }

        // ============================================================
        // CREATE (POST): Ghi log Lệnh Sơ Chế
        // NOTE: Inventory FROZEN → Chỉ hiển thị SweetAlert confirm
        // Chưa INSERT vào DB (chưa có ProductionOrder table)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(ProductionOrderVM model)
        {
            // TODO (Phase 2): Sau khi có ProductionOrder table
            // - INSERT ProductionOrder header
            // - INSERT ProductionOrderDetail foreach ingredient
            // - Trigger inventory deduction via service

            TempData["SuccessMsg"] = $"Đã ghi nhận lệnh sơ chế (Chế độ Preview — chưa trừ kho).";
            return RedirectToAction(nameof(Create));
        }

        private void PopulateDropdowns()
        {
            // Chỉ lấy Sub-recipes (Active) cho dropdown
            ViewBag.SubRecipes = _context.Recipes
                .Where(r => r.Active)
                .Select(r => new { r.RecipeId, r.Name })
                .ToList<object>();
        }
    }
}
