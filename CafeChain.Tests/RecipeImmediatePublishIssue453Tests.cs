using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Systems;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeChain.Tests;

public sealed class RecipeImmediatePublishIssue453Tests : IntegrationTestBase
{
    private static readonly DateTime BusinessInstantUtc =
        new(2026, 8, 14, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task NewVersion_PublishesImmediately()
    {
        await using var context = CreateDbContext();
        var source = await LoadSourceAsync(context);

        var result = await CreateService(context).UpdateRecipeAsync(
            source.RecipeId,
            BuildVersionModel(source));

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(result.EntityId);
        var published = await context.Recipes.SingleAsync(x => x.RecipeId == result.EntityId);
        Assert.True(published.Active);
        Assert.Equal("Active", published.Status);
        Assert.Equal(BusinessInstantUtc, published.EffectiveDate);
        Assert.False(source.Active);
        Assert.Equal("Archived", source.Status);
    }

    [Fact]
    public void NewVersion_UsesSharedAtomicPublishOperation()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminRecipeController.cs");

        Assert.Contains("_recipeService.UpdateRecipeAsync(id, model)", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("_context.Recipes", controller, StringComparison.Ordinal);
        Assert.Contains("nameof(Visualize)", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void FutureEffectiveDate_IsNotAvailableInNewUX()
    {
        var create = Read("CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Create.cshtml");
        var edit = Read("CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Edit.cshtml");

        Assert.DoesNotContain("type=\"date\"", create, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("type=\"date\"", edit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ngày hiệu lực", create, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ngày hiệu lực", edit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Lưu và áp dụng ngay", create, StringComparison.Ordinal);
        Assert.Contains("Lưu và áp dụng ngay", edit, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FutureEffectiveRequest_IsRejected()
    {
        await using var context = CreateDbContext();
        var source = await LoadSourceAsync(context);
        var model = BuildVersionModel(source);
        model.EffectiveDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Unspecified);

        var result = await CreateService(context).UpdateRecipeAsync(source.RecipeId, model);

        Assert.False(result.IsSuccess);
        Assert.Equal(BomRecipeErrorCodes.FutureEffectiveDateNotSupported, result.ErrorCode);
        Assert.Contains("áp dụng trong tương lai", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NewVersion_ReviewsChangedLines()
    {
        var edit = Read("CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Edit.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "Recipe", "bom-builder.js");

        Assert.Contains("publicationChangeSummary", edit, StringComparison.Ordinal);
        Assert.Contains("sourceLines", edit, StringComparison.Ordinal);
        Assert.Contains("Dòng thêm", script, StringComparison.Ordinal);
        Assert.Contains("Dòng bỏ", script, StringComparison.Ordinal);
        Assert.Contains("Dòng thay đổi", script, StringComparison.Ordinal);
        Assert.Contains("Số lượng chuẩn hóa", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewVersion_Conflict_IsBusinessReadable()
    {
        await using var context = CreateDbContext();
        var source = await LoadSourceAsync(context);
        var model = BuildVersionModel(source);
        source.Active = false;
        source.Status = "Archived";
        await context.SaveChangesAsync();

        var result = await CreateService(context).UpdateRecipeAsync(source.RecipeId, model);

        Assert.False(result.IsSuccess);
        Assert.Equal(BomRecipeErrorCodes.PublishConflict, result.ErrorCode);
        Assert.Contains("tải lại", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Archived", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MenuSize_CreateUsesExactTarget()
    {
        var create = Read("CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Create.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "Recipe", "bom-builder.js");

        Assert.Contains("name=\"DrinkId\"", create, StringComparison.Ordinal);
        Assert.Contains("name=\"SizeId\"", create, StringComparison.Ordinal);
        Assert.Contains("DrinkId:", script, StringComparison.Ordinal);
        Assert.Contains("SizeId:", script, StringComparison.Ordinal);
        Assert.Contains("Món bán và cỡ", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PreparedItem_CreateUsesBatchOutputSemantics()
    {
        var create = Read("CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Create.cshtml");
        var edit = Read("CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Edit.cshtml");

        Assert.Contains("Sản lượng chuẩn một mẻ", create, StringComparison.Ordinal);
        Assert.Contains("Sản lượng chuẩn một mẻ", edit, StringComparison.Ordinal);
        Assert.DoesNotContain("1 phần", create, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadOnlyActor_DoesNotReceiveMutationAction()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Visualize.cshtml");

        Assert.Contains("if (Model.CanWrite)", view, StringComparison.Ordinal);
        Assert.Contains("Tạo phiên bản mới", view, StringComparison.Ordinal);
    }

    private static async Task<Recipe> LoadSourceAsync(AppDbContext context) =>
        await context.Recipes
            .Include(recipe => recipe.RecipeDetails)
            .SingleAsync(recipe => recipe.RecipeId == 1);

    private static RecipeCreateVM BuildVersionModel(Recipe source) => new()
    {
        RecipeType = "POS",
        DrinkId = source.DrinkId,
        SizeId = source.SizeId,
        Active = true,
        EffectiveDate = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Unspecified),
        Details = source.RecipeDetails.Select(detail => new RecipeDetailVM
        {
            ItemCode = detail.IngredientId.HasValue
                ? $"ING_{detail.IngredientId.Value}"
                : $"REC_{detail.ChildRecipeId!.Value}",
            Quantity = detail.Quantity,
            UnitId = detail.UnitId,
            YieldPercentage = 100m
        }).ToList()
    };

    private static AdminRecipeService CreateService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var clock = new FixedTimeProvider(new DateTimeOffset(BusinessInstantUtc));
        return new AdminRecipeService(
            context,
            new RecipeOutputNormalizer(context, physical),
            NullLogger<AdminRecipeService>.Instance,
            clock,
            new BusinessDateService(clock),
            new CurrentRecipeResolver(context));
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([FindRepoRoot(), .. segments]));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc CafeChain.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
