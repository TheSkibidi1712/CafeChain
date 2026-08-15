using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.PreparedItems;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Orders;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CafeChain.Tests;

public sealed class RecipeWorkspaceIssue450Tests : IntegrationTestBase
{
    [Fact]
    public async Task DesignCost_And_StoreFifoCost_AreNeverPresentedAsSameAuthority()
    {
        using var context = CreateDbContext();
        var unit = context.Units.First();
        var ingredient = Ingredient(45001, unit.UnitId, "Sữa tươi");
        var recipe = Recipe(45001, ingredient, unit.UnitId);
        context.AddRange(ingredient, recipe);
        await context.SaveChangesAsync();

        var cost = CompleteCost(recipe, ingredient, unit.UnitId, 12500m);
        var page = await CreateQueryService(context, cost).GetVisualizePageAsync(recipe.RecipeId);

        Assert.NotNull(page);
        Assert.Equal(RecipeWorkspaceCostAuthorityCodes.DesignEstimate, page!.DesignCost.AuthorityCode);
        Assert.Equal("Giá vốn ước tính theo thiết kế", page.DesignCost.Label);
        Assert.Equal(12500m, page.DesignCost.Amount);
        Assert.Equal(RecipeWorkspaceCostAuthorityCodes.StoreFifo, page.StoreFifoCost.AuthorityCode);
        Assert.Equal("Giá vốn theo nhập trước - xuất trước (FIFO) tại chi nhánh", page.StoreFifoCost.Label);
        Assert.Null(page.StoreFifoCost.Amount);
    }

    [Fact]
    public async Task StoreFifoCost_RequiresSelectedAuthorizedStore()
    {
        using var context = CreateDbContext();
        var unit = context.Units.First();
        var store = context.Stores.AsNoTracking().First(x => x.Active);
        var ingredient = Ingredient(45002, unit.UnitId, "Cà phê rang xay");
        var recipe = Recipe(45002, ingredient, unit.UnitId, quantity: 10m);
        context.AddRange(ingredient, recipe);
        context.InventoryCostLayers.Add(new InventoryCostLayer
        {
            InventoryCostLayerId = 45002,
            StoreId = store.StoreId,
            IngredientId = ingredient.IngredientId,
            Quantity = 100m,
            RemainingQuantity = 100m,
            UnitCost = 25m,
            CreatedAt = new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var page = await CreateQueryService(context).GetVisualizePageAsync(recipe.RecipeId);
        Assert.NotNull(page);
        Assert.False(page!.StoreFifoCost.IsAvailable);
        Assert.Contains("Chọn chi nhánh", page.StoreFifoCost.Message, StringComparison.Ordinal);

        var evidence = await CreateQueryService(context).GetStoreEvidenceAsync(page, store.StoreId);

        Assert.NotNull(evidence);
        Assert.True(evidence!.Cost.IsAvailable);
        Assert.Equal(250m, evidence.Cost.Amount);
        Assert.Equal(store.Name, evidence.Cost.ContextLabel);
        Assert.Equal(new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc), evidence.Cost.EvidenceAtUtc);
    }

    [Fact]
    public async Task IncompleteDesignCost_DoesNotBecomeZero()
    {
        using var context = CreateDbContext();
        var unit = context.Units.First();
        var ingredient = Ingredient(45003, unit.UnitId, "Nguyên liệu chưa có báo giá");
        var recipe = Recipe(45003, ingredient, unit.UnitId);
        context.AddRange(ingredient, recipe);
        await context.SaveChangesAsync();

        var page = await CreateQueryService(context).GetVisualizePageAsync(recipe.RecipeId);

        Assert.NotNull(page);
        Assert.Equal(RecipeWorkspaceEvidenceState.Incomplete, page!.DesignCost.State);
        Assert.Null(page.DesignCost.Amount);
        Assert.NotEqual(0m, page.DesignCost.Amount);
        Assert.False(page.GlobalReadiness.Facets.Single(x => x.Code == RecipeWorkspaceReadinessCodes.Pricing).IsPassed);
    }

    [Fact]
    public void HistoricalOrderCost_DoesNotRepriceFromCurrentRecipe()
    {
        var snapshot = new OrderDetail
        {
            UnitCogs = 4200m,
            TotalCogs = 8400m
        };
        var querySource = Read("CafeChain/Application/Services/Admin/Recipes/AdminRecipeQueryService.cs");

        Assert.Equal(4200m, snapshot.UnitCogs);
        Assert.Equal(8400m, snapshot.TotalCogs);
        Assert.DoesNotContain("OrderDetails", querySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TotalCogs =", querySource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Readiness_ExposesIndependentFacets()
    {
        using var context = CreateDbContext();
        var unit = context.Units.First();
        var ingredient = Ingredient(45004, unit.UnitId, "Đường");
        var recipe = Recipe(45004, ingredient, unit.UnitId);
        context.AddRange(ingredient, recipe);
        await context.SaveChangesAsync();

        var page = await CreateQueryService(
            context,
            CompleteCost(recipe, ingredient, unit.UnitId, 500m))
            .GetVisualizePageAsync(recipe.RecipeId);

        Assert.NotNull(page);
        Assert.Equal(4, page!.GlobalReadiness.Facets.Count);
        Assert.Contains(page.GlobalReadiness.Facets, x => x.Code == RecipeWorkspaceReadinessCodes.Configuration);
        Assert.Contains(page.GlobalReadiness.Facets, x => x.Code == RecipeWorkspaceReadinessCodes.Pricing);
        Assert.Contains(page.GlobalReadiness.Facets, x => x.Code == RecipeWorkspaceReadinessCodes.PointOfSale);
        Assert.Contains(page.GlobalReadiness.Facets, x => x.Code == RecipeWorkspaceReadinessCodes.PreparedInputs);
        Assert.Contains("tiêu chí đạt", page.GlobalReadiness.SummaryLabel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GlobalReadiness_DoesNotImplyStoreOperationalReadiness()
    {
        using var context = CreateDbContext();
        var unit = context.Units.First();
        var ingredient = Ingredient(45005, unit.UnitId, "Trà đen");
        var recipe = Recipe(45005, ingredient, unit.UnitId);
        context.AddRange(ingredient, recipe);
        await context.SaveChangesAsync();

        var page = await CreateQueryService(
            context,
            CompleteCost(recipe, ingredient, unit.UnitId, 800m))
            .GetVisualizePageAsync(recipe.RecipeId);

        Assert.NotNull(page);
        Assert.True(page!.GlobalReadiness.IsReady);
        Assert.False(page.StoreReadiness.IsReady);
        Assert.Contains("chi nhánh", page.StoreReadiness.ScopeLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StoreFifoUnavailable_RemainsExplicitAndDoesNotFallbackToDesignCost()
    {
        using var context = CreateDbContext();
        var unit = context.Units.First();
        var store = context.Stores.AsNoTracking().First(x => x.Active);
        var ingredient = Ingredient(45006, unit.UnitId, "Siro");
        var recipe = Recipe(45006, ingredient, unit.UnitId);
        context.AddRange(ingredient, recipe);
        await context.SaveChangesAsync();

        var page = await CreateQueryService(
            context,
            CompleteCost(recipe, ingredient, unit.UnitId, 990m))
            .GetVisualizePageAsync(recipe.RecipeId);
        var evidence = await CreateQueryService(context).GetStoreEvidenceAsync(page!, store.StoreId);

        Assert.NotNull(evidence);
        Assert.False(evidence!.Cost.IsAvailable);
        Assert.Null(evidence.Cost.Amount);
        Assert.NotEqual(page!.DesignCost.Amount, evidence.Cost.Amount);
        Assert.Contains("Chưa đủ", evidence.Cost.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnauthorizedStoreContext_IsRejectedBeforeFifoQuery()
    {
        var controller = Read("CafeChain/Areas/Admin/Controllers/AdminRecipeController.cs");
        var authorizationPosition = controller.IndexOf("selectedStore == null", StringComparison.Ordinal);
        var evidencePosition = controller.IndexOf("GetStoreEvidenceAsync", StringComparison.Ordinal);

        Assert.True(authorizationPosition >= 0);
        Assert.True(evidencePosition > authorizationPosition);
        Assert.Contains("return Forbid()", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeWorkspace_UsesVietnameseCostAndReadinessLabelsWithoutReasonCodes()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");
        var controller = Read("CafeChain/Areas/Admin/Controllers/AdminRecipeController.cs");

        Assert.Contains("Giá vốn ước tính theo thiết kế", view, StringComparison.Ordinal);
        Assert.Contains("Giá vốn theo nhập trước - xuất trước (FIFO) tại chi nhánh", view, StringComparison.Ordinal);
        Assert.Contains("Mức sẵn sàng", view, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthorityCode", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ReasonCode", view, StringComparison.Ordinal);
        Assert.DoesNotContain(">DesignCost<", view, StringComparison.Ordinal);
        Assert.DoesNotContain(">Readiness<", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyProductionReadiness(null, readiness.Message)", controller, StringComparison.Ordinal);
        Assert.Contains("Chưa thể kiểm tra điều kiện sản xuất tại chi nhánh.", controller, StringComparison.Ordinal);
    }

    private static IEstimatedBomCostService CompleteCost(
        Recipe recipe,
        Ingredient ingredient,
        int unitId,
        decimal total)
    {
        var result = CostCalculationResult.Complete(total,
        [
            new CostLineResult
            {
                RecipeDetailId = recipe.RecipeDetails.Single().RecipeDetailId,
                ComponentKind = CostComponentKind.Ingredient,
                IngredientId = ingredient.IngredientId,
                Quantity = recipe.RecipeDetails.Single().Quantity,
                UnitId = unitId,
                QuantityInBase = recipe.RecipeDetails.Single().Quantity,
                BaseUnitCode = "g",
                LineCost = total,
                Status = CostCompletenessStatus.Complete
            }
        ]);
        var mock = new Mock<IEstimatedBomCostService>();
        mock.Setup(x => x.CalculateRecipeEstimatedCostAsync(recipe.RecipeId)).ReturnsAsync(result);
        return mock.Object;
    }

    private static AdminRecipeQueryService CreateQueryService(
        CafeChain.Data.AppDbContext context,
        IEstimatedBomCostService? cost = null)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(
            context,
            NullLogger<UnitConversionService>.Instance,
            physical);
        var normalizer = new RecipeOutputNormalizer(context, physical);
        cost ??= new EstimatedBomCostService(
            context,
            conversion,
            physical,
            normalizer,
            NullLogger<EstimatedBomCostService>.Instance);

        var current = context.Recipes.Local
            .Where(x => x.RecipeId >= 45000)
            .OrderByDescending(x => x.RecipeId)
            .FirstOrDefault();
        return new AdminRecipeQueryService(
            context,
            normalizer,
            cost,
            new AdminPreparedItemService(context),
            new RecipeBomTreeQueryService(context),
            new BomDataHealthEvaluator(),
            current == null ? null : new FixedCurrentRecipeResolver(current),
            timeProvider: new FixedTimeProvider());
    }

    private static Ingredient Ingredient(int id, int unitId, string name) => new()
    {
        IngredientId = id,
        Code = $"ING-{id}",
        Name = name,
        BaseUnitId = unitId,
        Active = true
    };

    private static Recipe Recipe(
        int id,
        Ingredient ingredient,
        int unitId,
        decimal quantity = 10m) => new()
    {
        RecipeId = id,
        RecipeCode = $"RCP-{id}",
        Name = $"Công thức {id}",
        DrinkId = 1,
        SizeId = 2,
        Active = true,
        Status = "Active",
        RecipeDetails =
        [
            new RecipeDetail
            {
                RecipeDetailId = id,
                IngredientId = ingredient.IngredientId,
                Quantity = quantity,
                UnitId = unitId
            }
        ]
    };

    private static string Read(string path) => File.ReadAllText(Path.Combine(FindRepoRoot(), path));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CafeChain.slnx"))
                || File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc CafeChain.");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class FixedCurrentRecipeResolver(Recipe recipe) : ICurrentRecipeResolver
    {
        public Task<CurrentRecipeResolution> ResolveAsync(
            RecipeTarget target,
            DateTime businessInstantUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CurrentRecipeResolution(
                CurrentRecipeResolutionStatus.Found,
                recipe,
                string.Empty));

        public async Task<IReadOnlyDictionary<RecipeTarget, CurrentRecipeResolution>> ResolveManyAsync(
            IReadOnlyCollection<RecipeTarget> targets,
            DateTime businessInstantUtc,
            CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<RecipeTarget, CurrentRecipeResolution>();
            foreach (var target in targets)
                results[target] = await ResolveAsync(target, businessInstantUtc, cancellationToken);
            return results;
        }
    }
}
