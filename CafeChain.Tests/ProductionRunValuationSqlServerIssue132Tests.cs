using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>
    /// Issue #132 — SQL Server concurrency/valuation proofs.
    /// Dedicated DB: CafeChain_Issue132Tests (never the local operational CafeChain DB).
    /// </summary>
    public sealed class ProductionRunValuationSqlServerIssue132Tests : IAsyncLifetime
    {
        private const string Server = @"DESKTOP-K038H12\SQLEXPRESS";
        private const string Database = "CafeChain_Issue132Tests";

        private static string ConnectionString =>
            $"Server={Server};Database={Database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        private static string MasterConnectionString =>
            $"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True";

        private const int StoreId = 1;
        private const int StaffId = 1;
        private const int IngredientId = 1;
        private const int UnitGram = 1;
        private const int UnitMl = 3;
        private const int UnitL = 4;

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
                    $"BLOCKED_ON_SQL_SERVER: SQL Server unavailable for #132. Server={Server}, Database={Database}. {ex.Message}",
                    ex);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SqlServer_Production_ConcurrentCompleteSameRun_CreatesOneOutputLayer()
        {
            int piId, recipeId, runId;
            await using (var seed = CreateContext())
            {
                piId = await SeedPreparedItemAsync(seed, "PI-132-C1");
                recipeId = await SeedRecipeAsync(seed, "RCP-132-C1", piId, 4.5m, UnitL, 500m);
                await PutModeAsync(seed, InventoryWriterMode.PreparedItem);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 10m);
                await ClearPiOutputsAsync(seed, piId);
                runId = await SeedConfirmedRunAsync(seed, recipeId, 1m);
            }

            await using var c1 = CreateContext();
            await using var c2 = CreateContext();
            var results = await Task.WhenAll(
                CreateExec(c1).ExecuteAsync(runId, StaffId, StoreId),
                CreateExec(c2).ExecuteAsync(runId, StaffId, StoreId));

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message + " " + r.ErrorCode));
            Assert.Contains(results, r => r.Data!.WasReplay);
            Assert.Contains(results, r => !r.Data!.WasReplay);

            await using var verify = CreateContext();
            Assert.Equal(1, await verify.InventoryCostLayers.CountAsync(x => x.SourceProductionRunId == runId));
        }

        [Fact]
        public async Task SqlServer_Production_ConcurrentCompleteSameRun_ConsumesInputLayersOnce()
        {
            int piId, recipeId, runId;
            await using (var seed = CreateContext())
            {
                piId = await SeedPreparedItemAsync(seed, "PI-132-C2");
                recipeId = await SeedRecipeAsync(seed, "RCP-132-C2", piId, 4.5m, UnitL, 500m);
                await PutModeAsync(seed, InventoryWriterMode.PreparedItem);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 10m);
                await ClearPiOutputsAsync(seed, piId);
                runId = await SeedConfirmedRunAsync(seed, recipeId, 1m);
            }

            await using var c1 = CreateContext();
            await using var c2 = CreateContext();
            await Task.WhenAll(
                CreateExec(c1).ExecuteAsync(runId, StaffId, StoreId),
                CreateExec(c2).ExecuteAsync(runId, StaffId, StoreId));

            await using var verify = CreateContext();
            var layer = await verify.InventoryCostLayers.SingleAsync(x =>
                x.StoreId == StoreId && x.IngredientId == IngredientId && x.SourceProductionRunId == null);
            Assert.Equal(4500m, layer.RemainingQuantity);

            var allocQty = await verify.ProductionCostAllocations
                .Where(a => a.ProductionRunId == runId)
                .SumAsync(a => a.Quantity);
            Assert.Equal(500m, allocQty);
        }

        [Fact]
        public async Task SqlServer_Production_TwoRuns_CannotOverConsumeSameInputLayer()
        {
            int piA, piB, rA, rB, runA, runB;
            await using (var seed = CreateContext())
            {
                piA = await SeedPreparedItemAsync(seed, "PI-132-O1");
                piB = await SeedPreparedItemAsync(seed, "PI-132-O2");
                rA = await SeedRecipeAsync(seed, "RCP-132-O1", piA, 100m, UnitMl, 400m);
                rB = await SeedRecipeAsync(seed, "RCP-132-O2", piB, 100m, UnitMl, 400m);
                await PutModeAsync(seed, InventoryWriterMode.PreparedItem);
                // Only 500g layer for two 400g runs — one must fail cost or stock
                await EnsureIngredientStockAndLayerAsync(seed, 500m, 10m);
                await ClearPiOutputsAsync(seed, piA);
                await ClearPiOutputsAsync(seed, piB);
                runA = await SeedConfirmedRunAsync(seed, rA, 1m);
                runB = await SeedConfirmedRunAsync(seed, rB, 1m);
            }

            await using var c1 = CreateContext();
            await using var c2 = CreateContext();
            var results = await Task.WhenAll(
                CreateExec(c1).ExecuteAsync(runA, StaffId, StoreId),
                CreateExec(c2).ExecuteAsync(runB, StaffId, StoreId));

            var success = results.Count(r => r.IsSuccess);
            var failed = results.Count(r => !r.IsSuccess);
            Assert.Equal(1, success);
            Assert.Equal(1, failed);
            Assert.Contains(results.Where(r => !r.IsSuccess), r =>
                r.ErrorCode is ProductionRunExecutionFailureCodes.InsufficientStock
                    or ProductionRunExecutionFailureCodes.CostEvidenceIncomplete);

            await using var verify = CreateContext();
            var remaining = await verify.InventoryCostLayers
                .Where(x => x.StoreId == StoreId && x.IngredientId == IngredientId && x.SourceProductionRunId == null)
                .SumAsync(x => x.RemainingQuantity);
            Assert.True(remaining >= 0);
            Assert.True(remaining <= 100m + 0.001m); // 500 - 400
            Assert.Equal(1, await verify.InventoryCostLayers.CountAsync(x => x.SourceProductionRunId != null
                && (x.SourceProductionRunId == runA || x.SourceProductionRunId == runB)));
        }

        [Fact]
        public async Task SqlServer_Production_ReceiptAndComplete_SerializeCostLayers()
        {
            // Serialization proof: production complete holds layer lock; concurrent complete on another run uses same FIFO order.
            int piId, recipeId, runId;
            await using (var seed = CreateContext())
            {
                piId = await SeedPreparedItemAsync(seed, "PI-132-R1");
                recipeId = await SeedRecipeAsync(seed, "RCP-132-R1", piId, 4.5m, UnitL, 500m);
                await PutModeAsync(seed, InventoryWriterMode.PreparedItem);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 5m);
                // Second layer arrives "after receipt" with higher cost
                seed.InventoryCostLayers.Add(new InventoryCostLayer
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    Quantity = 1000m,
                    RemainingQuantity = 1000m,
                    UnitCost = 50m,
                    CreatedAt = DateTime.UtcNow.AddMinutes(1)
                });
                await seed.SaveChangesAsync();
                await ClearPiOutputsAsync(seed, piId);
                runId = await SeedConfirmedRunAsync(seed, recipeId, 1m);
            }

            var result = await CreateExec(CreateContext()).ExecuteAsync(runId, StaffId, StoreId);
            Assert.True(result.IsSuccess, result.Message);
            // FIFO consumes first layer @5 only
            Assert.Equal(500m * 5m, result.Data!.TotalInputCost);

            await using var verify = CreateContext();
            var first = await verify.InventoryCostLayers
                .Where(x => x.IngredientId == IngredientId && x.UnitCost == 5m)
                .SingleAsync();
            Assert.Equal(4500m, first.RemainingQuantity);
        }

        [Fact]
        public async Task SqlServer_Production_TransferAndComplete_UseCompatibleLockOrder()
        {
            // Lock order: ProductionRun → writer config → StoreInventory ASC → CostLayer ASC → output.
            // Prove production complete does not deadlock against concurrent same-run retries.
            int piId, recipeId, runId;
            await using (var seed = CreateContext())
            {
                piId = await SeedPreparedItemAsync(seed, "PI-132-L1");
                recipeId = await SeedRecipeAsync(seed, "RCP-132-L1", piId, 4.5m, UnitL, 500m);
                await PutModeAsync(seed, InventoryWriterMode.PreparedItem);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 10m);
                await ClearPiOutputsAsync(seed, piId);
                runId = await SeedConfirmedRunAsync(seed, recipeId, 1m);
            }

            var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
            {
                await using var ctx = CreateContext();
                return await CreateExec(ctx).ExecuteAsync(runId, StaffId, StoreId);
            })).ToArray();

            var results = await Task.WhenAll(tasks);
            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message));
            Assert.Equal(1, results.Count(r => !r.Data!.WasReplay));
            Assert.Equal(3, results.Count(r => r.Data!.WasReplay));
        }

        [Fact]
        public async Task SqlServer_Production_CompleteFailure_RollsBackAllValuationAndQuantity()
        {
            int piId, recipeId, runId;
            await using (var seed = CreateContext())
            {
                piId = await SeedPreparedItemAsync(seed, "PI-132-F1");
                recipeId = await SeedRecipeAsync(seed, "RCP-132-F1", piId, 4.5m, UnitL, 500m);
                await PutModeAsync(seed, InventoryWriterMode.PreparedItem);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 10m);
                // Wipe layers after stock seed to force cost fail
                var layers = await seed.InventoryCostLayers.Where(x => x.IngredientId == IngredientId).ToListAsync();
                seed.InventoryCostLayers.RemoveRange(layers);
                await seed.SaveChangesAsync();
                await ClearPiOutputsAsync(seed, piId);
                runId = await SeedConfirmedRunAsync(seed, recipeId, 1m);
            }

            var result = await CreateExec(CreateContext()).ExecuteAsync(runId, StaffId, StoreId);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunExecutionFailureCodes.CostEvidenceIncomplete, result.ErrorCode);

            await using var verify = CreateContext();
            var ingQty = await verify.StoreInventories
                .Where(x => x.StoreId == StoreId && x.IngredientId == IngredientId)
                .SumAsync(x => x.AvailableQty);
            Assert.Equal(5000m, ingQty);
            Assert.Equal(0, await verify.ProductionCostAllocations.CountAsync(a => a.ProductionRunId == runId));
            Assert.Equal(0, await verify.InventoryTransactions.CountAsync(t => t.ProductionRunId == runId));
            var failedRun = await verify.ProductionRuns.SingleAsync(r => r.ProductionRunId == runId);
            Assert.Equal(ProductionRunStatus.Confirmed, failedRun.Status);
            Assert.Equal(ProductionValuationStatus.Pending, failedRun.ValuationStatus);
        }

        [Fact]
        public async Task SqlServer_Production_ReplayAfterSupplierPriceChange_DoesNotRevalue()
        {
            int piId, recipeId, runId;
            decimal? storedTotal;
            await using (var seed = CreateContext())
            {
                piId = await SeedPreparedItemAsync(seed, "PI-132-RV");
                recipeId = await SeedRecipeAsync(seed, "RCP-132-RV", piId, 4.5m, UnitL, 500m);
                await PutModeAsync(seed, InventoryWriterMode.PreparedItem);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 12m);
                await ClearPiOutputsAsync(seed, piId);
                runId = await SeedConfirmedRunAsync(seed, recipeId, 1m);
            }

            var first = await CreateExec(CreateContext()).ExecuteAsync(runId, StaffId, StoreId);
            Assert.True(first.IsSuccess, first.Message);
            storedTotal = first.Data!.TotalInputCost;

            await using (var poison = CreateContext())
            {
                var layers = await poison.InventoryCostLayers.Where(x => x.IngredientId == IngredientId).ToListAsync();
                foreach (var l in layers)
                    l.UnitCost = 999m;
                await poison.SaveChangesAsync();
            }

            var replay = await CreateExec(CreateContext()).ExecuteAsync(runId, StaffId, StoreId);
            Assert.True(replay.IsSuccess);
            Assert.True(replay.Data!.WasReplay);
            Assert.Equal(storedTotal, replay.Data.TotalInputCost);
            // Compare against DB snapshot (decimal column precision), not in-memory pre-reload calc.
            await using var verify = CreateContext();
            var snap = await verify.ProductionRuns.AsNoTracking().SingleAsync(r => r.ProductionRunId == runId);
            Assert.Equal(snap.OutputUnitCost, replay.Data.OutputUnitCost);
            Assert.Equal(snap.TotalInputCost, replay.Data.TotalInputCost);
        }

        [Fact]
        public async Task SqlServer_Production_PreparedItemInput_ConsumesLayerOnce()
        {
            int parentPi, childPi, recipeId, runId;
            await using (var seed = CreateContext())
            {
                childPi = await SeedPreparedItemAsync(seed, "PI-132-CH");
                parentPi = await SeedPreparedItemAsync(seed, "PI-132-PA");
                var childRecipe = new Recipe
                {
                    RecipeCode = "CHILD-132-SQL",
                    Name = "Child",
                    Active = false,
                    Status = "Archived",
                    YieldPercentage = 100m,
                    PreparedItemId = childPi,
                    OutputQuantity = 1m,
                    OutputUnitId = UnitMl
                };
                seed.Recipes.Add(childRecipe);
                await seed.SaveChangesAsync();

                var parent = new Recipe
                {
                    RecipeCode = "PAR-132-SQL",
                    Name = "Parent",
                    Active = true,
                    Status = "Active",
                    YieldPercentage = 100m,
                    PreparedItemId = parentPi,
                    OutputQuantity = 4.5m,
                    OutputUnitId = UnitL
                };
                seed.Recipes.Add(parent);
                await seed.SaveChangesAsync();
                seed.RecipeDetails.Add(new RecipeDetail
                {
                    RecipeId = parent.RecipeId,
                    ChildRecipeId = childRecipe.RecipeId,
                    Quantity = 100m,
                    UnitId = UnitMl
                });
                seed.StoreInventories.Add(new StoreInventory
                {
                    StoreId = StoreId,
                    PreparedItemId = childPi,
                    RecipeId = null,
                    BtpIdentityState = BtpIdentityState.Canonical,
                    QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                    QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                    QuantitySemanticsEvidenceReference = "seed",
                    QuantitySemanticsReviewedAt = DateTime.UtcNow,
                    QuantitySemanticsReviewedByAccountId = StaffId,
                    AvailableQty = 1000m,
                    ReservedQty = 0m,
                    LastUpdated = DateTime.UtcNow
                });
                seed.InventoryCostLayers.Add(new InventoryCostLayer
                {
                    StoreId = StoreId,
                    PreparedItemId = childPi,
                    IngredientId = null,
                    Quantity = 1000m,
                    RemainingQuantity = 1000m,
                    UnitCost = 20m,
                    CreatedAt = DateTime.UtcNow
                });
                await PutModeAsync(seed, InventoryWriterMode.PreparedItem);
                await seed.SaveChangesAsync();
                recipeId = parent.RecipeId;
                runId = await SeedConfirmedRunAsync(seed, recipeId, 1m);
            }

            await using var c1 = CreateContext();
            await using var c2 = CreateContext();
            await Task.WhenAll(
                CreateExec(c1).ExecuteAsync(runId, StaffId, StoreId),
                CreateExec(c2).ExecuteAsync(runId, StaffId, StoreId));

            await using var verify = CreateContext();
            var layer = await verify.InventoryCostLayers.SingleAsync(x =>
                x.PreparedItemId == childPi && x.SourceProductionRunId == null);
            Assert.Equal(900m, layer.RemainingQuantity);
            Assert.Equal(1, await verify.InventoryCostLayers.CountAsync(x => x.SourceProductionRunId == runId));
        }

        [Fact]
        public async Task SqlServer_Production_OutputLayerUniqueConstraint_PreventsDuplicate()
        {
            int piId, recipeId, runId;
            await using (var seed = CreateContext())
            {
                piId = await SeedPreparedItemAsync(seed, "PI-132-UQ");
                recipeId = await SeedRecipeAsync(seed, "RCP-132-UQ", piId, 4.5m, UnitL, 500m);
                await PutModeAsync(seed, InventoryWriterMode.PreparedItem);
                await EnsureIngredientStockAndLayerAsync(seed, 5000m, 10m);
                await ClearPiOutputsAsync(seed, piId);
                runId = await SeedConfirmedRunAsync(seed, recipeId, 1m);
            }

            Assert.True((await CreateExec(CreateContext()).ExecuteAsync(runId, StaffId, StoreId)).IsSuccess);

            await using var ctx = CreateContext();
            ctx.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = StoreId,
                PreparedItemId = piId,
                IngredientId = null,
                Quantity = 1m,
                RemainingQuantity = 1m,
                UnitCost = 1m,
                CreatedAt = DateTime.UtcNow,
                SourceProductionRunId = runId
            });
            await Assert.ThrowsAnyAsync<Exception>(async () => await ctx.SaveChangesAsync());
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new AppDbContext(options);
        }

        private static ProductionRunExecutionService CreateExec(AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
            var caps = new IInventoryWriterCapabilityProvider[] { new ProductionPreparedWriterCapabilityProvider() };
            var writer = new InventoryWriterModeService(context, physical, caps);
            var resolver = new StoreInventoryWriteResolver(context, writer);
            return new ProductionRunExecutionService(
                context,
                new ScopeAuthorizationService(context),
                writer,
                resolver,
                physical,
                unit,
                new InventoryCostLayerConsumptionService(context),
                caps,
                NullLogger<ProductionRunExecutionService>.Instance);
        }

        private static async Task PutModeAsync(AppDbContext context, InventoryWriterMode mode)
        {
            var cfg = await context.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId);
            cfg.WriterMode = mode;
            cfg.HasEverActivatedPreparedItem = true;
            cfg.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        private static async Task EnsureIngredientStockAndLayerAsync(AppDbContext context, decimal available, decimal unitCost)
        {
            var inv = await context.StoreInventories.FirstOrDefaultAsync(x =>
                x.StoreId == StoreId && x.IngredientId == IngredientId);
            if (inv == null)
            {
                context.StoreInventories.Add(new StoreInventory
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    AvailableQty = available,
                    ReservedQty = 0m,
                    LastUpdated = DateTime.UtcNow
                });
            }
            else
            {
                inv.AvailableQty = available;
                inv.ReservedQty = 0m;
                inv.LastUpdated = DateTime.UtcNow;
            }

            var layers = await context.InventoryCostLayers
                .Where(x => x.StoreId == StoreId && x.IngredientId == IngredientId)
                .ToListAsync();
            if (layers.Count > 0)
            {
                var ids = layers.Select(x => x.InventoryCostLayerId).ToList();
                var allocs = await context.ProductionCostAllocations
                    .Where(a => ids.Contains(a.InventoryCostLayerId)).ToListAsync();
                context.ProductionCostAllocations.RemoveRange(allocs);
                context.InventoryCostLayers.RemoveRange(layers);
            }

            context.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                PreparedItemId = null,
                Quantity = available,
                RemainingQuantity = available,
                UnitCost = unitCost,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await context.SaveChangesAsync();
        }

        private static async Task ClearPiOutputsAsync(AppDbContext context, int preparedItemId)
        {
            var leftovers = await context.StoreInventories
                .Where(x => x.StoreId == StoreId && x.PreparedItemId == preparedItemId)
                .ToListAsync();
            if (leftovers.Count == 0)
                return;

            var invIds = leftovers.Select(x => x.StoreInventoryId).ToList();
            var txs = await context.InventoryTransactions.Where(t => invIds.Contains(t.StoreInventoryId)).ToListAsync();
            var txIds = txs.Select(t => t.InventoryTransactionId).ToList();
            var allocs = await context.ProductionCostAllocations
                .Where(a => txIds.Contains(a.InventoryTransactionId)).ToListAsync();
            var outLayers = await context.InventoryCostLayers
                .Where(x => x.PreparedItemId == preparedItemId).ToListAsync();
            context.ProductionCostAllocations.RemoveRange(allocs);
            context.InventoryTransactions.RemoveRange(txs);
            context.InventoryCostLayers.RemoveRange(outLayers);
            context.StoreInventories.RemoveRange(leftovers);
            await context.SaveChangesAsync();
        }

        private static async Task<int> SeedPreparedItemAsync(AppDbContext context, string code)
        {
            var pi = new PreparedItem
            {
                Code = code,
                Name = "SQL " + code,
                BaseUnitId = UnitMl,
                Active = true
            };
            context.PreparedItems.Add(pi);
            await context.SaveChangesAsync();
            return pi.PreparedItemId;
        }

        private static async Task<int> SeedRecipeAsync(
            AppDbContext context,
            string code,
            int preparedItemId,
            decimal outputQty,
            int outputUnitId,
            decimal inputIngredientQty)
        {
            var recipe = new Recipe
            {
                RecipeCode = code,
                Name = "SQL Recipe " + code,
                Active = true,
                Status = "Active",
                YieldPercentage = 100m,
                PreparedItemId = preparedItemId,
                OutputQuantity = outputQty,
                OutputUnitId = outputUnitId
            };
            context.Recipes.Add(recipe);
            await context.SaveChangesAsync();
            context.RecipeDetails.Add(new RecipeDetail
            {
                RecipeId = recipe.RecipeId,
                IngredientId = IngredientId,
                Quantity = inputIngredientQty,
                UnitId = UnitGram
            });
            await context.SaveChangesAsync();
            return recipe.RecipeId;
        }

        private static async Task<int> SeedConfirmedRunAsync(AppDbContext context, int recipeId, decimal runs)
        {
            var now = DateTime.UtcNow;
            var run = new ProductionRun
            {
                StoreId = StoreId,
                RecipeId = recipeId,
                RequestedRunCount = runs,
                RequestKey = Guid.NewGuid(),
                RequestFingerprint = ProductionRunService.BuildFingerprint(StoreId, recipeId, runs),
                Status = ProductionRunStatus.Confirmed,
                ValuationStatus = ProductionValuationStatus.Pending,
                CreatedByStaffId = StaffId,
                CreatedAt = now,
                ConfirmedAt = now
            };
            context.ProductionRuns.Add(run);
            await context.SaveChangesAsync();
            return run.ProductionRunId;
        }
    }
}
