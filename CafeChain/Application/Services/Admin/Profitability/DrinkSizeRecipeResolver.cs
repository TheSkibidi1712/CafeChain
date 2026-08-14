using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Profitability
{
    public sealed class DrinkSizeRecipeResolver : IDrinkSizeRecipeResolver
    {
        private readonly AppDbContext _context;
        private readonly IUnitConversionService _unitConversion;
        private readonly IPhysicalUnitConversionService _physicalConversion;
        private readonly ICurrentRecipeResolver _currentRecipeResolver;

        public DrinkSizeRecipeResolver(
            AppDbContext context,
            IUnitConversionService unitConversion,
            IPhysicalUnitConversionService physicalConversion,
            ICurrentRecipeResolver? currentRecipeResolver = null)
        {
            _context = context;
            _unitConversion = unitConversion;
            _physicalConversion = physicalConversion;
            _currentRecipeResolver = currentRecipeResolver ?? new CurrentRecipeResolver(context);
        }

        public async Task<DrinkSizeRecipeResolution> ResolveExactAsync(int drinkId, int sizeId, DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            var genericFallback = await _context.Recipes.AsNoTracking().AnyAsync(r =>
                r.DrinkId == drinkId && r.SizeId == null && r.ToppingId == null && r.Active && r.Status == "Active", cancellationToken);
            var current = await _currentRecipeResolver.ResolveAsync(
                new RecipeTarget.MenuItemSize(drinkId, sizeId),
                asOfUtc,
                cancellationToken);
            var hasFutureExact = current.Status == CurrentRecipeResolutionStatus.Missing
                && await HasFutureExactAsync(drinkId, sizeId, asOfUtc, cancellationToken);
            return await BuildResolutionAsync(
                current,
                genericFallback,
                hasFutureExact,
                cancellationToken);
        }

        public async Task<IReadOnlyList<DrinkSizeRecipeHealthRow>> GetDataHealthAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            var sizes = await _context.DrinkSizes.AsNoTracking()
                .Where(x => x.Active && x.Drink.Active)
                .OrderBy(x => x.Drink.Name).ThenBy(x => x.Size.Name)
                .Select(x => new { x.DrinkSizeId, x.DrinkId, DrinkName = x.Drink.Name, x.SizeId, SizeName = x.Size.Name })
                .ToListAsync(cancellationToken);

            var rows = new List<DrinkSizeRecipeHealthRow>(sizes.Count);
            var targets = sizes
                .Select(size => (RecipeTarget)new RecipeTarget.MenuItemSize(size.DrinkId, size.SizeId))
                .ToArray();
            var resolutions = await _currentRecipeResolver.ResolveManyAsync(
                targets,
                asOfUtc,
                cancellationToken);
            var drinkIds = sizes.Select(size => size.DrinkId).Distinct().ToArray();
            var genericDrinkIds = (await _context.Recipes.AsNoTracking()
                    .Where(recipe => recipe.DrinkId.HasValue
                        && drinkIds.Contains(recipe.DrinkId.Value)
                        && recipe.SizeId == null
                        && recipe.ToppingId == null
                        && recipe.Active
                        && recipe.Status == "Active")
                    .Select(recipe => recipe.DrinkId!.Value)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .ToHashSet();
            var futurePairs = (await _context.Recipes.AsNoTracking()
                    .Where(recipe => recipe.DrinkId.HasValue
                        && drinkIds.Contains(recipe.DrinkId.Value)
                        && recipe.SizeId.HasValue
                        && recipe.ToppingId == null
                        && recipe.PreparedItemId == null
                        && recipe.Active
                        && recipe.Status == "Active"
                        && recipe.EffectiveDate.HasValue
                        && recipe.EffectiveDate.Value > asOfUtc)
                    .Select(recipe => new { recipe.DrinkId, recipe.SizeId })
                    .ToListAsync(cancellationToken))
                .Select(recipe => (recipe.DrinkId!.Value, recipe.SizeId!.Value))
                .ToHashSet();

            foreach (var size in sizes)
            {
                var target = new RecipeTarget.MenuItemSize(size.DrinkId, size.SizeId);
                var resolution = await BuildResolutionAsync(
                    resolutions[target],
                    genericDrinkIds.Contains(size.DrinkId),
                    futurePairs.Contains((size.DrinkId, size.SizeId)),
                    cancellationToken);
                rows.Add(new DrinkSizeRecipeHealthRow
                {
                    DrinkSizeId = size.DrinkSizeId,
                    DrinkId = size.DrinkId,
                    DrinkName = size.DrinkName,
                    SizeId = size.SizeId,
                    SizeName = size.SizeName,
                    Status = resolution.Status,
                    Message = resolution.Message,
                    RecipeId = resolution.Recipe?.RecipeId,
                    RecipeCode = resolution.Recipe?.RecipeCode,
                    EffectiveDate = resolution.Recipe?.EffectiveDate,
                    HasGenericFallback = resolution.HasGenericFallback
                });
            }
            return rows;
        }

        private async Task<DrinkSizeRecipeResolution> BuildResolutionAsync(
            CurrentRecipeResolution current,
            bool genericFallback,
            bool hasFutureExact,
            CancellationToken cancellationToken)
        {
            if (current.Status == CurrentRecipeResolutionStatus.Ambiguous)
                return Result(DrinkSizeRecipeHealthStatuses.MultipleActiveRecipe, "Có nhiều BOM đang áp dụng cho cùng đồ uống và kích cỡ.", null, 2, genericFallback);

            if (current.Status == CurrentRecipeResolutionStatus.InvalidTarget)
                return Result(DrinkSizeRecipeHealthStatuses.InvalidRecipe, "Đối tượng đồ uống hoặc kích cỡ chưa hợp lệ.", null, 0, genericFallback);

            if (current.Status == CurrentRecipeResolutionStatus.Missing || current.Recipe == null)
            {
                if (hasFutureExact)
                    return Result(DrinkSizeRecipeHealthStatuses.FutureRecipeOnly, "Chỉ có BOM có ngày áp dụng trong tương lai.", null, 1, genericFallback);
                if (genericFallback)
                    return Result(DrinkSizeRecipeHealthStatuses.GenericFallbackOnly, "Chỉ có BOM chung chưa gắn size; màn hình giá vốn không dùng công thức thay thế này.", null, 0, true);
                return Result(DrinkSizeRecipeHealthStatuses.MissingRecipe, "Chưa có BOM chính xác cho size.", null, 0, false);
            }

            var recipe = await RecipeQuery()
                .SingleOrDefaultAsync(candidate => candidate.RecipeId == current.Recipe.RecipeId, cancellationToken);
            if (recipe == null)
                return Result(DrinkSizeRecipeHealthStatuses.MissingRecipe, "Không tải được BOM đang áp dụng.", null, 0, genericFallback);

            var validation = await ValidateRecipeAsync(recipe, cancellationToken);
            if (validation != null)
                return Result(DrinkSizeRecipeHealthStatuses.InvalidRecipe, validation, recipe, 1, genericFallback);

            return Result(DrinkSizeRecipeHealthStatuses.ExactReady, "BOM chính xác theo size đã sẵn sàng.", recipe, 1, genericFallback);
        }

        private Task<bool> HasFutureExactAsync(
            int drinkId,
            int sizeId,
            DateTime asOfUtc,
            CancellationToken cancellationToken)
        {
            return _context.Recipes.AsNoTracking().AnyAsync(recipe =>
                recipe.DrinkId == drinkId
                && recipe.SizeId == sizeId
                && recipe.ToppingId == null
                && recipe.PreparedItemId == null
                && recipe.Active
                && recipe.Status == "Active"
                && recipe.EffectiveDate.HasValue
                && recipe.EffectiveDate.Value > asOfUtc,
                cancellationToken);
        }

        private IQueryable<Recipe> RecipeQuery() => _context.Recipes.AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.RecipeDetails).ThenInclude(d => d.Ingredient).ThenInclude(i => i!.BaseUnit)
            .Include(r => r.RecipeDetails).ThenInclude(d => d.ChildRecipe).ThenInclude(c => c!.PreparedItem).ThenInclude(p => p!.BaseUnit)
            .Include(r => r.RecipeDetails).ThenInclude(d => d.Unit);

        private async Task<string?> ValidateRecipeAsync(Recipe recipe, CancellationToken cancellationToken)
        {
            if (recipe.RecipeDetails == null || recipe.RecipeDetails.Count == 0)
                return "BOM không có thành phần.";

            if (await HasCycleAsync(recipe.RecipeId, recipe.RecipeId, new HashSet<int>(), cancellationToken))
                return "BOM có vòng lặp đệ quy.";

            foreach (var detail in recipe.RecipeDetails)
            {
                if (detail.Quantity <= 0 || detail.UnitId <= 0 || detail.IngredientId.HasValue == detail.ChildRecipeId.HasValue)
                    return $"Dòng BOM #{detail.RecipeDetailId} có identity, đơn vị hoặc định lượng không hợp lệ.";

                if (detail.IngredientId.HasValue)
                {
                    var converted = await _unitConversion.ConvertAsync(detail.IngredientId.Value, detail.Quantity, detail.UnitId);
                    if (!converted.IsSuccess)
                        return $"Dòng BOM #{detail.RecipeDetailId} thiếu quy đổi đơn vị: {converted.Message}";
                }
                else
                {
                    var child = detail.ChildRecipe;
                    if (child?.PreparedItem == null || !child.PreparedItem.Active)
                        return $"Công thức con #{detail.ChildRecipeId} chưa liên kết với bán thành phẩm đang hoạt động.";
                    if (!child.OutputQuantity.HasValue || child.OutputQuantity.Value <= 0 || !child.OutputUnitId.HasValue)
                        return $"Bán thành phẩm {child.PreparedItem.Name} chưa xác nhận sản lượng và đơn vị đầu ra.";
                    var normalizedOutput = await _physicalConversion.ConvertAsync(
                        child.OutputQuantity.Value,
                        child.OutputUnitId.Value,
                        child.PreparedItem.BaseUnitId);
                    if (!normalizedOutput.IsSuccess)
                        return $"Bán thành phẩm {child.PreparedItem.Name} chưa quy đổi được sản lượng về đơn vị tồn kho: {normalizedOutput.Message}";
                    var converted = await _physicalConversion.ConvertAsync(detail.Quantity, detail.UnitId, child.PreparedItem.BaseUnitId);
                    if (!converted.IsSuccess)
                        return $"Dòng BOM #{detail.RecipeDetailId} thiếu quy đổi cho bán thành phẩm: {converted.Message}";
                }
            }
            return null;
        }

        private async Task<bool> HasCycleAsync(int rootId, int currentId, HashSet<int> path, CancellationToken cancellationToken)
        {
            if (!path.Add(currentId)) return currentId == rootId;
            var children = await _context.RecipeDetails.AsNoTracking()
                .Where(x => x.RecipeId == currentId && x.ChildRecipeId.HasValue)
                .Select(x => x.ChildRecipeId!.Value).ToListAsync(cancellationToken);
            foreach (var child in children)
            {
                if (child == rootId || await HasCycleAsync(rootId, child, new HashSet<int>(path), cancellationToken))
                    return true;
            }
            return false;
        }

        private static DrinkSizeRecipeResolution Result(string status, string message, Recipe? recipe, int count, bool fallback) => new()
        { Status = status, Message = message, Recipe = recipe, CandidateCount = count, HasGenericFallback = fallback };
    }
}
