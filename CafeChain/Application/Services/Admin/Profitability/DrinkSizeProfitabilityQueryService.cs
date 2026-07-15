using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
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

        public DrinkSizeProfitabilityQueryService(AppDbContext context, IDrinkSizeRecipeResolver resolver,
            IUnitConversionService unitConversion, IPhysicalUnitConversionService physicalConversion,
            IEstimatedBomCostService estimatedBomCost, IScopeAuthorizationService scopeAuthorization)
        {
            _context = context;
            _resolver = resolver;
            _unitConversion = unitConversion;
            _physicalConversion = physicalConversion;
            _estimatedBomCost = estimatedBomCost;
            _scopeAuthorization = scopeAuthorization;
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
            await AddRecipeComponentsAsync(storeId, resolution.Recipe!, 1m, "DRINK_RECIPE", components, ct);

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
                        components.Add(MissingComponent("DEFAULT_TOPPING", "Topping", policy.ToppingId, policy.Topping.Name,
                            policy.QuantityPerDrink, ProfitabilityCostStatuses.MissingRecipe, "Topping chưa có BOM active hiệu lực."));
                    }
                    else
                    {
                        await AddRecipeComponentsAsync(storeId, toppingRecipe, policy.QuantityPerDrink, "DEFAULT_TOPPING", components, ct);
                    }
                }

                var priceImpact = policy.CostTreatment != ToppingCostTreatments.DisplayOnly
                    && policy.PriceTreatment == ToppingPriceTreatments.AddToppingPrice
                        ? policy.Topping.Price
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
                ? $"Thiếu chính sách cho {missingPolicies.Count} topping mặc định legacy."
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
                Components = components, DefaultToppings = toppingRows
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
                        rows.Add(MissingComponent(source, "Ingredient", detail.IngredientId.Value, ingredient?.Name ?? $"Ingredient #{detail.IngredientId}",
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
                        rows.Add(MissingComponent(source, "PreparedItem", detail.ChildRecipeId.Value, $"ChildRecipe #{detail.ChildRecipeId}",
                            detail.Quantity * multiplier, ProfitabilityCostStatuses.InvalidBom, "ChildRecipe chưa map PreparedItem."));
                        continue;
                    }
                    var converted = await _physicalConversion.ConvertAsync(detail.Quantity * multiplier, detail.UnitId, child.PreparedItem.BaseUnitId);
                    if (!converted.IsSuccess)
                    {
                        rows.Add(MissingComponent(source, "PreparedItem", child.PreparedItemId!.Value, child.PreparedItem.Name,
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
                Source = source, ItemType = ingredientId.HasValue ? "Ingredient" : "PreparedItem",
                ItemId = ingredientId ?? preparedItemId!.Value, ItemName = name, RequiredQuantity = required,
                AvailableCostQuantity = layers.Sum(x => x.RemainingQuantity), CoveredQuantity = covered,
                MissingQuantity = Math.Max(0, remaining), KnownCost = cost, UnitName = unitName ?? string.Empty,
                Status = status, Message = status == ProfitabilityCostStatuses.Complete ? "Đủ lớp giá FIFO."
                    : status == ProfitabilityCostStatuses.MissingCostLayer ? "Chưa có lớp giá FIFO tại cửa hàng." : "Lớp giá FIFO không đủ số lượng cần thiết."
            };
        }

        private async Task<Recipe?> ResolveToppingRecipeAsync(int toppingId, DateTime asOfUtc, CancellationToken ct) => await _context.Recipes.AsNoTracking()
            .Include(x => x.RecipeDetails)
            .Where(x => x.ToppingId == toppingId && x.DrinkId == null && x.Active && x.Status == "Active"
                && (!x.EffectiveDate.HasValue || x.EffectiveDate.Value <= asOfUtc))
            .OrderByDescending(x => x.EffectiveDate ?? DateTime.MinValue).ThenByDescending(x => x.RecipeId)
            .FirstOrDefaultAsync(ct);

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
        { Source = source, ItemType = type, ItemId = id, ItemName = name, RequiredQuantity = required, MissingQuantity = required, Status = status, Message = message };

        private static DrinkSizeProfitabilityRowDto EmptyRow(DrinkSize size, string status, string message, Recipe? recipe) => new()
        {
            DrinkSizeId = size.DrinkSizeId, SizeId = size.SizeId, SizeName = size.Size?.Name ?? $"Size #{size.SizeId}",
            RecipeId = recipe?.RecipeId, RecipeCode = recipe?.RecipeCode, RecipeEffectiveDate = recipe?.EffectiveDate,
            RecipeStatus = status, CostStatus = status, CostMessage = message, CurrentGlobalPrice = size.Price,
            EffectiveSellingPrice = size.Price, RowVersion = size.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(size.RowVersion)
        };
    }
}
