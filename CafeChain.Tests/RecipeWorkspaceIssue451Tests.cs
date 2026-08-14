using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Tests;

public sealed class RecipeWorkspaceIssue451Tests : IntegrationTestBase
{
    [Fact]
    public async Task WhereUsed_ReturnsCurrentParents()
    {
        using var context = CreateDbContext();
        var fixture = await SeedNestedUsageAsync(context);

        var result = await CreateService(context).GetCurrentAsync(
            fixture.CurrentChildRecipeId,
            Array.Empty<int>());

        Assert.Equal(3, result.CurrentParents.Count);
        Assert.Contains(result.CurrentParents, x =>
            x.RelationType == RecipeWhereUsedRelationTypes.MenuItemSize
            && x.BusinessName == "Trà sữa cốt đen"
            && x.ContextLabel == "Cỡ M");
        Assert.Contains(result.CurrentParents, x =>
            x.RelationType == RecipeWhereUsedRelationTypes.Topping
            && x.BusinessName == "Thạch trà đen");
        Assert.Contains(result.CurrentParents, x =>
            x.RelationType == RecipeWhereUsedRelationTypes.PreparedItem
            && x.BusinessName == "Nền trà sữa");
        Assert.All(result.CurrentParents, x => Assert.Equal(45100, x.PinnedChildRecipeId));
    }

    [Fact]
    public async Task WhereUsed_OneCurrentParent_ReturnsSingleBusinessRelation()
    {
        using var context = CreateDbContext();
        var unit = context.Units.AsNoTracking().First(x => x.UnitCode == "ml");
        var childOutput = PreparedItem(45110, unit.UnitId, "Cốt dùng một nơi");
        var child = PreparedRecipe(45110, childOutput, unit.UnitId, active: true);
        var parentOutput = PreparedItem(45111, unit.UnitId, "Nền trà duy nhất");
        var parent = PreparedRecipe(45111, parentOutput, unit.UnitId, active: true);
        parent.RecipeDetails.Add(PreparedLine(child.RecipeId, unit.UnitId));
        context.AddRange(childOutput, child, parentOutput, parent);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetCurrentAsync(child.RecipeId, Array.Empty<int>());

        var relation = Assert.Single(result.CurrentParents);
        Assert.Equal("Nền trà duy nhất", relation.BusinessName);
        Assert.Equal(RecipeWhereUsedRelationTypes.PreparedItem, relation.RelationType);
        Assert.Equal(child.RecipeId, relation.PinnedChildRecipeId);
    }

    [Fact]
    public async Task WhereUsed_ReturnsOnlyProvenDependencyTypes()
    {
        using var context = CreateDbContext();
        var fixture = await SeedNestedUsageAsync(context);

        var result = await CreateService(context).GetCurrentAsync(
            fixture.CurrentChildRecipeId,
            Array.Empty<int>());

        Assert.All(result.CurrentParents, item => Assert.Contains(
            item.RelationType,
            new[]
            {
                RecipeWhereUsedRelationTypes.MenuItemSize,
                RecipeWhereUsedRelationTypes.Topping,
                RecipeWhereUsedRelationTypes.PreparedItem
            }));
        Assert.All(result.CurrentParents, item => Assert.True(item.PinnedChildRecipeId > 0));
    }

    [Fact]
    public async Task WhereUsed_DoesNotDefaultToArchivedDependencies()
    {
        using var context = CreateDbContext();
        var fixture = await SeedNestedUsageAsync(context, includeArchivedParent: true);

        var result = await CreateService(context).GetCurrentAsync(
            fixture.CurrentChildRecipeId,
            Array.Empty<int>());

        Assert.DoesNotContain(result.CurrentParents, x => x.BusinessName == "Công thức cha đã lưu trữ");
        Assert.All(result.CurrentParents, x => Assert.NotEqual(fixture.ArchivedParentRecipeId, x.ParentRecipeId));
    }

    [Fact]
    public async Task WhereUsed_NoUsage_ReturnsBusinessEmptyState()
    {
        using var context = CreateDbContext();
        var unit = context.Units.AsNoTracking().First(x => x.UnitCode == "ml");
        var output = PreparedItem(45120, unit.UnitId, "Cốt không được sử dụng");
        var recipe = PreparedRecipe(45120, output, unit.UnitId, active: true);
        context.AddRange(output, recipe);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetCurrentAsync(recipe.RecipeId, Array.Empty<int>());

        Assert.Empty(result.CurrentParents);
        Assert.Empty(result.PointOfSaleLocations);
        Assert.Equal("Chưa ghi nhận nơi sử dụng hiện hành.", result.EmptyMessage);
    }

    [Fact]
    public async Task WhereUsed_IsPagedOrBounded()
    {
        using var context = CreateDbContext();
        var unit = context.Units.AsNoTracking().First(x => x.UnitCode == "ml");
        var childOutput = PreparedItem(45130, unit.UnitId, "Cốt dùng rộng rãi");
        var child = PreparedRecipe(45130, childOutput, unit.UnitId, active: true);
        context.AddRange(childOutput, child);

        for (var index = 0; index < RecipeWhereUsedLimits.MaxParentResults + 3; index++)
        {
            var parentOutput = PreparedItem(45200 + index, unit.UnitId, $"Bán thành phẩm cha {index:00}");
            var parent = PreparedRecipe(45200 + index, parentOutput, unit.UnitId, active: true);
            parent.RecipeDetails.Add(PreparedLine(child.RecipeId, unit.UnitId));
            context.AddRange(parentOutput, parent);
        }

        await context.SaveChangesAsync();

        var result = await CreateService(context).GetCurrentAsync(child.RecipeId, Array.Empty<int>());

        Assert.Equal(RecipeWhereUsedLimits.MaxParentResults, result.CurrentParents.Count);
        Assert.True(result.ParentResultsTruncated);
    }

    [Fact]
    public async Task WhereUsed_UnauthorizedPointOfSaleRelation_DoesNotLeakStore()
    {
        using var context = CreateDbContext();
        var drink = new Drink
        {
            DrinkId = 45140,
            DrinkCode = "DRINK-45140",
            Name = "Trà đào",
            Description = "",
            ProductTypeId = 1,
            Active = true,
            CreatedAt = new DateTime(2026, 1, 1)
        };
        var size = context.Sizes.AsNoTracking().First(x => x.Name == "M");
        var recipe = new Recipe
        {
            RecipeId = 45140,
            RecipeCode = "RCP-45140",
            Name = "Công thức trà đào",
            DrinkId = drink.DrinkId,
            SizeId = size.SizeId,
            Active = true,
            Status = "Active",
            RecipeDetails = []
        };
        var drinkSize = new DrinkSize
        {
            DrinkSizeId = 45140,
            DrinkId = drink.DrinkId,
            SizeId = size.SizeId,
            Price = 39000m,
            Active = true,
            UpdatedAtUtc = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc)
        };
        context.AddRange(drink, recipe, drinkSize);
        context.StoreMenuItems.AddRange(
            CurrentMenuItem(45140, storeId: 1, drinkSize.DrinkSizeId),
            CurrentMenuItem(45141, storeId: 2, drinkSize.DrinkSizeId));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetCurrentAsync(recipe.RecipeId, new[] { 1 });

        var location = Assert.Single(result.PointOfSaleLocations);
        Assert.Equal("CafeChain Thủ Dầu Một", location.BusinessName);
        Assert.DoesNotContain(result.PointOfSaleLocations, x => x.BusinessName == "CafeChain Thuận An");
    }

    [Fact]
    public void WhereUsed_ViewUsesVietnameseBusinessLanguageAndSecondaryTechnicalEvidence()
    {
        var view = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml"));

        Assert.Contains("Được sử dụng ở đâu", view, StringComparison.Ordinal);
        Assert.Contains("Chưa ghi nhận nơi sử dụng hiện hành.", view, StringComparison.Ordinal);
        Assert.Contains("recipe-workspace__where-used-technical", view, StringComparison.Ordinal);
        Assert.DoesNotContain(">Where-used<", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">ParentRecipe<", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">ChildRecipe<", view, StringComparison.OrdinalIgnoreCase);
    }

    private static RecipeWhereUsedQueryService CreateService(CafeChain.Data.AppDbContext context) =>
        new(context, new CurrentRecipeResolver(context), new FixedTimeProvider());

    private static async Task<NestedUsageFixture> SeedNestedUsageAsync(
        CafeChain.Data.AppDbContext context,
        bool includeArchivedParent = false)
    {
        var unit = context.Units.AsNoTracking().First(x => x.UnitCode == "ml");
        var size = context.Sizes.AsNoTracking().First(x => x.Name == "M");
        var childOutput = PreparedItem(45101, unit.UnitId, "Cốt trà đen");
        var pinnedChild = PreparedRecipe(45100, childOutput, unit.UnitId, active: false);
        pinnedChild.Status = "Archived";
        var currentChild = PreparedRecipe(45101, childOutput, unit.UnitId, active: true);
        currentChild.ParentVersionId = pinnedChild.RecipeId;

        var drink = new Drink
        {
            DrinkId = 45101,
            DrinkCode = "DRINK-45101",
            Name = "Trà sữa cốt đen",
            Description = "",
            ProductTypeId = 1,
            Active = true,
            CreatedAt = new DateTime(2026, 1, 1)
        };
        var menuParent = new Recipe
        {
            RecipeId = 45102,
            RecipeCode = "RCP-MENU-451",
            Name = "Công thức trà sữa cốt đen",
            DrinkId = drink.DrinkId,
            SizeId = size.SizeId,
            Active = true,
            Status = "Active",
            RecipeDetails = [PreparedLine(pinnedChild.RecipeId, unit.UnitId)]
        };

        var topping = new Topping
        {
            ToppingId = 45101,
            ToppingCode = "TOP-45101",
            Name = "Thạch trà đen",
            Price = 5000m,
            Active = true
        };
        var toppingParent = new Recipe
        {
            RecipeId = 45103,
            RecipeCode = "RCP-TOP-451",
            Name = "Công thức thạch trà đen",
            ToppingId = topping.ToppingId,
            Active = true,
            Status = "Active",
            RecipeDetails = [PreparedLine(pinnedChild.RecipeId, unit.UnitId)]
        };

        var parentOutput = PreparedItem(45104, unit.UnitId, "Nền trà sữa");
        var preparedParent = PreparedRecipe(45104, parentOutput, unit.UnitId, active: true);
        preparedParent.RecipeDetails.Add(PreparedLine(pinnedChild.RecipeId, unit.UnitId));

        context.AddRange(
            childOutput,
            pinnedChild,
            currentChild,
            drink,
            menuParent,
            topping,
            toppingParent,
            parentOutput,
            preparedParent);

        int? archivedParentId = null;
        if (includeArchivedParent)
        {
            var archivedOutput = PreparedItem(45105, unit.UnitId, "Công thức cha đã lưu trữ");
            var archivedParent = PreparedRecipe(45105, archivedOutput, unit.UnitId, active: false);
            archivedParent.Status = "Archived";
            archivedParent.RecipeDetails.Add(PreparedLine(pinnedChild.RecipeId, unit.UnitId));
            context.AddRange(archivedOutput, archivedParent);
            archivedParentId = archivedParent.RecipeId;
        }

        await context.SaveChangesAsync();
        return new NestedUsageFixture(currentChild.RecipeId, archivedParentId);
    }

    private static PreparedItem PreparedItem(int id, int unitId, string name) => new()
    {
        PreparedItemId = id,
        Code = $"PREP-{id}",
        Name = name,
        BaseUnitId = unitId,
        Active = true
    };

    private static Recipe PreparedRecipe(int id, PreparedItem output, int unitId, bool active) => new()
    {
        RecipeId = id,
        RecipeCode = $"RCP-{id}",
        Name = $"Công thức {output.Name}",
        PreparedItemId = output.PreparedItemId,
        OutputQuantity = 1000m,
        OutputUnitId = unitId,
        Active = active,
        Status = active ? "Active" : "Archived",
        RecipeDetails = []
    };

    private static RecipeDetail PreparedLine(int childRecipeId, int unitId) => new()
    {
        ChildRecipeId = childRecipeId,
        Quantity = 100m,
        UnitId = unitId
    };

    private static StoreMenuItem CurrentMenuItem(int id, int storeId, int drinkSizeId) => new()
    {
        StoreMenuItemId = id,
        StoreId = storeId,
        DrinkSizeId = drinkSizeId,
        IsEnabled = true,
        PublishedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
    };

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

    private sealed record NestedUsageFixture(int CurrentChildRecipeId, int? ArchivedParentRecipeId);
}
