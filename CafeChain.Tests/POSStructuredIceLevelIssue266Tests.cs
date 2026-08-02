using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Constants;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.POS;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ice;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CafeChain.Tests.POS;

public sealed class POSStructuredIceLevelIssue266Tests : IntegrationTestBase
{
    private const int StoreId = 2661;
    private const int DrinkId = 2662;
    private const int SizeId = 2663;
    private const int IceIngredientId = 2664;
    private const int UnitId = 2665;

    [Fact]
    public void IceLevelContract_AllowsOnlyZeroFiftyAndOneHundred()
    {
        Assert.True(POSIceLevels.IsAllowed(0));
        Assert.True(POSIceLevels.IsAllowed(50));
        Assert.True(POSIceLevels.IsAllowed(100));
        Assert.False(POSIceLevels.IsAllowed(-1));
        Assert.False(POSIceLevels.IsAllowed(25));
        Assert.False(POSIceLevels.IsAllowed(75));
    }

    [Fact]
    public async Task CreateSnapshot_UsesStructuredQuantityAndRejectsInvalidPercent()
    {
        using var context = CreateDbContext();
        SeedPolicyAndDirectRecipe(context, baseIceQuantity: 150m);
        var service = CreateService(context);

        var half = await service.CreateOrderSnapshotAsync(
            StoreId, DrinkId, SizeId, quantity: 2, iceLevelPercent: 50);

        Assert.True(half.IsSuccess, half.Message);
        Assert.NotNull(half.Data);
        Assert.Equal(50, half.Data!.IceLevelPercent);
        Assert.Equal(300m, half.Data.BaseIceQuantityBaseUnit);
        Assert.Equal(150m, half.Data.AppliedIceQuantityBaseUnit);
        Assert.Equal(IceIngredientId, half.Data.IceIngredientId);

        var invalid = await service.CreateOrderSnapshotAsync(
            StoreId, DrinkId, SizeId, quantity: 1, iceLevelPercent: 25);

        Assert.False(invalid.IsSuccess);
        Assert.Equal(POSIceCustomizationService.InvalidIceLevel, invalid.ErrorCode);
    }

    [Fact]
    public async Task CreateSnapshot_HidesIceForRecipeWithoutCanonicalIce()
    {
        using var context = CreateDbContext();
        context.IcePolicies.Add(new IcePolicy
        {
            StoreId = StoreId,
            IngredientId = IceIngredientId,
            DisplayUnitId = UnitId,
            Active = true,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByStaffId = 1
        });
        context.Recipes.Add(new Recipe
        {
            RecipeId = 2670,
            RecipeCode = "POS_NO_ICE",
            Name = "No ice recipe",
            DrinkId = DrinkId,
            SizeId = SizeId,
            Active = true,
            Status = "Active",
            RecipeDetails = new List<RecipeDetail>
            {
                new() { IngredientId = 2671, Quantity = 30m, UnitId = UnitId }
            }
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.CreateOrderSnapshotAsync(
            StoreId, DrinkId, SizeId, quantity: 1, iceLevelPercent: null);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task CreateSnapshot_FlattensChildRecipeWithoutScalingOtherLines()
    {
        using var context = CreateDbContext();
        context.IcePolicies.Add(new IcePolicy
        {
            StoreId = StoreId,
            IngredientId = IceIngredientId,
            DisplayUnitId = UnitId,
            Active = true,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByStaffId = 1
        });
        context.Recipes.AddRange(
            new Recipe
            {
                RecipeId = 2680,
                RecipeCode = "POS_PARENT_BOM",
                Name = "Parent BOM",
                DrinkId = DrinkId,
                SizeId = SizeId,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = 2681, Quantity = 1m, UnitId = UnitId }
                }
            },
            new Recipe
            {
                RecipeId = 2681,
                RecipeCode = "POS_CHILD_BOM",
                Name = "Child BOM",
                Active = true,
                Status = "Active",
                OutputQuantity = 1m,
                OutputUnitId = UnitId,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = IceIngredientId, Quantity = 150m, UnitId = UnitId },
                    new() { IngredientId = 2682, Quantity = 30m, UnitId = UnitId }
                }
            });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.CreateOrderSnapshotAsync(
            StoreId, DrinkId, SizeId, quantity: 1, iceLevelPercent: 50);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(150m, result.Data!.BaseIceQuantityBaseUnit);
        Assert.Equal(75m, result.Data.AppliedIceQuantityBaseUnit);
        Assert.Equal(IceIngredientId, result.Data.IceIngredientId);
    }

    [Fact]
    public async Task InventoryDeduction_UsesAppliedIceForNestedChildWithoutScalingWholeChild()
    {
        using var context = CreateDbContext();
        EnsureUnit(context);
        context.Ingredients.AddRange(
            new Ingredient { IngredientId = IceIngredientId, Code = "ICE266", Name = "Canonical ice", BaseUnitId = UnitId, Active = true },
            new Ingredient { IngredientId = 2682, Code = "OTHER266", Name = "Other child input", BaseUnitId = UnitId, Active = true });
        context.Recipes.AddRange(
            new Recipe
            {
                RecipeId = 2690,
                RecipeCode = "POS_PARENT_LEDGER",
                Name = "Parent ledger recipe",
                DrinkId = DrinkId,
                SizeId = SizeId,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = 2691, Quantity = 1m, UnitId = UnitId }
                }
            },
            new Recipe
            {
                RecipeId = 2691,
                RecipeCode = "POS_CHILD_LEDGER",
                Name = "Child ledger recipe",
                Active = true,
                Status = "Active",
                OutputQuantity = 1m,
                OutputUnitId = UnitId,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = IceIngredientId, Quantity = 150m, UnitId = UnitId },
                    new() { IngredientId = 2682, Quantity = 30m, UnitId = UnitId }
                }
            });
        context.StoreInventories.AddRange(
            new StoreInventory { StoreInventoryId = 2692, StoreId = StoreId, IngredientId = IceIngredientId, AvailableQty = 1000m, LastUpdated = DateTime.UtcNow },
            new StoreInventory { StoreInventoryId = 2693, StoreId = StoreId, IngredientId = 2682, AvailableQty = 1000m, LastUpdated = DateTime.UtcNow },
            new StoreInventory { StoreInventoryId = 2694, StoreId = StoreId, RecipeId = 2691, AvailableQty = 1000m, LastUpdated = DateTime.UtcNow });
        context.Orders.Add(new Order
        {
            OrderId = 2695,
            StoreId = StoreId,
            OrderStatusId = SystemConstants.OrderStatuses.Completed,
            PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
            OrderTypeId = SystemConstants.OrderTypes.Delivery,
            Source = "POS",
            SubTotal = 100,
            Total = 100,
            CreatedAt = DateTime.UtcNow,
            OrderDetails = new List<OrderDetail>
            {
                new()
                {
                    DrinkId = DrinkId,
                    SizeId = SizeId,
                    DrinkName = "Nested ice test",
                    Quantity = 1,
                    Price = 100,
                    Note = "",
                    IceLevelPercent = 50,
                    IceIngredientId = IceIngredientId,
                    BaseIceQuantityBaseUnit = 150m,
                    AppliedIceQuantityBaseUnit = 75m,
                    OrderToppings = new List<OrderTopping>()
                }
            }
        });
        await context.SaveChangesAsync();

        var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
        var unit = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
        var normalizer = new RecipeOutputNormalizer(context, physical);
        var estimated = new EstimatedBomCostService(
            context, unit, physical, normalizer, NullLogger<EstimatedBomCostService>.Instance);
        var service = new InventoryDeductionService(
            context,
            NullLogger<InventoryDeductionService>.Instance,
            unit,
            estimated,
            physical);

        var result = await service.DeductStockForCommittedOrderAsync(
            new List<POSSoldItemDto>
            {
                new() { DrinkId = DrinkId, SizeId = SizeId, Quantity = 1 }
            },
            StoreId,
            2695);

        Assert.True(result.IsSuccess, result.Message);
        var iceInventory = await context.StoreInventories.SingleAsync(x => x.StoreInventoryId == 2692);
        var otherInventory = await context.StoreInventories.SingleAsync(x => x.StoreInventoryId == 2693);
        var btpInventory = await context.StoreInventories.SingleAsync(x => x.StoreInventoryId == 2694);
        Assert.Equal(925m, iceInventory.AvailableQty);
        Assert.Equal(1000m, otherInventory.AvailableQty);
        Assert.Equal(999m, btpInventory.AvailableQty);
        Assert.Equal(75m, await context.InventoryTransactions
            .Where(x => x.ReferenceOrderId == 2695 && x.StoreInventoryId == 2692)
            .Select(x => x.Quantity)
            .SingleAsync());
    }

    private static POSIceCustomizationService CreateService(AppDbContext context)
    {
        var unit = new Mock<IUnitConversionService>();
        unit.Setup(x => x.ConvertAsync(
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<int>(),
                It.IsAny<int?>()))
            .ReturnsAsync((int _, decimal quantity, int _, int? _) =>
                ServiceResult<decimal>.Success(quantity));

        var physical = new Mock<IPhysicalUnitConversionService>();
        physical.Setup(x => x.ConvertAsync(
                It.IsAny<decimal>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync((decimal quantity, int _, int _) =>
                ServiceResult<decimal>.Success(quantity));

        return new POSIceCustomizationService(context, unit.Object, physical.Object);
    }

    private static void SeedPolicyAndDirectRecipe(AppDbContext context, decimal baseIceQuantity)
    {
        context.IcePolicies.Add(new IcePolicy
        {
            StoreId = StoreId,
            IngredientId = IceIngredientId,
            DisplayUnitId = UnitId,
            Active = true,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByStaffId = 1
        });
        context.Recipes.Add(new Recipe
        {
            RecipeId = 2669,
            RecipeCode = "POS_DIRECT_ICE",
            Name = "Direct ice recipe",
            DrinkId = DrinkId,
            SizeId = SizeId,
            Active = true,
            Status = "Active",
            RecipeDetails = new List<RecipeDetail>
            {
                new() { IngredientId = IceIngredientId, Quantity = baseIceQuantity, UnitId = UnitId },
                new() { IngredientId = 2672, Quantity = 30m, UnitId = UnitId }
            }
        });
        context.SaveChanges();
    }

    private static void EnsureUnit(AppDbContext context)
    {
        if (!context.Units.Any(x => x.UnitId == UnitId))
        {
            context.Units.Add(new Unit
            {
                UnitId = UnitId,
                UnitCode = "g266",
                Name = "Gram 266",
                Type = CafeChain.Models.Enums.Unit.UnitType.KhoiLuong,
                Active = true
            });
            context.SaveChanges();
        }
    }
}
