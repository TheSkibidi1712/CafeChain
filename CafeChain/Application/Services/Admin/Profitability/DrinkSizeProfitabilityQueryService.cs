using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Profitability
{
    public sealed class DrinkSizeProfitabilityQueryService : IDrinkSizeProfitabilityQueryService
    {
        private readonly AppDbContext _context;
        private readonly IDrinkSizeRecipeResolver _resolver;
        private readonly IUnitConversionService _unitConversion;
        private readonly IPhysicalUnitConversionService _physicalConversion;
        private readonly IEstimatedBomCostService _estimatedBomCost;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly ICurrentRecipeResolver _currentRecipeResolver;

        public DrinkSizeProfitabilityQueryService(AppDbContext context, IDrinkSizeRecipeResolver resolver,
            IUnitConversionService unitConversion, IPhysicalUnitConversionService physicalConversion,
            IEstimatedBomCostService estimatedBomCost, IScopeAuthorizationService scopeAuthorization,
            ICurrentRecipeResolver? currentRecipeResolver = null)
        {
            _context = context;
            _resolver = resolver;
            _unitConversion = unitConversion;
            _physicalConversion = physicalConversion;
            _estimatedBomCost = estimatedBomCost;
            _scopeAuthorization = scopeAuthorization;
            _currentRecipeResolver = currentRecipeResolver ?? new CurrentRecipeResolver(context);
        }

        public async Task<ServiceResult<DrinkProfitabilityPreviewDto>> PreviewAsync(int storeId, int drinkId, DateTime asOfUtc, int actorStaffId, CancellationToken cancellationToken = default)
        {
            if (!await CanViewAsync(actorStaffId, storeId, cancellationToken))
                return ServiceResult<DrinkProfitabilityPreviewDto>.Failure("Bạn không có quyền xem giá vốn tại cửa hàng này.", errorCode: "PROFITABILITY_FORBIDDEN");

            var store = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(x => x.StoreId == storeId && x.Active, cancellationToken);
            var drink = await _context.Drinks.AsNoTracking().FirstOrDefaultAsync(x => x.DrinkId == drinkId && x.Active, cancellationToken);
            if (store == null || drink == null)
                return ServiceResult<DrinkProfitabilityPreviewDto>.Failure("Cửa hàng hoặc đồ uống không tồn tại/đã ngừng hoạt động.");

            var sizes = await _context.DrinkSizes.AsNoTracking().Where(x => x.DrinkId == drinkId && x.Active)
                .Include(x => x.Size).OrderBy(x => x.Size.Name).ToListAsync(cancellationToken);
            var rows = new List<DrinkSizeProfitabilityRowDto>(sizes.Count);
            foreach (var size in sizes)
                rows.Add(await BuildSizeAsync(storeId, drinkId, size, asOfUtc, cancellationToken));

            return ServiceResult<DrinkProfitabilityPreviewDto>.Success(new DrinkProfitabilityPreviewDto
            {
                StoreId = storeId, StoreName = store.Name, DrinkId = drinkId, DrinkName = drink.Name,
                CostTimestampUtc = asOfUtc, Sizes = rows
            });
        }

        private async Task<DrinkSizeProfitabilityRowDto> BuildSizeAsync(int storeId, int drinkId, DrinkSize size, DateTime asOfUtc, CancellationToken ct)
        {
            var resolution = await _resolver.ResolveExactAsync(drinkId, size.SizeId, asOfUtc, ct);
            if (!resolution.IsReady)
                return EmptyRow(size, resolution.Status, resolution.Message, resolution.Recipe);

            var components = new List<FifoCostComponentDto>();
            await AddRecipeComponentsAsync(storeId, resolution.Recipe!, 1m, ProfitabilityComponentSources.BaseBom, components, ct);

            var configuredPolicies = await _context.DrinkSizeToppingPolicies.AsNoTracking()
                .Where(x => x.DrinkSizeId == size.DrinkSizeId && x.IsActive)
                .Include(x => x.Topping).OrderBy(x => x.Topping.Name).ToListAsync(ct);
            var activePolicies = configuredPolicies.Where(x => x.IsDefaultSelected).ToList();
            var legacyDefaultIds = await _context.DrinkDefaultToppings.AsNoTracking()
                .Where(x => x.DrinkId == drinkId).Select(x => x.ToppingId).ToListAsync(ct);
            var missingPolicies = legacyDefaultIds.Except(configuredPolicies.Select(x => x.ToppingId)).ToList();

            var toppingRows = new List<ProfitabilityToppingPolicyDto>();
            decimal toppingPriceImpact = 0m;
            foreach (var policy in activePolicies)
            {
                var componentStart = components.Count;
                var before = components.Sum(x => x.KnownCost);
                if (policy.CostTreatment == ToppingCostTreatments.AddToppingRecipeCost)
                {
                    var toppingRecipe = await ResolveToppingRecipeAsync(policy.ToppingId, asOfUtc, ct);
                    if (toppingRecipe == null)
                    {
                        components.Add(MissingComponent(ProfitabilityComponentSources.DefaultTopping, ProfitabilityComponentTypes.Topping, policy.ToppingId, policy.Topping.Name,
                            policy.QuantityPerDrink, ProfitabilityCostStatuses.MissingRecipe, "Topping chưa có BOM active hiệu lực."));
                    }
                    else
                    {
                        await AddRecipeComponentsAsync(storeId, toppingRecipe, policy.QuantityPerDrink, ProfitabilityComponentSources.DefaultTopping, components, ct);
                    }
                }

                var priceImpact = policy.PriceTreatment == ToppingPriceTreatments.AddToppingPrice
                    ? policy.Topping.Price * policy.QuantityPerDrink
                    : 0m;
                toppingPriceImpact += priceImpact;
                var added = components.Sum(x => x.KnownCost) - before;
                var addedIncomplete = components.Skip(componentStart)
                    .Any(x => x.Status != ProfitabilityCostStatuses.Complete);
                toppingRows.Add(new ProfitabilityToppingPolicyDto
                {
                    ToppingId = policy.ToppingId, ToppingName = policy.Topping.Name,
                    QuantityPerDrink = policy.QuantityPerDrink, PriceTreatment = policy.PriceTreatment,
                    CostTreatment = policy.CostTreatment, PriceImpact = priceImpact,
                    CostImpact = addedIncomplete ? null : added,
                    CostStatus = addedIncomplete ? ProfitabilityCostStatuses.Incomplete : ProfitabilityCostStatuses.Complete
                });
            }

            var componentIncomplete = components.Any(x => x.Status != ProfitabilityCostStatuses.Complete);
            var knownCost = components.Sum(x => x.KnownCost);
            var costStatus = missingPolicies.Count > 0 ? ProfitabilityCostStatuses.MissingDefaultToppingPolicy
                : componentIncomplete ? ProfitabilityCostStatuses.Incomplete : ProfitabilityCostStatuses.Complete;
            var costMessage = missingPolicies.Count > 0
                ? $"Có {missingPolicies.Count} cấu hình topping cũ cần xác nhận trước khi tính giá vốn."
                : componentIncomplete ? "Giá vốn FIFO chưa đầy đủ; không được xem phần thiếu là 0." : "Giá vốn FIFO đầy đủ.";
            var estimatedCost = costStatus == ProfitabilityCostStatuses.Complete ? knownCost : (decimal?)null;
            var effectiveSellingPrice = size.Price + toppingPriceImpact;
            var profit = estimatedCost.HasValue ? effectiveSellingPrice - estimatedCost.Value : (decimal?)null;
            var bomCost = await _estimatedBomCost.CalculateRecipeEstimatedCostAsync(resolution.Recipe!.RecipeId);

            return new DrinkSizeProfitabilityRowDto
            {
                DrinkSizeId = size.DrinkSizeId, SizeId = size.SizeId, SizeName = size.Size?.Name ?? $"Size #{size.SizeId}",
                RecipeId = resolution.Recipe.RecipeId, RecipeCode = resolution.Recipe.RecipeCode,
                RecipeEffectiveDate = resolution.Recipe.EffectiveDate, RecipeStatus = resolution.Status,
                CostStatus = costStatus, CostMessage = costMessage, KnownCost = knownCost, EstimatedCost = estimatedCost,
                BomConfigurationCost = bomCost.IsComplete ? bomCost.TotalCost : null,
                BomConfigurationCostStatus = bomCost.IsComplete ? ProfitabilityCostStatuses.Complete : ProfitabilityCostStatuses.Incomplete,
                CurrentGlobalPrice = size.Price, DefaultToppingPriceImpact = toppingPriceImpact,
                EffectiveSellingPrice = effectiveSellingPrice, GrossProfit = profit,
                GrossMarginPercent = profit.HasValue && effectiveSellingPrice > 0 ? decimal.Round(profit.Value / effectiveSellingPrice * 100m, 2) : null,
                MarkupPercent = profit.HasValue && estimatedCost > 0 ? decimal.Round(profit.Value / estimatedCost.Value * 100m, 2) : null,
                RowVersion = size.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(size.RowVersion),
                Components = components, DefaultToppings = toppingRows,
                CostSections = BuildCostSections(components, missingPolicies.Count)
            };
        }

        private async Task AddRecipeComponentsAsync(int storeId, Recipe recipe, decimal multiplier, string source, List<FifoCostComponentDto> rows, CancellationToken ct)
        {
            foreach (var detail in recipe.RecipeDetails)
            {
                if (detail.IngredientId.HasValue)
                {
                    var ingredient = await _context.Ingredients.AsNoTracking().Include(x => x.BaseUnit)
                        .FirstOrDefaultAsync(x => x.IngredientId == detail.IngredientId.Value, ct);
                    var converted = ingredient == null ? null : await _unitConversion.ConvertAsync(ingredient.IngredientId, detail.Quantity * multiplier, detail.UnitId);
                    if (ingredient == null || converted == null || !converted.IsSuccess)
                    {
                        rows.Add(MissingComponent(source, ProfitabilityComponentTypes.Ingredient, detail.IngredientId.Value, ingredient?.Name ?? $"Nguyên liệu #{detail.IngredientId}",
                            detail.Quantity * multiplier, ProfitabilityCostStatuses.MissingConversion, converted?.Message ?? "Nguyên liệu không tồn tại."));
                        continue;
                    }
                    rows.Add(await SimulateAsync(storeId, ingredient.IngredientId, null, ingredient.Name, ingredient.BaseUnit?.Name, converted.Data, source, ct));
                }
                else if (detail.ChildRecipeId.HasValue)
                {
                    var child = await _context.Recipes.AsNoTracking().Include(x => x.PreparedItem).ThenInclude(x => x!.BaseUnit)
                        .FirstOrDefaultAsync(x => x.RecipeId == detail.ChildRecipeId.Value, ct);
                    if (child?.PreparedItem == null)
                    {
                        rows.Add(MissingComponent(source, ProfitabilityComponentTypes.PreparedItem, detail.ChildRecipeId.Value, $"Công thức con #{detail.ChildRecipeId}",
                            detail.Quantity * multiplier, ProfitabilityCostStatuses.InvalidBom, "Công thức con chưa liên kết với bán thành phẩm tồn kho."));
                        continue;
                    }
                    var converted = await _physicalConversion.ConvertAsync(detail.Quantity * multiplier, detail.UnitId, child.PreparedItem.BaseUnitId);
                    if (!converted.IsSuccess)
                    {
                        rows.Add(MissingComponent(source, ProfitabilityComponentTypes.PreparedItem, child.PreparedItemId!.Value, child.PreparedItem.Name,
                            detail.Quantity * multiplier, ProfitabilityCostStatuses.MissingConversion, converted.Message));
                        continue;
                    }
                    rows.Add(await SimulateAsync(storeId, null, child.PreparedItemId, child.PreparedItem.Name, child.PreparedItem.BaseUnit?.Name, converted.Data, source, ct));
                }
            }
        }

        private async Task<FifoCostComponentDto> SimulateAsync(int storeId, int? ingredientId, int? preparedItemId, string name, string? unitName, decimal required, string source, CancellationToken ct)
        {
            var layers = await _context.InventoryCostLayers.AsNoTracking()
                .Where(x => x.StoreId == storeId && x.IngredientId == ingredientId && x.PreparedItemId == preparedItemId && x.RemainingQuantity > 0)
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.InventoryCostLayerId)
                .Select(x => new { x.RemainingQuantity, x.UnitCost }).ToListAsync(ct);
            var remaining = required;
            decimal covered = 0, cost = 0;
            foreach (var layer in layers)
            {
                var take = Math.Min(remaining, layer.RemainingQuantity);
                covered += take; cost += take * layer.UnitCost; remaining -= take;
                if (remaining <= 0) break;
            }
            var status = covered <= 0 ? ProfitabilityCostStatuses.MissingCostLayer
                : remaining > 0 ? ProfitabilityCostStatuses.InsufficientCostQuantity : ProfitabilityCostStatuses.Complete;
            return new FifoCostComponentDto
            {
                Source = source,
                SourceLabel = source == ProfitabilityComponentSources.DefaultTopping ? "Topping mặc định" : "Định mức nguyên liệu (BOM)",
                ItemType = ingredientId.HasValue ? ProfitabilityComponentTypes.Ingredient : ProfitabilityComponentTypes.PreparedItem,
                ItemTypeLabel = ingredientId.HasValue ? "Nguyên liệu" : "Bán thành phẩm",
                ItemId = ingredientId ?? preparedItemId!.Value, ItemName = name, RequiredQuantity = required,
                AvailableCostQuantity = layers.Sum(x => x.RemainingQuantity), CoveredQuantity = covered,
                MissingQuantity = Math.Max(0, remaining), KnownCost = cost, UnitName = unitName ?? string.Empty,
                Status = status, Message = status == ProfitabilityCostStatuses.Complete ? "Đủ lớp giá FIFO."
                    : status == ProfitabilityCostStatuses.MissingCostLayer ? "Chưa có lớp giá FIFO tại cửa hàng." : "Lớp giá FIFO không đủ số lượng cần thiết."
            };
        }

        private async Task<Recipe?> ResolveToppingRecipeAsync(
            int toppingId,
            DateTime asOfUtc,
            CancellationToken ct)
        {
            var resolution = await _currentRecipeResolver.ResolveAsync(
                new RecipeTarget.Topping(toppingId),
                asOfUtc,
                ct);
            if (resolution.Status != CurrentRecipeResolutionStatus.Found
                || resolution.Recipe == null)
            {
                return null;
            }

            return await _context.Recipes.AsNoTracking()
                .Include(recipe => recipe.RecipeDetails)
                .SingleOrDefaultAsync(
                    recipe => recipe.RecipeId == resolution.Recipe.RecipeId,
                    ct);
        }

        private async Task<bool> CanViewAsync(int staffId, int storeId, CancellationToken ct)
        {
            var roles = await _context.Staffs.AsNoTracking().Where(x => x.StaffId == staffId && x.Active)
                .SelectMany(x => x.Account.AccountRoles.Where(ar => ar.Role.Active).Select(ar => ar.Role.Name)).ToListAsync(ct);
            if (roles.Contains(RoleConstants.BusinessOwner) || roles.Contains(RoleConstants.AccountantWarehouse) || roles.Contains(RoleConstants.SystemAdmin)) return true;
            if (roles.Contains(RoleConstants.AreaManager)) return await _scopeAuthorization.CanAccessStoreAsync(staffId, storeId);
            if (roles.Contains(RoleConstants.StoreManager))
            {
                var ownsStore = await _context.Staffs.AsNoTracking().AnyAsync(x => x.StaffId == staffId && x.StoreId == storeId, ct);
                return ownsStore || await _scopeAuthorization.CanAccessStoreAsync(staffId, storeId);
            }
            return false;
        }

        private static FifoCostComponentDto MissingComponent(string source, string type, int id, string name, decimal required, string status, string message) => new()
        {
            Source = source,
            SourceLabel = source == ProfitabilityComponentSources.DefaultTopping ? "Topping mặc định" : "Định mức nguyên liệu (BOM)",
            ItemType = type,
            ItemTypeLabel = type == ProfitabilityComponentTypes.Ingredient ? "Nguyên liệu"
                : type == ProfitabilityComponentTypes.PreparedItem ? "Bán thành phẩm" : "Topping",
            ItemId = id,
            ItemName = name,
            RequiredQuantity = required,
            MissingQuantity = required,
            Status = status,
            Message = message
        };

        private static IReadOnlyList<CostSectionCompletenessDto> BuildCostSections(
            IReadOnlyList<FifoCostComponentDto> components,
            int legacyPolicyCount)
        {
            static CostSectionCompletenessDto Section(string code, string label, bool complete, string completeMessage, string incompleteMessage) => new()
            {
                Section = code,
                Label = label,
                Status = complete ? ProfitabilityCostStatuses.Complete : ProfitabilityCostStatuses.Incomplete,
                Message = complete ? completeMessage : incompleteMessage
            };

            var baseComponents = components.Where(x => x.Source == ProfitabilityComponentSources.BaseBom).ToList();
            var directIngredients = baseComponents.Where(x => x.ItemType == ProfitabilityComponentTypes.Ingredient).ToList();
            var preparedItems = baseComponents.Where(x => x.ItemType == ProfitabilityComponentTypes.PreparedItem).ToList();
            var toppingComponents = components.Where(x => x.Source == ProfitabilityComponentSources.DefaultTopping).ToList();
            var conversionComplete = components.All(x => x.Status != ProfitabilityCostStatuses.MissingConversion);

            return new[]
            {
                Section(ProfitabilityCostSections.BaseBom, "BOM cơ sở",
                    directIngredients.All(IsComplete),
                    directIngredients.Count == 0
                        ? "Món không có nguyên liệu trực tiếp ngoài bán thành phẩm."
                        : "Các nguyên liệu trực tiếp trong BOM cơ sở đã có đủ lớp giá FIFO.",
                    "BOM cơ sở còn thiếu dữ liệu giá hoặc số lượng FIFO."),
                Section(ProfitabilityCostSections.PreparedItem, "Bán thành phẩm",
                    preparedItems.All(IsComplete),
                    preparedItems.Count == 0 ? "Món không sử dụng bán thành phẩm." : "Bán thành phẩm đã xác định đơn vị và lớp giá.",
                    "Bán thành phẩm còn thiếu định danh tồn kho, quy đổi hoặc lớp giá."),
                Section(ProfitabilityCostSections.DefaultTopping, "Topping mặc định",
                    legacyPolicyCount == 0 && toppingComponents.All(IsComplete),
                    toppingComponents.Count == 0 ? "Không có topping mặc định cần cộng thêm giá vốn." : "Topping mặc định đã có đủ dữ liệu giá vốn.",
                    legacyPolicyCount > 0
                        ? $"Có {legacyPolicyCount} cấu hình topping cũ cần xác nhận."
                        : "Topping mặc định còn thiếu công thức, quy đổi hoặc lớp giá."),
                Section(ProfitabilityCostSections.UnitConversion, "Đơn vị và quy đổi",
                    conversionComplete,
                    "Các định lượng đã quy đổi được về đơn vị tồn kho.",
                    "Có định lượng chưa quy đổi được về đơn vị tồn kho.")
            };
        }

        private static bool IsComplete(FifoCostComponentDto component) =>
            component.Status == ProfitabilityCostStatuses.Complete;

        private static DrinkSizeProfitabilityRowDto EmptyRow(DrinkSize size, string status, string message, Recipe? recipe) => new()
        {
            DrinkSizeId = size.DrinkSizeId, SizeId = size.SizeId, SizeName = size.Size?.Name ?? $"Size #{size.SizeId}",
            RecipeId = recipe?.RecipeId, RecipeCode = recipe?.RecipeCode, RecipeEffectiveDate = recipe?.EffectiveDate,
            RecipeStatus = status, CostStatus = status, CostMessage = message, CurrentGlobalPrice = size.Price,
            EffectiveSellingPrice = size.Price, RowVersion = size.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(size.RowVersion)
        };
    }
}
