using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class ProductionSourceEligibilityTests : IntegrationTestBase
{
    private const int PreparedStoreId = 38602;
    private const int PreparedItemId = 38603;
    private const int PreparedRecipeId = 38604;

    [Fact]
    public async Task RawMaterialWithoutProductionCapability_CannotUseProduction()
    {
        using var context = CreateDbContext();
        var gram = context.Units.Single(x => x.UnitCode == "g");
        var store = new Store
        {
            StoreId = 38601,
            Name = "Cửa hàng eligibility",
            Address = "Test",
            Phone = "000",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        var ingredient = new Ingredient
        {
            IngredientId = 38601,
            Code = "ING-38601",
            Name = "Nguyên liệu thô",
            BaseUnitId = gram.UnitId,
            Active = true
        };
        context.Stores.Add(store);
        context.Ingredients.Add(ingredient);
        await context.SaveChangesAsync();

        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var service = new ProductionSourceEligibilityService(
            context,
            new RecipeOutputNormalizer(context, physical),
            CreateAllowedPermissionService());

        var result = await service.EvaluateAsync(new ProductionSourceEligibilityRequest
        {
            StoreId = store.StoreId,
            ActorAccountId = 5,
            IngredientId = ingredient.IngredientId,
            RequiredPermissionCode = PermissionConstants.RestockSelectProductionSource
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.False(result.Data!.Eligible);
        Assert.Equal(ProductionEligibilityReasonCodes.ItemCapabilityMissing, result.Data.ReasonCode);
        Assert.DoesNotContain(result.Data.ReasonCode, result.Data.Message, StringComparison.Ordinal);
        Assert.Contains("chưa được cấu hình", result.Data.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GlobalCapabilityWithoutStoreCapability_CannotUseProduction()
    {
        using var context = CreateDbContext();
        await SeedPreparedItemAsync(context, includeStoreCapability: false);
        var service = CreateService(context, CreateAllowedPermissionService());

        var result = await service.EvaluateAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.Eligible);
        Assert.Equal(ProductionEligibilityReasonCodes.StoreCapabilityMissing, result.Data.ReasonCode);
    }

    [Fact]
    public async Task GlobalAndStoreCapability_WithActiveRecipe_ReturnsExpectedYield()
    {
        using var context = CreateDbContext();
        await SeedPreparedItemAsync(context, includeStoreCapability: true);
        var service = CreateService(context, CreateAllowedPermissionService());

        var result = await service.EvaluateAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.Eligible);
        Assert.Equal(ProductionEligibilityReasonCodes.Eligible, result.Data.ReasonCode);
        Assert.Equal(PreparedRecipeId, result.Data.RecipeId);
        Assert.Equal(5_000m, result.Data.ExpectedOutputPerBatchBase);
    }

    [Fact]
    public async Task ValidCapabilityWithoutPermission_IsRejectedByBackend()
    {
        using var context = CreateDbContext();
        await SeedPreparedItemAsync(context, includeStoreCapability: true);
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto { Allowed = false }));
        var service = CreateService(context, permissions.Object);

        var result = await service.EvaluateAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.Eligible);
        Assert.Equal(ProductionEligibilityReasonCodes.PermissionDenied, result.Data.ReasonCode);
    }

    private static ProductionSourceEligibilityRequest Request() => new()
    {
        StoreId = PreparedStoreId,
        ActorAccountId = 5,
        PreparedItemId = PreparedItemId,
        RequiredPermissionCode = PermissionConstants.RestockSelectProductionSource
    };

    private static ProductionSourceEligibilityService CreateService(
        CafeChain.Data.AppDbContext context,
        IAdminPermissionService permissions)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        return new ProductionSourceEligibilityService(
            context,
            new RecipeOutputNormalizer(context, physical),
            permissions);
    }

    private static async Task SeedPreparedItemAsync(
        CafeChain.Data.AppDbContext context,
        bool includeStoreCapability)
    {
        var gram = context.Units.Single(x => x.UnitCode == "g");
        var now = DateTime.UtcNow;
        context.Stores.Add(new Store
        {
            StoreId = PreparedStoreId,
            Name = "Cửa hàng capability",
            Address = "Test",
            Phone = "000",
            Active = true,
            CreatedAt = now
        });
        context.PreparedItems.Add(new PreparedItem
        {
            PreparedItemId = PreparedItemId,
            Code = "BTP-CAP-386",
            Name = "BTP có năng lực sản xuất",
            BaseUnitId = gram.UnitId,
            Active = true
        });
        context.Recipes.Add(new Recipe
        {
            RecipeId = PreparedRecipeId,
            RecipeCode = "REC-CAP-386",
            Name = "Công thức capability",
            PreparedItemId = PreparedItemId,
            OutputQuantity = 5_000m,
            OutputUnitId = gram.UnitId,
            Status = "Active",
            Active = true,
            EffectiveDate = now.AddMinutes(-1)
        });
        context.InventoryItemSourceCapabilities.Add(new InventoryItemSourceCapability
        {
            PreparedItemId = PreparedItemId,
            CanProduce = true,
            Active = true,
            EffectiveFromUtc = now.AddMinutes(-1),
            CreatedByStaffId = 1,
            CreatedAtUtc = now
        });
        if (includeStoreCapability)
        {
            context.StoreProductionCapabilities.Add(new StoreProductionCapability
            {
                StoreId = PreparedStoreId,
                PreparedItemId = PreparedItemId,
                Active = true,
                EffectiveFromUtc = now.AddMinutes(-1),
                CreatedByStaffId = 1,
                CreatedAtUtc = now
            });
        }
        await context.SaveChangesAsync();
    }

    private static IAdminPermissionService CreateAllowedPermissionService()
    {
        var permissions = new Mock<IAdminPermissionService>();
        permissions
            .Setup(x => x.HasPermissionAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<int?>()))
            .ReturnsAsync((int accountId, string code, int? storeId) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = accountId,
                    PermissionCode = code,
                    TargetStoreId = storeId,
                    Allowed = true,
                    RoleAllowed = true,
                    ScopeAllowed = true
                }));
        return permissions.Object;
    }
}
