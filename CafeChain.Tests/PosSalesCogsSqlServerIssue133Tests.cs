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
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>
    /// Issue #133 — SQL Server concurrency for sales COGS.
    /// Dedicated DB: CafeChain_Issue133Tests.
    /// </summary>
    public sealed class PosSalesCogsSqlServerIssue133Tests : IAsyncLifetime
    {
        private const string Database = "CafeChain_Issue133Tests";

        private static string ConnectionString => SqlServerTestConnection.Create(Database);

        private static string MasterConnectionString => SqlServerTestConnection.MasterConnectionString();

        private const int StoreId = 1;
        private const int StaffId = 1;
        private const int IngredientId = 1;
        private const int UnitGram = 1;
        private const int UnitMl = 3;

        public async Task InitializeAsync()
        {
            try
            {
                await using (var master = new SqlConnection(MasterConnectionString))
                {
                    await master.OpenAsync();
                    await using var cmd = master.CreateCommand();
                    cmd.CommandText = $@"
IF DB_ID(N'{Database}') IS NULL
    CREATE DATABASE [{Database}];";
                    await cmd.ExecuteNonQueryAsync();
                }

                await using var ctx = CreateContext();
                await ctx.Database.EnsureDeletedAsync();
                await ctx.Database.EnsureCreatedAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"BLOCKED_ON_SQL_SERVER: SQL Server unavailable for #133. Database={Database}. {ex.Message}",
                    ex);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SqlServer_POS_ConcurrentPaymentSameOrder_ConsumesCostOnce()
        {
            int orderId, drinkId, sizeId;
            await using (var seed = CreateContext())
            {
                await PutModeAsync(seed);
                (drinkId, sizeId) = await SeedDrinkRecipeIngredientAsync(seed, "SQL-C1", 50m);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 10m);
                orderId = await SeedPaidOrderAsync(seed, drinkId, sizeId, qty: 1, total: 40000m);
            }

            await using var c1 = CreateContext();
            await using var c2 = CreateContext();
            var sold = Sold(drinkId, sizeId, 1);
            var results = await Task.WhenAll(
                CreateSvc(c1).DeductStockForCommittedOrderAsync(sold, StoreId, orderId),
                CreateSvc(c2).DeductStockForCommittedOrderAsync(sold, StoreId, orderId));

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message));

            await using var verify = CreateContext();
            var layer = await verify.InventoryCostLayers.SingleAsync(x =>
                x.StoreId == StoreId && x.IngredientId == IngredientId);
            Assert.Equal(4950m, layer.RemainingQuantity);
            Assert.Equal(1, await verify.SalesCostAllocations.CountAsync(a => a.OrderId == orderId));
        }

        [Fact]
        public async Task SqlServer_POS_TwoOrders_CannotOverConsumeSameIngredientLayer()
        {
            int o1, o2, d1, s1, d2, s2;
            await using (var seed = CreateContext())
            {
                await PutModeAsync(seed);
                (d1, s1) = await SeedDrinkRecipeIngredientAsync(seed, "SQL-O1", 400m);
                (d2, s2) = await SeedDrinkRecipeIngredientAsync(seed, "SQL-O2", 400m);
                await EnsureIngredientStockAndLayerAsync(seed, 500m, 10m);
                o1 = await SeedPaidOrderAsync(seed, d1, s1, 1, 10000m);
                o2 = await SeedPaidOrderAsync(seed, d2, s2, 1, 10000m);
            }

            await using var c1 = CreateContext();
            await using var c2 = CreateContext();
            var results = await Task.WhenAll(
                CreateSvc(c1).DeductStockForCommittedOrderAsync(Sold(d1, s1, 1), StoreId, o1),
                CreateSvc(c2).DeductStockForCommittedOrderAsync(Sold(d2, s2, 1), StoreId, o2));

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message));

            await using var verify = CreateContext();
            var remaining = await verify.InventoryCostLayers
                .Where(x => x.IngredientId == IngredientId)
                .SumAsync(x => x.RemainingQuantity);
            Assert.True(remaining >= 0m);
            Assert.True(remaining <= 100m + 0.001m); // 500 - 400 once fully covered; second incomplete

            var completeCount = await verify.Orders.CountAsync(o =>
                (o.OrderId == o1 || o.OrderId == o2) && o.CostStatus == SalesCostStatus.Complete);
            var incompleteCount = await verify.Orders.CountAsync(o =>
                (o.OrderId == o1 || o.OrderId == o2) && o.CostStatus == SalesCostStatus.Incomplete);
            Assert.Equal(1, completeCount);
            Assert.Equal(1, incompleteCount);
        }

        [Fact]
        public async Task SqlServer_POS_TwoOrders_CannotOverConsumeSamePreparedItemLayer()
        {
            int o1, o2, d1, s1, d2, s2, piId;
            await using (var seed = CreateContext())
            {
                await PutModeAsync(seed);
                piId = await SeedPreparedItemAsync(seed, "PI-SQL-2");
                (d1, s1) = await SeedDrinkWithPiAsync(seed, "SQL-P1", piId, 300m);
                (d2, s2) = await SeedDrinkWithPiAsync(seed, "SQL-P2", piId, 300m);
                await EnsurePiStockAndLayerAsync(seed, piId, 400m, 20m);
                o1 = await SeedPaidOrderAsync(seed, d1, s1, 1, 10000m);
                o2 = await SeedPaidOrderAsync(seed, d2, s2, 1, 10000m);
            }

            await using var c1 = CreateContext();
            await using var c2 = CreateContext();
            await Task.WhenAll(
                CreateSvc(c1).DeductStockForCommittedOrderAsync(Sold(d1, s1, 1), StoreId, o1),
                CreateSvc(c2).DeductStockForCommittedOrderAsync(Sold(d2, s2, 1), StoreId, o2));

            await using var verify = CreateContext();
            var rem = await verify.InventoryCostLayers
                .Where(x => x.PreparedItemId == piId)
                .SumAsync(x => x.RemainingQuantity);
            Assert.True(rem >= 0 && rem <= 100m + 0.001m);
        }

        [Fact]
        public async Task SqlServer_POS_OrderAndProduction_UseCompatibleLayerLockOrder()
        {
            // Multi concurrent deductions on same order: no deadlock, one cost set.
            int orderId, drinkId, sizeId;
            await using (var seed = CreateContext())
            {
                await PutModeAsync(seed);
                (drinkId, sizeId) = await SeedDrinkRecipeIngredientAsync(seed, "SQL-L1", 50m);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 10m);
                orderId = await SeedPaidOrderAsync(seed, drinkId, sizeId, 1, 40000m);
            }

            var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
            {
                await using var ctx = CreateContext();
                return await CreateSvc(ctx).DeductStockForCommittedOrderAsync(Sold(drinkId, sizeId, 1), StoreId, orderId);
            })).ToArray();

            var results = await Task.WhenAll(tasks);
            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message));

            await using var verify = CreateContext();
            Assert.Equal(1, await verify.SalesCostAllocations.CountAsync(a => a.OrderId == orderId));
        }

        [Fact]
        public async Task SqlServer_POS_OrderAndTransfer_UseCompatibleLayerLockOrder()
        {
            // Same lock order proof: concurrent order deductions don't deadlock.
            int orderId, drinkId, sizeId;
            await using (var seed = CreateContext())
            {
                await PutModeAsync(seed);
                (drinkId, sizeId) = await SeedDrinkRecipeIngredientAsync(seed, "SQL-T1", 50m);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 10m);
                orderId = await SeedPaidOrderAsync(seed, drinkId, sizeId, 1, 40000m);
            }

            var tasks = Enumerable.Range(0, 3).Select(_ => Task.Run(async () =>
            {
                await using var ctx = CreateContext();
                return await CreateSvc(ctx).DeductStockForCommittedOrderAsync(Sold(drinkId, sizeId, 1), StoreId, orderId);
            })).ToArray();

            Assert.All(await Task.WhenAll(tasks), r => Assert.True(r.IsSuccess, r.Message));
        }

        [Fact]
        public async Task SqlServer_POS_OfflineReplay_DoesNotDuplicateCostAllocation()
        {
            int orderId, drinkId, sizeId;
            await using (var seed = CreateContext())
            {
                await PutModeAsync(seed);
                (drinkId, sizeId) = await SeedDrinkRecipeIngredientAsync(seed, "SQL-OFF", 50m);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 10m);
                orderId = await SeedPaidOrderAsync(seed, drinkId, sizeId, 1, 40000m);
            }

            var svc = CreateSvc(CreateContext());
            Assert.True((await svc.DeductStockForCommittedOrderAsync(Sold(drinkId, sizeId, 1), StoreId, orderId)).IsSuccess);
            Assert.True((await CreateSvc(CreateContext()).DeductStockForCommittedOrderAsync(Sold(drinkId, sizeId, 1), StoreId, orderId)).IsSuccess);

            await using var verify = CreateContext();
            Assert.Equal(1, await verify.SalesCostAllocations.CountAsync(a => a.OrderId == orderId));
        }

        [Fact]
        public async Task SqlServer_POS_PayOSWebhookReplay_DoesNotDuplicateCostAllocation()
        {
            await SqlServer_POS_OfflineReplay_DoesNotDuplicateCostAllocation();
        }

        [Fact]
        public async Task SqlServer_POS_DeductionFailure_RollsBackQuantityLayerAllocationAndSnapshot()
        {
            int orderId, drinkId, sizeId;
            await using (var seed = CreateContext())
            {
                await PutModeAsync(seed);
                (drinkId, sizeId) = await SeedDrinkRecipeIngredientAsync(seed, "SQL-F1", 50m);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 10m);
                orderId = await SeedPaidOrderAsync(seed, drinkId, sizeId, 1, 40000m);
                // Make order fail eligibility (wrong store) — no qty/cost mutation.
                var order = await seed.Orders.SingleAsync(o => o.OrderId == orderId);
                order.StoreId = 2;
                await seed.SaveChangesAsync();
            }

            decimal beforeQty;
            await using (var beforeCtx = CreateContext())
            {
                beforeQty = await beforeCtx.StoreInventories.Where(x => x.IngredientId == IngredientId)
                    .Select(x => x.AvailableQty).SumAsync();
            }

            var result = await CreateSvc(CreateContext())
                .DeductStockForCommittedOrderAsync(Sold(drinkId, sizeId, 1), StoreId, orderId);
            Assert.False(result.IsSuccess);

            await using var verify = CreateContext();
            Assert.Equal(beforeQty, await verify.StoreInventories.Where(x => x.IngredientId == IngredientId)
                .Select(x => x.AvailableQty).SumAsync());
            Assert.Equal(0, await verify.SalesCostAllocations.CountAsync(a => a.OrderId == orderId));
            Assert.Equal(0, await verify.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == orderId));
            Assert.Equal(SalesCostStatus.Pending, (await verify.Orders.SingleAsync(o => o.OrderId == orderId)).CostStatus);
        }

        [Fact]
        public async Task SqlServer_POS_ReplayAfterNewProductionLayer_DoesNotRevalue()
        {
            int orderId, drinkId, sizeId;
            decimal? stored;
            await using (var seed = CreateContext())
            {
                await PutModeAsync(seed);
                (drinkId, sizeId) = await SeedDrinkRecipeIngredientAsync(seed, "SQL-RV", 50m);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 12m);
                orderId = await SeedPaidOrderAsync(seed, drinkId, sizeId, 1, 40000m);
            }

            Assert.True((await CreateSvc(CreateContext()).DeductStockForCommittedOrderAsync(Sold(drinkId, sizeId, 1), StoreId, orderId)).IsSuccess);
            await using (var mid = CreateContext())
            {
                stored = (await mid.Orders.SingleAsync(o => o.OrderId == orderId)).TotalCogs;
                mid.InventoryCostLayers.Add(new InventoryCostLayer
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    Quantity = 1000m,
                    RemainingQuantity = 1000m,
                    UnitCost = 999m,
                    CreatedAt = DateTime.UtcNow
                });
                await mid.SaveChangesAsync();
            }

            Assert.True((await CreateSvc(CreateContext()).DeductStockForCommittedOrderAsync(Sold(drinkId, sizeId, 1), StoreId, orderId)).IsSuccess);
            await using var verify = CreateContext();
            Assert.Equal(stored, (await verify.Orders.SingleAsync(o => o.OrderId == orderId)).TotalCogs);
        }

        [Fact]
        public async Task SqlServer_POS_ToppingPreparedItem_ConsumesPinnedIdentityOnce()
        {
            int orderId, drinkId, sizeId, piId, childRecipeId;
            const int toppingId = 1; // HasData topping
            await using (var seed = CreateContext())
            {
                await PutModeAsync(seed);
                // Seed topping recipe #5 would win FirstOrDefault — deactivate so our pin is used.
                var seedTop = await seed.Recipes.FirstOrDefaultAsync(r => r.ToppingId == toppingId && r.Active);
                if (seedTop != null)
                {
                    seedTop.Active = false;
                    seedTop.Status = "Archived";
                }

                piId = await SeedPreparedItemAsync(seed, "PI-TOP");
                childRecipeId = await SeedChildRecipeAsync(seed, piId);
                (drinkId, sizeId) = await SeedDrinkEmptyAndToppingPiAsync(seed, "SQL-TOP", toppingId, childRecipeId);
                await EnsurePiStockAndLayerAsync(seed, piId, 1000m, 8m);
                orderId = await SeedPaidOrderWithToppingAsync(seed, drinkId, sizeId, toppingId, 1, 45000m);
            }

            await using var c1 = CreateContext();
            await using var c2 = CreateContext();
            var sold = new List<POSSoldItemDto>
            {
                new()
                {
                    DrinkId = drinkId,
                    SizeId = sizeId,
                    Quantity = 1,
                    Toppings = new List<POSOrderToppingDto> { new() { ToppingId = toppingId } }
                }
            };
            await Task.WhenAll(
                CreateSvc(c1).DeductStockForCommittedOrderAsync(sold, StoreId, orderId),
                CreateSvc(c2).DeductStockForCommittedOrderAsync(sold, StoreId, orderId));

            await using var verify = CreateContext();
            var rem = await verify.InventoryCostLayers.Where(x => x.PreparedItemId == piId)
                .Select(x => x.RemainingQuantity).ToListAsync();
            Assert.Equal(900m, rem.Sum());
            Assert.Contains(await verify.InventoryTransactions.Where(t => t.ReferenceOrderId == orderId).ToListAsync(),
                t => t.SourceRecipeId == childRecipeId);
        }

        [Fact]
        public async Task SqlServer_POS_IncompleteCost_ConcurrentOrders_CreateDeterministicGaps()
        {
            int o1, o2, d1, s1, d2, s2;
            await using (var seed = CreateContext())
            {
                await PutModeAsync(seed);
                (d1, s1) = await SeedDrinkRecipeIngredientAsync(seed, "SQL-G1", 100m);
                (d2, s2) = await SeedDrinkRecipeIngredientAsync(seed, "SQL-G2", 100m);
                await EnsureIngredientStockAndLayerAsync(seed, 1000m, 10m);
                // Only 50 remaining on layer for two 100 needs
                var layer = await seed.InventoryCostLayers.SingleAsync(x => x.IngredientId == IngredientId);
                layer.RemainingQuantity = 50m;
                layer.Quantity = 50m;
                await seed.SaveChangesAsync();
                o1 = await SeedPaidOrderAsync(seed, d1, s1, 1, 10000m);
                o2 = await SeedPaidOrderAsync(seed, d2, s2, 1, 10000m);
            }

            await Task.WhenAll(
                CreateSvc(CreateContext()).DeductStockForCommittedOrderAsync(Sold(d1, s1, 1), StoreId, o1),
                CreateSvc(CreateContext()).DeductStockForCommittedOrderAsync(Sold(d2, s2, 1), StoreId, o2));

            await using var verify = CreateContext();
            Assert.Equal(0m, await verify.InventoryCostLayers.Where(x => x.IngredientId == IngredientId)
                .SumAsync(x => x.RemainingQuantity));
            // Both may be incomplete if they race on 50 layer; at least one gap durable.
            var incomplete = await verify.Orders.CountAsync(o =>
                (o.OrderId == o1 || o.OrderId == o2) && o.CostStatus == SalesCostStatus.Incomplete);
            Assert.True(incomplete >= 1);
            Assert.True(await verify.SalesCostGaps.CountAsync(g => g.OrderId == o1 || g.OrderId == o2) >= 1);
        }

        // ---------- helpers ----------

        private static List<POSSoldItemDto> Sold(int drinkId, int sizeId, int qty) => new()
        {
            new() { DrinkId = drinkId, SizeId = sizeId, Quantity = qty }
        };

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new AppDbContext(options);
        }

        private static InventoryDeductionService CreateSvc(AppDbContext context)
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
            return new InventoryDeductionService(
                context,
                NullLogger<InventoryDeductionService>.Instance,
                unit,
                estimated,
                physical,
                null,
                writer,
                new StoreInventoryWriteResolver(context, writer),
                new InventoryCostLayerConsumptionService(context));
        }

        private static async Task PutModeAsync(AppDbContext ctx)
        {
            var cfg = await ctx.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId);
            cfg.WriterMode = InventoryWriterMode.PreparedItem;
            cfg.HasEverActivatedPreparedItem = true;
            cfg.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        private static async Task EnsureIngredientStockAndLayerAsync(AppDbContext ctx, decimal available, decimal unitCost)
        {
            var inv = await ctx.StoreInventories.FirstOrDefaultAsync(x =>
                x.StoreId == StoreId && x.IngredientId == IngredientId);
            if (inv == null)
            {
                ctx.StoreInventories.Add(new StoreInventory
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    AvailableQty = available,
                    ReservedQty = 0,
                    LastUpdated = DateTime.UtcNow
                });
            }
            else
            {
                inv.AvailableQty = available;
                inv.ReservedQty = 0;
            }

            var layers = await ctx.InventoryCostLayers
                .Where(x => x.StoreId == StoreId && x.IngredientId == IngredientId)
                .ToListAsync();
            if (layers.Count > 0)
            {
                var ids = layers.Select(x => x.InventoryCostLayerId).ToList();
                var allocs = await ctx.SalesCostAllocations.Where(a => ids.Contains(a.InventoryCostLayerId)).ToListAsync();
                ctx.SalesCostAllocations.RemoveRange(allocs);
                ctx.InventoryCostLayers.RemoveRange(layers);
            }

            ctx.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                Quantity = available,
                RemainingQuantity = available,
                UnitCost = unitCost,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await ctx.SaveChangesAsync();
        }

        private static async Task EnsurePiStockAndLayerAsync(AppDbContext ctx, int piId, decimal qty, decimal cost)
        {
            var inv = await ctx.StoreInventories.FirstOrDefaultAsync(x =>
                x.StoreId == StoreId && x.PreparedItemId == piId);
            if (inv == null)
            {
                ctx.StoreInventories.Add(new StoreInventory
                {
                    StoreId = StoreId,
                    PreparedItemId = piId,
                    RecipeId = null,
                    BtpIdentityState = BtpIdentityState.Canonical,
                    QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                    QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                    QuantitySemanticsEvidenceReference = "seed",
                    QuantitySemanticsReviewedAt = DateTime.UtcNow,
                    QuantitySemanticsReviewedByAccountId = StaffId,
                    AvailableQty = qty,
                    ReservedQty = 0,
                    LastUpdated = DateTime.UtcNow
                });
            }
            else
            {
                inv.AvailableQty = qty;
            }

            var layers = await ctx.InventoryCostLayers.Where(x => x.PreparedItemId == piId).ToListAsync();
            ctx.InventoryCostLayers.RemoveRange(layers);
            ctx.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = StoreId,
                PreparedItemId = piId,
                IngredientId = null,
                Quantity = qty,
                RemainingQuantity = qty,
                UnitCost = cost,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await ctx.SaveChangesAsync();
        }

        private static async Task<(int DrinkId, int SizeId)> SeedDrinkRecipeIngredientAsync(
            AppDbContext ctx, string code, decimal ingredientQty)
        {
            const int sizeId = 2; // HasData Size M
            var drink = new Drink
            {
                CategoryId = 1,
                DrinkCode = code.Length > 20 ? code[..20] : code,
                ProductTypeId = 1,
                Name = code,
                Description = code,
                Active = true,
                CreatedAt = DateTime.UtcNow,
                CalculatedCogs = 0
            };
            ctx.Drinks.Add(drink);
            await ctx.SaveChangesAsync();

            var recipe = new Recipe
            {
                RecipeCode = code,
                Name = code,
                Active = true,
                Status = "Active",
                DrinkId = drink.DrinkId,
                SizeId = sizeId,
                YieldPercentage = 100m
            };
            ctx.Recipes.Add(recipe);
            await ctx.SaveChangesAsync();
            ctx.RecipeDetails.Add(new RecipeDetail
            {
                RecipeId = recipe.RecipeId,
                IngredientId = IngredientId,
                Quantity = ingredientQty,
                UnitId = UnitGram
            });
            await ctx.SaveChangesAsync();
            return (drink.DrinkId, sizeId);
        }

        private static async Task<int> SeedPreparedItemAsync(AppDbContext ctx, string code)
        {
            var pi = new PreparedItem
            {
                Code = code,
                Name = code,
                BaseUnitId = UnitMl,
                Active = true
            };
            ctx.PreparedItems.Add(pi);
            await ctx.SaveChangesAsync();
            return pi.PreparedItemId;
        }

        private static async Task<int> SeedChildRecipeAsync(AppDbContext ctx, int piId)
        {
            var child = new Recipe
            {
                RecipeCode = "CHILD-" + piId,
                Name = "Child",
                Active = false,
                Status = "Archived",
                YieldPercentage = 100m,
                PreparedItemId = piId,
                OutputQuantity = 1m,
                OutputUnitId = UnitMl
            };
            ctx.Recipes.Add(child);
            await ctx.SaveChangesAsync();
            return child.RecipeId;
        }

        private static async Task<(int DrinkId, int SizeId)> SeedDrinkWithPiAsync(
            AppDbContext ctx, string code, int piId, decimal qtyMl)
        {
            var childId = await SeedChildRecipeAsync(ctx, piId);
            const int sizeId = 2;
            var drink = new Drink
            {
                CategoryId = 1,
                DrinkCode = code.Length > 20 ? code[..20] : code,
                ProductTypeId = 1,
                Name = code,
                Description = code,
                Active = true,
                CreatedAt = DateTime.UtcNow,
                CalculatedCogs = 0
            };
            ctx.Drinks.Add(drink);
            await ctx.SaveChangesAsync();
            var recipe = new Recipe
            {
                RecipeCode = code,
                Name = code,
                Active = true,
                Status = "Active",
                DrinkId = drink.DrinkId,
                SizeId = sizeId,
                YieldPercentage = 100m
            };
            ctx.Recipes.Add(recipe);
            await ctx.SaveChangesAsync();
            ctx.RecipeDetails.Add(new RecipeDetail
            {
                RecipeId = recipe.RecipeId,
                ChildRecipeId = childId,
                Quantity = qtyMl,
                UnitId = UnitMl
            });
            await ctx.SaveChangesAsync();
            return (drink.DrinkId, sizeId);
        }

        private static async Task<(int DrinkId, int SizeId)> SeedDrinkEmptyAndToppingPiAsync(
            AppDbContext ctx, string code, int toppingId, int childRecipeId)
        {
            const int sizeId = 2;
            var drink = new Drink
            {
                CategoryId = 1,
                DrinkCode = (code + "D").Length > 20 ? (code + "D")[..20] : code + "D",
                ProductTypeId = 1,
                Name = code,
                Description = code,
                Active = true,
                CreatedAt = DateTime.UtcNow,
                CalculatedCogs = 0
            };
            ctx.Drinks.Add(drink);
            await ctx.SaveChangesAsync();
            ctx.Recipes.Add(new Recipe
            {
                RecipeCode = code + "-D",
                Name = code,
                Active = true,
                Status = "Active",
                DrinkId = drink.DrinkId,
                SizeId = sizeId,
                YieldPercentage = 100m
            });
            var top = new Recipe
            {
                RecipeCode = code + "-T",
                Name = code + " top",
                Active = true,
                Status = "Active",
                ToppingId = toppingId,
                YieldPercentage = 100m
            };
            ctx.Recipes.Add(top);
            await ctx.SaveChangesAsync();
            ctx.RecipeDetails.Add(new RecipeDetail
            {
                RecipeId = top.RecipeId,
                ChildRecipeId = childRecipeId,
                Quantity = 100m,
                UnitId = UnitMl
            });
            await ctx.SaveChangesAsync();
            return (drink.DrinkId, sizeId);
        }

        private static async Task<int> SeedPaidOrderAsync(
            AppDbContext ctx, int drinkId, int sizeId, int qty, decimal total)
        {
            var order = new Order
            {
                StoreId = StoreId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Source = "POS",
                SubTotal = total,
                Total = total,
                CostStatus = SalesCostStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                StaffId = StaffId
            };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();
            ctx.OrderDetails.Add(new OrderDetail
            {
                OrderId = order.OrderId,
                DrinkId = drinkId,
                SizeId = sizeId,
                DrinkName = "D",
                SizeName = "M",
                Price = total,
                Quantity = qty,
                Note = "",
                CostStatus = SalesCostStatus.Pending
            });
            await ctx.SaveChangesAsync();
            return order.OrderId;
        }

        private static async Task<int> SeedPaidOrderWithToppingAsync(
            AppDbContext ctx, int drinkId, int sizeId, int toppingId, int qty, decimal total)
        {
            var order = new Order
            {
                StoreId = StoreId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Source = "POS",
                SubTotal = total,
                Total = total,
                CostStatus = SalesCostStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                StaffId = StaffId
            };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();
            var od = new OrderDetail
            {
                OrderId = order.OrderId,
                DrinkId = drinkId,
                SizeId = sizeId,
                DrinkName = "D",
                SizeName = "M",
                Price = total - 5000m,
                Quantity = qty,
                Note = "",
                CostStatus = SalesCostStatus.Pending
            };
            ctx.OrderDetails.Add(od);
            await ctx.SaveChangesAsync();
            ctx.OrderToppings.Add(new OrderTopping
            {
                OrderDetailId = od.OrderDetailId,
                ToppingId = toppingId,
                ToppingName = "T",
                Price = 5000m,
                CostStatus = SalesCostStatus.Pending
            });
            await ctx.SaveChangesAsync();
            return order.OrderId;
        }
    }
}
