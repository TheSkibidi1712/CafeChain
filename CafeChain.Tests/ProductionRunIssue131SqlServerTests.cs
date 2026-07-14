using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
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
    /// Issue #131 — SQL Server proofs for PreparedItem-only create + execute contract.
    /// Dedicated DB: CafeChain_Issue131Tests (not the local CafeChain app DB).
    /// </summary>
    public sealed class ProductionRunIssue131SqlServerTests : IAsyncLifetime
    {
        private const string Server = @"DESKTOP-K038H12\SQLEXPRESS";
        private const string Database = "CafeChain_Issue131Tests";

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
                    $"SQL Server unavailable for #131 tests. Server={Server}, Database={Database}. {ex.Message}",
                    ex);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SqlServer_ProductionRun_CreateAndExecute_PreparedItemMode_EndToEnd()
        {
            await using var ctx = CreateContext();
            var piId = await SeedPreparedItemAsync(ctx, "PI-131-E2E");
            var recipeId = await SeedRecipeAsync(ctx, "RCP-131-E2E", piId, 4.5m, UnitL, 500m);
            await PutModeAsync(ctx, InventoryWriterMode.PreparedItem);
            await EnsureIngredientStockAsync(ctx, 5000m, 0m);
            await ClearOutputsAsync(ctx, piId);

            var confirm = CreateConfirmService(ctx);
            var create = await confirm.CreateAndConfirmAsync(
                new CreateAndConfirmProductionRunRequest
                {
                    RequestKey = Guid.NewGuid(),
                    StoreId = StoreId,
                    RecipeId = recipeId,
                    RequestedRunCount = 1m
                },
                StaffId,
                StoreId);

            Assert.True(create.IsSuccess, create.Message);
            Assert.False(create.Data!.StockApplied);

            var exec = CreateExecutionService(ctx);
            var applied = await exec.ExecuteAsync(create.Data.ProductionRunId, StaffId, StoreId);

            Assert.True(applied.IsSuccess, applied.Message);
            Assert.True(applied.Data!.StockApplied);
            Assert.Equal(4500m, applied.Data.NormalizedOutputQuantity);

            var outRow = await ctx.StoreInventories.SingleAsync(x =>
                x.StoreId == StoreId && x.PreparedItemId == piId && x.RecipeId == null);
            Assert.Equal(4500m, outRow.AvailableQty);

            var txs = await ctx.InventoryTransactions
                .Where(t => t.ProductionRunId == create.Data.ProductionRunId)
                .ToListAsync();
            Assert.Equal(2, txs.Count);
            Assert.Contains(txs, t => t.Type == InventoryTransactionTypeEnum.PRODUCTION_OUT);
            Assert.Contains(txs, t => t.Type == InventoryTransactionTypeEnum.PRODUCTION_IN);
            Assert.DoesNotContain(
                await ctx.StoreInventories.Where(x => x.StoreId == StoreId && x.RecipeId == recipeId).ToListAsync(),
                x => x.PreparedItemId == null && x.RecipeId == recipeId && x.AvailableQty > 0);
        }

        [Fact]
        public async Task SqlServer_ProductionRun_RequestKeyReplay_DoesNotDuplicateRun()
        {
            await using var ctx = CreateContext();
            var piId = await SeedPreparedItemAsync(ctx, "PI-131-RPL");
            var recipeId = await SeedRecipeAsync(ctx, "RCP-131-RPL", piId, 1m, UnitGram, 10m);
            await PutModeAsync(ctx, InventoryWriterMode.PreparedItem);

            var key = Guid.NewGuid();
            var svc = CreateConfirmService(ctx);
            var a = await svc.CreateAndConfirmAsync(Req(key, recipeId, 1m), StaffId, StoreId);
            var b = await svc.CreateAndConfirmAsync(Req(key, recipeId, 1m), StaffId, StoreId);

            Assert.True(a.IsSuccess && b.IsSuccess);
            Assert.True(b.Data!.WasReplay);
            Assert.Equal(a.Data!.ProductionRunId, b.Data.ProductionRunId);
            Assert.Equal(1, await ctx.ProductionRuns.CountAsync());
        }

        [Fact]
        public async Task SqlServer_ProductionRun_ConcurrentExecuteSameRun_MutatesOnce()
        {
            int runId;
            int piId;
            await using (var seed = CreateContext())
            {
                piId = await SeedPreparedItemAsync(seed, "PI-131-CONC");
                var recipeId = await SeedRecipeAsync(seed, "RCP-131-CONC", piId, 1m, UnitL, 100m);
                await PutModeAsync(seed, InventoryWriterMode.PreparedItem);
                await EnsureIngredientStockAsync(seed, 5000m, 0m);
                await ClearOutputsAsync(seed, piId);

                var created = await CreateConfirmService(seed).CreateAndConfirmAsync(
                    Req(Guid.NewGuid(), recipeId, 1m), StaffId, StoreId);
                Assert.True(created.IsSuccess, created.Message);
                runId = created.Data!.ProductionRunId;
            }

            await using var c1 = CreateContext();
            await using var c2 = CreateContext();
            var results = await Task.WhenAll(
                CreateExecutionService(c1).ExecuteAsync(runId, StaffId, StoreId),
                CreateExecutionService(c2).ExecuteAsync(runId, StaffId, StoreId));

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message));
            Assert.Equal(1, results.Count(r => !r.Data!.WasReplay));
            Assert.Equal(1, results.Count(r => r.Data!.WasReplay));

            await using var verify = CreateContext();
            Assert.Equal(2, await verify.InventoryTransactions.CountAsync(t => t.ProductionRunId == runId));
            var qty = await verify.StoreInventories
                .Where(x => x.StoreId == StoreId && x.PreparedItemId == piId)
                .SumAsync(x => x.AvailableQty);
            Assert.Equal(1000m, qty); // 1 L → 1000 ml base
        }

        [Fact]
        public async Task SqlServer_ProductionRun_LegacyConfirmed_ExecutesOnlyAfterCutover()
        {
            await using var ctx = CreateContext();
            var piId = await SeedPreparedItemAsync(ctx, "PI-131-LEG");
            var recipeId = await SeedRecipeAsync(ctx, "RCP-131-LEG", piId, 1m, UnitL, 100m);
            await PutModeAsync(ctx, InventoryWriterMode.LegacyRecipe);
            await EnsureIngredientStockAsync(ctx, 5000m, 0m);
            await ClearOutputsAsync(ctx, piId);

            // Pre-cutover Confirmed seed (create would reject Legacy under #131)
            var now = DateTime.UtcNow;
            var run = new ProductionRun
            {
                StoreId = StoreId,
                RecipeId = recipeId,
                RequestedRunCount = 1m,
                RequestKey = Guid.NewGuid(),
                RequestFingerprint = ProductionRunService.BuildFingerprint(StoreId, recipeId, 1m),
                Status = ProductionRunStatus.Confirmed,
                CreatedByStaffId = StaffId,
                CreatedAt = now,
                ConfirmedAt = now
            };
            ctx.ProductionRuns.Add(run);
            await ctx.SaveChangesAsync();

            var failLegacy = await CreateExecutionService(ctx).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.False(failLegacy.IsSuccess);
            Assert.Equal(ProductionRunExecutionFailureCodes.ModeLegacy, failLegacy.ErrorCode);

            await PutModeAsync(ctx, InventoryWriterMode.PreparedItem);
            var ok = await CreateExecutionService(ctx).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(ok.IsSuccess, ok.Message);
            Assert.True(ok.Data!.StockApplied);
        }

        [Fact]
        public async Task SqlServer_ProductionRun_CreateVsBlock_SerializesSafely()
        {
            await using var ctx = CreateContext();
            var piId = await SeedPreparedItemAsync(ctx, "PI-131-BLK");
            var recipeId = await SeedRecipeAsync(ctx, "RCP-131-BLK", piId, 1m, UnitGram, 10m);
            await PutModeAsync(ctx, InventoryWriterMode.PreparedItem);

            // Concurrent: create intent vs block store
            await using var createCtx = CreateContext();
            await using var blockCtx = CreateContext();

            var createTask = Task.Run(async () =>
            {
                await Task.Delay(30);
                return await CreateConfirmService(createCtx).CreateAndConfirmAsync(
                    Req(Guid.NewGuid(), recipeId, 1m), StaffId, StoreId);
            });

            var blockTask = Task.Run(async () =>
            {
                var cfg = await blockCtx.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId);
                cfg.WriterMode = InventoryWriterMode.Blocked;
                cfg.UpdatedAt = DateTime.UtcNow;
                await blockCtx.SaveChangesAsync();
                return true;
            });

            await Task.WhenAll(createTask, blockTask);
            var createResult = await createTask;

            // After both complete: either create won before block (success) or lost (ModeBlocked).
            // Must not leave a Confirmed run while store is Blocked if create observed Blocked.
            await using var verify = CreateContext();
            var mode = (await verify.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId)).WriterMode;
            var runCount = await verify.ProductionRuns.CountAsync();

            if (createResult.IsSuccess)
            {
                Assert.Equal(1, runCount);
                // Store may still be Blocked after create — that is allowed for pre-existing Confirmed.
            }
            else
            {
                Assert.True(
                    createResult.ErrorCode is ProductionRunFailureCodes.ModeBlocked
                        or ProductionRunFailureCodes.ModeLegacy
                        or ProductionRunFailureCodes.CapabilityNotReady
                        or ProductionRunFailureCodes.MissingWriterConfiguration
                        or ProductionRunFailureCodes.InvalidRequest,
                    createResult.ErrorCode + " " + createResult.Message);
                if (mode == InventoryWriterMode.Blocked)
                    Assert.True(runCount == 0 || createResult.ErrorCode == ProductionRunFailureCodes.ModeBlocked);
            }
        }

        [Fact]
        public async Task SqlServer_ProductionRun_CreateDuringPreparedItemActivation_SerializesSafely()
        {
            await using var ctx = CreateContext();
            var piId = await SeedPreparedItemAsync(ctx, "PI-131-ACT");
            var recipeId = await SeedRecipeAsync(ctx, "RCP-131-ACT", piId, 1m, UnitGram, 10m);
            await PutModeAsync(ctx, InventoryWriterMode.LegacyRecipe);

            await using var createCtx = CreateContext();
            await using var actCtx = CreateContext();

            var createTask = Task.Run(async () =>
                await CreateConfirmService(createCtx).CreateAndConfirmAsync(
                    Req(Guid.NewGuid(), recipeId, 1m), StaffId, StoreId));

            var activateTask = Task.Run(async () =>
            {
                await Task.Delay(20);
                var cfg = await actCtx.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId);
                cfg.WriterMode = InventoryWriterMode.PreparedItem;
                cfg.HasEverActivatedPreparedItem = true;
                cfg.UpdatedAt = DateTime.UtcNow;
                await actCtx.SaveChangesAsync();
                return true;
            });

            await Task.WhenAll(createTask, activateTask);
            var createResult = await createTask;

            await using var verify = CreateContext();
            var mode = (await verify.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId)).WriterMode;
            var runs = await verify.ProductionRuns.CountAsync();

            // Create either failed closed on Legacy or succeeded after reading PreparedItem — never duplicate.
            Assert.True(runs <= 1);
            if (createResult.IsSuccess)
            {
                Assert.Equal(InventoryWriterMode.PreparedItem, mode);
                Assert.Equal(1, runs);
            }
            else
            {
                Assert.Equal(ProductionRunFailureCodes.ModeLegacy, createResult.ErrorCode);
            }
        }

        private static CreateAndConfirmProductionRunRequest Req(Guid key, int recipeId, decimal runs)
            => new()
            {
                RequestKey = key,
                StoreId = StoreId,
                RecipeId = recipeId,
                RequestedRunCount = runs
            };

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new AppDbContext(options);
        }

        private static ProductionRunService CreateConfirmService(AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
            var caps = new IInventoryWriterCapabilityProvider[] { new ProductionPreparedWriterCapabilityProvider() };
            var writer = new InventoryWriterModeService(context, physical, caps);
            return new ProductionRunService(
                context,
                new ScopeAuthorizationService(context),
                writer,
                caps,
                NullLogger<ProductionRunService>.Instance);
        }

        private static ProductionRunExecutionService CreateExecutionService(AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
            var caps = new IInventoryWriterCapabilityProvider[] { new ProductionPreparedWriterCapabilityProvider() };
            var writer = new InventoryWriterModeService(context, physical, caps);
            var resolver = new StoreInventoryWriteResolver(context, writer);
            var cost = new InventoryCostLayerConsumptionService(context);
            return new ProductionRunExecutionService(
                context,
                new ScopeAuthorizationService(context),
                writer,
                resolver,
                physical,
                unit,
                cost,
                caps,
                NullLogger<ProductionRunExecutionService>.Instance);
        }

        private static async Task PutModeAsync(AppDbContext context, InventoryWriterMode mode)
        {
            var cfg = await context.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId);
            cfg.WriterMode = mode;
            cfg.HasEverActivatedPreparedItem = mode == InventoryWriterMode.PreparedItem || cfg.HasEverActivatedPreparedItem;
            cfg.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        private static async Task EnsureIngredientStockAsync(AppDbContext context, decimal available, decimal reserved)
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
                    ReservedQty = reserved,
                    LastUpdated = DateTime.UtcNow
                });
            }
            else
            {
                inv.AvailableQty = available;
                inv.ReservedQty = reserved;
                inv.LastUpdated = DateTime.UtcNow;
            }

            var layers = await context.InventoryCostLayers
                .Where(x => x.StoreId == StoreId && x.IngredientId == IngredientId)
                .ToListAsync();
            if (layers.Count > 0)
            {
                var layerIds = layers.Select(x => x.InventoryCostLayerId).ToList();
                var allocs = await context.ProductionCostAllocations
                    .Where(a => layerIds.Contains(a.InventoryCostLayerId))
                    .ToListAsync();
                context.ProductionCostAllocations.RemoveRange(allocs);
                context.InventoryCostLayers.RemoveRange(layers);
            }

            if (available > 0)
            {
                context.InventoryCostLayers.Add(new CafeChain.Models.Inventories.Costing.InventoryCostLayer
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    PreparedItemId = null,
                    Quantity = available,
                    RemainingQuantity = available,
                    UnitCost = 10.00m,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
        }

        private static async Task ClearOutputsAsync(AppDbContext context, int preparedItemId)
        {
            var leftovers = await context.StoreInventories
                .Where(x => x.StoreId == StoreId && x.PreparedItemId == preparedItemId)
                .ToListAsync();
            if (leftovers.Count == 0) return;
            var invIds = leftovers.Select(x => x.StoreInventoryId).ToList();
            var txs = await context.InventoryTransactions.Where(t => invIds.Contains(t.StoreInventoryId)).ToListAsync();
            context.InventoryTransactions.RemoveRange(txs);
            context.StoreInventories.RemoveRange(leftovers);
            await context.SaveChangesAsync();
        }

        private static async Task<int> SeedPreparedItemAsync(AppDbContext context, string code)
        {
            var pi = new PreparedItem { Code = code, Name = code, BaseUnitId = UnitMl, Active = true };
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
                Name = code,
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
    }
}
