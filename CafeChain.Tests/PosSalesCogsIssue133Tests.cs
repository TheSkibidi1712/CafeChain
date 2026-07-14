using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>Issue #133 — actual sales COGS from FIFO layers (Option B incomplete).</summary>
    public sealed class PosSalesCogsIssue133Tests : IntegrationTestBase
    {
        private const int StoreId = 13301;
        private const int UnitGram = 1;
        private const int UnitMl = 3;
        private const int DrinkId = 13310;
        private const int SizeId = 13311;
        private const int ToppingId = 13312;
        private const int IngredientId = 13320;
        private const int ChildRecipeId = 13330;
        private const int ParentRecipeId = 13331;
        private const int ToppingRecipeId = 13332;
        private const int PreparedItemId = 13340;
        private const int OrderId = 13390;

        [Fact]
        public async Task POS_CompletedOrder_ConsumesIngredientCostLayers()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, layerCost: 10m, layerQty: 1000m);
            var svc = CreateService(ctx);

            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            var layer = await ctx.InventoryCostLayers.SingleAsync(x => x.IngredientId == IngredientId);
            Assert.Equal(950m, layer.RemainingQuantity); // 50g per drink
        }

        [Fact]
        public async Task POS_CompletedOrder_ConsumesPreparedItemCostLayers()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedItemDrinkAsync(ctx, piLayerCost: 20m, piLayerQty: 500m);
            var svc = CreateService(ctx);

            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            var layer = await ctx.InventoryCostLayers.SingleAsync(x =>
                x.PreparedItemId == PreparedItemId && x.SourceProductionRunId == null);
            Assert.Equal(400m, layer.RemainingQuantity); // 100ml BOM
        }

        [Fact]
        public async Task POS_CompletedOrder_CreatesDurableSalesCostAllocations()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 1000m);
            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);

            var allocs = await ctx.SalesCostAllocations.Where(a => a.OrderId == OrderId).ToListAsync();
            Assert.NotEmpty(allocs);
            Assert.All(allocs, a =>
            {
                Assert.True(a.Quantity > 0);
                Assert.True(a.UnitCost > 0);
                Assert.Equal(a.Quantity * a.UnitCost, a.TotalCost);
                Assert.True(a.IngredientId.HasValue ^ a.PreparedItemId.HasValue);
            });
        }

        [Fact]
        public async Task POS_CompletedOrder_CreatesDurableCostGaps_WhenEvidenceMissing()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, layerCost: 10m, layerQty: 10m); // need 50
            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId);
            Assert.True(result.IsSuccess);
            Assert.Contains(result.Errors, e => e.Contains(SalesCogsCodes.Incomplete));

            var gaps = await ctx.SalesCostGaps.Where(g => g.OrderId == OrderId).ToListAsync();
            Assert.NotEmpty(gaps);
            Assert.All(gaps, g => Assert.True(g.MissingCostQuantity > 0));
            Assert.Equal(SalesCostStatus.Incomplete, (await ctx.Orders.SingleAsync(o => o.OrderId == OrderId)).CostStatus);
        }

        [Fact]
        public async Task POS_CompletedOrder_SetsSalesDeductionUnitAndTotalCost_WhenComplete()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 1000m);
            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);

            var txs = await ctx.InventoryTransactions
                .Where(t => t.ReferenceOrderId == OrderId
                            && t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION)
                .ToListAsync();
            Assert.NotEmpty(txs);
            Assert.All(txs, t =>
            {
                Assert.NotNull(t.UnitCost);
                Assert.NotNull(t.TotalCost);
                Assert.True(t.UnitCost > 0);
                Assert.True(t.TotalCost > 0);
            });
        }

        [Fact]
        public async Task POS_CompletedOrder_DoesNotSetZeroCost_WhenIncomplete()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 10m);
            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);

            var txs = await ctx.InventoryTransactions
                .Where(t => t.ReferenceOrderId == OrderId)
                .ToListAsync();
            Assert.All(txs, t =>
            {
                Assert.Null(t.UnitCost);
                Assert.Null(t.TotalCost);
            });
            var order = await ctx.Orders.SingleAsync(o => o.OrderId == OrderId);
            Assert.Null(order.TotalCogs);
            Assert.Null(order.GrossProfit);
        }

        [Fact]
        public async Task POS_CompletedOrder_SnapshotsOrderDetailCogs()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 1000m);
            var detailRow = await ctx.OrderDetails.SingleAsync(d => d.OrderId == OrderId);
            detailRow.Quantity = 2;
            await ctx.SaveChangesAsync();

            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(2), StoreId, OrderId)).IsSuccess);

            var detail = await ctx.OrderDetails.SingleAsync(d => d.OrderId == OrderId);
            Assert.Equal(SalesCostStatus.Complete, detail.CostStatus);
            Assert.Equal(1000m, detail.TotalCogs); // 100g * 10
            Assert.Equal(500m, detail.UnitCogs); // / qty 2
        }

        [Fact]
        public async Task POS_CompletedOrder_SnapshotsToppingCogs()
        {
            using var ctx = CreateDbContext();
            await SeedDrinkWithToppingIngredientAsync(ctx);
            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldDrinkWithTopping(1), StoreId, OrderId)).IsSuccess);

            var topping = await ctx.OrderToppings.SingleAsync();
            Assert.Equal(SalesCostStatus.Complete, topping.CostStatus);
            Assert.NotNull(topping.TotalCogs);
            Assert.True(topping.TotalCogs > 0);
        }

        [Fact]
        public async Task POS_CompletedOrder_TotalCogs_EqualsActualAllocations()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 12m, 1000m);
            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);

            var allocSum = (await ctx.SalesCostAllocations.Where(a => a.OrderId == OrderId).ToListAsync())
                .Sum(a => a.TotalCost);
            var order = await ctx.Orders.SingleAsync(o => o.OrderId == OrderId);
            Assert.Equal(SalesCostStatus.Complete, order.CostStatus);
            Assert.Equal(allocSum, order.TotalCogs);
        }

        [Fact]
        public async Task POS_CompletedOrder_GrossProfit_UsesApprovedRevenueFormula()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 1000m);
            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);

            var order = await ctx.Orders.SingleAsync(o => o.OrderId == OrderId);
            // GrossProfit = Order.Total (post voucher/points) − TotalCogs
            Assert.Equal(order.Total - order.TotalCogs, order.GrossProfit);
            Assert.Equal(45000m - 500m, order.GrossProfit);
        }

        [Fact]
        public async Task POS_CompletedOrder_DoesNotUseRecipeEstimateAsActual()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, layerCost: 7m, layerQty: 1000m);
            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);

            var order = await ctx.Orders.SingleAsync(o => o.OrderId == OrderId);
            Assert.Equal(50m * 7m, order.TotalCogs);
            // Estimate path is separate — actual is layer only
            Assert.Equal(SalesCostStatus.Complete, order.CostStatus);
        }

        [Fact]
        public async Task POS_CompletedOrder_IncompleteCost_DoesNotBlockPayment()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 5m);
            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId);
            Assert.True(result.IsSuccess);
            Assert.Equal(SystemConstants.PaymentStatuses.Paid,
                (await ctx.Orders.SingleAsync(o => o.OrderId == OrderId)).PaymentStatusId);
        }

        [Fact]
        public async Task POS_CompletedOrder_IncompleteCost_StillDeductsQuantity()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 5m, invQty: 1000m);
            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            Assert.Equal(950m, await ctx.StoreInventories.Where(x => x.IngredientId == IngredientId)
                .Select(x => x.AvailableQty).SingleAsync());
        }

        [Fact]
        public async Task POS_CompletedOrder_Replay_DoesNotConsumeLayerTwice()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 1000m);
            var svc = CreateService(ctx);
            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            Assert.Equal(950m, (await ctx.InventoryCostLayers.SingleAsync(x => x.IngredientId == IngredientId)).RemainingQuantity);
        }

        [Fact]
        public async Task POS_CompletedOrder_Replay_DoesNotDuplicateAllocationsOrGaps()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 10m);
            var svc = CreateService(ctx);
            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            var a = await ctx.SalesCostAllocations.CountAsync(x => x.OrderId == OrderId);
            var g = await ctx.SalesCostGaps.CountAsync(x => x.OrderId == OrderId);
            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            Assert.Equal(a, await ctx.SalesCostAllocations.CountAsync(x => x.OrderId == OrderId));
            Assert.Equal(g, await ctx.SalesCostGaps.CountAsync(x => x.OrderId == OrderId));
        }

        [Fact]
        public async Task POS_CompletedOrder_Replay_ReturnsStoredCogs()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 11m, 1000m);
            var svc = CreateService(ctx);
            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            var first = await ctx.Orders.AsNoTracking().SingleAsync(o => o.OrderId == OrderId);

            var layer = await ctx.InventoryCostLayers.SingleAsync(x => x.IngredientId == IngredientId);
            layer.UnitCost = 999m;
            await ctx.SaveChangesAsync();

            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            var replay = await ctx.Orders.AsNoTracking().SingleAsync(o => o.OrderId == OrderId);
            Assert.Equal(first.TotalCogs, replay.TotalCogs);
            Assert.Equal(first.GrossProfit, replay.GrossProfit);
            Assert.Equal(first.CostStatus, replay.CostStatus);
        }

        [Fact]
        public async Task POS_CompletedOrder_Failure_RollsBackQuantityAndCostArtifacts()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 1000m);
            // Force failure: missing conversion by using broken unit on recipe after seed
            var detail = await ctx.RecipeDetails.FirstAsync(d => d.RecipeId == ParentRecipeId);
            detail.UnitId = 99999; // invalid
            await ctx.SaveChangesAsync();

            var before = await ctx.StoreInventories.Where(x => x.IngredientId == IngredientId)
                .Select(x => x.AvailableQty).SingleAsync();
            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId);
            Assert.False(result.IsSuccess);

            Assert.Equal(before, await ctx.StoreInventories.Where(x => x.IngredientId == IngredientId)
                .Select(x => x.AvailableQty).SingleAsync());
            Assert.Equal(0, await ctx.SalesCostAllocations.CountAsync(a => a.OrderId == OrderId));
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == OrderId));
            Assert.Equal(SalesCostStatus.Pending, (await ctx.Orders.SingleAsync(o => o.OrderId == OrderId)).CostStatus);
        }

        [Fact]
        public async Task POS_PreparedItemConsumption_UsesProductionOutputLayer()
        {
            using var ctx = CreateDbContext();
            // Layer shaped like production output (#132): PreparedItem identity + unit cost.
            await SeedPreparedItemDrinkAsync(ctx, 15m, 1000m);
            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);

            var layer = await ctx.InventoryCostLayers.SingleAsync(x => x.PreparedItemId == PreparedItemId);
            Assert.Equal(900m, layer.RemainingQuantity);
            var allocTotal = (await ctx.SalesCostAllocations.Where(a => a.OrderId == OrderId).ToListAsync())
                .Sum(a => a.TotalCost);
            Assert.Equal(15m * 100m, allocTotal);
            Assert.All(await ctx.SalesCostAllocations.Where(a => a.OrderId == OrderId).ToListAsync(),
                a => Assert.Equal(PreparedItemId, a.PreparedItemId));
        }

        [Fact]
        public async Task POS_ToppingChildRecipe_PreservesPinnedRecipeIdentity()
        {
            using var ctx = CreateDbContext();
            await SeedDrinkWithToppingPreparedItemAsync(ctx);
            // Newer recipe same PI — must not substitute pin
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ToppingRecipeId + 50,
                RecipeCode = "TOP-NEW",
                Name = "Newer topping recipe",
                Active = true,
                Status = "Active",
                ToppingId = ToppingId,
                YieldPercentage = 100m,
                PreparedItemId = PreparedItemId,
                OutputQuantity = 1m,
                OutputUnitId = UnitMl
            });
            await ctx.SaveChangesAsync();

            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldDrinkWithTopping(1), StoreId, OrderId)).IsSuccess);

            var txs = await ctx.InventoryTransactions
                .Where(t => t.ReferenceOrderId == OrderId)
                .ToListAsync();
            Assert.Contains(txs, t => t.SourceRecipeId == ChildRecipeId);
        }

        [Fact]
        public async Task POS_OfflineSync_Replay_DoesNotDuplicateCogs()
        {
            // Same as order replay via DeductStockForCommittedOrderAsync (offline path uses same API)
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 1000m);
            var svc = CreateService(ctx);
            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            var n = await ctx.SalesCostAllocations.CountAsync(a => a.OrderId == OrderId);
            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            Assert.Equal(n, await ctx.SalesCostAllocations.CountAsync(a => a.OrderId == OrderId));
        }

        [Fact]
        public async Task POS_OfflineSync_IncompleteCost_StillSucceedsWithWarning()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 1m);
            var r = await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId);
            Assert.True(r.IsSuccess);
            Assert.NotEmpty(r.Errors);
            Assert.Contains(r.Errors, e => e.Contains(SalesCogsCodes.Incomplete));
        }

        [Fact]
        public async Task POS_PayOSWebhookReplay_ConsumesCostOnce()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 1000m);
            var svc = CreateService(ctx);
            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            Assert.Equal(1, await ctx.InventoryTransactions
                .Where(t => t.ReferenceOrderId == OrderId && t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION)
                .Select(t => t.StoreInventoryId)
                .Distinct()
                .CountAsync());
            Assert.Equal(950m, (await ctx.InventoryCostLayers.SingleAsync(x => x.IngredientId == IngredientId)).RemainingQuantity);
        }

        [Fact]
        public async Task POS_SellingPrice_RemainsDrinkSizePlusToppings()
        {
            using var ctx = CreateDbContext();
            await SeedDrinkWithToppingIngredientAsync(ctx);
            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldDrinkWithTopping(1), StoreId, OrderId)).IsSuccess);

            var detail = await ctx.OrderDetails.Include(d => d.OrderToppings).SingleAsync(d => d.OrderId == OrderId);
            // Selling prices seeded: drink line 40000, topping 5000
            Assert.Equal(40000m, detail.Price);
            Assert.Equal(5000m, detail.OrderToppings.Single().Price);
            Assert.NotEqual(detail.Price, detail.TotalCogs);
        }

        [Fact]
        public async Task POS_OrderOldSellingPriceSnapshot_RemainsUnchanged()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyAsync(ctx, 10m, 1000m);
            var beforePrice = (await ctx.OrderDetails.SingleAsync(d => d.OrderId == OrderId)).Price;
            Assert.True((await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldDrink(1), StoreId, OrderId)).IsSuccess);
            Assert.Equal(beforePrice, (await ctx.OrderDetails.SingleAsync(d => d.OrderId == OrderId)).Price);
        }

        // ---------- helpers ----------

        private static List<POSSoldItemDto> SoldDrink(int qty) => new()
        {
            new() { DrinkId = DrinkId, SizeId = SizeId, Quantity = qty }
        };

        private static List<POSSoldItemDto> SoldDrinkWithTopping(int qty) => new()
        {
            new()
            {
                DrinkId = DrinkId,
                SizeId = SizeId,
                Quantity = qty,
                Toppings = new List<POSOrderToppingDto> { new() { ToppingId = ToppingId } }
            }
        };

        private static InventoryDeductionService CreateService(AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
            var normalizer = new RecipeOutputNormalizer(context, physical);
            var estimated = new EstimatedBomCostService(
                context, unit, physical, normalizer, NullLogger<EstimatedBomCostService>.Instance);
            var caps = new IInventoryWriterCapabilityProvider[]
            {
                new ProductionPreparedWriterCapabilityProvider(),
                new PosPreparedWriterCapabilityProvider()
            };
            var writer = new InventoryWriterModeService(context, physical, caps);
            var resolver = new StoreInventoryWriteResolver(context, writer);
            var cost = new InventoryCostLayerConsumptionService(context);
            return new InventoryDeductionService(
                context,
                NullLogger<InventoryDeductionService>.Instance,
                unit,
                estimated,
                physical,
                stockAlertService: null,
                writerModeService: writer,
                writeResolver: resolver,
                costLayerConsumption: cost);
        }

        private static async Task SeedBaseStoreOrderAsync(AppDbContext ctx, decimal orderTotal = 45000m)
        {
            var now = DateTime.UtcNow;
            ctx.Stores.Add(new Store
            {
                StoreId = StoreId,
                Name = "S133",
                Address = "A",
                Phone = "1",
                Active = true,
                CreatedAt = now
            });
            ctx.StoreInventoryWriterConfigurations.Add(new StoreInventoryWriterConfiguration
            {
                StoreId = StoreId,
                WriterMode = InventoryWriterMode.PreparedItem,
                HasEverActivatedPreparedItem = true,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = new byte[] { 0 }
            });
            ctx.Orders.Add(new Order
            {
                OrderId = OrderId,
                StoreId = StoreId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Source = "POS",
                SubTotal = orderTotal,
                Total = orderTotal,
                CostStatus = SalesCostStatus.Pending,
                CreatedAt = now
            });
            await ctx.SaveChangesAsync();
        }

        private static async Task SeedIngredientOnlyAsync(
            AppDbContext ctx,
            decimal layerCost,
            decimal layerQty,
            decimal invQty = 1000m)
        {
            await SeedBaseStoreOrderAsync(ctx);
            var now = DateTime.UtcNow;
            ctx.Ingredients.Add(new Ingredient
            {
                IngredientId = IngredientId,
                Code = "ING133",
                Name = "Milk133",
                BaseUnitId = UnitGram,
                Active = true
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ParentRecipeId,
                RecipeCode = "RCP133",
                Name = "Drink recipe",
                Active = true,
                Status = "Active",
                DrinkId = DrinkId,
                SizeId = SizeId,
                YieldPercentage = 100m,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = IngredientId, Quantity = 50m, UnitId = UnitGram }
                }
            });
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                AvailableQty = invQty,
                ReservedQty = 0,
                LastUpdated = now,
                RowVersion = new byte[] { 0 }
            });
            ctx.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                Quantity = layerQty,
                RemainingQuantity = layerQty,
                UnitCost = layerCost,
                CreatedAt = now
            });
            ctx.OrderDetails.Add(new OrderDetail
            {
                OrderId = OrderId,
                DrinkId = DrinkId,
                SizeId = SizeId,
                DrinkName = "Latte",
                SizeName = "M",
                Price = 45000m,
                Quantity = 1,
                Note = "",
                CostStatus = SalesCostStatus.Pending
            });
            await ctx.SaveChangesAsync();
            // Align order detail quantity with sold qty in tests that use qty>1 via re-seed sold
            // Quantity on detail used for UnitCogs; update after for multi qty tests in Snapshots
        }

        private static async Task SeedPreparedItemDrinkAsync(
            AppDbContext ctx,
            decimal piLayerCost,
            decimal piLayerQty,
            int? sourceProductionRunId = null)
        {
            await SeedBaseStoreOrderAsync(ctx);
            var now = DateTime.UtcNow;
            ctx.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = PreparedItemId,
                Code = "PI133",
                Name = "Syrup base",
                BaseUnitId = UnitMl,
                Active = true
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ChildRecipeId,
                RecipeCode = "CHILD133",
                Name = "Child BTP formula",
                Active = false,
                Status = "Archived",
                YieldPercentage = 100m,
                PreparedItemId = PreparedItemId,
                OutputQuantity = 1m,
                OutputUnitId = UnitMl
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ParentRecipeId,
                RecipeCode = "PARENT133",
                Name = "Drink with BTP",
                Active = true,
                Status = "Active",
                DrinkId = DrinkId,
                SizeId = SizeId,
                YieldPercentage = 100m,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = ChildRecipeId, Quantity = 100m, UnitId = UnitMl }
                }
            });
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                RecipeId = null,
                BtpIdentityState = BtpIdentityState.Canonical,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                QuantitySemanticsEvidenceReference = "seed",
                QuantitySemanticsReviewedAt = now,
                QuantitySemanticsReviewedByAccountId = 1,
                AvailableQty = 5000m,
                ReservedQty = 0,
                LastUpdated = now,
                RowVersion = new byte[] { 0 }
            });
            ctx.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                IngredientId = null,
                Quantity = piLayerQty,
                RemainingQuantity = piLayerQty,
                UnitCost = piLayerCost,
                CreatedAt = now,
                SourceProductionRunId = sourceProductionRunId
            });
            ctx.OrderDetails.Add(new OrderDetail
            {
                OrderId = OrderId,
                DrinkId = DrinkId,
                SizeId = SizeId,
                DrinkName = "Drink",
                SizeName = "M",
                Price = 45000m,
                Quantity = 1,
                Note = "",
                CostStatus = SalesCostStatus.Pending
            });
            await ctx.SaveChangesAsync();
        }

        private static async Task SeedDrinkWithToppingIngredientAsync(AppDbContext ctx)
        {
            await SeedBaseStoreOrderAsync(ctx, orderTotal: 45000m);
            var now = DateTime.UtcNow;
            ctx.Ingredients.Add(new Ingredient
            {
                IngredientId = IngredientId,
                Code = "ING-TOP",
                Name = "Pearl",
                BaseUnitId = UnitGram,
                Active = true
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ParentRecipeId,
                RecipeCode = "DRINK",
                Name = "Drink",
                Active = true,
                Status = "Active",
                DrinkId = DrinkId,
                SizeId = SizeId,
                YieldPercentage = 100m,
                RecipeDetails = new List<RecipeDetail>()
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ToppingRecipeId,
                RecipeCode = "TOP",
                Name = "Topping recipe",
                Active = true,
                Status = "Active",
                ToppingId = ToppingId,
                YieldPercentage = 100m,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = IngredientId, Quantity = 30m, UnitId = UnitGram }
                }
            });
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                AvailableQty = 1000m,
                ReservedQty = 0,
                LastUpdated = now,
                RowVersion = new byte[] { 0 }
            });
            ctx.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                Quantity = 1000m,
                RemainingQuantity = 1000m,
                UnitCost = 5m,
                CreatedAt = now
            });
            var detail = new OrderDetail
            {
                OrderId = OrderId,
                DrinkId = DrinkId,
                SizeId = SizeId,
                DrinkName = "Drink",
                SizeName = "M",
                Price = 40000m,
                Quantity = 1,
                Note = "",
                CostStatus = SalesCostStatus.Pending,
                OrderToppings = new List<OrderTopping>
                {
                    new()
                    {
                        ToppingId = ToppingId,
                        ToppingName = "Pearl",
                        Price = 5000m,
                        CostStatus = SalesCostStatus.Pending
                    }
                }
            };
            ctx.OrderDetails.Add(detail);
            await ctx.SaveChangesAsync();
        }

        private static async Task SeedDrinkWithToppingPreparedItemAsync(AppDbContext ctx)
        {
            await SeedBaseStoreOrderAsync(ctx);
            var now = DateTime.UtcNow;
            ctx.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = PreparedItemId,
                Code = "PI-TOP",
                Name = "Pearl BTP",
                BaseUnitId = UnitMl,
                Active = true
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ChildRecipeId,
                RecipeCode = "CHILD-TOP",
                Name = "Pinned child",
                Active = false,
                Status = "Archived",
                YieldPercentage = 100m,
                PreparedItemId = PreparedItemId,
                OutputQuantity = 1m,
                OutputUnitId = UnitMl
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ParentRecipeId,
                RecipeCode = "DRINK-EMPTY",
                Name = "Drink empty bom",
                Active = true,
                Status = "Active",
                DrinkId = DrinkId,
                SizeId = SizeId,
                YieldPercentage = 100m
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ToppingRecipeId,
                RecipeCode = "TOP-PI",
                Name = "Topping with child",
                Active = true,
                Status = "Active",
                ToppingId = ToppingId,
                YieldPercentage = 100m,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = ChildRecipeId, Quantity = 50m, UnitId = UnitMl }
                }
            });
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                RecipeId = null,
                BtpIdentityState = BtpIdentityState.Canonical,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                QuantitySemanticsEvidenceReference = "seed",
                QuantitySemanticsReviewedAt = now,
                QuantitySemanticsReviewedByAccountId = 1,
                AvailableQty = 5000m,
                ReservedQty = 0,
                LastUpdated = now,
                RowVersion = new byte[] { 0 }
            });
            ctx.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                Quantity = 5000m,
                RemainingQuantity = 5000m,
                UnitCost = 8m,
                CreatedAt = now
            });
            var od = new OrderDetail
            {
                OrderId = OrderId,
                DrinkId = DrinkId,
                SizeId = SizeId,
                DrinkName = "D",
                SizeName = "M",
                Price = 40000m,
                Quantity = 1,
                Note = "",
                CostStatus = SalesCostStatus.Pending
            };
            ctx.OrderDetails.Add(od);
            await ctx.SaveChangesAsync();
            ctx.OrderToppings.Add(new OrderTopping
            {
                OrderDetailId = od.OrderDetailId,
                ToppingId = ToppingId,
                ToppingName = "T",
                Price = 5000m,
                CostStatus = SalesCostStatus.Pending
            });
            await ctx.SaveChangesAsync();
        }
    }
}
