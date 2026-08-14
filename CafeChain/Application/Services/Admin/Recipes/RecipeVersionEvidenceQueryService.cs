using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Recipes;

public sealed class RecipeVersionEvidenceQueryService : IRecipeVersionEvidenceQueryService
{
    public const string DifferentTargetReason = "RECIPE_COMPARE_DIFFERENT_TARGET";
    public const string InvalidTargetReason = "RECIPE_COMPARE_INVALID_TARGET";
    public const string MissingVersionReason = "RECIPE_COMPARE_VERSION_NOT_FOUND";

    private readonly AppDbContext _context;
    private readonly IEstimatedBomCostService _estimatedBomCost;
    private readonly ICurrentRecipeResolver _currentRecipeResolver;
    private readonly TimeProvider _timeProvider;

    public RecipeVersionEvidenceQueryService(
        AppDbContext context,
        IEstimatedBomCostService estimatedBomCost,
        ICurrentRecipeResolver currentRecipeResolver,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _estimatedBomCost = estimatedBomCost;
        _currentRecipeResolver = currentRecipeResolver;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RecipeVersionHistoryVM> GetHistoryAsync(
        int recipeId,
        CancellationToken cancellationToken = default)
    {
        var reference = await _context.Recipes
            .AsNoTracking()
            .Where(x => x.RecipeId == recipeId)
            .Select(x => new TargetProjection(
                x.DrinkId,
                x.SizeId,
                x.ToppingId,
                x.PreparedItemId))
            .FirstOrDefaultAsync(cancellationToken);

        var target = CreateTarget(reference);
        if (target == null)
            return new RecipeVersionHistoryVM();

        var targetQuery = ApplyTarget(_context.Recipes.AsNoTracking(), target);
        var totalCount = await targetQuery.CountAsync(cancellationToken);
        var versions = await targetQuery
            .OrderByDescending(x => x.RecipeId)
            .Take(RecipeVersionHistoryVM.ResultLimit)
            .Select(x => new
            {
                x.RecipeId,
                x.ParentVersionId,
                x.Active,
                x.Status,
                x.EffectiveDate
            })
            .ToListAsync(cancellationToken);
        var current = await _currentRecipeResolver.ResolveAsync(
            target,
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        var currentId = current.Status == CurrentRecipeResolutionStatus.Found
            ? current.Recipe?.RecipeId
            : null;

        return new RecipeVersionHistoryVM
        {
            TotalCount = totalCount,
            Items = versions.Select(x => new RecipeVersionHistoryItemVM
            {
                RecipeId = x.RecipeId,
                ParentVersionId = x.ParentVersionId,
                VersionLabel = $"Phiên bản {x.RecipeId}",
                IsCurrent = x.RecipeId == currentId,
                StateLabel = x.RecipeId == currentId
                    ? "Đang áp dụng"
                    : "Phiên bản lịch sử",
                AppliedAt = x.EffectiveDate,
                RelationshipLabel = x.ParentVersionId.HasValue
                    ? $"Kế thừa phiên bản {x.ParentVersionId.Value}"
                    : "Phiên bản gốc"
            }).ToList()
        };
    }

    public async Task<RecipeVersionCompareResult> CompareAsync(
        int fromRecipeId,
        int toRecipeId,
        CancellationToken cancellationToken = default)
    {
        if (fromRecipeId <= 0 || toRecipeId <= 0 || fromRecipeId == toRecipeId)
        {
            return RecipeVersionCompareResult.Failure(
                InvalidTargetReason,
                "Hãy chọn hai phiên bản khác nhau của cùng một đối tượng công thức.");
        }

        var recipes = await _context.Recipes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Size)
            .Include(x => x.PreparedItem)
            .Include(x => x.OutputUnit)
            .Include(x => x.RecipeDetails)
                .ThenInclude(x => x.Unit)
            .Include(x => x.RecipeDetails)
                .ThenInclude(x => x.Ingredient)
            .Include(x => x.RecipeDetails)
                .ThenInclude(x => x.ChildRecipe)
                    .ThenInclude(x => x!.PreparedItem)
            .Where(x => x.RecipeId == fromRecipeId || x.RecipeId == toRecipeId)
            .ToListAsync(cancellationToken);

        var from = recipes.FirstOrDefault(x => x.RecipeId == fromRecipeId);
        var to = recipes.FirstOrDefault(x => x.RecipeId == toRecipeId);
        if (from == null || to == null)
        {
            return RecipeVersionCompareResult.Failure(
                MissingVersionReason,
                "Không tìm thấy đầy đủ hai phiên bản cần so sánh.");
        }

        var fromTarget = CreateTarget(from);
        var toTarget = CreateTarget(to);
        if (fromTarget == null || toTarget == null)
        {
            return RecipeVersionCompareResult.Failure(
                InvalidTargetReason,
                "Một phiên bản chưa xác định đúng đối tượng công thức nên không thể so sánh an toàn.");
        }

        if (fromTarget != toTarget)
        {
            return RecipeVersionCompareResult.Failure(
                DifferentTargetReason,
                "Chỉ có thể so sánh hai phiên bản của cùng một đối tượng công thức.");
        }

        var costs = await _estimatedBomCost.CalculateRecipesEstimatedCostAsync(
            new[] { fromRecipeId, toRecipeId });
        var fromCost = costs[fromRecipeId];
        var toCost = costs[toRecipeId];
        var fromLines = BuildLines(from, fromCost);
        var toLines = BuildLines(to, toCost);
        var comparison = new RecipeVersionCompareVM
        {
            BusinessName = ResolveBusinessName(to),
            TargetLabel = ResolveTargetLabel(toTarget, to.Size?.Name),
            From = BuildSide(from, fromCost),
            To = BuildSide(to, toCost),
            OutputChangeLabel = BuildOutputChange(from, to),
            DesignCostDelta = fromCost.IsComplete && toCost.IsComplete
                ? toCost.TotalCost - fromCost.TotalCost
                : null,
            CostCompletenessChangeLabel = BuildCostCompletenessChange(fromCost, toCost)
        };

        foreach (var key in fromLines.Keys.Union(toLines.Keys).OrderBy(x => x))
        {
            var hasBefore = fromLines.TryGetValue(key, out var before);
            var hasAfter = toLines.TryGetValue(key, out var after);
            if (!hasBefore)
            {
                comparison.AddedLines.Add(BuildChange(null, after!, "Đã thêm"));
                continue;
            }

            if (!hasAfter)
            {
                comparison.RemovedLines.Add(BuildChange(before!, null, "Đã bỏ"));
                continue;
            }

            if (before!.Quantity != after!.Quantity
                || before.UnitId != after.UnitId
                || before.NormalizedQuantity != after.NormalizedQuantity
                || !string.Equals(before.BaseUnitCode, after.BaseUnitCode, StringComparison.OrdinalIgnoreCase))
            {
                comparison.ChangedLines.Add(BuildChange(before, after, BuildLineChangeSummary(before, after)));
            }
        }

        return RecipeVersionCompareResult.Success(comparison);
    }

    private static Dictionary<string, CompareLine> BuildLines(
        Recipe recipe,
        CostCalculationResult cost)
    {
        var result = new Dictionary<string, CompareLine>(StringComparer.Ordinal);
        foreach (var group in recipe.RecipeDetails
                     .OrderBy(x => x.RecipeDetailId)
                     .GroupBy(BuildBusinessComponentKey))
        {
            var ordinal = 0;
            foreach (var detail in group)
            {
                ordinal++;
                var costLine = cost.Lines.FirstOrDefault(x => x.RecipeDetailId == detail.RecipeDetailId);
                var prepared = detail.ChildRecipe?.PreparedItem;
                result[$"{group.Key}:{ordinal}"] = new CompareLine(
                    detail.Ingredient?.Name ?? prepared?.Name ?? detail.ChildRecipe?.Name ?? "Thành phần chưa xác định",
                    detail.IngredientId.HasValue ? "Nguyên liệu trực tiếp" : "Bán thành phẩm đầu vào",
                    detail.Ingredient?.Code ?? prepared?.Code ?? detail.ChildRecipe?.RecipeCode,
                    detail.Quantity,
                    detail.UnitId,
                    detail.Unit?.UnitCode ?? detail.Unit?.Name ?? "",
                    costLine?.QuantityInBase,
                    costLine?.BaseUnitCode);
            }
        }

        return result;
    }

    private static string BuildBusinessComponentKey(RecipeDetail detail)
    {
        if (detail.IngredientId.HasValue)
            return $"I:{detail.IngredientId.Value}";

        return detail.ChildRecipe?.PreparedItemId is int preparedItemId
            ? $"P:{preparedItemId}"
            : $"R:{detail.ChildRecipeId}";
    }

    private static RecipeVersionCompareSideVM BuildSide(
        Recipe recipe,
        CostCalculationResult cost) => new()
    {
        RecipeId = recipe.RecipeId,
        VersionLabel = $"Phiên bản {recipe.RecipeId}",
        StateLabel = recipe.Active && string.Equals(recipe.Status, "Active", StringComparison.OrdinalIgnoreCase)
            ? "Đang áp dụng"
            : "Phiên bản lịch sử",
        OutputDisplay = BuildOutputDisplay(recipe),
        DesignCost = cost.IsComplete ? cost.TotalCost : null,
        CostCompletenessLabel = cost.IsComplete ? "Đủ dữ liệu giá" : "Chưa đủ dữ liệu giá"
    };

    private static RecipeVersionLineChangeVM BuildChange(
        CompareLine? before,
        CompareLine? after,
        string summary)
    {
        var evidence = after ?? before!;
        return new RecipeVersionLineChangeVM
        {
            BusinessName = evidence.BusinessName,
            InputTypeLabel = evidence.InputTypeLabel,
            TechnicalCode = evidence.TechnicalCode,
            BeforeQuantity = before == null ? null : FormatQuantity(before.Quantity, before.UnitCode),
            AfterQuantity = after == null ? null : FormatQuantity(after.Quantity, after.UnitCode),
            BeforeNormalizedQuantity = FormatNormalized(before),
            AfterNormalizedQuantity = FormatNormalized(after),
            ChangeSummary = summary
        };
    }

    private static string BuildLineChangeSummary(CompareLine before, CompareLine after)
    {
        var changes = new List<string>();
        if (before.Quantity != after.Quantity)
            changes.Add("đổi định lượng");
        if (before.UnitId != after.UnitId)
            changes.Add("đổi đơn vị");
        if (before.NormalizedQuantity != after.NormalizedQuantity
            || !string.Equals(before.BaseUnitCode, after.BaseUnitCode, StringComparison.OrdinalIgnoreCase))
            changes.Add("đổi lượng chuẩn hóa");
        return changes.Count == 0 ? "Không thay đổi" : string.Join(", ", changes);
    }

    private static string BuildOutputChange(Recipe from, Recipe to)
    {
        var before = BuildOutputDisplay(from);
        var after = BuildOutputDisplay(to);
        return string.Equals(before, after, StringComparison.Ordinal)
            ? "Không thay đổi"
            : $"{before} → {after}";
    }

    private static string BuildCostCompletenessChange(
        CostCalculationResult from,
        CostCalculationResult to)
    {
        var before = from.IsComplete ? "Đủ dữ liệu giá" : "Chưa đủ dữ liệu giá";
        var after = to.IsComplete ? "Đủ dữ liệu giá" : "Chưa đủ dữ liệu giá";
        return before == after ? after : $"{before} → {after}";
    }

    private static string BuildOutputDisplay(Recipe recipe)
    {
        if (recipe.PreparedItemId.HasValue)
        {
            var quantity = recipe.OutputQuantity?.ToString("0.####") ?? "Chưa cấu hình";
            var unit = recipe.OutputUnit?.UnitCode ?? recipe.OutputUnit?.Name ?? "đơn vị";
            return $"{quantity} {unit} / mẻ";
        }

        if (recipe.ToppingId.HasValue)
            return "Một lần sử dụng theo định mức topping";

        var size = recipe.Size?.Name ?? "chưa xác định";
        return $"1 phần · Cỡ {size}";
    }

    private static string ResolveBusinessName(Recipe recipe) =>
        recipe.PreparedItem?.Name ?? recipe.Name ?? "Công thức chưa xác định";

    private static string ResolveTargetLabel(RecipeTarget target, string? sizeName) => target switch
    {
        RecipeTarget.MenuItemSize => $"Món bán · Cỡ {sizeName ?? "chưa xác định"}",
        RecipeTarget.Topping => "Topping",
        RecipeTarget.PreparedItem => "Bán thành phẩm",
        _ => "Công thức"
    };

    private static string FormatQuantity(decimal quantity, string unitCode) =>
        $"{quantity:0.####} {unitCode}".Trim();

    private static string? FormatNormalized(CompareLine? line) => line?.NormalizedQuantity.HasValue == true
        ? $"{line.NormalizedQuantity.Value:0.####} {line.BaseUnitCode}".Trim()
        : null;

    private static RecipeTarget? CreateTarget(Recipe recipe) => CreateTarget(new TargetProjection(
        recipe.DrinkId,
        recipe.SizeId,
        recipe.ToppingId,
        recipe.PreparedItemId));

    private static RecipeTarget? CreateTarget(TargetProjection? recipe)
    {
        if (recipe == null)
            return null;

        if (recipe.DrinkId.HasValue && recipe.SizeId.HasValue
            && !recipe.ToppingId.HasValue && !recipe.PreparedItemId.HasValue)
            return new RecipeTarget.MenuItemSize(recipe.DrinkId.Value, recipe.SizeId.Value);

        if (recipe.ToppingId.HasValue && !recipe.DrinkId.HasValue
            && !recipe.SizeId.HasValue && !recipe.PreparedItemId.HasValue)
            return new RecipeTarget.Topping(recipe.ToppingId.Value);

        if (recipe.PreparedItemId.HasValue && !recipe.DrinkId.HasValue
            && !recipe.SizeId.HasValue && !recipe.ToppingId.HasValue)
            return new RecipeTarget.PreparedItem(recipe.PreparedItemId.Value);

        return null;
    }

    private static IQueryable<Recipe> ApplyTarget(IQueryable<Recipe> query, RecipeTarget target) => target switch
    {
        RecipeTarget.MenuItemSize menu => query.Where(x =>
            x.DrinkId == menu.DrinkId && x.SizeId == menu.SizeId
            && !x.ToppingId.HasValue && !x.PreparedItemId.HasValue),
        RecipeTarget.Topping topping => query.Where(x =>
            x.ToppingId == topping.ToppingId && !x.DrinkId.HasValue
            && !x.SizeId.HasValue && !x.PreparedItemId.HasValue),
        RecipeTarget.PreparedItem prepared => query.Where(x =>
            x.PreparedItemId == prepared.PreparedItemId && !x.DrinkId.HasValue
            && !x.SizeId.HasValue && !x.ToppingId.HasValue),
        _ => query.Where(_ => false)
    };

    private sealed record TargetProjection(
        int? DrinkId,
        int? SizeId,
        int? ToppingId,
        int? PreparedItemId);

    private sealed record CompareLine(
        string BusinessName,
        string InputTypeLabel,
        string? TechnicalCode,
        decimal Quantity,
        int UnitId,
        string UnitCode,
        decimal? NormalizedQuantity,
        string? BaseUnitCode);
}
