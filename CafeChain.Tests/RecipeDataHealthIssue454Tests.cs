using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.Interfaces.Admin.PreparedItems;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CafeChain.Tests;

public sealed class RecipeDataHealthIssue454Tests : IntegrationTestBase
{
    [Fact]
    public async Task DataHealth_TotalRepresentsEntireFilteredResult()
    {
        using var context = CreateDbContext();
        SeedRecipes(context, 25, "Khớp bộ lọc");
        SeedRecipes(context, 4, "Ngoài phạm vi", startId: 45500);
        await context.SaveChangesAsync();

        var page = await CreateService(context).GetDataHealthPageAsync(
            page: 2,
            pageSize: 10,
            search: "Khớp bộ lọc");

        Assert.Equal(25, page.TotalCount);
        Assert.Equal(10, page.CurrentPageCount);
        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public void CurrentPageCount_IsLabeledAsPageCount()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/DataHealth.cshtml");

        Assert.Contains("Trên trang này", view, StringComparison.Ordinal);
        Assert.Contains("Tổng kết quả lọc", view, StringComparison.Ordinal);
        Assert.DoesNotContain("@Model.Items.Count / @Model.TotalCount", view, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AmbiguousCurrentRecipe_IsSurfacedFailClosed()
    {
        using var context = CreateDbContext();
        context.Recipes.AddRange(
            ToppingRecipe(45401, "Phiên bản A", 45401),
            ToppingRecipe(45402, "Phiên bản B", 45401));
        await context.SaveChangesAsync();

        var page = await CreateService(context).GetDataHealthPageAsync(
            pageSize: 10,
            search: "Phiên bản");

        Assert.Equal(2, page.CurrentRecipeIssueCount);
        Assert.All(page.Items, row =>
        {
            Assert.Equal(BomCurrentRecipeHealthCodes.Ambiguous, row.CurrentRecipe.Code);
            Assert.Equal("Cần kiểm tra phiên bản áp dụng", row.CurrentRecipe.Label);
            Assert.Contains("nhiều phiên bản", row.CurrentRecipe.Message, StringComparison.Ordinal);
            Assert.True(row.CurrentRecipe.IsBlocking);
        });
    }

    [Fact]
    public async Task DataHealth_DoesNotRepairLegacyRowsDuringRead()
    {
        using var context = CreateDbContext();
        context.Recipes.AddRange(
            ToppingRecipe(45411, "Mâu thuẫn A", 45411),
            ToppingRecipe(45412, "Mâu thuẫn B", 45411));
        await context.SaveChangesAsync();

        var before = await context.Recipes
            .AsNoTracking()
            .OrderBy(x => x.RecipeId)
            .Select(x => new { x.RecipeId, x.Active, x.Status })
            .ToListAsync();

        await CreateService(context).GetDataHealthPageAsync();
        context.ChangeTracker.Clear();

        var after = await context.Recipes
            .AsNoTracking()
            .OrderBy(x => x.RecipeId)
            .Select(x => new { x.RecipeId, x.Active, x.Status })
            .ToListAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public void DataHealth_ReasonCodesAreLocalizedAtBoundary()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/DataHealth.cshtml");

        Assert.Contains("Phiên bản áp dụng", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ReasonCode", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BOM_CURRENT_RECIPE_", view, StringComparison.Ordinal);
        Assert.DoesNotContain(">AMBIGUOUS<", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">INVALID_TARGET<", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FilteredAggregate_IsIndependentFromPageSize()
    {
        using var context = CreateDbContext();
        SeedRecipes(context, 23, "Tập lọc");
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var smallPage = await service.GetDataHealthPageAsync(pageSize: 5, search: "Tập lọc");
        var largePage = await service.GetDataHealthPageAsync(pageSize: 20, search: "Tập lọc");

        Assert.Equal(23, smallPage.TotalCount);
        Assert.Equal(smallPage.TotalCount, largePage.TotalCount);
        Assert.Equal(5, smallPage.CurrentPageCount);
        Assert.Equal(20, largePage.CurrentPageCount);
    }

    private static AdminRecipeQueryService CreateService(CafeChain.Data.AppDbContext context)
    {
        var cost = new Mock<IEstimatedBomCostService>();
        cost.Setup(x => x.CalculateRecipesEstimatedCostAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync((IEnumerable<int> ids) => ids.ToDictionary(
                id => id,
                _ => CostCalculationResult.Incomplete([], [])));

        return new AdminRecipeQueryService(
            context,
            Mock.Of<IRecipeOutputNormalizer>(),
            cost.Object,
            Mock.Of<IAdminPreparedItemService>(),
            Mock.Of<IRecipeBomTreeQueryService>(),
            new BomDataHealthEvaluator(),
            new CurrentRecipeResolver(context),
            TimeProvider.System);
    }

    private static void SeedRecipes(
        CafeChain.Data.AppDbContext context,
        int count,
        string name,
        int startId = 45450)
    {
        for (var index = 0; index < count; index++)
        {
            var id = startId + index;
            context.Recipes.Add(Recipe(id, $"{name} {index + 1}", id, 1));
        }
    }

    private static Recipe Recipe(int id, string name, int drinkId, int sizeId) => new()
    {
        RecipeId = id,
        RecipeCode = $"RCP-{id}",
        Name = name,
        DrinkId = drinkId,
        SizeId = sizeId,
        Active = true,
        Status = "Active",
        RecipeDetails = []
    };

    private static Recipe ToppingRecipe(int id, string name, int toppingId) => new()
    {
        RecipeId = id,
        RecipeCode = $"RCP-{id}",
        Name = name,
        ToppingId = toppingId,
        Active = true,
        Status = "Active",
        RecipeDetails = []
    };

    private static string Read(string path) => File.ReadAllText(Path.Combine(FindRepoRoot(), path));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CafeChain.slnx"))
                || File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc CafeChain.");
    }
}
