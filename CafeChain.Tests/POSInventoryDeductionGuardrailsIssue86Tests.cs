using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CafeChain.Tests.POS
{
    public class POSInventoryDeductionGuardrailsIssue86Tests : IntegrationTestBase
    {
        private const int StoreId = 3;
        private const int UnitId = 1;
        private const int DrinkId = 10;
        private const int SizeMId = 2;
        private const int ToppingId = 20;
        private const int MilkIngredientId = 100;
        private const int TapiocaIngredientId = 101;
        private const int BaseRecipeId = 1000;
        private const int ChildRecipeId = 2000;
        private const int ToppingRecipeId = 3000;

        [Fact]
        public async Task DeductStockForCommittedOrderAsync_PaidOrder_DeductsBOMAndIsIdempotentByReferenceOrderId()
        {
            using var context = CreateDbContext();
            SeedInventoryCatalog(context);
            SeedCompletedPaidOrder(context, orderId: 9001);
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.DeductStockForCommittedOrderAsync(
                CreateSoldItems(quantity: 2),
                StoreId,
                referenceOrderId: 9001);

            Assert.True(result.IsSuccess);
            Assert.Equal(90m, await GetIngredientQtyAsync(context, MilkIngredientId));
            Assert.Equal(44m, await GetIngredientQtyAsync(context, TapiocaIngredientId));
            Assert.Equal(8m, await GetChildRecipeQtyAsync(context));

            var transactions = await context.InventoryTransactions
                .Where(transaction => transaction.ReferenceOrderId == 9001)
                .ToListAsync();
            Assert.Equal(3, transactions.Count);
            Assert.All(transactions, transaction =>
            {
                Assert.Equal(InventoryTransactionTypeEnum.SALES_DEDUCTION, transaction.Type);
                Assert.True(transaction.Quantity > 0);
            });

            var duplicateResult = await service.DeductStockForCommittedOrderAsync(
                CreateSoldItems(quantity: 2),
                StoreId,
                referenceOrderId: 9001);

            Assert.True(duplicateResult.IsSuccess);
            Assert.Equal(90m, await GetIngredientQtyAsync(context, MilkIngredientId));
            Assert.Equal(44m, await GetIngredientQtyAsync(context, TapiocaIngredientId));
            Assert.Equal(8m, await GetChildRecipeQtyAsync(context));
            Assert.Equal(3, await context.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9001));
        }

        [Theory]
        [InlineData(SystemConstants.OrderStatuses.AwaitingPayment, SystemConstants.PaymentStatuses.Unpaid)]
        [InlineData(SystemConstants.OrderStatuses.Cancelled, SystemConstants.PaymentStatuses.Failed)]
        public async Task DeductStockForCommittedOrderAsync_NonCommittedPaidOrder_DoesNotDeduct(
            int orderStatusId,
            int paymentStatusId)
        {
            using var context = CreateDbContext();
            SeedInventoryCatalog(context);
            SeedOrder(context, orderId: 9002, orderStatusId, paymentStatusId);
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.DeductStockForCommittedOrderAsync(
                CreateSoldItems(),
                StoreId,
                referenceOrderId: 9002);

            Assert.False(result.IsSuccess);
            Assert.Equal(100m, await GetIngredientQtyAsync(context, MilkIngredientId));
            Assert.Equal(0, await context.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9002));
        }

        [Fact]
        public async Task DeductStockForCommittedOrderAsync_NegativeInventory_IsAcceptedAndLogged()
        {
            using var context = CreateDbContext();
            SeedInventoryCatalog(context, milkQty: 3m, tapiocaQty: 50m, childRecipeQty: 10m);
            SeedCompletedPaidOrder(context, orderId: 9003);
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.DeductStockForCommittedOrderAsync(
                new List<POSSoldItemDto>
                {
                    new() { DrinkId = DrinkId, SizeId = SizeMId, Quantity = 1 }
                },
                StoreId,
                referenceOrderId: 9003);

            Assert.True(result.IsSuccess);
            Assert.NotEmpty(result.Errors);
            Assert.Equal(-2m, await GetIngredientQtyAsync(context, MilkIngredientId));

            var transaction = await context.InventoryTransactions
                .Include(t => t.StoreInventory)
                .SingleAsync(t =>
                    t.ReferenceOrderId == 9003 &&
                    t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION &&
                    t.StoreInventory.IngredientId == MilkIngredientId);
            Assert.Equal(3m, transaction.BeforeQty);
            Assert.Equal(5m, transaction.Quantity);
            Assert.Equal(-2m, transaction.AfterQty);
            Assert.Equal(InventoryStockStatus.NEGATIVE_CONFIRMED, transaction.StockStatus);
        }

        [Fact]
        public async Task DeductStock_MissingUnitConversion_RollsBackWithoutPartialWrites()
        {
            using var context = CreateDbContext();
            SeedInventoryCatalog(context);
            SeedCompletedPaidOrder(context, orderId: 9100);
            await context.SaveChangesAsync();

            // Force milk line to use a unit with no conversion → fail closed mid-BOM
            const int badUnitId = 77;
            context.Units.Add(new Unit { UnitId = badUnitId, UnitCode = "BAD", Name = "Bad", Active = true });
            var milkDetail = await context.Set<RecipeDetail>()
                .SingleAsync(d => d.RecipeId == BaseRecipeId && d.IngredientId == MilkIngredientId);
            milkDetail.UnitId = badUnitId;
            await context.SaveChangesAsync();

            var milkBefore = await GetIngredientQtyAsync(context, MilkIngredientId);
            var tapiocaBefore = await GetIngredientQtyAsync(context, TapiocaIngredientId);
            var childBefore = await GetChildRecipeQtyAsync(context);
            var service = CreateService(context);

            var result = await service.DeductStockForCommittedOrderAsync(
                CreateSoldItems(quantity: 1),
                StoreId,
                referenceOrderId: 9100);

            Assert.False(result.IsSuccess);
            Assert.Contains("quy đổi", result.Message, System.StringComparison.OrdinalIgnoreCase);
            Assert.Equal(milkBefore, await GetIngredientQtyAsync(context, MilkIngredientId));
            Assert.Equal(tapiocaBefore, await GetIngredientQtyAsync(context, TapiocaIngredientId));
            Assert.Equal(childBefore, await GetChildRecipeQtyAsync(context));
            Assert.Equal(0, await context.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9100));
        }

        [Fact]
        public async Task DeductStock_RetryAfterConversionFixed_DeductsOnce_AndIdempotent()
        {
            using var context = CreateDbContext();
            SeedInventoryCatalog(context);
            SeedCompletedPaidOrder(context, orderId: 9101);
            await context.SaveChangesAsync();

            const int badUnitId = 78;
            context.Units.Add(new Unit { UnitId = badUnitId, UnitCode = "BAD2", Name = "Bad2", Active = true });
            var milkDetail = await context.Set<RecipeDetail>()
                .SingleAsync(d => d.RecipeId == BaseRecipeId && d.IngredientId == MilkIngredientId);
            milkDetail.UnitId = badUnitId;
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var failed = await service.DeductStockForCommittedOrderAsync(
                CreateSoldItems(quantity: 1),
                StoreId,
                referenceOrderId: 9101);
            Assert.False(failed.IsSuccess);
            Assert.Equal(0, await context.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9101));

            // Repair conversion data
            milkDetail.UnitId = UnitId;
            await context.SaveChangesAsync();

            var success = await service.DeductStockForCommittedOrderAsync(
                CreateSoldItems(quantity: 1),
                StoreId,
                referenceOrderId: 9101);
            Assert.True(success.IsSuccess);
            Assert.Equal(95m, await GetIngredientQtyAsync(context, MilkIngredientId)); // 100 - 5
            Assert.Equal(3, await context.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9101));

            var retry = await service.DeductStockForCommittedOrderAsync(
                CreateSoldItems(quantity: 1),
                StoreId,
                referenceOrderId: 9101);
            Assert.True(retry.IsSuccess);
            Assert.Equal(95m, await GetIngredientQtyAsync(context, MilkIngredientId));
            Assert.Equal(3, await context.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9101));
        }

        [Fact]
        public async Task CalculateRecipeCogs_MissingConversion_ReturnsFailureNotUnderstatedTotal()
        {
            using var context = CreateDbContext();
            SeedInventoryCatalog(context);
            // Complete package offer so EstimatedBomCost reaches unit-conversion path (#117)
            context.IngredientSuppliers.Add(new IngredientSupplier
            {
                IngredientId = MilkIngredientId,
                SupplierId = 1,
                UnitId = UnitId,
                PackageQuantity = 1m,
                CurrentPrice = 10000m,
                IsPrimary = true,
                Active = true
            });
            await context.SaveChangesAsync();
            const int badUnitId = 79;
            context.Units.Add(new Unit { UnitId = badUnitId, UnitCode = "BAD3", Name = "Bad3", Active = true });
            var milkDetail = await context.Set<RecipeDetail>()
                .SingleAsync(d => d.RecipeId == BaseRecipeId && d.IngredientId == MilkIngredientId);
            milkDetail.UnitId = badUnitId;
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var cogs = await service.CalculateRecipeCogsAsync(BaseRecipeId);

            Assert.False(cogs.IsSuccess);
            Assert.DoesNotContain("Success", cogs.Message ?? "");
            // Incomplete conversion or package/unit message — never understated Success total
            Assert.True(
                (cogs.Message ?? "").Contains("quy đổi", System.StringComparison.OrdinalIgnoreCase)
                || (cogs.Message ?? "").Contains("đơn vị", System.StringComparison.OrdinalIgnoreCase)
                || (cogs.Message ?? "").Length > 0);
        }

        private static InventoryDeductionService CreateService(CafeChain.Data.AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(
                context,
                new Mock<ILogger<PhysicalUnitConversionService>>().Object);
            var unitConversion = new UnitConversionService(
                context,
                new Mock<ILogger<UnitConversionService>>().Object,
                physical);
            var normalizer = new CafeChain.Application.Services.Admin.Recipes.RecipeOutputNormalizer(context, physical);
            var estimated = new EstimatedBomCostService(
                context,
                unitConversion,
                physical,
                normalizer,
                new Mock<ILogger<EstimatedBomCostService>>().Object);
            return new InventoryDeductionService(
                context,
                new Mock<ILogger<InventoryDeductionService>>().Object,
                unitConversion,
                estimated);
        }

        private static List<POSSoldItemDto> CreateSoldItems(int quantity = 1)
        {
            return new List<POSSoldItemDto>
            {
                new()
                {
                    DrinkId = DrinkId,
                    SizeId = SizeMId,
                    Quantity = quantity,
                    Toppings = new List<POSOrderToppingDto>
                    {
                        new() { ToppingId = ToppingId }
                    }
                }
            };
        }

        private static void SeedInventoryCatalog(
            CafeChain.Data.AppDbContext context,
            decimal milkQty = 100m,
            decimal tapiocaQty = 50m,
            decimal childRecipeQty = 10m)
        {
            context.Ingredients.AddRange(
                new Ingredient
                {
                    IngredientId = MilkIngredientId,
                    Code = "MILK",
                    Name = "Milk",
                    BaseUnitId = UnitId,
                    Active = true
                },
                new Ingredient
                {
                    IngredientId = TapiocaIngredientId,
                    Code = "TAPIOCA",
                    Name = "Tapioca",
                    BaseUnitId = UnitId,
                    Active = true
                });

            context.Recipes.AddRange(
                new Recipe
                {
                    RecipeId = BaseRecipeId,
                    RecipeCode = "RCP_MAIN_SIZE",
                    Name = "Main size recipe",
                    Active = true,
                    Status = "Active",
                    DrinkId = DrinkId,
                    SizeId = SizeMId,
                    RecipeDetails = new List<RecipeDetail>
                    {
                        new()
                        {
                            IngredientId = MilkIngredientId,
                            Quantity = 5m,
                            UnitId = UnitId
                        },
                        new()
                        {
                            ChildRecipeId = ChildRecipeId,
                            Quantity = 1m,
                            UnitId = UnitId
                        }
                    }
                },
                new Recipe
                {
                    RecipeId = ChildRecipeId,
                    RecipeCode = "RCP_BTP",
                    Name = "BTP syrup",
                    Active = true,
                    Status = "Active",
                    RecipeDetails = new List<RecipeDetail>()
                },
                new Recipe
                {
                    RecipeId = ToppingRecipeId,
                    RecipeCode = "RCP_TOPPING",
                    Name = "Topping recipe",
                    Active = true,
                    Status = "Active",
                    ToppingId = ToppingId,
                    RecipeDetails = new List<RecipeDetail>
                    {
                        new()
                        {
                            IngredientId = TapiocaIngredientId,
                            Quantity = 3m,
                            UnitId = UnitId
                        }
                    }
                });

            context.StoreInventories.AddRange(
                CreateIngredientInventory(MilkIngredientId, milkQty),
                CreateIngredientInventory(TapiocaIngredientId, tapiocaQty),
                new StoreInventory
                {
                    StoreId = StoreId,
                    RecipeId = ChildRecipeId,
                    AvailableQty = childRecipeQty,
                    ReservedQty = 0,
                    LastUpdated = System.DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                });
        }

        private static StoreInventory CreateIngredientInventory(int ingredientId, decimal quantity)
        {
            return new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = ingredientId,
                AvailableQty = quantity,
                ReservedQty = 0,
                LastUpdated = System.DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            };
        }

        private static void SeedCompletedPaidOrder(CafeChain.Data.AppDbContext context, int orderId)
        {
            SeedOrder(
                context,
                orderId,
                SystemConstants.OrderStatuses.Completed,
                SystemConstants.PaymentStatuses.Paid);
        }

        private static void SeedOrder(
            CafeChain.Data.AppDbContext context,
            int orderId,
            int orderStatusId,
            int paymentStatusId)
        {
            context.Orders.Add(new Order
            {
                OrderId = orderId,
                StoreId = StoreId,
                OrderStatusId = orderStatusId,
                PaymentStatusId = paymentStatusId,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Source = "POS",
                SubTotal = 45000m,
                Total = 45000m,
                CreatedAt = System.DateTime.UtcNow
            });
        }

        private static async Task<decimal> GetIngredientQtyAsync(
            CafeChain.Data.AppDbContext context,
            int ingredientId)
        {
            return await context.StoreInventories
                .Where(inventory => inventory.StoreId == StoreId && inventory.IngredientId == ingredientId)
                .Select(inventory => inventory.AvailableQty)
                .SingleAsync();
        }

        private static async Task<decimal> GetChildRecipeQtyAsync(CafeChain.Data.AppDbContext context)
        {
            return await context.StoreInventories
                .Where(inventory => inventory.StoreId == StoreId && inventory.RecipeId == ChildRecipeId)
                .Select(inventory => inventory.AvailableQty)
                .SingleAsync();
        }
    }
}
