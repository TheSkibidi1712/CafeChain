using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Recipes;

public sealed class RecipeWhereUsedQueryService : IRecipeWhereUsedQueryService
{
    private readonly AppDbContext _context;
    private readonly ICurrentRecipeResolver _currentRecipeResolver;
    private readonly TimeProvider _timeProvider;

    public RecipeWhereUsedQueryService(
        AppDbContext context,
        ICurrentRecipeResolver currentRecipeResolver,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _currentRecipeResolver = currentRecipeResolver;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RecipeWhereUsedVM> GetCurrentAsync(
        int recipeId,
        IReadOnlyCollection<int> authorizedStoreIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizedStoreIds);

        var source = await _context.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.RecipeId == recipeId)
            .Select(recipe => new RecipeIdentityRow(
                recipe.RecipeId,
                recipe.DrinkId,
                recipe.SizeId,
                recipe.ToppingId,
                recipe.PreparedItemId))
            .FirstOrDefaultAsync(cancellationToken);
        if (source == null)
            return new RecipeWhereUsedVM();

        var sourceTarget = CreateTarget(source);
        if (sourceTarget == null)
            return new RecipeWhereUsedVM();

        var businessInstantUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var sourceResolution = await _currentRecipeResolver.ResolveAsync(
            sourceTarget,
            businessInstantUtc,
            cancellationToken);
        if (sourceResolution.Status != CurrentRecipeResolutionStatus.Found
            || sourceResolution.Recipe == null)
        {
            return new RecipeWhereUsedVM();
        }

        var result = new RecipeWhereUsedVM();
        if (sourceResolution.Recipe.PreparedItemId.HasValue)
        {
            await LoadCurrentParentRecipesAsync(
                result,
                sourceResolution.Recipe.PreparedItemId.Value,
                businessInstantUtc,
                cancellationToken);
        }

        await LoadPointOfSaleLocationsAsync(
            result,
            sourceTarget,
            authorizedStoreIds,
            businessInstantUtc,
            cancellationToken);
        return result;
    }

    private async Task LoadCurrentParentRecipesAsync(
        RecipeWhereUsedVM result,
        int preparedItemId,
        DateTime businessInstantUtc,
        CancellationToken cancellationToken)
    {
        var candidates = await _context.RecipeDetails
            .AsNoTracking()
            .Where(detail =>
                detail.ChildRecipeId.HasValue
                && detail.ChildRecipe.PreparedItemId == preparedItemId
                && detail.Recipe.Active
                && detail.Recipe.Status == "Active")
            .Select(detail => new ParentCandidateRow
            {
                ParentRecipeId = detail.RecipeId,
                ParentRecipeCode = detail.Recipe.RecipeCode,
                DrinkId = detail.Recipe.DrinkId,
                SizeId = detail.Recipe.SizeId,
                ToppingId = detail.Recipe.ToppingId,
                PreparedItemId = detail.Recipe.PreparedItemId,
                PinnedChildRecipeId = detail.ChildRecipeId!.Value,
                SizeName = detail.Recipe.Size != null ? detail.Recipe.Size.Name : null,
                BusinessName = detail.Recipe.DrinkId.HasValue
                    ? _context.Drinks
                        .Where(drink => drink.DrinkId == detail.Recipe.DrinkId.Value)
                        .Select(drink => drink.Name)
                        .FirstOrDefault() ?? detail.Recipe.Name
                    : detail.Recipe.ToppingId.HasValue
                        ? _context.Toppings
                            .Where(topping => topping.ToppingId == detail.Recipe.ToppingId.Value)
                            .Select(topping => topping.Name)
                            .FirstOrDefault() ?? detail.Recipe.Name
                        : detail.Recipe.PreparedItemId.HasValue
                            ? _context.PreparedItems
                                .Where(item => item.PreparedItemId == detail.Recipe.PreparedItemId.Value)
                                .Select(item => item.Name)
                                .FirstOrDefault() ?? detail.Recipe.Name
                            : detail.Recipe.Name
            })
            .Distinct()
            .OrderBy(candidate => candidate.BusinessName)
            .ThenBy(candidate => candidate.ParentRecipeId)
            .Take(RecipeWhereUsedLimits.MaxParentResults + 1)
            .ToListAsync(cancellationToken);

        var candidateTargets = candidates
            .Select(CreateTarget)
            .Where(target => target != null)
            .Cast<RecipeTarget>()
            .Distinct()
            .ToArray();
        var resolutions = await _currentRecipeResolver.ResolveManyAsync(
            candidateTargets,
            businessInstantUtc,
            cancellationToken);

        var currentParents = candidates
            .Where(candidate => IsResolvedCurrent(candidate, resolutions))
            .GroupBy(candidate => candidate.ParentRecipeId)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.BusinessName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(candidate => candidate.ParentRecipeId)
            .Take(RecipeWhereUsedLimits.MaxParentResults)
            .Select(ToViewModel)
            .ToList();

        result.CurrentParents = currentParents;
        result.ParentResultsTruncated = candidates.Count > RecipeWhereUsedLimits.MaxParentResults;
    }

    private async Task LoadPointOfSaleLocationsAsync(
        RecipeWhereUsedVM result,
        RecipeTarget sourceTarget,
        IReadOnlyCollection<int> authorizedStoreIds,
        DateTime businessInstantUtc,
        CancellationToken cancellationToken)
    {
        var storeIds = authorizedStoreIds
            .Where(storeId => storeId > 0)
            .Distinct()
            .ToArray();
        if (storeIds.Length == 0)
            return;

        List<RecipeWhereUsedItemVM> locations;
        switch (sourceTarget)
        {
            case RecipeTarget.MenuItemSize menu:
                locations = await _context.StoreMenuItems
                    .AsNoTracking()
                    .Where(item =>
                        storeIds.Contains(item.StoreId)
                        && item.DrinkSize.DrinkId == menu.DrinkId
                        && item.DrinkSize.SizeId == menu.SizeId
                        && item.DrinkSize.Active
                        && item.PublishedAtUtc.HasValue
                        && item.IsEnabled
                        && (!item.EffectiveFromUtc.HasValue || item.EffectiveFromUtc.Value <= businessInstantUtc)
                        && (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value > businessInstantUtc))
                    .OrderBy(item => item.Store.Name)
                    .ThenBy(item => item.StoreId)
                    .Take(RecipeWhereUsedLimits.MaxPointOfSaleResults + 1)
                    .Select(item => new RecipeWhereUsedItemVM
                    {
                        RelationType = RecipeWhereUsedRelationTypes.PointOfSale,
                        TypeLabel = "Điểm bán hàng",
                        BusinessName = item.Store.Name,
                        ContextLabel = "Món bán đang được mở bán tại chi nhánh"
                    })
                    .ToListAsync(cancellationToken);
                break;

            case RecipeTarget.Topping topping:
                locations = await _context.StoreToppings
                    .AsNoTracking()
                    .Where(item =>
                        storeIds.Contains(item.StoreId)
                        && item.ToppingId == topping.ToppingId
                        && item.Active
                        && item.Topping.Active)
                    .OrderBy(item => item.Store.Name)
                    .ThenBy(item => item.StoreId)
                    .Take(RecipeWhereUsedLimits.MaxPointOfSaleResults + 1)
                    .Select(item => new RecipeWhereUsedItemVM
                    {
                        RelationType = RecipeWhereUsedRelationTypes.PointOfSale,
                        TypeLabel = "Điểm bán hàng",
                        BusinessName = item.Store.Name,
                        ContextLabel = "Topping đang được phục vụ tại chi nhánh"
                    })
                    .ToListAsync(cancellationToken);
                break;

            default:
                return;
        }

        result.PointOfSaleResultsTruncated = locations.Count > RecipeWhereUsedLimits.MaxPointOfSaleResults;
        result.PointOfSaleLocations = locations
            .Take(RecipeWhereUsedLimits.MaxPointOfSaleResults)
            .ToList();
    }

    private static bool IsResolvedCurrent(
        ParentCandidateRow candidate,
        IReadOnlyDictionary<RecipeTarget, CurrentRecipeResolution> resolutions)
    {
        var target = CreateTarget(candidate);
        return target != null
            && resolutions.TryGetValue(target, out var resolution)
            && resolution.Status == CurrentRecipeResolutionStatus.Found
            && resolution.Recipe?.RecipeId == candidate.ParentRecipeId;
    }

    private static RecipeWhereUsedItemVM ToViewModel(ParentCandidateRow candidate)
    {
        var (relationType, typeLabel, contextLabel) = candidate switch
        {
            { DrinkId: not null, SizeId: not null } => (
                RecipeWhereUsedRelationTypes.MenuItemSize,
                "Món bán",
                string.IsNullOrWhiteSpace(candidate.SizeName) ? null : $"Cỡ {candidate.SizeName}"),
            { ToppingId: not null } => (
                RecipeWhereUsedRelationTypes.Topping,
                "Topping",
                "Công thức topping hiện hành"),
            { PreparedItemId: not null } => (
                RecipeWhereUsedRelationTypes.PreparedItem,
                "Bán thành phẩm",
                "Công thức bán thành phẩm hiện hành"),
            _ => ("", "Công thức", null)
        };

        return new RecipeWhereUsedItemVM
        {
            RelationType = relationType,
            TypeLabel = typeLabel,
            BusinessName = candidate.BusinessName,
            ContextLabel = contextLabel,
            TechnicalCode = candidate.ParentRecipeCode,
            ParentRecipeId = candidate.ParentRecipeId,
            PinnedChildRecipeId = candidate.PinnedChildRecipeId
        };
    }

    private static RecipeTarget? CreateTarget(RecipeIdentityRow recipe) => recipe switch
    {
        { DrinkId: > 0, SizeId: > 0, ToppingId: null, PreparedItemId: null } =>
            new RecipeTarget.MenuItemSize(recipe.DrinkId.Value, recipe.SizeId.Value),
        { DrinkId: null, SizeId: null, ToppingId: > 0, PreparedItemId: null } =>
            new RecipeTarget.Topping(recipe.ToppingId.Value),
        { DrinkId: null, SizeId: null, ToppingId: null, PreparedItemId: > 0 } =>
            new RecipeTarget.PreparedItem(recipe.PreparedItemId.Value),
        _ => null
    };

    private static RecipeTarget? CreateTarget(ParentCandidateRow recipe) => recipe switch
    {
        { DrinkId: > 0, SizeId: > 0, ToppingId: null, PreparedItemId: null } =>
            new RecipeTarget.MenuItemSize(recipe.DrinkId.Value, recipe.SizeId.Value),
        { DrinkId: null, SizeId: null, ToppingId: > 0, PreparedItemId: null } =>
            new RecipeTarget.Topping(recipe.ToppingId.Value),
        { DrinkId: null, SizeId: null, ToppingId: null, PreparedItemId: > 0 } =>
            new RecipeTarget.PreparedItem(recipe.PreparedItemId.Value),
        _ => null
    };

    private sealed record RecipeIdentityRow(
        int RecipeId,
        int? DrinkId,
        int? SizeId,
        int? ToppingId,
        int? PreparedItemId);

    private sealed class ParentCandidateRow
    {
        public int ParentRecipeId { get; init; }
        public string ParentRecipeCode { get; init; } = "";
        public int? DrinkId { get; init; }
        public int? SizeId { get; init; }
        public int? ToppingId { get; init; }
        public int? PreparedItemId { get; init; }
        public int PinnedChildRecipeId { get; init; }
        public string? SizeName { get; init; }
        public string BusinessName { get; init; } = "";
    }
}
