using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.StoreMenu
{
    public sealed class StoreMenuAvailabilityEvaluator : IStoreMenuAvailabilityEvaluator
    {
        private readonly AppDbContext _context;
        private readonly IDrinkSizeRecipeResolver _recipeResolver;
        private readonly IUnitConversionService _unitConversion;
        private readonly IPhysicalUnitConversionService _physicalConversion;
        private readonly ICurrentRecipeResolver _currentRecipeResolver;

        public StoreMenuAvailabilityEvaluator(
            AppDbContext context,
            IDrinkSizeRecipeResolver recipeResolver,
            IUnitConversionService unitConversion,
            IPhysicalUnitConversionService physicalConversion,
            ICurrentRecipeResolver? currentRecipeResolver = null)
        {
            _context = context;
            _recipeResolver = recipeResolver;
            _unitConversion = unitConversion;
            _physicalConversion = physicalConversion;
            _currentRecipeResolver = currentRecipeResolver ?? new CurrentRecipeResolver(context);
        }

        public async Task<StoreMenuAvailabilityDto> EvaluateAsync(
            int storeId,
            int drinkSizeId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default)
        {
            var item = await _context.StoreMenuItems.AsNoTracking()
                .Include(x => x.Store)
                .Include(x => x.DrinkSize).ThenInclude(x => x.Drink)
                .Include(x => x.DrinkSize).ThenInclude(x => x.Size)
                .SingleOrDefaultAsync(x => x.StoreId == storeId && x.DrinkSizeId == drinkSizeId, cancellationToken);

            if (item == null)
                return Missing(storeId, drinkSizeId);

            var configuredStatus = item.GetConfiguredStatus(asOfUtc);
            if (!item.Store.Active || !item.DrinkSize.Active || !item.DrinkSize.Drink.Active || !item.DrinkSize.Size.Active)
                return Result(item, configuredStatus, StoreMenuAvailabilityStatuses.Unknown,
                    "SKU hoặc dữ liệu đồ uống/size đã ngừng hoạt động.");

            var resolution = await _recipeResolver.ResolveExactAsync(
                item.DrinkSize.DrinkId,
                item.DrinkSize.SizeId,
                asOfUtc,
                cancellationToken);
            if (!resolution.IsReady)
                return Result(item, configuredStatus, StoreMenuAvailabilityStatuses.RecipeInvalid, resolution.Message);

            var writerMode = await _context.StoreInventoryWriterConfigurations.AsNoTracking()
                .Where(x => x.StoreId == storeId)
                .Select(x => (InventoryWriterMode?)x.WriterMode)
                .SingleOrDefaultAsync(cancellationToken) ?? InventoryWriterMode.LegacyRecipe;

            if (writerMode == InventoryWriterMode.Blocked
                && resolution.Recipe!.RecipeDetails.Any(x => x.ChildRecipeId.HasValue))
            {
                return Result(item, configuredStatus, StoreMenuAvailabilityStatuses.StoreNotReady,
                    "Kho bán thành phẩm của cửa hàng đang bị khóa.");
            }

            var requirements = new List<StockRequirement>();
            var mainBuild = await AddRecipeRequirementsAsync(
                resolution.Recipe!, 1m, storeId, writerMode, false, requirements, cancellationToken);
            if (!mainBuild.Success)
                return Result(item, configuredStatus, mainBuild.Status, mainBuild.Message);

            var requiredPolicies = await _context.DrinkSizeToppingPolicies.AsNoTracking()
                .Include(x => x.Topping)
                .Where(x => x.DrinkSizeId == drinkSizeId && x.IsActive && x.IsRequired)
                .OrderBy(x => x.ToppingId)
                .ToListAsync(cancellationToken);

            foreach (var policy in requiredPolicies)
            {
                if (!policy.IsDefaultSelected || !policy.Topping.Active
                    || !await _context.StoreToppings.AsNoTracking().AnyAsync(
                        x => x.StoreId == storeId && x.ToppingId == policy.ToppingId && x.Active,
                        cancellationToken))
                {
                    return Result(item, configuredStatus, StoreMenuAvailabilityStatuses.ToppingUnavailable,
                        $"Topping bắt buộc '{policy.Topping.Name}' không khả dụng tại cửa hàng.");
                }

                var toppingRecipe = await ResolveToppingRecipeAsync(policy.ToppingId, asOfUtc, cancellationToken);
                if (toppingRecipe == null)
                    return Result(item, configuredStatus, StoreMenuAvailabilityStatuses.ToppingUnavailable,
                        $"Topping bắt buộc '{policy.Topping.Name}' chưa có công thức đang áp dụng hợp lệ.");

                if (writerMode == InventoryWriterMode.Blocked
                    && toppingRecipe.RecipeDetails.Any(x => x.ChildRecipeId.HasValue))
                {
                    return Result(item, configuredStatus, StoreMenuAvailabilityStatuses.StoreNotReady,
                        "Kho bán thành phẩm của cửa hàng đang bị khóa.");
                }

                var toppingBuild = await AddRecipeRequirementsAsync(
                    toppingRecipe,
                    policy.QuantityPerDrink,
                    storeId,
                    writerMode,
                    true,
                    requirements,
                    cancellationToken);
                if (!toppingBuild.Success)
                {
                    var status = toppingBuild.Status == StoreMenuAvailabilityStatuses.StoreNotReady
                        ? toppingBuild.Status
                        : StoreMenuAvailabilityStatuses.ToppingUnavailable;
                    return Result(item, configuredStatus, status,
                        $"Topping bắt buộc '{policy.Topping.Name}' không khả dụng: {toppingBuild.Message}");
                }
            }

            var lowWarnings = new List<string>();
            foreach (var group in requirements.GroupBy(x => x.Inventory.StoreInventoryId))
            {
                var inventory = group.First().Inventory;
                var required = group.Sum(x => x.RequiredQuantity);
                var usable = inventory.AvailableQty - inventory.ReservedQty;
                if (usable < required)
                {
                    var toppingRelated = group.Any(x => x.IsRequiredTopping);
                    return Result(
                        item,
                        configuredStatus,
                        toppingRelated
                            ? StoreMenuAvailabilityStatuses.ToppingUnavailable
                            : StoreMenuAvailabilityStatuses.OutOfStock,
                        toppingRelated
                            ? "Topping bắt buộc không đủ tồn khả dụng."
                            : "Không đủ nguyên liệu khả dụng cho SKU này.");
                }

                if (inventory.MinStockLevel.HasValue
                    && usable - required <= inventory.MinStockLevel.Value)
                {
                    lowWarnings.Add($"Tồn khả dụng sau một sản phẩm sẽ chạm ngưỡng tối thiểu ({inventory.MinStockLevel.Value:N3}).");
                }
            }

            if (lowWarnings.Count > 0)
                return Result(item, configuredStatus, StoreMenuAvailabilityStatuses.LowStock,
                    "Nguyên liệu sắp chạm ngưỡng tồn tối thiểu.", lowWarnings);

            return Result(item, configuredStatus, StoreMenuAvailabilityStatuses.Available,
                "SKU đủ điều kiện bán tại cửa hàng.");
        }

        public async Task<IReadOnlyDictionary<int, StoreMenuAvailabilityDto>> EvaluateStoreAsync(
            int storeId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default)
        {
            var drinkSizeIds = await _context.StoreMenuItems.AsNoTracking()
                .Where(x => x.StoreId == storeId)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.DrinkSizeId)
                .Select(x => x.DrinkSizeId)
                .ToListAsync(cancellationToken);

            var result = new Dictionary<int, StoreMenuAvailabilityDto>(drinkSizeIds.Count);
            foreach (var drinkSizeId in drinkSizeIds)
                result[drinkSizeId] = await EvaluateAsync(storeId, drinkSizeId, asOfUtc, cancellationToken);
            return result;
        }

        private async Task<RequirementBuildResult> AddRecipeRequirementsAsync(
            Recipe recipe,
            decimal multiplier,
            int storeId,
            InventoryWriterMode writerMode,
            bool isRequiredTopping,
            ICollection<StockRequirement> requirements,
            CancellationToken cancellationToken)
        {
            foreach (var detail in recipe.RecipeDetails.OrderBy(x => x.RecipeDetailId))
            {
                var rawRequired = detail.Quantity * multiplier;
                if (detail.IngredientId.HasValue)
                {
                    var converted = await _unitConversion.ConvertAsync(
                        detail.IngredientId.Value,
                        rawRequired,
                        detail.UnitId);
                    if (!converted.IsSuccess)
                        return RequirementBuildResult.Fail(StoreMenuAvailabilityStatuses.RecipeInvalid,
                            converted.Message ?? "Không quy đổi được đơn vị nguyên liệu.");

                    var inventory = await _context.StoreInventories.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.StoreId == storeId
                            && x.IngredientId == detail.IngredientId.Value
                            && x.SupersededByStoreInventoryId == null,
                            cancellationToken);
                    if (inventory == null)
                        return RequirementBuildResult.Fail(StoreMenuAvailabilityStatuses.OutOfStock,
                            "Chưa có tồn kho nguyên liệu tại cửa hàng.");

                    requirements.Add(new StockRequirement(inventory, converted.Data, isRequiredTopping));
                    continue;
                }

                if (!detail.ChildRecipeId.HasValue)
                    return RequirementBuildResult.Fail(StoreMenuAvailabilityStatuses.RecipeInvalid,
                        $"Dòng BOM #{detail.RecipeDetailId} không có identity hợp lệ.");

                if (writerMode == InventoryWriterMode.Blocked)
                    return RequirementBuildResult.Fail(StoreMenuAvailabilityStatuses.StoreNotReady,
                        "Kho bán thành phẩm của cửa hàng đang bị khóa.");

                if (writerMode == InventoryWriterMode.LegacyRecipe)
                {
                    var rows = await _context.StoreInventories.AsNoTracking()
                        .Where(x => x.StoreId == storeId
                            && x.RecipeId == detail.ChildRecipeId.Value
                            && x.IngredientId == null
                            && x.SupersededByStoreInventoryId == null)
                        .ToListAsync(cancellationToken);
                    if (rows.Count == 0)
                        return RequirementBuildResult.Fail(StoreMenuAvailabilityStatuses.OutOfStock,
                            "Chưa có tồn kho bán thành phẩm tại cửa hàng.");
                    if (rows.Count > 1)
                        return RequirementBuildResult.Fail(StoreMenuAvailabilityStatuses.StoreNotReady,
                            "Tồn kho bán thành phẩm đang trùng identity.");

                    requirements.Add(new StockRequirement(rows[0], rawRequired, isRequiredTopping));
                    continue;
                }

                var child = detail.ChildRecipe;
                var preparedItem = child?.PreparedItem;
                if (preparedItem == null || !preparedItem.Active)
                    return RequirementBuildResult.Fail(StoreMenuAvailabilityStatuses.StoreNotReady,
                        "Công thức đầu vào chưa liên kết với bán thành phẩm đang hoạt động.");

                var convertedPrepared = await _physicalConversion.ConvertAsync(
                    rawRequired,
                    detail.UnitId,
                    preparedItem.BaseUnitId);
                if (!convertedPrepared.IsSuccess)
                    return RequirementBuildResult.Fail(StoreMenuAvailabilityStatuses.StoreNotReady,
                        convertedPrepared.Message ?? "Không quy đổi được đơn vị bán thành phẩm.");

                var preparedRows = await _context.StoreInventories.AsNoTracking()
                    .Where(x => x.StoreId == storeId
                        && x.PreparedItemId == preparedItem.PreparedItemId
                        && x.SupersededByStoreInventoryId == null
                        && x.BtpIdentityState != BtpIdentityState.Superseded)
                    .ToListAsync(cancellationToken);
                if (preparedRows.Count == 0)
                    return RequirementBuildResult.Fail(StoreMenuAvailabilityStatuses.OutOfStock,
                        "Chưa có tồn kho bán thành phẩm tại cửa hàng.");
                if (preparedRows.Count > 1)
                    return RequirementBuildResult.Fail(StoreMenuAvailabilityStatuses.StoreNotReady,
                        "Bán thành phẩm có nhiều bản ghi tồn kho chưa được xử lý.");

                requirements.Add(new StockRequirement(preparedRows[0], convertedPrepared.Data, isRequiredTopping));
            }

            return RequirementBuildResult.Ok();
        }

        private async Task<Recipe?> ResolveToppingRecipeAsync(
            int toppingId,
            DateTime asOfUtc,
            CancellationToken cancellationToken)
        {
            var resolution = await _currentRecipeResolver.ResolveAsync(
                new RecipeTarget.Topping(toppingId),
                asOfUtc,
                cancellationToken);
            if (resolution.Status != CurrentRecipeResolutionStatus.Found
                || resolution.Recipe == null)
            {
                return null;
            }

            var recipe = await _context.Recipes.AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.RecipeDetails).ThenInclude(x => x.ChildRecipe).ThenInclude(x => x!.PreparedItem)
                .SingleOrDefaultAsync(
                    x => x.RecipeId == resolution.Recipe.RecipeId,
                    cancellationToken);
            return recipe?.RecipeDetails.Count > 0 ? recipe : null;
        }

        private static StoreMenuAvailabilityDto Missing(int storeId, int drinkSizeId) => new()
        {
            StoreId = storeId,
            DrinkSizeId = drinkSizeId,
            ConfiguredStatus = StoreMenuConfiguredStatuses.Draft,
            OperationalStatus = StoreMenuAvailabilityStatuses.Unknown,
            Reason = "SKU chưa được cấu hình trong menu cửa hàng.",
            IsSellable = false
        };

        private static StoreMenuAvailabilityDto Result(
            StoreMenuItem item,
            string configuredStatus,
            string operationalStatus,
            string reason,
            IReadOnlyList<string>? warnings = null) => new()
        {
            StoreId = item.StoreId,
            StoreMenuItemId = item.StoreMenuItemId,
            DrinkSizeId = item.DrinkSizeId,
            ConfiguredStatus = configuredStatus,
            OperationalStatus = operationalStatus,
            Reason = reason,
            IsSellable = configuredStatus == StoreMenuConfiguredStatuses.Active
                && StoreMenuAvailabilityStatuses.IsSellable(operationalStatus),
            Warnings = warnings ?? Array.Empty<string>()
        };

        private sealed record StockRequirement(
            StoreInventory Inventory,
            decimal RequiredQuantity,
            bool IsRequiredTopping);

        private sealed record RequirementBuildResult(bool Success, string Status, string Message)
        {
            public static RequirementBuildResult Ok() => new(true, string.Empty, string.Empty);
            public static RequirementBuildResult Fail(string status, string message) => new(false, status, message);
        }
    }
}
