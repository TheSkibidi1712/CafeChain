using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Application.Interfaces.Inventories;
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

        public DrinkSizeRecipeResolver(AppDbContext context, IUnitConversionService unitConversion, IPhysicalUnitConversionService physicalConversion)
        {
            _context = context;
            _unitConversion = unitConversion;
            _physicalConversion = physicalConversion;
        }

        public async Task<DrinkSizeRecipeResolution> ResolveExactAsync(int drinkId, int sizeId, DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            var exactActive = await RecipeQuery()
                .Where(r => r.DrinkId == drinkId && r.SizeId == sizeId && r.ToppingId == null && r.Active && r.Status == "Active")
                .ToListAsync(cancellationToken);

            var genericFallback = await _context.Recipes.AsNoTracking().AnyAsync(r =>
                r.DrinkId == drinkId && r.SizeId == null && r.ToppingId == null && r.Active && r.Status == "Active", cancellationToken);

            var eligible = exactActive
                .Where(r => !r.EffectiveDate.HasValue || r.EffectiveDate.Value.ToUniversalTime() <= asOfUtc)
                .OrderByDescending(r => r.EffectiveDate ?? DateTime.MinValue)
                .ThenByDescending(r => r.RecipeId)
                .ToList();

            if (eligible.Count > 1)
                return Result(DrinkSizeRecipeHealthStatuses.MultipleActiveRecipe, $"Có {eligible.Count} BOM hiệu lực cho cùng đồ uống và size.", null, eligible.Count, genericFallback);

            if (eligible.Count == 0)
            {
                if (exactActive.Count > 0)
                    return Result(DrinkSizeRecipeHealthStatuses.FutureRecipeOnly, "Chỉ có BOM có ngày hiệu lực trong tương lai.", null, exactActive.Count, genericFallback);
                if (genericFallback)
                    return Result(DrinkSizeRecipeHealthStatuses.GenericFallbackOnly, "Chỉ có BOM compatibility không gắn size; profitability không sử dụng fallback này.", null, 0, true);
                return Result(DrinkSizeRecipeHealthStatuses.MissingRecipe, "Chưa có BOM chính xác cho size.", null, 0, false);
            }

            var recipe = eligible[0];
            var validation = await ValidateRecipeAsync(recipe, cancellationToken);
            if (validation != null)
                return Result(DrinkSizeRecipeHealthStatuses.InvalidRecipe, validation, recipe, 1, genericFallback);

            return Result(DrinkSizeRecipeHealthStatuses.ExactReady, "BOM chính xác theo size đã sẵn sàng.", recipe, 1, genericFallback);
        }

        public async Task<IReadOnlyList<DrinkSizeRecipeHealthRow>> GetDataHealthAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            var sizes = await _context.DrinkSizes.AsNoTracking()
                .Where(x => x.Active && x.Drink.Active)
                .OrderBy(x => x.Drink.Name).ThenBy(x => x.Size.Name)
                .Select(x => new { x.DrinkSizeId, x.DrinkId, DrinkName = x.Drink.Name, x.SizeId, SizeName = x.Size.Name })
                .ToListAsync(cancellationToken);

            var rows = new List<DrinkSizeRecipeHealthRow>(sizes.Count);
            foreach (var size in sizes)
            {
                var resolution = await ResolveExactAsync(size.DrinkId, size.SizeId, asOfUtc, cancellationToken);
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
                        return $"ChildRecipe #{detail.ChildRecipeId} chưa map PreparedItem hoạt động.";
                    var converted = await _physicalConversion.ConvertAsync(detail.Quantity, detail.UnitId, child.PreparedItem.BaseUnitId);
                    if (!converted.IsSuccess)
                        return $"Dòng BOM #{detail.RecipeDetailId} thiếu quy đổi PreparedItem: {converted.Message}";
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
