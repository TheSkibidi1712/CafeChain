using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.DTOs.Admin.Production;
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
        private readonly ICurrentRecipeResolver _currentRecipeResolver;
        private readonly TimeProvider _timeProvider;

        public AdminRecipeQueryService(
            AppDbContext context,
            IRecipeOutputNormalizer outputNormalizer,
            IEstimatedBomCostService estimatedBomCost,
            IAdminPreparedItemService preparedItemService,
            IRecipeBomTreeQueryService bomTree,
            IBomDataHealthEvaluator healthEvaluator,
            ICurrentRecipeResolver? currentRecipeResolver = null,
            TimeProvider? timeProvider = null)
        {
            _context = context;
            _outputNormalizer = outputNormalizer;
            _estimatedBomCost = estimatedBomCost;
            _preparedItemService = preparedItemService;
            _bomTree = bomTree;
            _healthEvaluator = healthEvaluator;
            _currentRecipeResolver = currentRecipeResolver ?? new CurrentRecipeResolver(context);
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public async Task<BomDataHealthPageVM> GetDataHealthPageAsync(
            int page = 1,
            int pageSize = 20,
            string? search = null,
            string? typeFilter = null)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? "" : search.Trim();
            var normalizedType = NormalizeDataHealthTypeFilter(typeFilter);

            var baseQuery = _context.Recipes.AsNoTracking();
            baseQuery = normalizedType switch
            {
                "POS" => baseQuery.Where(r => r.DrinkId.HasValue),
                "TOPPING" => baseQuery.Where(r => r.ToppingId.HasValue),
                "SUBRECIPE" => baseQuery.Where(r => !r.DrinkId.HasValue && !r.ToppingId.HasValue),
                _ => baseQuery
            };
            if (normalizedSearch.Length > 0)
            {
                baseQuery = baseQuery.Where(r =>
                    r.Name.Contains(normalizedSearch)
                    || r.RecipeCode.Contains(normalizedSearch)
                    || (r.PreparedItem != null
                        && (r.PreparedItem.Name.Contains(normalizedSearch)
                            || r.PreparedItem.Code.Contains(normalizedSearch))));
            }

            var totalCount = await baseQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);

            var recipes = await baseQuery
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
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var targets = recipes
                .Select(CreateRecipeTarget)
                .Where(target => target != null)
                .Cast<RecipeTarget>()
                .Distinct()
                .ToArray();
            var resolutions = await _currentRecipeResolver.ResolveManyAsync(
                targets,
                _timeProvider.GetUtcNow().UtcDateTime);

            var costResults = await _estimatedBomCost.CalculateRecipesEstimatedCostAsync(
                recipes.Select(x => x.RecipeId));
            var result = new BomDataHealthPageVM
            {
                Page = page,
                PageSize = pageSize,
                Search = normalizedSearch,
                TypeFilter = normalizedType,
                TotalCount = totalCount
            };

            foreach (var recipe in recipes)
            {
                var typeKey = ResolveRecipeTypeKey(recipe);
                var configuration = _healthEvaluator.EvaluateConfiguration(recipe);
                var target = CreateRecipeTarget(recipe);
                var currentRecipe = target == null
                    ? BuildCurrentRecipeHealth(recipe, null)
                    : BuildCurrentRecipeHealth(recipe, resolutions[target]);
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

                result.Items.Add(new BomDataHealthRowVM
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
                    CurrentRecipe = currentRecipe,
                    Configuration = configuration,
                    Costing = costing,
                    EstimatedCost = costResult?.IsComplete == true ? costResult.TotalCost : null
                });
            }

            result.CompleteCount = result.Items.Count(x =>
                !x.CurrentRecipe.IsBlocking
                && x.Configuration.IsComplete
                && x.Costing.IsComplete);
            result.MissingQuoteCount = result.Items.Count(x => x.Costing.Reasons.Any(r =>
                r.GroupCode == BomCostingHealthCodes.MissingQuote));
            result.MissingConversionCount = result.Items.Count(x => x.Costing.Reasons.Any(r =>
                r.GroupCode == BomCostingHealthCodes.MissingConversion));
            result.MissingOutputCount = result.Items.Count(x => x.Configuration.Reasons.Any(r =>
                r.Code == BomConfigurationHealthCodes.MissingOutputIdentity
                || r.Code == BomConfigurationHealthCodes.MissingOutputQuantity
                || r.Code == BomConfigurationHealthCodes.MissingOutputUnit));
            result.MappingErrorCount = result.Items.Count(x => x.Configuration.Reasons.Any(r =>
                r.Code == BomConfigurationHealthCodes.InvalidPreparedItemMapping));
            result.CurrentRecipeIssueCount = result.Items.Count(x => x.CurrentRecipe.IsBlocking);

            return result;
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

            var targets = ids
                .Select(id => (RecipeTarget)new RecipeTarget.Topping(id))
                .ToArray();
            var resolutions = await _currentRecipeResolver.ResolveManyAsync(
                targets,
                _timeProvider.GetUtcNow().UtcDateTime);
            foreach (var target in targets.OfType<RecipeTarget.Topping>())
            {
                if (resolutions[target].Status == CurrentRecipeResolutionStatus.Ambiguous)
                {
                    summaries[target.ToppingId] = new ToppingConsumptionSourceVM
                    {
                        ToppingId = target.ToppingId,
                        SourceCode = ToppingConsumptionSourceCodes.MixedOrInvalid,
                        SourceLabel = "Liên kết nguồn không hợp lệ",
                        MappingValid = false,
                        Reason = "Topping có nhiều công thức đang áp dụng; cần xử lý trước khi sử dụng."
                    };
                }
            }
            var currentRecipeIds = resolutions.Values
                .Where(result => result.Status == CurrentRecipeResolutionStatus.Found)
                .Select(result => result.Recipe!.RecipeId)
                .ToArray();

            var recipes = await _context.Recipes
                .AsNoTracking()
                .AsSplitQuery()
                .Where(r => currentRecipeIds.Contains(r.RecipeId))
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.Unit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.Ingredient)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.ChildRecipe)
                        .ThenInclude(c => c!.PreparedItem)
                            .ThenInclude(p => p!.BaseUnit)
                .ToListAsync();

            var costs = await _estimatedBomCost.CalculateRecipesEstimatedCostAsync(
                recipes.Select(x => x.RecipeId));

            foreach (var group in recipes.GroupBy(x => x.ToppingId!.Value))
            {
                var recipe = group.Single();
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

        public async Task<AdminRecipeFormPageVM?> GetEditPageAsync(
            int recipeId,
            bool includeHistoricalSource = false)
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
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId
                    && (includeHistoricalSource || r.Status == "Active"));

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
                        .ThenInclude(i => i!.BaseUnit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.ChildRecipe)
                        .ThenInclude(c => c!.PreparedItem)
                            .ThenInclude(p => p!.BaseUnit)
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

            if (recipe == null)
                return null;

            var typeKey = ResolveRecipeTypeKey(recipe);
            var typeLabel = typeKey switch
            {
                "TOPPING" => "Topping",
                "POS" => "Món bán",
                "SUBRECIPE" => "Bán thành phẩm",
                _ => "Công thức"
            };
            var target = CreateRecipeTarget(recipe);
            var currentResolution = target == null
                ? null
                : await _currentRecipeResolver.ResolveAsync(
                    target,
                    _timeProvider.GetUtcNow().UtcDateTime);
            var isCurrentVersion = currentResolution?.Status == CurrentRecipeResolutionStatus.Found
                && currentResolution.Recipe?.RecipeId == recipe.RecipeId;
            var identity = await GetWorkspaceIdentityAsync(recipe, typeKey);
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

            var output = BuildWorkspaceOutput(recipe, typeKey, identity.BusinessName, identity.SizeName);
            var appliedState = BuildAppliedState(currentResolution, isCurrentVersion);
            var designCostResult = await _estimatedBomCost.CalculateRecipeEstimatedCostAsync(recipe.RecipeId);
            var costingHealth = _healthEvaluator.EvaluateCosting(designCostResult);

            var page = new AdminRecipeVisualizePageVM
            {
                RecipeId = recipe.RecipeId,
                RecipeCode = recipe.RecipeCode ?? "",
                Name = recipe.Name ?? "",
                BusinessName = identity.BusinessName,
                BusinessCode = identity.BusinessCode,
                SizeName = identity.SizeName,
                TargetLabel = BuildTargetLabel(typeKey, identity.SizeName),
                VersionLabel = $"Phiên bản {recipe.RecipeId}",
                IsCurrentVersion = isCurrentVersion,
                AppliedStateLabel = appliedState.Label,
                AppliedStateCssClass = appliedState.CssClass,
                OutputHeading = output.Heading,
                OutputDisplay = output.Display,
                OutputContext = output.Context,
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
                ConfigurationHealth = _healthEvaluator.EvaluateConfiguration(recipe)
            };

            page.CostingHealth = costingHealth;
            page.DesignCost = BuildDesignCostEvidence(designCostResult, typeKey, outputBaseUnitCode);

            foreach (var detail in recipe.RecipeDetails.OrderBy(x => x.RecipeDetailId))
            {
                var child = detail.ChildRecipe;
                var preparedItem = child?.PreparedItem;
                var baseUnit = detail.Ingredient?.BaseUnit ?? preparedItem?.BaseUnit;
                var costLine = designCostResult.Lines.FirstOrDefault(x =>
                    x.RecipeDetailId == detail.RecipeDetailId);
                var normalizedQuantity = costLine?.QuantityInBase
                    ?? TryNormalizeComponent(detail, baseUnit);
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
                        : preparedItem?.Name ?? child?.Name ?? "Bán thành phẩm chưa được liên kết",
                    Quantity = detail.Quantity,
                    UnitCode = detail.Unit?.UnitCode ?? detail.Unit?.Name ?? "",
                    NormalizedQuantity = normalizedQuantity,
                    BaseUnitCode = costLine?.BaseUnitCode ?? baseUnit?.UnitCode,
                    EstimatedLineCost = costLine?.LineCost,
                    CostStatus = costLine?.Status == CostCompletenessStatus.Complete
                        ? "Đã đủ dữ liệu"
                        : "Chưa đủ dữ liệu"
                });
            }

            if (page.DesignCost.IsAvailable && page.DesignCost.Amount > 0)
            {
                foreach (var component in page.Components.Where(x => x.EstimatedLineCost.HasValue))
                {
                    component.CostContributionPercent = decimal.Round(
                        component.EstimatedLineCost!.Value / page.DesignCost.Amount.Value * 100m,
                        1);
                }
            }

            page.GlobalReadiness = BuildGlobalReadiness(page);

            if (recipe.ToppingId.HasValue)
            {
                page.ToppingConsumptionSource = BuildToppingConsumptionSource(recipe);
            }

            return page;
        }

        public async Task<RecipeWorkspaceStoreEvidenceVM?> GetStoreEvidenceAsync(
            AdminRecipeVisualizePageVM page,
            int storeId)
        {
            ArgumentNullException.ThrowIfNull(page);
            if (storeId <= 0)
                return null;

            var store = await _context.Stores
                .AsNoTracking()
                .Where(x => x.StoreId == storeId && x.Active)
                .Select(x => new { x.StoreId, x.Name })
                .FirstOrDefaultAsync();
            if (store == null)
                return null;

            var ingredientIds = page.Components
                .Where(x => x.IngredientId.HasValue)
                .Select(x => x.IngredientId!.Value)
                .Distinct()
                .ToList();
            var preparedItemIds = page.Components
                .Where(x => x.PreparedItemId.HasValue)
                .Select(x => x.PreparedItemId!.Value)
                .Distinct()
                .ToList();
            var layers = await _context.InventoryCostLayers
                .AsNoTracking()
                .Where(x => x.StoreId == storeId
                    && x.RemainingQuantity > 0
                    && ((x.IngredientId.HasValue && ingredientIds.Contains(x.IngredientId.Value))
                        || (x.PreparedItemId.HasValue && preparedItemIds.Contains(x.PreparedItemId.Value))))
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.InventoryCostLayerId)
                .Select(x => new
                {
                    x.IngredientId,
                    x.PreparedItemId,
                    x.RemainingQuantity,
                    x.UnitCost,
                    x.CreatedAt,
                    x.InventoryCostLayerId
                })
                .ToListAsync();

            decimal total = 0m;
            var complete = page.Components.Count > 0;
            DateTime? evidenceAt = null;
            foreach (var component in page.Components)
            {
                if (!component.NormalizedQuantity.HasValue || component.NormalizedQuantity.Value <= 0)
                {
                    complete = false;
                    continue;
                }

                var componentLayers = layers.Where(x => component.IngredientId.HasValue
                        ? x.IngredientId == component.IngredientId && !x.PreparedItemId.HasValue
                        : x.PreparedItemId == component.PreparedItemId && !x.IngredientId.HasValue)
                    .Where(x => x.UnitCost > 0)
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.InventoryCostLayerId)
                    .ToList();
                var remaining = component.NormalizedQuantity.Value;
                foreach (var layer in componentLayers)
                {
                    if (remaining <= 0)
                        break;
                    var take = Math.Min(remaining, layer.RemainingQuantity);
                    if (take <= 0)
                        continue;
                    total += take * layer.UnitCost;
                    remaining -= take;
                    evidenceAt = !evidenceAt.HasValue || layer.CreatedAt > evidenceAt.Value
                        ? layer.CreatedAt
                        : evidenceAt;
                }

                if (remaining > 0)
                    complete = false;
            }

            var cost = new RecipeWorkspaceCostEvidenceVM
            {
                AuthorityCode = RecipeWorkspaceCostAuthorityCodes.StoreFifo,
                Label = "Giá vốn theo nhập trước - xuất trước (FIFO) tại chi nhánh",
                State = complete
                    ? RecipeWorkspaceEvidenceState.Available
                    : RecipeWorkspaceEvidenceState.Incomplete,
                Amount = complete ? total : null,
                UnitLabel = BuildCostUnitLabel(page.RecipeTypeKey, page.OutputBaseUnitCode),
                ContextLabel = store.Name,
                EvidenceAtUtc = evidenceAt,
                Message = complete
                    ? "Đã đủ lớp giá cho toàn bộ định mức của phiên bản này."
                    : "Chưa đủ lớp giá FIFO cho toàn bộ định mức tại chi nhánh đã chọn."
            };

            return new RecipeWorkspaceStoreEvidenceVM
            {
                StoreId = store.StoreId,
                StoreName = store.Name,
                Cost = cost,
                Readiness = new RecipeWorkspaceReadinessSummaryVM
                {
                    ScopeLabel = $"Chi nhánh {store.Name}",
                    Facets =
                    [
                        new RecipeWorkspaceReadinessFacetVM
                        {
                            Code = RecipeWorkspaceReadinessCodes.StoreFifo,
                            Label = "Bằng chứng giá FIFO",
                            State = complete
                                ? RecipeWorkspaceEvidenceState.Available
                                : RecipeWorkspaceEvidenceState.Incomplete,
                            Message = cost.Message
                        },
                        new RecipeWorkspaceReadinessFacetVM
                        {
                            Code = RecipeWorkspaceReadinessCodes.StoreOperations,
                            Label = "Khả năng vận hành tại chi nhánh",
                            State = page.IsPreparedItemRecipe
                                ? RecipeWorkspaceEvidenceState.Unavailable
                                : RecipeWorkspaceEvidenceState.NotApplicable,
                            Message = page.IsPreparedItemRecipe
                                ? "Đang chờ kiểm tra tồn kho và điều kiện sản xuất tại chi nhánh."
                                : "Tiêu chí sản xuất theo mẻ không áp dụng cho đối tượng này."
                        }
                    ]
                }
            };
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

            var runRows = await (
                from run in _context.ProductionRuns.AsNoTracking()
                where run.StoreId == storeId && run.RecipeId == recipeId
                join staff in _context.Staffs.AsNoTracking()
                    on run.CreatedByStaffId equals staff.StaffId into staffRows
                from staff in staffRows.DefaultIfEmpty()
                orderby run.CreatedAt descending
                select new
                {
                    run.ProductionRunId,
                    run.RequestedRunCount,
                    run.Status,
                    run.ConfirmedAt,
                    run.CompletedAt,
                    ActorName = staff != null ? staff.FullName : null,
                    AcceptedOutputQuantity = run.ActualOutput != null
                        ? (decimal?)run.ActualOutput.AcceptedOutputBase
                        : null,
                    OutputUnitCode = run.ActualOutput != null && run.ActualOutput.BaseUnit != null
                        ? run.ActualOutput.BaseUnit.UnitCode
                        : null,
                    ActualTotalInputCost = run.TotalInputCost,
                    ActualOutputUnitCost = run.OutputUnitCost
                })
                .Take(5)
                .ToListAsync();

            var runs = runRows.Select(run => new BomProductionRunVM
            {
                ProductionRunId = run.ProductionRunId,
                RequestedRunCount = run.RequestedRunCount,
                Status = ProductionRunDisplay.Status(run.Status),
                ConfirmedAt = run.ConfirmedAt,
                CompletedAt = run.CompletedAt,
                ActorName = run.ActorName,
                AcceptedOutputQuantity = run.AcceptedOutputQuantity,
                OutputUnitCode = run.OutputUnitCode ?? recipe.BaseUnitCode,
                ActualTotalInputCost = run.ActualTotalInputCost,
                ActualOutputUnitCost = run.ActualOutputUnitCost
            }).ToList();

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
                    BaseUnitCode = FormatUnitLabel(cost.BaseUnitCode ?? x.BaseUnit?.UnitCode),
                    CostMessage = cost.IsComplete
                        ? null
                        : (cost.Issues.FirstOrDefault()?.Message ?? "Chưa đủ dữ liệu giá vốn"),
                    UnitId = x.BaseUnitId,
                    UnitName = FormatUnitLabel(x.BaseUnit?.UnitCode)
                });
            }

            var preparedItemIds = await _context.PreparedItems
                .AsNoTracking()
                .Where(item => item.Active)
                .Select(item => item.PreparedItemId)
                .ToListAsync();
            var preparedTargets = preparedItemIds
                .Select(id => (RecipeTarget)new RecipeTarget.PreparedItem(id))
                .ToArray();
            var preparedResolutions = await _currentRecipeResolver.ResolveManyAsync(
                preparedTargets,
                _timeProvider.GetUtcNow().UtcDateTime);
            var currentPreparedRecipeIds = preparedResolutions.Values
                .Where(result => result.Status == CurrentRecipeResolutionStatus.Found)
                .Select(result => result.Recipe!.RecipeId)
                .ToArray();

            var currentPreparedRecipes = await _context.Recipes
                .AsNoTracking()
                .Include(x => x.PreparedItem)
                    .ThenInclude(item => item!.BaseUnit)
                .Include(x => x.OutputUnit)
                .Where(x => currentPreparedRecipeIds.Contains(x.RecipeId)
                    && x.PreparedItemId.HasValue
                    && x.PreparedItem != null
                    && x.PreparedItem.Active
                    && x.OutputQuantity.HasValue
                    && x.OutputQuantity > 0
                    && x.OutputUnitId.HasValue
                    && x.OutputUnit != null
                    && x.OutputUnit.Active)
                .OrderBy(x => x.Name)
                .ToListAsync();
            var currentPreparedCosts = await _estimatedBomCost.CalculateRecipesEstimatedCostAsync(
                currentPreparedRecipes.Select(recipe => recipe.RecipeId));

            foreach (var recipe in currentPreparedRecipes)
            {
                var cost = currentPreparedCosts[recipe.RecipeId];
                var normalizedOutput = await _outputNormalizer.NormalizeAsync(
                    recipe.PreparedItemId!.Value,
                    recipe.OutputQuantity!.Value,
                    recipe.OutputUnitId!.Value);
                var costComplete = cost.IsComplete
                    && cost.TotalCost.HasValue
                    && normalizedOutput.IsSuccess
                    && normalizedOutput.Data != null
                    && normalizedOutput.Data.NormalizedQuantityInBase > 0;
                var costMessage = costComplete
                    ? null
                    : normalizedOutput.IsSuccess
                        ? cost.Issues.FirstOrDefault()?.Message ?? "Chưa đủ dữ liệu giá vốn BTP."
                        : normalizedOutput.Message ?? "Không thể chuẩn hóa sản lượng đầu ra BTP.";

                options.SubRecipes.Add(new RecipeBomChildRecipeOptionVM
                {
                    Id = recipe.RecipeId,
                    Name = recipe.Name ?? "",
                    RecipeCode = recipe.RecipeCode,
                    PreparedItemId = recipe.PreparedItemId,
                    PreparedItemCode = recipe.PreparedItem?.Code,
                    PreparedItemName = recipe.PreparedItem?.Name,
                    OutputQuantity = recipe.OutputQuantity,
                    OutputUnitCode = recipe.OutputUnit?.UnitCode,
                    BaseCost = costComplete
                        ? cost.TotalCost!.Value / normalizedOutput.Data!.NormalizedQuantityInBase
                        : 0m,
                    CostComplete = costComplete,
                    UnitId = recipe.PreparedItem!.BaseUnitId,
                    UnitName = FormatUnitLabel(recipe.PreparedItem.BaseUnit?.UnitCode),
                    CostMessage = costMessage
                });
            }

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
                    Name = FormatUnitLabel(u.UnitCode),
                    UnitCode = FormatUnitLabel(u.UnitCode)
                })
                .ToList();

            return options;
        }

        private static string FormatUnitLabel(string? unitCode)
        {
            return PhysicalUnitConversionRegistry.NormalizeUnitCode(unitCode) switch
            {
                "g" => "g",
                "gram" => "g",
                "kg" => "kg",
                "kilogram" => "kg",
                "ml" => "ml",
                "milliliter" => "ml",
                "l" => "L",
                "liter" => "L",
                "pcs" => "cái",
                "piece" => "cái",
                _ => string.IsNullOrWhiteSpace(unitCode) ? "ĐVT" : unitCode.Trim()
            };
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

        private async Task<(string BusinessName, string? BusinessCode, string? SizeName)>
            GetWorkspaceIdentityAsync(Models.Drinks.Recipe recipe, string typeKey)
        {
            if (typeKey == "POS" && recipe.DrinkId.HasValue)
            {
                var drink = await _context.Drinks
                    .AsNoTracking()
                    .Where(x => x.DrinkId == recipe.DrinkId.Value)
                    .Select(x => new { x.Name, x.DrinkCode })
                    .FirstOrDefaultAsync();
                return (
                    drink?.Name ?? recipe.Name ?? "Món bán chưa xác định",
                    drink?.DrinkCode,
                    recipe.Size?.Name);
            }

            if (typeKey == "TOPPING" && recipe.ToppingId.HasValue)
            {
                var topping = await _context.Toppings
                    .AsNoTracking()
                    .Where(x => x.ToppingId == recipe.ToppingId.Value)
                    .Select(x => new { x.Name, x.ToppingCode })
                    .FirstOrDefaultAsync();
                return (
                    topping?.Name ?? recipe.Name ?? "Topping chưa xác định",
                    topping?.ToppingCode,
                    null);
            }

            if (typeKey == "SUBRECIPE")
            {
                return (
                    recipe.PreparedItem?.Name ?? recipe.Name ?? "Bán thành phẩm chưa xác định",
                    recipe.PreparedItem?.Code,
                    null);
            }

            return (recipe.Name ?? "Công thức chưa xác định", null, recipe.Size?.Name);
        }

        private static RecipeTarget? CreateRecipeTarget(Models.Drinks.Recipe recipe)
        {
            if (recipe.DrinkId.HasValue
                && recipe.SizeId.HasValue
                && !recipe.ToppingId.HasValue
                && !recipe.PreparedItemId.HasValue)
            {
                return new RecipeTarget.MenuItemSize(recipe.DrinkId.Value, recipe.SizeId.Value);
            }

            if (recipe.ToppingId.HasValue
                && !recipe.DrinkId.HasValue
                && !recipe.SizeId.HasValue
                && !recipe.PreparedItemId.HasValue)
            {
                return new RecipeTarget.Topping(recipe.ToppingId.Value);
            }

            if (recipe.PreparedItemId.HasValue
                && !recipe.DrinkId.HasValue
                && !recipe.SizeId.HasValue
                && !recipe.ToppingId.HasValue)
            {
                return new RecipeTarget.PreparedItem(recipe.PreparedItemId.Value);
            }

            return null;
        }

        private static BomCurrentRecipeHealthVM BuildCurrentRecipeHealth(
            Models.Drinks.Recipe recipe,
            CurrentRecipeResolution? resolution)
        {
            if (resolution?.Status == CurrentRecipeResolutionStatus.Found)
            {
                var isCurrent = resolution.Recipe?.RecipeId == recipe.RecipeId;
                return new BomCurrentRecipeHealthVM
                {
                    Code = isCurrent
                        ? BomCurrentRecipeHealthCodes.Current
                        : BomCurrentRecipeHealthCodes.Historical,
                    Label = isCurrent ? "Đang áp dụng" : "Phiên bản lịch sử",
                    Message = isCurrent
                        ? "Đây là phiên bản đang áp dụng cho đối tượng công thức."
                        : "Một phiên bản khác đang được áp dụng cho đối tượng công thức này."
                };
            }

            return resolution?.Status switch
            {
                CurrentRecipeResolutionStatus.Ambiguous => new BomCurrentRecipeHealthVM
                {
                    Code = BomCurrentRecipeHealthCodes.Ambiguous,
                    ReasonCode = resolution.ReasonCode,
                    Label = "Cần kiểm tra phiên bản áp dụng",
                    Message = "Có nhiều phiên bản cùng được đánh dấu đang áp dụng. Hệ thống không tự chọn một phiên bản.",
                    IsBlocking = true
                },
                CurrentRecipeResolutionStatus.Missing => new BomCurrentRecipeHealthVM
                {
                    Code = BomCurrentRecipeHealthCodes.Missing,
                    ReasonCode = resolution.ReasonCode,
                    Label = "Chưa có phiên bản đang áp dụng",
                    Message = "Chưa tìm thấy phiên bản đang áp dụng cho đối tượng công thức này.",
                    IsBlocking = true
                },
                _ => new BomCurrentRecipeHealthVM
                {
                    Code = BomCurrentRecipeHealthCodes.InvalidTarget,
                    ReasonCode = resolution?.ReasonCode ?? BomRecipeErrorCodes.CurrentRecipeInvalidTarget,
                    Label = "Đối tượng công thức không hợp lệ",
                    Message = "Công thức chưa xác định đúng một đối tượng nghiệp vụ để áp dụng.",
                    IsBlocking = true
                }
            };
        }

        private static (string Label, string CssClass) BuildAppliedState(
            CurrentRecipeResolution? resolution,
            bool isCurrentVersion)
        {
            if (isCurrentVersion)
                return ("Đang áp dụng", "rb-status-active");

            return resolution?.Status switch
            {
                CurrentRecipeResolutionStatus.Found => ("Phiên bản lịch sử", "rb-status-inactive"),
                CurrentRecipeResolutionStatus.Ambiguous => ("Cần kiểm tra phiên bản áp dụng", "rb-status-incomplete"),
                CurrentRecipeResolutionStatus.Missing => ("Chưa có phiên bản đang áp dụng", "rb-status-incomplete"),
                _ => ("Cần kiểm tra đối tượng công thức", "rb-status-incomplete")
            };
        }

        private static (string Heading, string Display, string Context) BuildWorkspaceOutput(
            Models.Drinks.Recipe recipe,
            string typeKey,
            string businessName,
            string? sizeName)
        {
            if (typeKey == "POS")
            {
                var size = string.IsNullOrWhiteSpace(sizeName) ? "chưa xác định" : sizeName;
                return (
                    "Đầu ra",
                    $"1 phần {businessName} · Cỡ {size}",
                    "Một phần bán theo đúng món và cỡ đang xem.");
            }

            if (typeKey == "TOPPING")
            {
                return (
                    "Phạm vi áp dụng",
                    $"Một lần sử dụng {businessName} theo định mức topping",
                    "Số lượng tiêu hao do các dòng định mức bên dưới xác định; không suy diễn sản lượng vật lý.");
            }

            if (typeKey == "SUBRECIPE")
            {
                var quantity = recipe.OutputQuantity?.ToString("0.####") ?? "Chưa cấu hình";
                var unit = recipe.OutputUnit?.UnitCode ?? recipe.OutputUnit?.Name ?? "đơn vị";
                return (
                    "Sản lượng chuẩn một mẻ",
                    $"{quantity} {unit} / mẻ",
                    "Mẻ là số lần thực hiện công thức; tồn kho vẫn ghi nhận theo đơn vị vật lý.");
            }

            return ("Đầu ra", "Chưa xác định", "Cần hoàn thiện đối tượng công thức.");
        }

        private static string BuildTargetLabel(string typeKey, string? sizeName)
        {
            return typeKey switch
            {
                "POS" => $"Món bán · Cỡ {(string.IsNullOrWhiteSpace(sizeName) ? "chưa xác định" : sizeName)}",
                "TOPPING" => "Topping",
                "SUBRECIPE" => "Bán thành phẩm",
                _ => "Công thức"
            };
        }

        private static RecipeWorkspaceCostEvidenceVM BuildDesignCostEvidence(
            CostCalculationResult result,
            string recipeTypeKey,
            string? outputBaseUnitCode)
        {
            if (!result.IsComplete || !result.TotalCost.HasValue)
            {
                return new RecipeWorkspaceCostEvidenceVM
                {
                    AuthorityCode = RecipeWorkspaceCostAuthorityCodes.DesignEstimate,
                    Label = "Giá vốn ước tính theo thiết kế",
                    State = RecipeWorkspaceEvidenceState.Incomplete,
                    Amount = null,
                    UnitLabel = BuildCostUnitLabel(recipeTypeKey, outputBaseUnitCode),
                    Message = "Chưa đủ báo giá hoặc quy đổi để xác định toàn bộ giá vốn thiết kế."
                };
            }

            return new RecipeWorkspaceCostEvidenceVM
            {
                AuthorityCode = RecipeWorkspaceCostAuthorityCodes.DesignEstimate,
                Label = "Giá vốn ước tính theo thiết kế",
                State = RecipeWorkspaceEvidenceState.Available,
                Amount = result.TotalCost,
                UnitLabel = BuildCostUnitLabel(recipeTypeKey, outputBaseUnitCode),
                Message = "Được tính từ định mức, quy đổi đơn vị và dữ liệu giá mua hiện có."
            };
        }

        private static string BuildCostUnitLabel(string recipeTypeKey, string? outputBaseUnitCode) =>
            recipeTypeKey switch
            {
                "POS" => "mỗi phần",
                "TOPPING" => "mỗi lần sử dụng",
                "SUBRECIPE" when !string.IsNullOrWhiteSpace(outputBaseUnitCode) =>
                    $"mỗi mẻ · đầu ra theo {outputBaseUnitCode}",
                "SUBRECIPE" => "mỗi mẻ",
                _ => "theo định mức"
            };

        private static RecipeWorkspaceReadinessSummaryVM BuildGlobalReadiness(
            AdminRecipeVisualizePageVM page)
        {
            var configurationPassed = page.ConfigurationHealth.IsComplete;
            var pricingPassed = page.DesignCost.IsAvailable;
            var pointOfSaleApplies = page.RecipeTypeKey is "POS" or "TOPPING";
            var pointOfSalePassed = pointOfSaleApplies
                && !string.IsNullOrWhiteSpace(page.BusinessCode)
                && page.IsCurrentVersion;
            var preparedInputsApply = page.PreparedInputs.Count > 0;
            var preparedInputsPassed = preparedInputsApply
                && page.PreparedInputs.All(x =>
                    x.ChildRecipeId.HasValue
                    && x.PreparedItemId.HasValue
                    && !string.IsNullOrWhiteSpace(x.ItemName));

            return new RecipeWorkspaceReadinessSummaryVM
            {
                ScopeLabel = "Cấu hình dùng chung",
                Facets =
                [
                    new RecipeWorkspaceReadinessFacetVM
                    {
                        Code = RecipeWorkspaceReadinessCodes.Configuration,
                        Label = "Cấu hình",
                        State = configurationPassed
                            ? RecipeWorkspaceEvidenceState.Available
                            : RecipeWorkspaceEvidenceState.Incomplete,
                        Message = configurationPassed
                            ? "Đối tượng, đầu ra và các dòng định mức đã được cấu hình hợp lệ."
                            : "Công thức còn thiếu hoặc có cấu hình định mức chưa hợp lệ."
                    },
                    new RecipeWorkspaceReadinessFacetVM
                    {
                        Code = RecipeWorkspaceReadinessCodes.Pricing,
                        Label = "Dữ liệu giá",
                        State = pricingPassed
                            ? RecipeWorkspaceEvidenceState.Available
                            : RecipeWorkspaceEvidenceState.Incomplete,
                        Message = page.DesignCost.Message
                    },
                    new RecipeWorkspaceReadinessFacetVM
                    {
                        Code = RecipeWorkspaceReadinessCodes.PointOfSale,
                        Label = "Điểm bán hàng",
                        State = !pointOfSaleApplies
                            ? RecipeWorkspaceEvidenceState.NotApplicable
                            : pointOfSalePassed
                                ? RecipeWorkspaceEvidenceState.Available
                                : RecipeWorkspaceEvidenceState.Incomplete,
                        Message = !pointOfSaleApplies
                            ? "Tiêu chí bán tại quầy không áp dụng cho công thức bán thành phẩm."
                            : pointOfSalePassed
                                ? "Đã liên kết đúng đối tượng đang áp dụng tại điểm bán hàng."
                                : "Chưa xác nhận được đối tượng đang áp dụng tại điểm bán hàng."
                    },
                    new RecipeWorkspaceReadinessFacetVM
                    {
                        Code = RecipeWorkspaceReadinessCodes.PreparedInputs,
                        Label = "Bán thành phẩm đầu vào",
                        State = !preparedInputsApply
                            ? RecipeWorkspaceEvidenceState.NotApplicable
                            : preparedInputsPassed
                                ? RecipeWorkspaceEvidenceState.Available
                                : RecipeWorkspaceEvidenceState.Incomplete,
                        Message = !preparedInputsApply
                            ? "Công thức không sử dụng bán thành phẩm đầu vào."
                            : preparedInputsPassed
                                ? "Các bán thành phẩm đầu vào đều có định danh và phiên bản nguồn."
                                : "Có bán thành phẩm đầu vào chưa xác định đủ nguồn công thức."
                    }
                ]
            };
        }

        private static decimal? TryNormalizeComponent(
            Models.Drinks.RecipeDetail detail,
            Models.Inventories.Ingredients.Unit? baseUnit)
        {
            if (detail.Unit == null || baseUnit == null)
                return null;

            if (detail.UnitId == baseUnit.UnitId)
                return detail.Quantity;

            return PhysicalUnitConversionRegistry.TryGetPairFactor(
                detail.Unit.UnitCode,
                baseUnit.UnitCode,
                detail.Unit.Type,
                baseUnit.Type,
                out var factor)
                ? detail.Quantity * factor
                : null;
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

        private static string NormalizeDataHealthTypeFilter(string? typeFilter)
        {
            return typeFilter?.Trim().ToUpperInvariant() switch
            {
                "POS" => "POS",
                "TOPPING" => "TOPPING",
                "SUBRECIPE" => "SUBRECIPE",
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
