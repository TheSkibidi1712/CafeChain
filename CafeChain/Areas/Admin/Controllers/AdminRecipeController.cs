using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.ViewModels.Admin.Recipes;
using CafeChain.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(PermissionConstants.RecipeView)]
    public class AdminRecipeController : AdminBaseController
    {
        private readonly IAdminRecipeService _recipeService;
        private readonly IAdminRecipeQueryService _queryService;
        private readonly IRecipeBomTreeQueryService _bomTreeQuery;
        private readonly IRecipeOutputNormalizer _outputNormalizer;
        private readonly IEstimatedBomCostService _estimatedBomCost;
        private readonly IProductionReadinessService _productionReadiness;
        private readonly IAdminStoreInventoryService _storeInventoryService;
        private readonly IRecipeWhereUsedQueryService? _whereUsedQuery;

        public AdminRecipeController(
            IAdminRecipeService recipeService,
            IAdminRecipeQueryService queryService,
            IRecipeBomTreeQueryService bomTreeQuery,
            IRecipeOutputNormalizer outputNormalizer,
            IEstimatedBomCostService estimatedBomCost,
            IProductionReadinessService productionReadiness,
            IAdminStoreInventoryService storeInventoryService,
            IRecipeWhereUsedQueryService? whereUsedQuery = null)
        {
            _recipeService = recipeService;
            _queryService = queryService;
            _bomTreeQuery = bomTreeQuery;
            _outputNormalizer = outputNormalizer;
            _estimatedBomCost = estimatedBomCost;
            _productionReadiness = productionReadiness;
            _storeInventoryService = storeInventoryService;
            _whereUsedQuery = whereUsedQuery;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? type = null,
            string? search = null,
            string? status = null,
            int page = 1)
        {
            var model = await _queryService.GetIndexPageAsync(type, search, status, page);
            model.CanWrite = await HasEffectivePermissionAsync(PermissionConstants.RecipeCreate)
                || await HasEffectivePermissionAsync(PermissionConstants.RecipeUpdate)
                || await HasEffectivePermissionAsync(PermissionConstants.RecipeDelete);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> DataHealth(int page = 1)
        {
            return View(await _queryService.GetDataHealthPageAsync(page));
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
        public async Task<IActionResult> Visualize(
            int recipeId,
            int? storeId = null,
            string? returnUrl = null)
        {
            var page = await _queryService.GetVisualizePageAsync(recipeId);
            if (page == null)
                return NotFound("Không tìm thấy công thức.");

            page.CanWrite = await HasEffectivePermissionAsync(PermissionConstants.RecipeCreate)
                || await HasEffectivePermissionAsync(PermissionConstants.RecipeUpdate)
                || await HasEffectivePermissionAsync(PermissionConstants.RecipeDelete);
            page.BackUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : Url.Action(nameof(Index), "AdminRecipe", new { area = "Admin" }) ?? "/Admin/AdminRecipe";

            var accountId = GetAccountId();
            var stores = accountId > 0
                ? await _storeInventoryService.GetStoresByStaffAsync(accountId)
                : new List<InventoryStoreDTO>();
            page.Stores = stores.Select(x => new BomStoreOptionVM
            {
                StoreId = x.StoreId,
                StoreName = x.StoreName
            }).ToList();
            if (_whereUsedQuery != null)
            {
                page.WhereUsed = await _whereUsedQuery.GetCurrentAsync(
                    recipeId,
                    stores.Select(store => store.StoreId).ToArray(),
                    HttpContext.RequestAborted);
            }

            if (storeId.HasValue && storeId.Value > 0)
            {
                var selectedStore = stores.FirstOrDefault(x => x.StoreId == storeId.Value);
                if (selectedStore == null)
                    return Forbid();

                page.SelectedStoreId = selectedStore.StoreId;
                page.SelectedStoreName = selectedStore.StoreName;
                var storeEvidence = await _queryService.GetStoreEvidenceAsync(
                    page,
                    selectedStore.StoreId);
                if (storeEvidence != null)
                    page.ApplyStoreEvidence(storeEvidence);
                if (page.IsPreparedItemRecipe)
                {
                    page.Operational = await _queryService.GetOperationalDetailAsync(
                        recipeId,
                        selectedStore.StoreId);
                    var readiness = await _productionReadiness.PreviewAsync(
                        selectedStore.StoreId,
                        recipeId,
                        1m);
                    if (readiness.IsSuccess && readiness.Data != null)
                    {
                        page.Operational ??= new BomOperationalDetailVM
                        {
                            StoreId = selectedStore.StoreId,
                            StoreName = selectedStore.StoreName
                        };
                        page.Operational.Readiness = readiness.Data;
                        page.ApplyProductionReadiness(readiness.Data);
                    }
                    else
                    {
                        page.OperationalError = "Chưa thể kiểm tra điều kiện sản xuất tại chi nhánh.";
                        page.ApplyProductionReadiness(null);
                    }
                }
            }

            return View(page);
        }

        [HttpGet]
        [RequirePermission(PermissionConstants.RecipeCreate)]
        public async Task<IActionResult> Create()
        {
            var page = await _queryService.GetCreatePageAsync();
            return View(page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.RecipeCreate)]
        public async Task<IActionResult> Create([FromBody] RecipeCreateVM model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu công thức chưa hợp lệ.",
                    errorCode = BomRecipeErrorCodes.InvalidPayload,
                    errors
                });
            }

            var result = await _recipeService.CreateRecipeAsync(model);
            if (result.IsSuccess)
                return Ok(new { success = true, message = result.Message });

            var response = new
            {
                success = false,
                message = result.Message,
                errorCode = result.ErrorCode,
                errors = result.Errors
            };

            if (result.ErrorCode == BomRecipeErrorCodes.RecipeOverlap)
                return Conflict(response);

            if (result.ErrorCode == BomRecipeErrorCodes.TechnicalError)
                return StatusCode(500, response);

            return BadRequest(response);
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
                return Json(new { success = false, message = "Mã công thức không hợp lệ." });

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
        [RequirePermission(PermissionConstants.RecipeUpdate)]
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
        [RequirePermission(PermissionConstants.RecipeUpdate)]
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
        [RequirePermission(PermissionConstants.RecipeDelete)]
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

        private int GetAccountId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("AccountId")
                ?? User.FindFirst("sub");
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }

    public class RecipeOutputPreviewRequest
    {
        public int PreparedItemId { get; set; }
        public decimal OutputQuantity { get; set; }
        public int OutputUnitId { get; set; }
    }
}
