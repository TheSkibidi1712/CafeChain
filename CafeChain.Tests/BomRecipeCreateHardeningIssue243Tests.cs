using System.Reflection;
using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests.POS;

public sealed class BomRecipeCreateHardeningIssue243Tests : IntegrationTestBase
{
    private const int GramUnitId = 1;
    private const int MilliliterUnitId = 3;
    private const int AvailableDrinkId = 3;
    private const int AvailableSizeId = 2;

    [Fact]
    public async Task CreateRecipe_ValidPayload_SavesSuccessfully()
    {
        using var context = CreateDbContext();
        var result = await CreateService(context).CreateRecipeAsync(ValidPosRecipe());

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(await context.Recipes.AnyAsync(r =>
            r.DrinkId == AvailableDrinkId
            && r.SizeId == AvailableSizeId
            && r.Active));
    }

    [Fact]
    public async Task CreateRecipe_PreservesFormOnValidationError()
    {
        var controller = new AdminRecipeController(
            null!, null!, null!, null!, null!, null!, null!);
        controller.ModelState.AddModelError("Details", "Định lượng phải lớn hơn 0.");
        var submitted = ValidPosRecipe();

        var result = await controller.Create(submitted);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains(BomRecipeErrorCodes.InvalidPayload, json);
        Assert.DoesNotContain("redirect", json, StringComparison.OrdinalIgnoreCase);

        var script = ReadRepoFile("CafeChain/wwwroot/js/Admin/Recipe/bom-builder.js");
        Assert.Contains("showFormError", script);
        Assert.DoesNotContain("location.reload", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipe_DoesNotExposeDbException()
    {
        using var context = CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER TR_Recipe_TechnicalFailure
            BEFORE INSERT ON Recipes
            BEGIN
                SELECT RAISE(ABORT, 'SENSITIVE DATABASE DETAIL');
            END;
            """);

        var result = await CreateService(context).CreateRecipeAsync(ValidPosRecipe());

        Assert.False(result.IsSuccess);
        Assert.Equal(BomRecipeErrorCodes.TechnicalError, result.ErrorCode);
        Assert.Contains("Không thể lưu công thức", result.Message);
        Assert.DoesNotContain("SENSITIVE", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbUpdateException", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inner exception", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipe_ConcurrentDuplicate_ReturnsBusinessConflict()
    {
        using var context = CreateDbContext();
        // Simulates the final database guard winning after an application pre-check race.
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER TR_Recipe_ConcurrentUniqueConflict
            BEFORE INSERT ON Recipes
            BEGIN
                SELECT RAISE(ABORT, 'UX_Recipes_OneActive_Drink_Size');
            END;
            """);

        var result = await CreateService(context).CreateRecipeAsync(ValidPosRecipe());

        Assert.False(result.IsSuccess);
        Assert.Equal(BomRecipeErrorCodes.RecipeOverlap, result.ErrorCode);
        Assert.Contains("đã có công thức", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipe_OverlappingEffectivePeriod_IsRejectedClearly()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);
        Assert.True((await service.CreateRecipeAsync(ValidPosRecipe())).IsSuccess);

        var overlapping = ValidPosRecipe();
        overlapping.EffectiveDate = DateTime.Today.AddDays(7);
        var result = await service.CreateRecipeAsync(overlapping);

        Assert.False(result.IsSuccess);
        Assert.Equal(BomRecipeErrorCodes.RecipeOverlap, result.ErrorCode);
        Assert.Contains("phiên bản kế tiếp", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipe_DuplicateComponent_IsRejectedOrMergedByContract()
    {
        using var context = CreateDbContext();
        var model = ValidPosRecipe();
        model.Details.Add(IngredientDetail(1, GramUnitId, 5m));

        var result = await CreateService(context).CreateRecipeAsync(model);

        Assert.False(result.IsSuccess);
        Assert.Equal(BomRecipeErrorCodes.ComponentDuplicate, result.ErrorCode);
        Assert.Contains("Trùng thành phần", result.Message);
    }

    [Fact]
    public async Task CreateRecipe_IncompatibleUom_IsRejected()
    {
        using var context = CreateDbContext();
        var model = ValidPosRecipe();
        model.Details = new() { IngredientDetail(1, MilliliterUnitId, 10m) };

        var result = await CreateService(context).CreateRecipeAsync(model);

        Assert.False(result.IsSuccess);
        Assert.Equal(BomRecipeErrorCodes.ComponentUomIncompatible, result.ErrorCode);
        Assert.Contains("không cùng loại", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipe_MissingConversion_IsRejectedClearly()
    {
        using var context = CreateDbContext();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var baseUnit = new Unit
        {
            UnitCode = $"mass-base-{suffix}",
            Name = "Đơn vị khối lượng gốc",
            Type = UnitType.KhoiLuong,
            Active = true
        };
        var selectedUnit = new Unit
        {
            UnitCode = $"mass-input-{suffix}",
            Name = "Đơn vị khối lượng nhập",
            Type = UnitType.KhoiLuong,
            Active = true
        };
        context.Units.AddRange(baseUnit, selectedUnit);
        await context.SaveChangesAsync();
        var ingredient = new Ingredient
        {
            Code = $"ING-MISSING-{suffix}",
            Name = "Nguyên liệu thiếu quy đổi",
            BaseUnitId = baseUnit.UnitId,
            Active = true
        };
        context.Ingredients.Add(ingredient);
        await context.SaveChangesAsync();

        var model = ValidPosRecipe();
        model.Details = new() { IngredientDetail(ingredient.IngredientId, selectedUnit.UnitId, 1m) };
        var result = await CreateService(context).CreateRecipeAsync(model);

        Assert.False(result.IsSuccess);
        Assert.Equal(BomRecipeErrorCodes.ComponentConversionMissing, result.ErrorCode);
        Assert.Contains("Thiếu quy đổi", result.Message);
    }

    [Fact]
    public async Task CreateRecipe_CircularBom_IsRejected()
    {
        using var context = CreateDbContext();
        var preparedItem = new PreparedItem
        {
            Code = $"BTP-CYCLE-{Guid.NewGuid():N}",
            Name = "BTP kiểm tra vòng lặp",
            BaseUnitId = GramUnitId,
            Active = true
        };
        context.PreparedItems.Add(preparedItem);
        await context.SaveChangesAsync();

        var childRecipe = new Recipe
        {
            RecipeCode = $"RCP-CYCLE-{Guid.NewGuid():N}",
            Name = "Công thức con gây vòng lặp",
            Active = true,
            Status = "Active",
            PreparedItemId = preparedItem.PreparedItemId,
            OutputQuantity = 1m,
            OutputUnitId = GramUnitId,
            RecipeDetails = new List<RecipeDetail>
            {
                new() { IngredientId = 1, Quantity = 1m, UnitId = GramUnitId }
            }
        };
        context.Recipes.Add(childRecipe);
        await context.SaveChangesAsync();

        var model = new RecipeCreateVM
        {
            RecipeType = "SUBRECIPE",
            PreparedItemId = preparedItem.PreparedItemId,
            ExpectedYield = 1m,
            OutputUnitId = GramUnitId,
            EffectiveDate = DateTime.Today,
            Active = true,
            Details = new List<RecipeDetailVM>
            {
                new()
                {
                    ItemCode = $"REC_{childRecipe.RecipeId}",
                    Quantity = 1m,
                    UnitId = GramUnitId,
                    YieldPercentage = 100m
                }
            }
        };

        var result = await CreateService(context).CreateRecipeAsync(model);

        Assert.False(result.IsSuccess);
        Assert.Equal(BomRecipeErrorCodes.CircularDependency, result.ErrorCode);
        Assert.Contains("vòng lặp", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipe_InactiveComponent_IsRejected()
    {
        using var context = CreateDbContext();
        var ingredient = await context.Ingredients.SingleAsync(i => i.IngredientId == 1);
        ingredient.Active = false;
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateRecipeAsync(ValidPosRecipe());

        Assert.False(result.IsSuccess);
        Assert.Equal(BomRecipeErrorCodes.ComponentInactive, result.ErrorCode);
        Assert.Contains("ngưng hoạt động", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateRecipe_UsesVietnameseUnitLabels()
    {
        var formatter = typeof(AdminRecipeService).GetMethod(
            "FormatUnitLabel",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(formatter);
        Assert.Equal("g", formatter!.Invoke(null, new object?[] { "Gram" }));
        Assert.Equal("kg", formatter.Invoke(null, new object?[] { "kg" }));
        Assert.Equal("ml", formatter.Invoke(null, new object?[] { "ml" }));
        Assert.Equal("L", formatter.Invoke(null, new object?[] { "l" }));
        Assert.Equal("cái", formatter.Invoke(null, new object?[] { "pcs" }));

        var script = ReadRepoFile("CafeChain/wwwroot/js/Admin/Recipe/bom-builder.js");
        Assert.Contains("code === 'pcs' || code === 'piece'", script);
        Assert.Contains("code === 'l' || code === 'liter'", script);
    }

    [Fact]
    public void CreateRecipe_PreventsDoubleSubmit()
    {
        var script = ReadRepoFile("CafeChain/wwwroot/js/Admin/Recipe/bom-builder.js");

        Assert.Contains("saveInFlight = false", script);
        Assert.Contains("if (saveInFlight) return", script);
        Assert.Contains("setSaving(true)", script);
        Assert.Contains("setSaving(false)", script);
        Assert.Contains("prop('disabled', isSaving || createBlockedByActiveRecipe)", script);
    }

    [Fact]
    public async Task CreateRecipe_CostIncomplete_FollowsConfiguredPolicy()
    {
        using var context = CreateDbContext();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var noPriceIngredient = new Ingredient
        {
            Code = $"ING-NOPRICE-{suffix}",
            Name = "Nguyên liệu chưa có giá",
            BaseUnitId = GramUnitId,
            Active = true
        };
        context.Ingredients.Add(noPriceIngredient);
        await context.SaveChangesAsync();

        var model = ValidPosRecipe();
        model.Details = new()
        {
            IngredientDetail(noPriceIngredient.IngredientId, GramUnitId, 2m)
        };
        var result = await CreateService(context).CreateRecipeAsync(model);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(await context.Recipes.AnyAsync(r =>
            r.DrinkId == AvailableDrinkId && r.SizeId == AvailableSizeId));
    }

    private static AdminRecipeService CreateService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        return new AdminRecipeService(
            context,
            new RecipeOutputNormalizer(context, physical),
            NullLogger<AdminRecipeService>.Instance);
    }

    private static RecipeCreateVM ValidPosRecipe() => new()
    {
        RecipeType = "POS",
        DrinkId = AvailableDrinkId,
        SizeId = AvailableSizeId,
        Active = true,
        EffectiveDate = DateTime.Today,
        Details = new List<RecipeDetailVM>
        {
            IngredientDetail(1, GramUnitId, 10m)
        }
    };

    private static RecipeDetailVM IngredientDetail(int ingredientId, int unitId, decimal quantity) => new()
    {
        ItemCode = $"ING_{ingredientId}",
        Quantity = quantity,
        UnitId = unitId,
        YieldPercentage = 100m
    };

    private static string ReadRepoFile(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", relativePath));
        Assert.True(File.Exists(path), $"Không tìm thấy file kiểm thử: {path}");
        return File.ReadAllText(path);
    }
}
