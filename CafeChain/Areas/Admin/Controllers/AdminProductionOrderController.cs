using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Extensions;
using CafeChain.ViewModels.Admin.Productions;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminProductionOrderController : AdminBaseController
    {
        private readonly IProductionRunService _productionRunService;
        private readonly IProductionRunExecutionService _executionService;
        private readonly IProductionReadinessService _readinessService;
        private readonly IAdminStoreInventoryService _storeInventoryService;

        public AdminProductionOrderController(
            IProductionRunService productionRunService,
            IProductionRunExecutionService executionService,
            IProductionReadinessService readinessService,
            IAdminStoreInventoryService storeInventoryService)
        {
            _productionRunService = productionRunService;
            _executionService = executionService;
            _readinessService = readinessService;
            _storeInventoryService = storeInventoryService;
        }

        [HttpGet]
        public IActionResult Index() => RedirectToAction(nameof(Create));

        [HttpGet]
        public async Task<IActionResult> Create(int storeId = 0, int recipeId = 0)
        {
            var accountId = GetAccountId();
            var stores = accountId > 0
                ? await _storeInventoryService.GetStoresByStaffAsync(accountId)
                : new List<InventoryStoreDTO>();
            var homeStoreId = User.GetStoreIdOrDefault();
            var selectedStoreId = stores.Any(x => x.StoreId == storeId)
                ? storeId
                : stores.Any(x => x.StoreId == homeStoreId)
                    ? homeStoreId
                    : stores.FirstOrDefault()?.StoreId ?? 0;
            var recipeOptions = (await _readinessService.GetRecipeOptionsAsync()).ToList();
            var selectedRecipeId = recipeOptions.Any(x => x.RecipeId == recipeId && x.Selectable)
                ? recipeId
                : 0;
            var model = new ProductionOrderVM
            {
                StoreId = selectedStoreId,
                TargetRecipeId = selectedRecipeId,
                Stores = stores,
                RecipeOptions = recipeOptions
            };

            ViewBag.RecentHistory = selectedStoreId > 0
                ? await _productionRunService.GetRecentAsync(selectedStoreId, 5)
                : Array.Empty<ProductionRunHistoryItemDto>();

            return View(model);
        }

        /// <summary>Read-only BOM preview (no stock mutation).</summary>
        [HttpGet]
        public async Task<IActionResult> CalculateIngredients(int storeId, int recipeId, decimal batches)
        {
            if (storeId <= 0 || recipeId <= 0 || batches <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            var accountId = GetAccountId();
            var accessibleStores = accountId > 0
                ? await _storeInventoryService.GetStoresByStaffAsync(accountId)
                : new List<InventoryStoreDTO>();
            if (!accessibleStores.Any(x => x.StoreId == storeId))
            {
                return Json(new
                {
                    success = false,
                    message = "Bạn không có quyền xem readiness của cửa hàng này."
                });
            }

            var result = await _readinessService.PreviewAsync(storeId, recipeId, batches);
            if (!result.IsSuccess || result.Data == null)
                return Json(new { success = false, message = result.Message, errorCode = result.ErrorCode });

            return Json(new
            {
                success = true,
                data = result.Data
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
                    message = "Mã lệnh sơ chế không hợp lệ.",
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

        private int GetAccountId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("AccountId")
                ?? User.FindFirst("sub");
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
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
