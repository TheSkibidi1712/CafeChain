using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.Interfaces.Admin.PreparedItems;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CafeChain.Tests;

public sealed class RecipeWorkspaceIssue452Tests : IntegrationTestBase
{
    [Fact]
    public async Task VersionCompare_ShowsAddedRemovedChangedLines()
    {
        using var context = CreateDbContext();
        var fixture = await SeedComparisonAsync(context);

        var result = await CreateEvidenceService(context, fixture.Costs)
            .CompareAsync(fixture.OldRecipeId, fixture.NewRecipeId);

        var comparison = AssertSuccess(result);
        Assert.Equal("Đường", Assert.Single(comparison.ChangedLines).BusinessName);
        Assert.Equal("Sữa", Assert.Single(comparison.RemovedLines).BusinessName);
        Assert.Equal("Trà", Assert.Single(comparison.AddedLines).BusinessName);
        Assert.Contains("đổi định lượng", comparison.ChangedLines[0].ChangeSummary);
    }

    [Fact]
    public async Task VersionCompare_ShowsNormalizedQuantityAndCostDelta()
    {
        using var context = CreateDbContext();
        var fixture = await SeedComparisonAsync(context);

        var result = await CreateEvidenceService(context, fixture.Costs)
            .CompareAsync(fixture.OldRecipeId, fixture.NewRecipeId);

        var comparison = AssertSuccess(result);
        var changed = Assert.Single(comparison.ChangedLines);
        Assert.Equal("10 g", changed.BeforeNormalizedQuantity);
        Assert.Equal("15 g", changed.AfterNormalizedQuantity);
        Assert.Equal(40m, comparison.DesignCostDelta);
        Assert.Equal("Đủ dữ liệu giá", comparison.CostCompletenessChangeLabel);
    }

    [Fact]
    public async Task VersionCompare_RejectsDifferentTargets()
    {
        using var context = CreateDbContext();
        var fixture = await SeedComparisonAsync(context);
        var other = new Recipe
        {
            RecipeId = 45299,
            RecipeCode = "RCP-45299",
            Name = "Công thức đối tượng khác",
            DrinkId = 45299,
            SizeId = fixture.SizeId,
            Active = true,
            Status = "Active",
            RecipeDetails = []
        };
        context.Recipes.Add(other);
        await context.SaveChangesAsync();

        var result = await CreateEvidenceService(context, fixture.Costs)
            .CompareAsync(fixture.OldRecipeId, other.RecipeId);

        Assert.False(result.IsSuccess);
        Assert.Equal(RecipeVersionEvidenceQueryService.DifferentTargetReason, result.ReasonCode);
        Assert.Equal("Chỉ có thể so sánh hai phiên bản của cùng một đối tượng công thức.", result.Message);
    }

    [Fact]
    public async Task History_IsBoundedAndUsesBusinessReadableStates()
    {
        using var context = CreateDbContext();
        for (var index = 0; index < 22; index++)
        {
            context.Recipes.Add(new Recipe
            {
                RecipeId = 45300 + index,
                RecipeCode = $"RCP-{45300 + index}",
                Name = "Công thức chuỗi phiên bản",
                DrinkId = 45300,
                SizeId = 1,
                ParentVersionId = index == 0 ? null : 45299 + index,
                Active = index == 21,
                Status = index == 21 ? "Active" : "Archived",
                EffectiveDate = new DateTime(2026, 1, 1).AddDays(index),
                RecipeDetails = []
            });
        }
        await context.SaveChangesAsync();

        var service = CreateEvidenceService(context, new Dictionary<int, CostCalculationResult>());
        var history = await service.GetHistoryAsync(45321);

        Assert.Equal(22, history.TotalCount);
        Assert.Equal(RecipeVersionHistoryVM.ResultLimit, history.Items.Count);
        Assert.True(history.IsTruncated);
        Assert.Equal("Đang áp dụng", history.Items[0].StateLabel);
        Assert.DoesNotContain(history.Items, x => x.StateLabel is "Active" or "Archived");
    }

    [Fact]
    public async Task RecentProductionEvidence_IsBounded()
    {
        using var context = CreateDbContext();
        var fixture = await SeedProductionEvidenceAsync(context);

        var result = await CreateAdminQueryService(context)
            .GetOperationalDetailAsync(fixture.OldRecipeId, fixture.StoreId);

        Assert.NotNull(result);
        Assert.Equal(5, result!.RecentRuns.Count);
        Assert.All(result.RecentRuns, run =>
        {
            Assert.Equal(12m, run.AcceptedOutputQuantity);
            Assert.Equal("g", run.OutputUnitCode);
            Assert.Equal("Hoàn tất", run.Status);
        });
    }

    [Fact]
    public async Task RecentProductionEvidence_UsesPinnedRecipeEvidence()
    {
        using var context = CreateDbContext();
        var fixture = await SeedProductionEvidenceAsync(context);

        var result = await CreateAdminQueryService(context)
            .GetOperationalDetailAsync(fixture.OldRecipeId, fixture.StoreId);

        Assert.NotNull(result);
        Assert.DoesNotContain(result!.RecentRuns, x => x.ProductionRunId == fixture.NewRecipeRunId);
        Assert.All(result.RecentRuns, x => Assert.StartsWith("PR-", x.BusinessCode));
    }

    [Fact]
    public void History_DoesNotExposeRawTechnicalData()
    {
        var workspace = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");
        var compare = Read("CafeChain/Areas/Admin/Views/AdminRecipe/CompareVersions.cshtml");
        var combined = workspace + compare;

        Assert.Contains("Lịch sử phiên bản", combined, StringComparison.Ordinal);
        Assert.Contains("Sản xuất gần đây", combined, StringComparison.Ordinal);
        Assert.DoesNotContain(">RecipeDetail<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">ChildRecipe<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw JSON", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ReasonCode", combined, StringComparison.OrdinalIgnoreCase);
    }

    private static RecipeVersionCompareVM AssertSuccess(RecipeVersionCompareResult result)
    {
        Assert.True(result.IsSuccess, result.Message);
        return Assert.IsType<RecipeVersionCompareVM>(result.Comparison);
    }

    private static RecipeVersionEvidenceQueryService CreateEvidenceService(
        CafeChain.Data.AppDbContext context,
        IReadOnlyDictionary<int, CostCalculationResult> costs)
    {
        var cost = new Mock<IEstimatedBomCostService>();
        cost.Setup(x => x.CalculateRecipesEstimatedCostAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync((IEnumerable<int> ids) => ids.ToDictionary(
                id => id,
                id => costs.TryGetValue(id, out var result)
                    ? result
                    : CostCalculationResult.Incomplete([], [])));
        return new RecipeVersionEvidenceQueryService(
            context,
            cost.Object,
            new CurrentRecipeResolver(context),
            TimeProvider.System);
    }

    private static AdminRecipeQueryService CreateAdminQueryService(
        CafeChain.Data.AppDbContext context)
    {
        return new AdminRecipeQueryService(
            context,
            Mock.Of<IRecipeOutputNormalizer>(),
            Mock.Of<IEstimatedBomCostService>(),
            Mock.Of<IAdminPreparedItemService>(),
            Mock.Of<IRecipeBomTreeQueryService>(),
            new BomDataHealthEvaluator(),
            new CurrentRecipeResolver(context),
            TimeProvider.System);
    }

    private static async Task<ComparisonFixture> SeedComparisonAsync(
        CafeChain.Data.AppDbContext context)
    {
        var unit = context.Units.AsNoTracking().First(x => x.UnitCode == "g");
        var sugar = Ingredient(45201, unit.UnitId, "Đường");
        var milk = Ingredient(45202, unit.UnitId, "Sữa");
        var tea = Ingredient(45203, unit.UnitId, "Trà");
        var oldRecipe = Recipe(45201, active: false, parentVersionId: null,
        [
            Line(45201, sugar, unit.UnitId, 10m),
            Line(45202, milk, unit.UnitId, 20m)
        ]);
        var newRecipe = Recipe(45202, active: true, parentVersionId: oldRecipe.RecipeId,
        [
            Line(45203, sugar, unit.UnitId, 15m),
            Line(45204, tea, unit.UnitId, 5m)
        ]);
        context.AddRange(sugar, milk, tea, oldRecipe, newRecipe);
        await context.SaveChangesAsync();

        return new ComparisonFixture(
            oldRecipe.RecipeId,
            newRecipe.RecipeId,
            oldRecipe.SizeId!.Value,
            new Dictionary<int, CostCalculationResult>
            {
                [oldRecipe.RecipeId] = CompleteCost(100m,
                    CostLine(oldRecipe.RecipeDetails.ElementAt(0), 10m, 40m),
                    CostLine(oldRecipe.RecipeDetails.ElementAt(1), 20m, 60m)),
                [newRecipe.RecipeId] = CompleteCost(140m,
                    CostLine(newRecipe.RecipeDetails.ElementAt(0), 15m, 75m),
                    CostLine(newRecipe.RecipeDetails.ElementAt(1), 5m, 65m))
            });
    }

    private static async Task<ProductionFixture> SeedProductionEvidenceAsync(
        CafeChain.Data.AppDbContext context)
    {
        var unit = context.Units.AsNoTracking().First(x => x.UnitCode == "g");
        var store = new Store
        {
            StoreId = 45401,
            Name = "Chi nhánh kiểm thử",
            Address = "Test",
            Phone = "0900454001",
            Active = true,
            CreatedAt = new DateTime(2026, 1, 1)
        };
        var prepared = new PreparedItem
        {
            PreparedItemId = 45401,
            Code = "PREP-45401",
            Name = "Cốt kiểm thử",
            BaseUnitId = unit.UnitId,
            Active = true
        };
        var oldRecipe = new Recipe
        {
            RecipeId = 45401,
            RecipeCode = "RCP-45401",
            Name = "Công thức cốt kiểm thử",
            PreparedItemId = prepared.PreparedItemId,
            OutputQuantity = 12m,
            OutputUnitId = unit.UnitId,
            Active = false,
            Status = "Archived",
            RecipeDetails = []
        };
        var newRecipe = new Recipe
        {
            RecipeId = 45402,
            RecipeCode = "RCP-45402",
            Name = "Công thức cốt kiểm thử",
            PreparedItemId = prepared.PreparedItemId,
            OutputQuantity = 12m,
            OutputUnitId = unit.UnitId,
            ParentVersionId = oldRecipe.RecipeId,
            Active = true,
            Status = "Active",
            RecipeDetails = []
        };
        context.AddRange(store, prepared, oldRecipe, newRecipe);

        for (var index = 0; index < 7; index++)
        {
            var run = Run(45410 + index, store.StoreId, oldRecipe.RecipeId, index);
            context.ProductionRuns.Add(run);
            context.ProductionRunOutputs.Add(Output(45410 + index, run.ProductionRunId, unit.UnitId));
        }
        var newRun = Run(45499, store.StoreId, newRecipe.RecipeId, 20);
        context.ProductionRuns.Add(newRun);
        context.ProductionRunOutputs.Add(Output(45499, newRun.ProductionRunId, unit.UnitId));
        await context.SaveChangesAsync();

        return new ProductionFixture(store.StoreId, oldRecipe.RecipeId, newRun.ProductionRunId);
    }

    private static ProductionRun Run(int id, int storeId, int recipeId, int dayOffset) => new()
    {
        ProductionRunId = id,
        StoreId = storeId,
        RecipeId = recipeId,
        RequestedRunCount = 1m,
        RequestKey = Guid.NewGuid(),
        RequestFingerprint = $"RUN-{id}",
        Status = ProductionRunStatus.Completed,
        CreatedByStaffId = 999,
        CreatedAt = new DateTime(2026, 1, 1).AddDays(dayOffset),
        ConfirmedAt = new DateTime(2026, 1, 1).AddDays(dayOffset),
        CompletedAt = new DateTime(2026, 1, 1).AddDays(dayOffset).AddHours(1)
    };

    private static ProductionRunOutput Output(int id, int runId, int unitId) => new()
    {
        ProductionRunOutputId = id,
        ProductionRunId = runId,
        BaseUnitId = unitId,
        ExpectedOutputBase = 12m,
        ActualProducedBase = 12m,
        AcceptedOutputBase = 12m,
        RejectedOutputBase = 0m,
        VariancePercent = 0m,
        RecordedByStaffId = 999,
        RecordedAtUtc = new DateTime(2026, 1, 1)
    };

    private static Recipe Recipe(
        int id,
        bool active,
        int? parentVersionId,
        ICollection<RecipeDetail> lines) => new()
    {
        RecipeId = id,
        RecipeCode = $"RCP-{id}",
        Name = "Trà sữa kiểm thử",
        DrinkId = 45201,
        SizeId = 1,
        ParentVersionId = parentVersionId,
        Active = active,
        Status = active ? "Active" : "Archived",
        EffectiveDate = new DateTime(2026, 1, active ? 2 : 1),
        RecipeDetails = lines
    };

    private static Ingredient Ingredient(int id, int unitId, string name) => new()
    {
        IngredientId = id,
        Code = $"ING-{id}",
        Name = name,
        BaseUnitId = unitId,
        Active = true
    };

    private static RecipeDetail Line(int id, Ingredient ingredient, int unitId, decimal quantity) => new()
    {
        RecipeDetailId = id,
        IngredientId = ingredient.IngredientId,
        Quantity = quantity,
        UnitId = unitId
    };

    private static CostLineResult CostLine(RecipeDetail line, decimal normalized, decimal cost) => new()
    {
        RecipeDetailId = line.RecipeDetailId,
        ComponentKind = CostComponentKind.Ingredient,
        IngredientId = line.IngredientId,
        Quantity = line.Quantity,
        UnitId = line.UnitId,
        UnitCode = "g",
        QuantityInBase = normalized,
        BaseUnitCode = "g",
        LineCost = cost,
        Status = CostCompletenessStatus.Complete
    };

    private static CostCalculationResult CompleteCost(decimal total, params CostLineResult[] lines) =>
        CostCalculationResult.Complete(total, lines);

    private static string Read(string path) => File.ReadAllText(Path.Combine(FindRepoRoot(), path));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
               && !(Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
                    && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests"))))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc.");
    }

    private sealed record ComparisonFixture(
        int OldRecipeId,
        int NewRecipeId,
        int SizeId,
        IReadOnlyDictionary<int, CostCalculationResult> Costs);

    private sealed record ProductionFixture(int StoreId, int OldRecipeId, int NewRecipeRunId);
}
