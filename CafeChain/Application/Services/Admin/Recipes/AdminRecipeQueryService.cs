using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.Interfaces.Admin.PreparedItems;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Recipes
{
    /// <summary>
    /// #129 Read/orchestration for Recipe Admin pages. Does not own write/create domain rules.
    /// Cost/normalize use existing services (no algorithm rewrite).
    /// </summary>
    public sealed class AdminRecipeQueryService : IAdminRecipeQueryService
    {
        private readonly AppDbContext _context;
        private readonly IRecipeOutputNormalizer _outputNormalizer;
        private readonly IEstimatedBomCostService _estimatedBomCost;
        private readonly IAdminPreparedItemService _preparedItemService;
        private readonly IRecipeBomTreeQueryService _bomTree;
        private readonly IBomDataHealthEvaluator _healthEvaluator;

        public AdminRecipeQueryService(
            AppDbContext context,
            IRecipeOutputNormalizer outputNormalizer,
            IEstimatedBomCostService estimatedBomCost,
            IAdminPreparedItemService preparedItemService,
            IRecipeBomTreeQueryService bomTree,
            IBomDataHealthEvaluator healthEvaluator)
        {
            _context = context;
            _outputNormalizer = outputNormalizer;
            _estimatedBomCost = estimatedBomCost;
            _preparedItemService = preparedItemService;
            _bomTree = bomTree;
            _healthEvaluator = healthEvaluator;
        }

        public async Task<BomDataHealthPageVM> GetDataHealthPageAsync()
        {
            var recipes = await _context.Recipes
                .AsNoTracking()
                .AsSplitQuery()
                .Include(r => r.PreparedItem)
                    .ThenInclude(p => p!.BaseUnit)
                .Include(r => r.OutputUnit)
                .Include(r => r.Size)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.Unit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.Ingredient)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.ChildRecipe)
                        .ThenInclude(c => c!.PreparedItem)
                            .ThenInclude(p => p!.BaseUnit)
                .OrderByDescending(r => r.RecipeId)
                .ToListAsync();

            var costResults = await _estimatedBomCost.CalculateRecipesEstimatedCostAsync(
                recipes.Select(x => x.RecipeId));
            var page = new BomDataHealthPageVM();

            foreach (var recipe in recipes)
            {
                var typeKey = ResolveRecipeTypeKey(recipe);
                var configuration = _healthEvaluator.EvaluateConfiguration(recipe);
                var costing = costResults.TryGetValue(recipe.RecipeId, out var costResult)
                    ? _healthEvaluator.EvaluateCosting(costResult)
                    : new BomHealthStatusVM
                    {
                        Code = BomCostingHealthCodes.Indeterminate,
                        Label = "Không xác định được giá vốn",
                        Reasons = new List<BomHealthReasonVM>
                        {
                            new()
                            {
                                Code = BomCostingHealthCodes.Indeterminate,
                                GroupCode = BomCostingHealthCodes.Indeterminate,
                                Message = "Không nhận được kết quả tính giá vốn cho công thức.",
                                CtaLabel = "Kiểm tra BOM",
                                CtaController = "AdminRecipe",
                                CtaAction = "Edit",
                                CtaId = recipe.RecipeId
                            }
                        }
                    };

                page.Items.Add(new BomDataHealthRowVM
                {
                    RecipeId = recipe.RecipeId,
                    RecipeCode = recipe.RecipeCode ?? "",
                    Name = recipe.Name ?? "",
                    TypeLabel = typeKey switch
                    {
                        "POS" => "Món bán",
                        "TOPPING" => "Topping",
                        "SUBRECIPE" => "Bán thành phẩm",
                        _ => "Khác"
                    },
                    IdentityDisplay = BuildIdentityDisplay(recipe, typeKey),
                    Configuration = configuration,
                    Costing = costing,
                    EstimatedCost = costResult?.IsComplete == true ? costResult.TotalCost : null
                });
            }

            page.CompleteCount = page.Items.Count(x => x.Configuration.IsComplete && x.Costing.IsComplete);
            page.MissingQuoteCount = page.Items.Count(x => x.Costing.Reasons.Any(r =>
                r.GroupCode == BomCostingHealthCodes.MissingQuote));
            page.MissingConversionCount = page.Items.Count(x => x.Costing.Reasons.Any(r =>
                r.GroupCode == BomCostingHealthCodes.MissingConversion));
            page.MissingOutputCount = page.Items.Count(x => x.Configuration.Reasons.Any(r =>
                r.Code == BomConfigurationHealthCodes.MissingOutputIdentity
                || r.Code == BomConfigurationHealthCodes.MissingOutputQuantity
                || r.Code == BomConfigurationHealthCodes.MissingOutputUnit));
            page.MappingErrorCount = page.Items.Count(x => x.Configuration.Reasons.Any(r =>
                r.Code == BomConfigurationHealthCodes.InvalidPreparedItemMapping));

            return page;
        }

        public async Task<AdminRecipeListPageVM> GetIndexPageAsync(
            string? typeFilter = null,
            string? search = null,
            string? status = null,
            int page = 1,
            int pageSize = 15)
        {
            var normalizedType = NormalizeRecipeTypeFilter(typeFilter);
            var normalizedStatus = NormalizeStatusFilter(status);
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Recipes.AsNoTracking();
            query = normalizedType switch
            {
                "TOPPING" => query.Where(r => r.ToppingId.HasValue),
                "SUBRECIPE" => query.Where(r => !r.DrinkId.HasValue && !r.ToppingId.HasValue),
                _ => query.Where(r => r.DrinkId.HasValue)
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                query = query.Where(r =>
                    r.Name.Contains(keyword)
                    || r.RecipeCode.Contains(keyword)
                    || (r.PreparedItem != null
                        && (r.PreparedItem.Name.Contains(keyword)
                            || r.PreparedItem.Code.Contains(keyword))));
            }

            if (normalizedStatus == "ACTIVE")
            {
                query = query.Where(r => r.Active && r.Status == "Active");
            }
            else if (normalizedStatus == "INACTIVE")
            {
                query = query.Where(r => !r.Active || r.Status != "Active");
            }

            var total = await query.CountAsync();
            var recipes = await query
                .AsSplitQuery()
                .Include(r => r.PreparedItem)
                    .ThenInclude(p => p!.BaseUnit)
                .Include(r => r.OutputUnit)
                .Include(r => r.Size)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.Unit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.ChildRecipe)
                        .ThenInclude(c => c!.PreparedItem)
                .OrderByDescending(r => r.RecipeId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var costs = await _estimatedBomCost.CalculateRecipesEstimatedCostAsync(
                recipes.Select(x => x.RecipeId));
            var items = new List<AdminRecipeListItemVM>(recipes.Count);

            foreach (var recipe in recipes)
            {
                var typeKey = ResolveRecipeTypeKey(recipe);
                costs.TryGetValue(recipe.RecipeId, out var cost);
                var configuration = _healthEvaluator.EvaluateConfiguration(recipe);
                var costing = cost != null
                    ? _healthEvaluator.EvaluateCosting(cost)
                    : new BomHealthStatusVM
                    {
                        Code = BomCostingHealthCodes.Indeterminate,
                        Label = "Không xác định được giá vốn"
                    };
                var normalizedOutput = TryNormalizeOutput(recipe);

                var vm = new AdminRecipeListItemVM
                {
                    RecipeId = recipe.RecipeId,
                    RecipeCode = recipe.RecipeCode ?? "",
                    Name = recipe.Name ?? "",
                    RecipeType = typeKey,
                    TypeLabel = typeKey switch
                    {
                        "POS" => "Món bán",
                        "TOPPING" => "Topping",
                        "SUBRECIPE" => "Bán thành phẩm",
                        _ => "Khác"
                    },
                    IdentityDisplay = BuildIdentityDisplay(recipe, typeKey),
                    PreparedItemId = recipe.PreparedItemId,
                    PreparedItemCode = recipe.PreparedItem?.Code,
                    PreparedItemName = recipe.PreparedItem?.Name,
                    DrinkId = recipe.DrinkId,
                    SizeId = recipe.SizeId,
                    ToppingId = recipe.ToppingId,
                    OutputQuantity = recipe.OutputQuantity,
                    OutputUnitCode = recipe.OutputUnit?.UnitCode,
                    OutputUnitName = recipe.OutputUnit?.Name,
                    Active = recipe.Active,
                    Status = recipe.Status ?? "",
                    EffectiveDate = recipe.EffectiveDate,
                    ParentVersionId = recipe.ParentVersionId,
                    BaseUnitCode = recipe.PreparedItem?.BaseUnit?.UnitCode,
                    ComponentCount = recipe.RecipeDetails?.Count ?? 0,
                    PortionDefinitionDisplay = BuildPortionDefinition(recipe),
                    ConfigurationHealth = configuration,
                    CostingHealth = costing,
                    CostComplete = cost?.IsComplete,
                    EstimatedCost = cost?.IsComplete == true ? cost.TotalCost : null,
                    CostStatus = costing.Label
                };

                if (typeKey == "TOPPING")
                {
                    vm.ToppingConsumptionSource = BuildToppingConsumptionSource(recipe);
                    ApplyToppingCost(
                        vm.ToppingConsumptionSource,
                        cost?.IsComplete == true,
                        cost?.IsComplete == true ? cost.TotalCost : null,
                        costing.Label);
                    vm.ConsumptionSourceDisplay = vm.ToppingConsumptionSource.SourceLabel;
                }
                else
                {
                    vm.ConsumptionSourceDisplay = BuildConsumptionSource(recipe);
                }

                if (typeKey == "SUBRECIPE"
                    && recipe.OutputQuantity.HasValue
                    && recipe.OutputUnitId.HasValue)
                {
                    vm.OutputPerBatchDisplay =
                        $"{recipe.OutputQuantity.Value:0.####} {recipe.OutputUnit?.UnitCode ?? recipe.OutputUnit?.Name ?? ""}".Trim();
                }

                if (normalizedOutput.HasValue)
                {
                    vm.NormalizedQuantityInBase = normalizedOutput.Value;
                    vm.NormalizedOutputDisplay =
                        $"{normalizedOutput.Value:0.####} {vm.BaseUnitCode}";
                    if (vm.EstimatedCost.HasValue && normalizedOutput.Value > 0)
                        vm.EstimatedUnitCost = vm.EstimatedCost.Value / normalizedOutput.Value;
                }

                items.Add(vm);
            }

            return new AdminRecipeListPageVM
            {
                TypeFilter = normalizedType,
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                StatusFilter = normalizedStatus,
                Page = page,
                TotalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize)),
                TotalCount = total,
                Items = items
            };
        }

        public async Task<IReadOnlyDictionary<int, ToppingConsumptionSourceVM>> GetToppingConsumptionSourcesAsync(
            IEnumerable<int> toppingIds)
        {
            var ids = toppingIds
                .Where(x => x > 0)
                .Distinct()
                .ToList();
            var summaries = ids.ToDictionary(x => x, CreateMissingToppingSource);
            if (ids.Count == 0)
                return summaries;

            var recipes = await _context.Recipes
                .AsNoTracking()
                .AsSplitQuery()
                .Where(r => r.ToppingId.HasValue
                    && ids.Contains(r.ToppingId.Value)
                    && r.Active
                    && r.Status == "Active")
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.Unit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.Ingredient)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.ChildRecipe)
                        .ThenInclude(c => c!.PreparedItem)
                            .ThenInclude(p => p!.BaseUnit)
                .OrderByDescending(r => r.EffectiveDate)
                .ThenByDescending(r => r.RecipeId)
                .ToListAsync();

            var costs = await _estimatedBomCost.CalculateRecipesEstimatedCostAsync(
                recipes.Select(x => x.RecipeId));

            foreach (var group in recipes.GroupBy(x => x.ToppingId!.Value))
            {
                var activeRecipes = group.ToList();
                if (activeRecipes.Count > 1)
                {
                    summaries[group.Key] = new ToppingConsumptionSourceVM
                    {
                        ToppingId = group.Key,
                        ActiveRecipeId = activeRecipes[0].RecipeId,
                        ActiveRecipeCode = activeRecipes[0].RecipeCode,
                        SourceCode = ToppingConsumptionSourceCodes.MixedOrInvalid,
                        SourceLabel = "Liên kết nguồn không hợp lệ",
                        MappingValid = false,
                        Reason = $"Có {activeRecipes.Count} công thức Active cho cùng topping; cần chỉ giữ một phiên bản hiệu lực."
                    };
                    continue;
                }

                var recipe = activeRecipes[0];
                var source = BuildToppingConsumptionSource(recipe);
                costs.TryGetValue(recipe.RecipeId, out var cost);
                var costLabel = cost?.IsComplete == true
                    ? "Giá vốn BOM đã xác định"
                    : "Chưa xác định đầy đủ giá vốn BOM";
                ApplyToppingCost(
                    source,
                    cost?.IsComplete == true,
                    cost?.IsComplete == true ? cost.TotalCost : null,
                    costLabel);
                summaries[group.Key] = source;
            }

            return summaries;
        }

        public async Task<AdminRecipeFormPageVM> GetCreatePageAsync()
        {
            return new AdminRecipeFormPageVM
            {
                Form = new RecipeCreateVM(),
                Options = await GetFormOptionsAsync(),
                IsEdit = false
            };
        }

        public async Task<AdminRecipeFormPageVM?> GetEditPageAsync(int recipeId)
        {
            var recipe = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Ingredient)
                        .ThenInclude(i => i!.BaseUnit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.ChildRecipe)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Unit)
                .Include(r => r.PreparedItem)
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId && r.Status == "Active");

            if (recipe == null)
                return null;

            bool isLegacyUnmapped =
                !recipe.DrinkId.HasValue
                && !recipe.ToppingId.HasValue
                && !recipe.PreparedItemId.HasValue;

            string recipeType = recipe.ToppingId.HasValue
                ? "TOPPING"
                : recipe.DrinkId.HasValue
                    ? "POS"
                    : "SUBRECIPE";

            var form = new RecipeCreateVM
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
                EffectiveDate = recipe.EffectiveDate ?? DateTime.Today,
                Description = null,
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

            return new AdminRecipeFormPageVM
            {
                Form = form,
                Options = await GetFormOptionsAsync(),
                SourceRecipeId = recipeId,
                RecipeName = recipe.Name,
                IsEdit = true
            };
        }

        public async Task<AdminRecipeVisualizePageVM?> GetVisualizePageAsync(int recipeId)
        {
            var recipe = await _context.Recipes
                .AsNoTracking()
                .AsSplitQuery()
                .Include(r => r.PreparedItem)
                    .ThenInclude(p => p!.BaseUnit)
                .Include(r => r.OutputUnit)
                .Include(r => r.Size)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.Unit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.Ingredient)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.ChildRecipe)
                        .ThenInclude(c => c!.PreparedItem)
                            .ThenInclude(p => p!.BaseUnit)
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

            if (recipe == null)
                return null;

            var tree = await _bomTree.BuildTreeAsync(recipeId);
            var typeKey = ResolveRecipeTypeKey(recipe);
            var typeLabel = typeKey switch
            {
                "TOPPING" => "Topping",
                "POS" => "Món bán",
                "SUBRECIPE" => "Bán thành phẩm",
                _ => "Công thức"
            };
            var cost = await _estimatedBomCost.CalculateRecipeEstimatedCostAsync(recipe.RecipeId);
            var configuration = _healthEvaluator.EvaluateConfiguration(recipe);
            var costing = _healthEvaluator.EvaluateCosting(cost);
            decimal? normalizedOutput = null;
            string? outputBaseUnitCode = recipe.PreparedItem?.BaseUnit?.UnitCode;

            if (typeKey == "SUBRECIPE"
                && recipe.PreparedItemId.HasValue
                && recipe.OutputQuantity.HasValue
                && recipe.OutputQuantity.Value > 0
                && recipe.OutputUnitId.HasValue)
            {
                var normalized = await _outputNormalizer.NormalizeAsync(
                    recipe.PreparedItemId.Value,
                    recipe.OutputQuantity.Value,
                    recipe.OutputUnitId.Value);
                if (normalized.IsSuccess && normalized.Data != null)
                {
                    normalizedOutput = normalized.Data.NormalizedQuantityInBase;
                    outputBaseUnitCode = normalized.Data.BaseUnitCode;
                }
            }

            var page = new AdminRecipeVisualizePageVM
            {
                RecipeId = recipe.RecipeId,
                RecipeCode = recipe.RecipeCode ?? "",
                Name = recipe.Name ?? "",
                Status = recipe.Status ?? "",
                Active = recipe.Active,
                EffectiveDate = recipe.EffectiveDate,
                ParentVersionId = recipe.ParentVersionId,
                RecipeTypeKey = typeKey,
                TypeLabel = typeLabel,
                IdentityDisplay = BuildIdentityDisplay(recipe, typeKey),
                PreparedItemId = recipe.PreparedItemId,
                PreparedItemCode = recipe.PreparedItem?.Code,
                PreparedItemName = recipe.PreparedItem?.Name,
                OutputQuantity = recipe.OutputQuantity,
                OutputUnitCode = recipe.OutputUnit?.UnitCode,
                OutputUnitName = recipe.OutputUnit?.Name,
                NormalizedOutputQuantity = normalizedOutput,
                OutputBaseUnitCode = outputBaseUnitCode,
                ConfigurationHealth = configuration,
                CostingHealth = costing,
                EstimatedBatchCost = typeKey == "SUBRECIPE" && cost.IsComplete ? cost.TotalCost : null,
                EstimatedPortionCost = typeKey != "SUBRECIPE" && cost.IsComplete ? cost.TotalCost : null,
                EstimatedUnitCost = typeKey == "SUBRECIPE"
                    && cost.IsComplete
                    && normalizedOutput > 0
                        ? cost.TotalCost / normalizedOutput.Value
                        : null,
                FirstLevelNodes = tree.Roots
            };

            var costLines = cost.Lines
                .Where(x => x.RecipeDetailId.HasValue)
                .GroupBy(x => x.RecipeDetailId!.Value)
                .ToDictionary(x => x.Key, x => x.First());
            var issuesByDetail = cost.Issues
                .Where(x => x.RecipeDetailId.HasValue)
                .GroupBy(x => x.RecipeDetailId!.Value)
                .ToDictionary(x => x.Key, x => x.ToList());

            foreach (var detail in recipe.RecipeDetails.OrderBy(x => x.RecipeDetailId))
            {
                costLines.TryGetValue(detail.RecipeDetailId, out var line);
                issuesByDetail.TryGetValue(detail.RecipeDetailId, out var detailIssues);
                var child = detail.ChildRecipe;
                var preparedItem = child?.PreparedItem;
                var reasons = detailIssues == null || detailIssues.Count == 0
                    ? new List<BomHealthReasonVM>()
                    : _healthEvaluator.EvaluateCosting(CostCalculationResult.Incomplete(
                        line == null ? Array.Empty<CostLineResult>() : new[] { line },
                        detailIssues)).Reasons;

                page.Components.Add(new BomComponentDetailVM
                {
                    RecipeDetailId = detail.RecipeDetailId,
                    ComponentType = detail.IngredientId.HasValue ? "Nguyên liệu" : "Bán thành phẩm",
                    IngredientId = detail.IngredientId,
                    ChildRecipeId = detail.ChildRecipeId,
                    ChildRecipeCode = child?.RecipeCode,
                    PreparedItemId = child?.PreparedItemId,
                    PreparedItemCode = preparedItem?.Code,
                    ItemCode = detail.IngredientId.HasValue
                        ? detail.Ingredient?.Code ?? $"ING_{detail.IngredientId}"
                        : preparedItem?.Code ?? $"REC_{detail.ChildRecipeId}",
                    ItemName = detail.IngredientId.HasValue
                        ? detail.Ingredient?.Name ?? "Nguyên liệu không tồn tại"
                        : preparedItem?.Name ?? child?.Name ?? "BTP chưa ánh xạ",
                    Quantity = detail.Quantity,
                    UnitCode = detail.Unit?.UnitCode ?? detail.Unit?.Name ?? "",
                    NormalizedQuantity = line?.QuantityInBase,
                    BaseUnitCode = line?.BaseUnitCode ?? preparedItem?.BaseUnit?.UnitCode,
                    EstimatedLineCost = line?.LineCost,
                    CostStatus = line?.Status == CostCompletenessStatus.Complete
                        ? "Đủ dữ liệu ước tính"
                        : reasons.FirstOrDefault()?.Message ?? "Chưa đủ dữ liệu giá vốn",
                    CostReasons = reasons
                });
            }

            if (recipe.ToppingId.HasValue)
            {
                page.ToppingConsumptionSource = BuildToppingConsumptionSource(recipe);
                ApplyToppingCost(
                    page.ToppingConsumptionSource,
                    cost.IsComplete,
                    cost.IsComplete ? cost.TotalCost : null,
                    cost.IsComplete
                        ? "Giá vốn BOM đã xác định"
                        : "Chưa xác định đầy đủ giá vốn BOM");
            }

            return page;
        }

        public async Task<BomOperationalDetailVM?> GetOperationalDetailAsync(int recipeId, int storeId)
        {
            if (recipeId <= 0 || storeId <= 0)
                return null;

            var recipe = await _context.Recipes
                .AsNoTracking()
                .Where(x => x.RecipeId == recipeId)
                .Select(x => new
                {
                    x.RecipeId,
                    x.PreparedItemId,
                    PreparedItemCode = x.PreparedItem != null ? x.PreparedItem.Code : null,
                    PreparedItemName = x.PreparedItem != null ? x.PreparedItem.Name : null,
                    BaseUnitCode = x.PreparedItem != null && x.PreparedItem.BaseUnit != null
                        ? x.PreparedItem.BaseUnit.UnitCode
                        : null
                })
                .FirstOrDefaultAsync();
            var store = await _context.Stores
                .AsNoTracking()
                .Where(x => x.StoreId == storeId && x.Active)
                .Select(x => new { x.StoreId, x.Name })
                .FirstOrDefaultAsync();
            if (recipe == null || store == null)
                return null;

            var result = new BomOperationalDetailVM
            {
                StoreId = store.StoreId,
                StoreName = store.Name
            };

            if (recipe.PreparedItemId.HasValue)
            {
                var stockRows = await _context.StoreInventories
                    .AsNoTracking()
                    .Where(x => x.StoreId == storeId
                        && x.PreparedItemId == recipe.PreparedItemId.Value)
                    .OrderBy(x => x.StoreInventoryId)
                    .ToListAsync();
                var canonical = stockRows
                    .Where(x => x.BtpIdentityState == BtpIdentityState.Canonical
                        && x.QuantitySemanticsStatus == InventoryQuantitySemanticsStatus.BaseUnitConfirmed)
                    .ToList();
                var hasLegacyCollision = stockRows.Any(x => x.BtpIdentityState == BtpIdentityState.Legacy);

                if (canonical.Count == 1 && !hasLegacyCollision)
                {
                    var row = canonical[0];
                    var latestLayer = await _context.InventoryCostLayers
                        .AsNoTracking()
                        .Where(x => x.StoreId == storeId
                            && x.PreparedItemId == recipe.PreparedItemId.Value
                            && x.SourceProductionRunId.HasValue)
                        .OrderByDescending(x => x.CreatedAt)
                        .ThenByDescending(x => x.InventoryCostLayerId)
                        .Select(x => new
                        {
                            x.InventoryCostLayerId,
                            x.SourceProductionRunId,
                            x.UnitCost,
                            x.CreatedAt
                        })
                        .FirstOrDefaultAsync();

                    result.OutputStock = new BomPreparedItemStockVM
                    {
                        StoreInventoryId = row.StoreInventoryId,
                        PreparedItemId = recipe.PreparedItemId.Value,
                        PreparedItemCode = recipe.PreparedItemCode ?? "",
                        PreparedItemName = recipe.PreparedItemName ?? "",
                        BaseUnitCode = recipe.BaseUnitCode ?? "",
                        CurrentQuantity = row.AvailableQty,
                        ReservedQuantity = row.ReservedQty,
                        UsableQuantity = row.AvailableQty - row.ReservedQty,
                        LatestCostLayerId = latestLayer?.InventoryCostLayerId,
                        SourceProductionRunId = latestLayer?.SourceProductionRunId,
                        ActualUnitCost = latestLayer?.UnitCost,
                        ActualLayerCreatedAt = latestLayer?.CreatedAt
                    };
                }
            }

            var runs = await (
                from run in _context.ProductionRuns.AsNoTracking()
                where run.StoreId == storeId && run.RecipeId == recipeId
                join staff in _context.Staffs.AsNoTracking()
                    on run.CreatedByStaffId equals staff.StaffId into staffRows
                from staff in staffRows.DefaultIfEmpty()
                orderby run.CreatedAt descending
                select new BomProductionRunVM
                {
                    ProductionRunId = run.ProductionRunId,
                    RequestedRunCount = run.RequestedRunCount,
                    Status = run.Status == ProductionRunStatus.Completed
                        ? "Đã hoàn tất"
                        : "Đã xác nhận",
                    ConfirmedAt = run.ConfirmedAt,
                    CompletedAt = run.CompletedAt,
                    ActorName = staff != null ? staff.FullName : null,
                    ActualTotalInputCost = run.TotalInputCost,
                    ActualOutputUnitCost = run.OutputUnitCost
                })
                .Take(5)
                .ToListAsync();

            if (runs.Count > 0)
            {
                var runIds = runs.Select(x => x.ProductionRunId).ToList();
                var outputRows = await _context.InventoryTransactions
                    .AsNoTracking()
                    .Where(x => x.ProductionRunId.HasValue
                        && runIds.Contains(x.ProductionRunId.Value)
                        && x.Type == InventoryTransactionTypeEnum.PRODUCTION_IN)
                    .Select(x => new { ProductionRunId = x.ProductionRunId!.Value, x.Quantity })
                    .ToListAsync();
                var outputQuantities = outputRows
                    .GroupBy(x => x.ProductionRunId)
                    .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
                foreach (var run in runs)
                {
                    if (outputQuantities.TryGetValue(run.ProductionRunId, out var quantity))
                        run.NormalizedOutputQuantity = quantity;
                }
            }

            result.RecentRuns = runs;
            return result;
        }

        public async Task<AdminRecipeFormOptionsVM> GetFormOptionsAsync()
        {
            var options = new AdminRecipeFormOptionsVM();

            // Preserve previous behavior: resolve ingredient base-unit cost via existing service (sequential).
            var ingredients = await _context.Ingredients
                .AsNoTracking()
                .Include(i => i.BaseUnit)
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .ToListAsync();

            foreach (var x in ingredients)
            {
                var cost = await _estimatedBomCost.ResolveIngredientBaseUnitCostAsync(x.IngredientId);
                options.Ingredients.Add(new RecipeBomIngredientOptionVM
                {
                    Id = x.IngredientId,
                    Name = x.Name ?? "",
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

            options.SubRecipes = await _context.Recipes
                .AsNoTracking()
                .Include(x => x.PreparedItem)
                .Include(x => x.OutputUnit)
                .Where(x => x.Active && x.Status == "Active")
                .OrderBy(x => x.Name)
                .Select(x => new RecipeBomChildRecipeOptionVM
                {
                    Id = x.RecipeId,
                    Name = x.Name ?? "",
                    RecipeCode = x.RecipeCode,
                    PreparedItemId = x.PreparedItemId,
                    PreparedItemCode = x.PreparedItem != null ? x.PreparedItem.Code : null,
                    PreparedItemName = x.PreparedItem != null ? x.PreparedItem.Name : null,
                    OutputQuantity = x.OutputQuantity,
                    OutputUnitCode = x.OutputUnit != null ? x.OutputUnit.UnitCode : null,
                    BaseCost = 0m,
                    CostComplete = false,
                    UnitId = x.OutputUnitId ?? 0,
                    UnitName = x.OutputUnit != null ? x.OutputUnit.Name : "Phần",
                    CostMessage = "BTP con: pin phiên bản Recipe — EstimateBomCost trên server"
                })
                .ToListAsync();

            options.Drinks = await _context.Drinks
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .Select(x => new RecipeFormIdNameOption { Id = x.DrinkId, Name = x.Name ?? "" })
                .ToListAsync();

            options.Toppings = await _context.Toppings
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .Select(x => new RecipeFormIdNameOption { Id = x.ToppingId, Name = x.Name ?? "" })
                .ToListAsync();

            options.PreparedItems = await _preparedItemService.GetBomOptionsAsync(null);

            var units = await _context.Units
                .AsNoTracking()
                .Where(x => x.Active
                    && (x.Type == UnitType.KhoiLuong
                        || x.Type == UnitType.TheTich
                        || x.Type == UnitType.Dem))
                .OrderBy(x => x.UnitCode)
                .Select(x => new { x.UnitId, x.Name, x.UnitCode })
                .ToListAsync();

            options.Units = units
                .Where(u => !PackageUnitCodes.IsRejectedCommercialPackaging(u.UnitCode))
                .Select(u => new RecipeFormUnitOption
                {
                    UnitId = u.UnitId,
                    Name = u.Name ?? "",
                    UnitCode = u.UnitCode ?? ""
                })
                .ToList();

            return options;
        }

        public async Task<List<RecipeSizeOptionVM>> GetSizesByDrinkAsync(int drinkId)
        {
            return await _context.DrinkSizes
                .AsNoTracking()
                .Include(ds => ds.Size)
                .Where(ds => ds.DrinkId == drinkId && ds.Active)
                .Select(ds => new RecipeSizeOptionVM
                {
                    SizeId = ds.SizeId,
                    SizeName = ds.Size.Name ?? "",
                    Price = ds.Price
                })
                .ToListAsync();
        }

        private static string ResolveRecipeTypeKey(Models.Drinks.Recipe r)
        {
            if (r.ToppingId.HasValue) return "TOPPING";
            if (r.DrinkId.HasValue) return "POS";
            if (r.PreparedItemId.HasValue) return "SUBRECIPE";
            if (!r.DrinkId.HasValue && !r.ToppingId.HasValue) return "SUBRECIPE";
            return "OTHER";
        }

        private static string NormalizeRecipeTypeFilter(string? typeFilter)
        {
            return typeFilter?.Trim().ToUpperInvariant() switch
            {
                "TOPPING" => "TOPPING",
                "SUBRECIPE" => "SUBRECIPE",
                _ => "POS"
            };
        }

        private static string NormalizeStatusFilter(string? status)
        {
            return status?.Trim().ToUpperInvariant() switch
            {
                "ACTIVE" => "ACTIVE",
                "INACTIVE" => "INACTIVE",
                _ => "ALL"
            };
        }

        private static decimal? TryNormalizeOutput(Models.Drinks.Recipe recipe)
        {
            if (!recipe.PreparedItemId.HasValue
                || recipe.PreparedItem == null
                || !recipe.OutputQuantity.HasValue
                || recipe.OutputQuantity.Value <= 0
                || recipe.OutputUnit == null
                || recipe.PreparedItem.BaseUnit == null)
            {
                return null;
            }

            if (recipe.OutputUnit.UnitId == recipe.PreparedItem.BaseUnitId)
                return recipe.OutputQuantity.Value;

            return PhysicalUnitConversionRegistry.TryGetPairFactor(
                recipe.OutputUnit.UnitCode,
                recipe.PreparedItem.BaseUnit.UnitCode,
                recipe.OutputUnit.Type,
                recipe.PreparedItem.BaseUnit.Type,
                out var factor)
                ? recipe.OutputQuantity.Value * factor
                : null;
        }

        private static string BuildPortionDefinition(Models.Drinks.Recipe recipe)
        {
            var details = recipe.RecipeDetails?.OrderBy(x => x.RecipeDetailId).ToList()
                ?? new List<Models.Drinks.RecipeDetail>();
            if (details.Count == 0)
                return "Chưa có thành phần";

            if (details.Count == 1)
            {
                var detail = details[0];
                var unit = detail.Unit?.UnitCode ?? detail.Unit?.Name ?? "";
                return $"{detail.Quantity:0.####} {unit}".Trim();
            }

            return $"{details.Count} thành phần";
        }

        private static string BuildConsumptionSource(Models.Drinks.Recipe recipe)
        {
            var details = recipe.RecipeDetails?.ToList()
                ?? new List<Models.Drinks.RecipeDetail>();
            if (details.Count == 0)
                return "Chưa cấu hình nguồn tiêu hao";

            var hasIngredient = details.Any(x => x.IngredientId.HasValue);
            var hasPreparedItem = details.Any(x =>
                x.ChildRecipeId.HasValue
                && x.ChildRecipe?.PreparedItemId.HasValue == true);

            if (hasIngredient && hasPreparedItem)
                return "Nguyên liệu và BTP đã sơ chế";
            if (hasPreparedItem)
                return "Bán thành phẩm đã sơ chế";
            if (hasIngredient)
                return "Nguyên liệu trực tiếp";
            return "Liên kết nguồn không hợp lệ";
        }

        private static ToppingConsumptionSourceVM CreateMissingToppingSource(int toppingId)
            => new()
            {
                ToppingId = toppingId,
                SourceCode = ToppingConsumptionSourceCodes.NoActiveRecipe,
                SourceLabel = "Chưa cấu hình nguồn tiêu hao",
                MappingValid = false,
                Reason = "Topping chưa có công thức Active. Tên topping không được dùng để tự suy luận nguồn tiêu hao."
            };

        private static ToppingConsumptionSourceVM BuildToppingConsumptionSource(
            Models.Drinks.Recipe recipe)
        {
            var source = new ToppingConsumptionSourceVM
            {
                ToppingId = recipe.ToppingId ?? 0,
                ActiveRecipeId = recipe.RecipeId,
                ActiveRecipeCode = recipe.RecipeCode
            };
            var details = recipe.RecipeDetails?
                .OrderBy(x => x.RecipeDetailId)
                .ToList() ?? new List<Models.Drinks.RecipeDetail>();

            if (details.Count == 0)
            {
                source.SourceCode = ToppingConsumptionSourceCodes.MixedOrInvalid;
                source.SourceLabel = "Liên kết nguồn không hợp lệ";
                source.Reason = "Công thức Active chưa có RecipeDetail nguồn tiêu hao.";
                return source;
            }

            var hasInvalid = false;
            foreach (var detail in details)
            {
                var hasIngredientId = detail.IngredientId.HasValue;
                var hasChildRecipeId = detail.ChildRecipeId.HasValue;
                if (hasIngredientId == hasChildRecipeId)
                {
                    hasInvalid = true;
                    continue;
                }

                var unitCode = detail.Unit?.UnitCode ?? detail.Unit?.Name ?? "";
                if (hasIngredientId)
                {
                    if (detail.Ingredient == null)
                        hasInvalid = true;

                    source.Components.Add(new ToppingConsumptionComponentVM
                    {
                        SourceKind = ToppingConsumptionSourceCodes.DirectIngredient,
                        IngredientId = detail.IngredientId,
                        IngredientCode = detail.Ingredient?.Code,
                        IngredientName = detail.Ingredient?.Name,
                        Quantity = detail.Quantity,
                        UnitCode = unitCode
                    });
                    continue;
                }

                var childRecipe = detail.ChildRecipe;
                var preparedItem = childRecipe?.PreparedItem;
                if (childRecipe == null
                    || !childRecipe.PreparedItemId.HasValue
                    || preparedItem == null)
                {
                    hasInvalid = true;
                }

                source.Components.Add(new ToppingConsumptionComponentVM
                {
                    SourceKind = ToppingConsumptionSourceCodes.PreparedItem,
                    ChildRecipeId = detail.ChildRecipeId,
                    ChildRecipeCode = childRecipe?.RecipeCode,
                    ChildRecipeName = childRecipe?.Name,
                    PreparedItemId = childRecipe?.PreparedItemId,
                    PreparedItemCode = preparedItem?.Code,
                    PreparedItemName = preparedItem?.Name,
                    PreparedItemBaseUnitCode = preparedItem?.BaseUnit?.UnitCode,
                    Quantity = detail.Quantity,
                    UnitCode = unitCode
                });
            }

            var hasIngredient = source.Components.Any(x =>
                x.SourceKind == ToppingConsumptionSourceCodes.DirectIngredient);
            var hasPreparedItem = source.Components.Any(x =>
                x.SourceKind == ToppingConsumptionSourceCodes.PreparedItem);

            if (hasInvalid || source.Components.Count != details.Count)
            {
                source.SourceCode = ToppingConsumptionSourceCodes.MixedOrInvalid;
                source.SourceLabel = "Liên kết nguồn không hợp lệ";
                source.MappingValid = false;
                source.Reason = "Một hoặc nhiều dòng BOM thiếu đúng một IngredientId hoặc ChildRecipeId pin tới BTP hợp lệ.";
            }
            else if (hasIngredient && hasPreparedItem)
            {
                source.SourceCode = ToppingConsumptionSourceCodes.MixedOrInvalid;
                source.SourceLabel = "Nguồn hỗn hợp cần kiểm tra";
                source.MappingValid = true;
                source.Reason = "BOM tiêu hao đồng thời nguyên liệu trực tiếp và BTP đã sơ chế.";
            }
            else if (hasPreparedItem)
            {
                source.SourceCode = ToppingConsumptionSourceCodes.PreparedItem;
                source.SourceLabel = "Nguồn bán thành phẩm đã sơ chế";
                source.MappingValid = true;
                source.Reason = "ChildRecipeId pin chính xác phiên bản công thức tạo BTP; không dùng phiên bản latest theo tên.";
            }
            else
            {
                source.SourceCode = ToppingConsumptionSourceCodes.DirectIngredient;
                source.SourceLabel = "Nguồn nguyên liệu trực tiếp";
                source.MappingValid = true;
                source.Reason = "RecipeDetail trỏ trực tiếp tới IngredientId.";
            }

            return source;
        }

        private static void ApplyToppingCost(
            ToppingConsumptionSourceVM source,
            bool costComplete,
            decimal? estimatedCost,
            string costStatus)
        {
            source.CostComplete = source.MappingValid && costComplete;
            source.EstimatedCostPerPortion = source.CostComplete ? estimatedCost : null;
            source.CostStatus = source.CostComplete
                ? costStatus
                : "Chưa xác định đầy đủ giá vốn BOM";
        }

        private static string BuildIdentityDisplay(Models.Drinks.Recipe r, string typeKey)
        {
            if (typeKey == "SUBRECIPE" && r.PreparedItem != null)
                return $"[{r.PreparedItem.Code}] {r.PreparedItem.Name}";
            if (typeKey == "SUBRECIPE")
                return r.Name ?? $"Recipe #{r.RecipeId}";
            if (typeKey == "TOPPING")
                return r.Name ?? $"Topping #{r.ToppingId}";
            if (typeKey == "POS")
            {
                var size = r.Size?.Name;
                return string.IsNullOrWhiteSpace(size) ? (r.Name ?? "") : $"{r.Name} · {size}";
            }
            return r.Name ?? $"#{r.RecipeId}";
        }
    }
}
