using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Services.Admin.PreparedItems;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeChain.Tests;

public sealed class RecipeWorkspaceIssue449Tests : IntegrationTestBase
{
    [Fact]
    public async Task RecipeWorkspace_MenuItem_ShowsPortionOutput()
    {
        using var context = CreateDbContext();
        var unit = context.Units.First();
        var drink = new Drink
        {
            DrinkId = 44901,
            DrinkCode = "DRINK-449",
            Name = "Trà sữa nha đam",
            Description = "",
            ProductTypeId = 1,
            Active = true,
            CreatedAt = new DateTime(2026, 1, 1)
        };
        var size = context.Sizes.AsNoTracking().First(x => x.Name == "M");
        var ingredient = Ingredient(44901, unit.UnitId, "Sữa tươi");
        var recipe = Recipe(44901, "RCP-MENU-449", ingredient, unit.UnitId);
        recipe.DrinkId = drink.DrinkId;
        recipe.SizeId = size.SizeId;

        context.AddRange(drink, ingredient, recipe);
        await context.SaveChangesAsync();

        var resolver = new RecordingCurrentRecipeResolver(recipe);
        var page = await CreateQueryService(context, resolver).GetVisualizePageAsync(recipe.RecipeId);

        Assert.NotNull(page);
        Assert.Equal("Trà sữa nha đam", page!.BusinessName);
        Assert.Equal("Món bán · Cỡ M", page.TargetLabel);
        Assert.Equal("Đầu ra", page.OutputHeading);
        Assert.Equal("1 phần Trà sữa nha đam · Cỡ M", page.OutputDisplay);
        Assert.True(page.IsCurrentVersion);
        Assert.Equal("Đang áp dụng", page.AppliedStateLabel);
        Assert.Equal(new RecipeTarget.MenuItemSize(drink.DrinkId, size.SizeId), resolver.LastTarget);
    }

    [Fact]
    public async Task RecipeWorkspace_Topping_UsesCurrentBusinessSemantics()
    {
        using var context = CreateDbContext();
        var unit = context.Units.First();
        var topping = context.Toppings.AsNoTracking().First(x => x.Name == "Trân châu đen");
        var ingredient = Ingredient(44902, unit.UnitId, "Hạt trân châu");
        var recipe = Recipe(44902, "RCP-TOP-449", ingredient, unit.UnitId);
        recipe.ToppingId = topping.ToppingId;

        context.AddRange(ingredient, recipe);
        await context.SaveChangesAsync();

        var resolver = new RecordingCurrentRecipeResolver(recipe);
        var page = await CreateQueryService(context, resolver).GetVisualizePageAsync(recipe.RecipeId);

        Assert.NotNull(page);
        Assert.Equal("Trân châu đen", page!.BusinessName);
        Assert.Equal("Topping", page.TargetLabel);
        Assert.Equal("Phạm vi áp dụng", page.OutputHeading);
        Assert.Equal("Một lần sử dụng Trân châu đen theo định mức topping", page.OutputDisplay);
        Assert.Null(page.OutputQuantity);
        Assert.Equal(new RecipeTarget.Topping(topping.ToppingId), resolver.LastTarget);
    }

    [Fact]
    public async Task RecipeWorkspace_PreparedItem_ShowsBatchOutput()
    {
        using var context = CreateDbContext();
        var unit = context.Units.First(x => x.UnitCode == "ml");
        var preparedItem = PreparedItem(44903, unit.UnitId, "Cốt trà đen");
        var ingredient = Ingredient(44903, unit.UnitId, "Nước lọc");
        var recipe = Recipe(44903, "RCP-PREP-449", ingredient, unit.UnitId);
        recipe.PreparedItemId = preparedItem.PreparedItemId;
        recipe.OutputQuantity = 5000m;
        recipe.OutputUnitId = unit.UnitId;

        context.AddRange(preparedItem, ingredient, recipe);
        await context.SaveChangesAsync();

        var resolver = new RecordingCurrentRecipeResolver(recipe);
        var page = await CreateQueryService(context, resolver).GetVisualizePageAsync(recipe.RecipeId);

        Assert.NotNull(page);
        Assert.Equal("Cốt trà đen", page!.BusinessName);
        Assert.Equal("Bán thành phẩm", page.TargetLabel);
        Assert.Equal("Sản lượng chuẩn một mẻ", page.OutputHeading);
        Assert.Equal("5000 ml / mẻ", page.OutputDisplay);
        Assert.DoesNotContain("phần", page.OutputDisplay, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new RecipeTarget.PreparedItem(preparedItem.PreparedItemId), resolver.LastTarget);
    }

    [Fact]
    public async Task RecipeWorkspace_DistinguishesPreparedInputsFromDirectIngredients()
    {
        using var context = CreateDbContext();
        var unit = context.Units.First(x => x.UnitCode == "ml");
        var childOutput = PreparedItem(44904, unit.UnitId, "Cốt trà ô long");
        var parentOutput = PreparedItem(44905, unit.UnitId, "Trà sữa ô long");
        var directIngredient = Ingredient(44904, unit.UnitId, "Sữa tươi");
        var childRecipe = new Recipe
        {
            RecipeId = 44904,
            RecipeCode = "RCP-CHILD-449",
            Name = "Công thức cốt trà ô long",
            PreparedItemId = childOutput.PreparedItemId,
            OutputQuantity = 1000m,
            OutputUnitId = unit.UnitId,
            Active = true,
            Status = "Active",
            RecipeDetails = []
        };
        var parentRecipe = new Recipe
        {
            RecipeId = 44905,
            RecipeCode = "RCP-PARENT-449",
            Name = "Công thức trà sữa ô long",
            PreparedItemId = parentOutput.PreparedItemId,
            OutputQuantity = 1000m,
            OutputUnitId = unit.UnitId,
            Active = true,
            Status = "Active",
            RecipeDetails =
            [
                new RecipeDetail
                {
                    ChildRecipeId = childRecipe.RecipeId,
                    Quantity = 120m,
                    UnitId = unit.UnitId
                },
                new RecipeDetail
                {
                    IngredientId = directIngredient.IngredientId,
                    Quantity = 90m,
                    UnitId = unit.UnitId
                }
            ]
        };

        context.AddRange(childOutput, parentOutput, directIngredient, childRecipe, parentRecipe);
        await context.SaveChangesAsync();

        var page = await CreateQueryService(
            context,
            new RecordingCurrentRecipeResolver(parentRecipe)).GetVisualizePageAsync(parentRecipe.RecipeId);

        Assert.NotNull(page);
        var prepared = Assert.Single(page!.PreparedInputs);
        Assert.Equal("Bán thành phẩm đầu vào", prepared.InputTypeLabel);
        Assert.Equal("Cốt trà ô long", prepared.ItemName);
        Assert.Equal("Phiên bản 44904", prepared.SourceVersionLabel);
        var direct = Assert.Single(page.DirectIngredients);
        Assert.Equal("Nguyên liệu trực tiếp", direct.InputTypeLabel);
        Assert.Equal("Sữa tươi", direct.ItemName);
    }

    [Fact]
    public void RecipeWorkspace_TechnicalCodesAreSecondary()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");
        var namePosition = view.IndexOf("Model.BusinessName", StringComparison.Ordinal);
        var codePosition = view.IndexOf("Model.RecipeCode", StringComparison.Ordinal);

        Assert.True(namePosition >= 0);
        Assert.True(codePosition > namePosition);
        Assert.Contains("recipe-workspace__technical-code", view, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeWorkspace_DoesNotExposeRawEnglishDomainTerms()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");

        Assert.DoesNotContain(">PreparedItem<", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">RecipeDetail<", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">ChildRecipe<", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Readiness<", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Effective<", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bán thành phẩm lồng", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bán thành phẩm đầu vào", view, StringComparison.Ordinal);
        Assert.Contains("Nguyên liệu trực tiếp", view, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeWorkspace_ReadOnlyActorDoesNotSeeMutationActions()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");

        Assert.Contains("if (Model.CanWrite)", view, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecipeWorkspace_EditorSeesAuthorizedContextualActions()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");

        Assert.Contains("Tạo phiên bản mới", view, StringComparison.Ordinal);
        Assert.Contains("Model.CanWrite", view, StringComparison.Ordinal);
    }

    private static AdminRecipeQueryService CreateQueryService(
        CafeChain.Data.AppDbContext context,
        ICurrentRecipeResolver resolver)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(
            context,
            NullLogger<UnitConversionService>.Instance,
            physical);
        var normalizer = new RecipeOutputNormalizer(context, physical);
        var cost = new EstimatedBomCostService(
            context,
            conversion,
            physical,
            normalizer,
            NullLogger<EstimatedBomCostService>.Instance);

        return new AdminRecipeQueryService(
            context,
            normalizer,
            cost,
            new AdminPreparedItemService(context),
            new RecipeBomTreeQueryService(context),
            new BomDataHealthEvaluator(),
            resolver,
            new FixedTimeProvider());
    }

    private static Ingredient Ingredient(int id, int unitId, string name) => new()
    {
        IngredientId = id,
        Code = $"ING-{id}",
        Name = name,
        BaseUnitId = unitId,
        Active = true
    };

    private static PreparedItem PreparedItem(int id, int unitId, string name) => new()
    {
        PreparedItemId = id,
        Code = $"PREP-{id}",
        Name = name,
        BaseUnitId = unitId,
        Active = true
    };

    private static Recipe Recipe(int id, string code, Ingredient ingredient, int unitId) => new()
    {
        RecipeId = id,
        RecipeCode = code,
        Name = $"Công thức {code}",
        Active = true,
        Status = "Active",
        RecipeDetails =
        [
            new RecipeDetail
            {
                IngredientId = ingredient.IngredientId,
                Quantity = 10m,
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
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc CafeChain.");
    }

    private sealed class RecordingCurrentRecipeResolver(Recipe currentRecipe) : ICurrentRecipeResolver
    {
        public RecipeTarget? LastTarget { get; private set; }

        public Task<CurrentRecipeResolution> ResolveAsync(
            RecipeTarget target,
            DateTime businessInstantUtc,
            CancellationToken cancellationToken = default)
        {
            LastTarget = target;
            return Task.FromResult(new CurrentRecipeResolution(
                CurrentRecipeResolutionStatus.Found,
                currentRecipe,
                string.Empty));
        }

        public async Task<IReadOnlyDictionary<RecipeTarget, CurrentRecipeResolution>> ResolveManyAsync(
            IReadOnlyCollection<RecipeTarget> targets,
            DateTime businessInstantUtc,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<RecipeTarget, CurrentRecipeResolution>();
            foreach (var target in targets)
                result[target] = await ResolveAsync(target, businessInstantUtc, cancellationToken);
            return result;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);
    }
}
