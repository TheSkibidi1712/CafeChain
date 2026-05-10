using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.ViewModels.Admin.Productions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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
        // INDEX: Redirect to Create form
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

            // Lấy 5 lệnh sơ chế mới nhất (dựa trên giao dịch nhập Bán thành phẩm)
            ViewBag.RecentHistory = _context.InventoryTransactions
                .Include(it => it.StoreInventory)
                    .ThenInclude(si => si.Recipe)
                .Where(it => it.Type == InventoryDocumentType.PRODUCTION_IN)
                .OrderByDescending(it => it.CreatedAt)
                .Take(5)
                .Select(it => new ProductionHistoryDTO
                {
                    TransactionId = it.InventoryTransactionId,
                    RecipeName = it.StoreInventory.Recipe != null ? it.StoreInventory.Recipe.Name : "BTP",
                    Quantity = it.Quantity,
                    CreatedAt = it.CreatedAt
                })
                .ToList();

            return View(new ProductionOrderVM());
        }

        // ============================================================
        // API: Tính toán nguyên liệu tiêu hao (AJAX - Chế độ Preview)
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
                estimatedOutput = batches
            });
        }

        // ============================================================
        // EXECUTE (POST AJAX): Hoàn tất mẻ nấu — Trừ/Cộng kho thực tế
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> Execute([FromBody] ProductionExecuteRequest request)
        {
            if (request == null || request.RecipeId <= 0 || request.Batches <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            // Giả định StoreId = 1 hoặc lấy từ Claim (TODO: lấy từ user session)
            int storeId = 1;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Load Recipe + BOM
                var recipe = await _context.Recipes
                    .Include(r => r.RecipeDetails)
                        .ThenInclude(rd => rd.Ingredient)
                    .Include(r => r.RecipeDetails)
                        .ThenInclude(rd => rd.Unit)
                    .FirstOrDefaultAsync(r => r.RecipeId == request.RecipeId);

                if (recipe == null)
                    return Json(new { success = false, message = "Không tìm thấy công thức." });

                var errors = new List<string>();

                // 2. DEDUCT (Trừ): Nguyên liệu thô từ StoreInventory
                foreach (var detail in recipe.RecipeDetails)
                {
                    decimal qtyToDeduct = detail.Quantity * request.Batches;
                    
                    if (detail.IngredientId.HasValue)
                    {
                        var inv = await _context.StoreInventories
                            .FirstOrDefaultAsync(si => si.StoreId == storeId && si.IngredientId == detail.IngredientId);

                        if (inv == null)
                        {
                            errors.Add($"Nguyên liệu '{detail.Ingredient?.Name}' chưa có trong kho cửa hàng.");
                            continue;
                        }

                        if (inv.AvailableQty < qtyToDeduct)
                        {
                            errors.Add($"'{detail.Ingredient?.Name}' không đủ tồn kho (Cần: {qtyToDeduct}, Còn: {inv.AvailableQty}).");
                            continue;
                        }

                        // Ghi Transaction Log
                        decimal beforeQty = inv.AvailableQty;
                        inv.AvailableQty -= qtyToDeduct;
                        inv.LastUpdated = DateTime.UtcNow;

                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            StoreInventoryId = inv.StoreInventoryId,
                            Type = InventoryDocumentType.PRODUCTION_OUT,
                            Quantity = -qtyToDeduct,
                            BeforeQty = beforeQty,
                            AfterQty = inv.AvailableQty,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    else if (detail.ChildRecipeId.HasValue)
                    {
                        // Trừ bán thành phẩm (nếu BOM lồng nhau)
                        var inv = await _context.StoreInventories
                            .FirstOrDefaultAsync(si => si.StoreId == storeId && si.RecipeId == detail.ChildRecipeId);

                        if (inv == null || inv.AvailableQty < qtyToDeduct)
                        {
                            var childName = (await _context.Recipes.FindAsync(detail.ChildRecipeId))?.Name ?? "BTP";
                            errors.Add($"'{childName}' không đủ tồn kho (Cần: {qtyToDeduct}, Còn: {inv?.AvailableQty ?? 0}).");
                            continue;
                        }

                        decimal beforeQty = inv.AvailableQty;
                        inv.AvailableQty -= qtyToDeduct;
                        inv.LastUpdated = DateTime.UtcNow;

                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            StoreInventoryId = inv.StoreInventoryId,
                            Type = InventoryDocumentType.PRODUCTION_OUT,
                            Quantity = -qtyToDeduct,
                            BeforeQty = beforeQty,
                            AfterQty = inv.AvailableQty,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Nếu có bất kỳ lỗi tồn kho nào, ROLLBACK
                if (errors.Any())
                {
                    await transaction.RollbackAsync();
                    return Json(new { 
                        success = false, 
                        message = "Không đủ nguyên liệu trong kho để thực hiện mẻ nấu.",
                        errors = errors 
                    });
                }

                // 3. ADD (Cộng): Bán thành phẩm sản xuất ra vào StoreInventory
                decimal outputQty = request.Batches; // 1 batch = 1 đơn vị BTP (có thể nhân với ExpectedYield sau)

                var outputInv = await _context.StoreInventories
                    .FirstOrDefaultAsync(si => si.StoreId == storeId && si.RecipeId == request.RecipeId);

                if (outputInv == null)
                {
                    // Tạo mới dòng tồn kho cho BTP này
                    outputInv = new Models.Stores.StoreInventory
                    {
                        StoreId = storeId,
                        RecipeId = request.RecipeId,
                        AvailableQty = 0,
                        ReservedQty = 0,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.StoreInventories.Add(outputInv);
                    await _context.SaveChangesAsync(); // Lấy StoreInventoryId
                }

                decimal beforeOutputQty = outputInv.AvailableQty;
                outputInv.AvailableQty += outputQty;
                outputInv.LastUpdated = DateTime.UtcNow;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    StoreInventoryId = outputInv.StoreInventoryId,
                    Type = InventoryDocumentType.PRODUCTION_IN,
                    Quantity = outputQty,
                    BeforeQty = beforeOutputQty,
                    AfterQty = outputInv.AvailableQty,
                    CreatedAt = DateTime.UtcNow
                });

                // 4. COMMIT
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = $"Hoàn tất mẻ nấu! Đã trừ nguyên liệu và cộng {outputQty} đơn vị '{recipe.Name}' vào kho."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
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

    // DTO cho AJAX Execute request
    public class ProductionExecuteRequest
    {
        public int RecipeId { get; set; }
        public decimal Batches { get; set; }
        public string? Notes { get; set; }
    }

    public class ProductionHistoryDTO
    {
        public int TransactionId { get; set; }
        public string RecipeName { get; set; }
        public decimal Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
