using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ingredients;
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
    /// <summary>
    /// Issue #95 / ADR-0004: Guardrails for one-level POS BOM deduction.
    /// Does not change production behavior — locks Ingredient vs BTP paths and non-recursive ChildRecipe.
    /// </summary>
    public class POSBomBranchInventoryBaselineIssue95Tests : IntegrationTestBase
    {
        private const int StoreId = 11;
        private const int UnitId = 1;
        private const int DrinkId = 501;
        private const int SizeId = 2;
        private const int DirectIngredientId = 701;
        private const int LeafInsideBtpIngredientId = 702;
        private const int MainRecipeId = 8100;
        private const int BtpRecipeId = 8200;

        [Fact]
        public async Task Deduct_IngredientDetail_DecrementsStoreInventoryByIngredientId()
        {
            using var context = CreateDbContext();
            SeedCatalogWithBtpLeaves(context, directIngredientQty: 100m, btpQty: 50m, leafInsideBtpQty: 200m);
            SeedPaidOrder(context, orderId: 9501);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.DeductStockForCommittedOrderAsync(
                SoldItems(quantity: 2),
                StoreId,
                referenceOrderId: 9501);

            Assert.True(result.IsSuccess);

            // Main recipe: 10 ingredient * qty 2 = 20
            Assert.Equal(80m, await QtyByIngredientAsync(context, DirectIngredientId));

            var ingredientTxn = await context.InventoryTransactions
                .Include(t => t.StoreInventory)
                .SingleAsync(t =>
                    t.ReferenceOrderId == 9501 &&
                    t.StoreInventory.IngredientId == DirectIngredientId);

            Assert.Equal(InventoryTransactionTypeEnum.SALES_DEDUCTION, ingredientTxn.Type);
            Assert.Equal(20m, ingredientTxn.Quantity);
            Assert.Equal(100m, ingredientTxn.BeforeQty);
            Assert.Equal(80m, ingredientTxn.AfterQty);
            Assert.Null(ingredientTxn.StoreInventory.RecipeId);
        }

        [Fact]
        public async Task Deduct_ChildRecipeDetail_DecrementsStoreInventoryByRecipeId_NotLeafIngredients()
        {
            using var context = CreateDbContext();
            SeedCatalogWithBtpLeaves(context, directIngredientQty: 100m, btpQty: 50m, leafInsideBtpQty: 200m);
            SeedPaidOrder(context, orderId: 9502);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.DeductStockForCommittedOrderAsync(
                SoldItems(quantity: 3),
                StoreId,
                referenceOrderId: 9502);

            Assert.True(result.IsSuccess);

            // ChildRecipe qty on main: 1 * 3 = 3 → BTP stock 50 - 3 = 47
            Assert.Equal(47m, await QtyByRecipeAsync(context, BtpRecipeId));

            // Leaf ingredient only lives under ChildRecipe BOM — must NOT be deducted (one-level)
            Assert.Equal(200m, await QtyByIngredientAsync(context, LeafInsideBtpIngredientId));

            var btpTxn = await context.InventoryTransactions
                .Include(t => t.StoreInventory)
                .SingleAsync(t =>
                    t.ReferenceOrderId == 9502 &&
                    t.StoreInventory.RecipeId == BtpRecipeId);

            Assert.Equal(InventoryTransactionTypeEnum.SALES_DEDUCTION, btpTxn.Type);
            Assert.Equal(3m, btpTxn.Quantity);
            Assert.Null(btpTxn.StoreInventory.IngredientId);

            // No SALES_DEDUCTION against leaf ingredient for this order
            var leafTxnCount = await context.InventoryTransactions
                .Include(t => t.StoreInventory)
                .CountAsync(t =>
                    t.ReferenceOrderId == 9502 &&
                    t.StoreInventory.IngredientId == LeafInsideBtpIngredientId);
            Assert.Equal(0, leafTxnCount);
        }

        [Fact]
        public async Task Deduct_DoesNotRecursivelyExplodeChildRecipe_ToLeafIngredients()
        {
            using var context = CreateDbContext();
            // Give leaf plenty of stock; if recursive explode ran (qty 4 per BTP * 2 sold = 8), leaf would drop.
            SeedCatalogWithBtpLeaves(
                context,
                directIngredientQty: 100m,
                btpQty: 10m,
                leafInsideBtpQty: 40m,
                leafPerBtp: 4m);
            SeedPaidOrder(context, orderId: 9503);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.DeductStockForCommittedOrderAsync(
                SoldItems(quantity: 2),
                StoreId,
                referenceOrderId: 9503);

            Assert.True(result.IsSuccess);
            Assert.Equal(8m, await QtyByRecipeAsync(context, BtpRecipeId));
            Assert.Equal(40m, await QtyByIngredientAsync(context, LeafInsideBtpIngredientId));

            // Exactly two deduction lines: direct ingredient + BTP recipe (not leaf)
            var txns = await context.InventoryTransactions
                .Include(t => t.StoreInventory)
                .Where(t => t.ReferenceOrderId == 9503)
                .ToListAsync();
            Assert.Equal(2, txns.Count);
            Assert.Contains(txns, t => t.StoreInventory.IngredientId == DirectIngredientId);
            Assert.Contains(txns, t => t.StoreInventory.RecipeId == BtpRecipeId);
            Assert.DoesNotContain(txns, t => t.StoreInventory.IngredientId == LeafInsideBtpIngredientId);
        }

        [Fact]
        public async Task Deduct_ReferenceOrderId_Idempotency_PreventsDuplicateDeduction()
        {
            using var context = CreateDbContext();
            SeedCatalogWithBtpLeaves(context, directIngredientQty: 100m, btpQty: 20m, leafInsideBtpQty: 100m);
            SeedPaidOrder(context, orderId: 9504);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var first = await service.DeductStockForCommittedOrderAsync(
                SoldItems(quantity: 1),
                StoreId,
                referenceOrderId: 9504);
            Assert.True(first.IsSuccess);

            var ingredientAfterFirst = await QtyByIngredientAsync(context, DirectIngredientId);
            var btpAfterFirst = await QtyByRecipeAsync(context, BtpRecipeId);
            var txnCountAfterFirst = await context.InventoryTransactions
                .CountAsync(t => t.ReferenceOrderId == 9504);

            var second = await service.DeductStockForCommittedOrderAsync(
                SoldItems(quantity: 1),
                StoreId,
                referenceOrderId: 9504);
            Assert.True(second.IsSuccess);
            Assert.Contains("đã được trừ kho trước đó", second.Message);

            Assert.Equal(ingredientAfterFirst, await QtyByIngredientAsync(context, DirectIngredientId));
            Assert.Equal(btpAfterFirst, await QtyByRecipeAsync(context, BtpRecipeId));
            Assert.Equal(txnCountAfterFirst, await context.InventoryTransactions
                .CountAsync(t => t.ReferenceOrderId == 9504));
            Assert.Equal(2, txnCountAfterFirst); // ingredient + BTP only once
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
                estimated,
                physical);
        }

        private static List<POSSoldItemDto> SoldItems(int quantity)
        {
            return new List<POSSoldItemDto>
            {
                new()
                {
                    DrinkId = DrinkId,
                    SizeId = SizeId,
                    Quantity = quantity,
                    Toppings = new List<POSOrderToppingDto>()
                }
            };
        }

        /// <summary>
        /// Main size recipe: direct ingredient + ChildRecipe(BTP).
        /// BTP recipe itself has leaf ingredients — recursive explode would touch those leaves.
        /// </summary>
        private static void SeedCatalogWithBtpLeaves(
            CafeChain.Data.AppDbContext context,
            decimal directIngredientQty,
            decimal btpQty,
            decimal leafInsideBtpQty,
            decimal leafPerBtp = 4m)
        {
            context.Ingredients.AddRange(
                new Ingredient
                {
                    IngredientId = DirectIngredientId,
                    Code = "DIR",
                    Name = "Direct milk",
                    BaseUnitId = UnitId,
                    Active = true
                },
                new Ingredient
                {
                    IngredientId = LeafInsideBtpIngredientId,
                    Code = "LEAF",
                    Name = "Leaf syrup sugar",
                    BaseUnitId = UnitId,
                    Active = true
                });

            context.Recipes.AddRange(
                new Recipe
                {
                    RecipeId = MainRecipeId,
                    RecipeCode = "RCP_MAIN_95",
                    Name = "Main size recipe #95",
                    Active = true,
                    Status = "Active",
                    DrinkId = DrinkId,
                    SizeId = SizeId,
                    RecipeDetails = new List<RecipeDetail>
                    {
                        new()
                        {
                            IngredientId = DirectIngredientId,
                            Quantity = 10m,
                            UnitId = UnitId
                        },
                        new()
                        {
                            ChildRecipeId = BtpRecipeId,
                            Quantity = 1m,
                            UnitId = UnitId
                        }
                    }
                },
                new Recipe
                {
                    RecipeId = BtpRecipeId,
                    RecipeCode = "RCP_BTP_95",
                    Name = "BTP with leaf ingredients",
                    Active = true,
                    Status = "Active",
                    RecipeDetails = new List<RecipeDetail>
                    {
                        new()
                        {
                            IngredientId = LeafInsideBtpIngredientId,
                            Quantity = leafPerBtp,
                            UnitId = UnitId
                        }
                    }
                });

            context.StoreInventories.AddRange(
                new StoreInventory
                {
                    StoreId = StoreId,
                    IngredientId = DirectIngredientId,
                    AvailableQty = directIngredientQty,
                    ReservedQty = 0,
                    LastUpdated = System.DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new StoreInventory
                {
                    StoreId = StoreId,
                    IngredientId = LeafInsideBtpIngredientId,
                    AvailableQty = leafInsideBtpQty,
                    ReservedQty = 0,
                    LastUpdated = System.DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new StoreInventory
                {
                    StoreId = StoreId,
                    RecipeId = BtpRecipeId,
                    AvailableQty = btpQty,
                    ReservedQty = 0,
                    LastUpdated = System.DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                });
        }

        private static void SeedPaidOrder(CafeChain.Data.AppDbContext context, int orderId)
        {
            context.Orders.Add(new Order
            {
                OrderId = orderId,
                StoreId = StoreId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Source = "POS",
                SubTotal = 50000m,
                Total = 50000m,
                CreatedAt = System.DateTime.UtcNow
            });
        }

        private static async Task<decimal> QtyByIngredientAsync(
            CafeChain.Data.AppDbContext context,
            int ingredientId)
        {
            return await context.StoreInventories
                .Where(i => i.StoreId == StoreId && i.IngredientId == ingredientId)
                .Select(i => i.AvailableQty)
                .SingleAsync();
        }

        private static async Task<decimal> QtyByRecipeAsync(
            CafeChain.Data.AppDbContext context,
            int recipeId)
        {
            return await context.StoreInventories
                .Where(i => i.StoreId == StoreId && i.RecipeId == recipeId)
                .Select(i => i.AvailableQty)
                .SingleAsync();
        }
    }
}
