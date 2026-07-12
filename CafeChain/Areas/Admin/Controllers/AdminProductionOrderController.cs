using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Data;
using CafeChain.Extensions;
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
        private readonly IProductionRunService _productionRunService;
        private readonly IProductionRunExecutionService _executionService;

        public AdminProductionOrderController(
            AppDbContext context,
            IProductionRunService productionRunService,
            IProductionRunExecutionService executionService)
        {
            _context = context;
            _productionRunService = productionRunService;
            _executionService = executionService;
        }

        [HttpGet]
        public IActionResult Index() => RedirectToAction(nameof(Create));

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            PopulateDropdowns();

            var storeId = User.GetStoreIdOrDefault();
            ViewBag.DefaultStoreId = storeId;
            ViewBag.RecentHistory = storeId > 0
                ? await _productionRunService.GetRecentAsync(storeId, 5)
                : Array.Empty<ProductionRunHistoryItemDto>();

            return View(new ProductionOrderVM());
        }

        /// <summary>Read-only BOM preview (no stock mutation).</summary>
        [HttpGet]
        public async Task<IActionResult> CalculateIngredients(int recipeId, decimal batches)
        {
            if (recipeId <= 0 || batches <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            var recipe = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.RecipeDetails).ThenInclude(rd => rd.Ingredient)
                .Include(r => r.RecipeDetails).ThenInclude(rd => rd.Unit)
                .Include(r => r.RecipeDetails).ThenInclude(rd => rd.ChildRecipe)
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

            if (recipe == null)
                return Json(new { success = false, message = "Không tìm thấy công thức" });

            var details = recipe.RecipeDetails.Select(rd =>
            {
                var baseQty = rd.Quantity;
                var totalQty = baseQty * batches;
                return new ProductionOrderDetailVM
                {
                    ItemName = rd.IngredientId.HasValue
                        ? (rd.Ingredient?.Name ?? $"ING_{rd.IngredientId}")
                        : (rd.ChildRecipe?.Name ?? $"REC_{rd.ChildRecipeId}"),
                    ItemType = rd.IngredientId.HasValue ? "Nguyên liệu" : "Bán thành phẩm",
                    BaseQuantity = baseQty,
                    TotalQuantity = totalQty,
                    UnitName = rd.Unit?.Name ?? "N/A",
                    YieldPercentage = 100m,
                    ActualQuantity = Math.Round(totalQty, 2)
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

        /// <summary>
        /// Issue #119 — confirm durable production intent. No stock mutation (114C / #120).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Execute([FromBody] ProductionExecuteRequest request)
        {
            if (request == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ.",
                    errorCode = "INVALID_REQUEST"
                });
            }

            int staffId;
            int staffHomeStoreId;
            try
            {
                staffId = User.GetStaffId();
                staffHomeStoreId = User.GetStoreId();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    errorCode = "STAFF_UNAUTHORIZED"
                });
            }

            Guid? requestKey = request.RequestKey;
            if (!requestKey.HasValue
                && !string.IsNullOrWhiteSpace(request.RequestKeyString)
                && Guid.TryParse(request.RequestKeyString, out var parsed))
            {
                requestKey = parsed;
            }

            var runCount = request.RequestedRunCount > 0
                ? request.RequestedRunCount
                : request.Batches;

            var result = await _productionRunService.CreateAndConfirmAsync(
                new CreateAndConfirmProductionRunRequest
                {
                    RequestKey = requestKey,
                    StoreId = request.StoreId,
                    RecipeId = request.RecipeId,
                    RequestedRunCount = runCount,
                    Notes = request.Notes
                },
                staffId,
                staffHomeStoreId);

            if (!result.IsSuccess || result.Data == null)
            {
                return Json(new
                {
                    success = false,
                    message = result.Message,
                    errorCode = result.ErrorCode
                });
            }

            var data = result.Data;
            return Json(new
            {
                success = true,
                productionRunId = data.ProductionRunId,
                storeId = data.StoreId,
                recipeId = data.RecipeId,
                requestedRunCount = data.RequestedRunCount,
                status = data.Status,
                confirmedAt = data.ConfirmedAt,
                wasReplay = data.WasReplay,
                stockApplied = data.StockApplied,
                messageKey = data.MessageKey,
                message = result.Message
            });
        }

        /// <summary>
        /// Issue #120 — apply stock for one CONFIRMED ProductionRun (PreparedItem writer).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteStock([FromBody] ProductionExecuteStockRequest request)
        {
            if (request == null || request.ProductionRunId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "ProductionRunId không hợp lệ.",
                    errorCode = "INVALID_REQUEST"
                });
            }

            int staffId;
            int staffHomeStoreId;
            try
            {
                staffId = User.GetStaffId();
                staffHomeStoreId = User.GetStoreId();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    errorCode = "STAFF_UNAUTHORIZED"
                });
            }

            var result = await _executionService.ExecuteAsync(
                request.ProductionRunId,
                staffId,
                staffHomeStoreId);

            if (!result.IsSuccess)
            {
                return Json(new
                {
                    success = false,
                    message = result.Message,
                    errorCode = result.ErrorCode,
                    stockApplied = false,
                    costEvidenceGaps = result.Data?.CostEvidenceGaps
                });
            }

            if (result.Data == null)
            {
                return Json(new
                {
                    success = false,
                    message = result.Message ?? "Không có dữ liệu kết quả.",
                    errorCode = result.ErrorCode ?? "EXECUTION_FAILED",
                    stockApplied = false
                });
            }

            var data = result.Data;
            return Json(new
            {
                success = true,
                wasReplay = data.WasReplay,
                productionRunId = data.ProductionRunId,
                storeId = data.StoreId,
                recipeId = data.RecipeId,
                requestedRunCount = data.RequestedRunCount,
                status = data.Status,
                stockApplied = data.StockApplied,
                completedAt = data.CompletedAt,
                normalizedOutputQuantity = data.NormalizedOutputQuantity,
                outputBaseUnitId = data.OutputBaseUnitId,
                outputStoreInventoryId = data.OutputStoreInventoryId,
                outputPreparedItemId = data.OutputPreparedItemId,
                valuationStatus = data.ValuationStatus,
                totalInputCost = data.TotalInputCost,
                outputUnitCost = data.OutputUnitCost,
                valuedAtUtc = data.ValuedAtUtc,
                movements = data.Movements,
                messageKey = data.MessageKey,
                message = result.Message
            });
        }

        private void PopulateDropdowns()
        {
            ViewBag.SubRecipes = _context.Recipes
                .AsNoTracking()
                .Where(r => r.Active)
                .Select(r => new { r.RecipeId, r.Name })
                .ToList<object>();
        }
    }

    public class ProductionExecuteStockRequest
    {
        public int ProductionRunId { get; set; }
    }

    /// <summary>POST body for production intent confirm (Issue #119).</summary>
    public class ProductionExecuteRequest
    {
        public Guid? RequestKey { get; set; }

        /// <summary>Optional string form of RequestKey for clients that send UUID as string.</summary>
        public string? RequestKeyString { get; set; }

        public int? StoreId { get; set; }
        public int RecipeId { get; set; }
        public decimal RequestedRunCount { get; set; }

        /// <summary>Legacy field alias for RequestedRunCount.</summary>
        public decimal Batches { get; set; }

        public string? Notes { get; set; }
    }
}
