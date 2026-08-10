using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>
    /// Issue #120 — SQL Server concurrency proof (UPDLOCK/HOLDLOCK + unique canonical).
    /// Uses dedicated database CafeChain_Issue120Tests on local SQLEXPRESS.
    /// Seeds rely on EnsureCreated HasData (units/stores/staff/ingredients) and auto-identity for new rows.
    /// </summary>
    [Trait("Category", "SqlServerIntegration")]
    public sealed class ProductionRunExecutionSqlServerIssue120Tests : IAsyncLifetime
    {
        private const string Database = "CafeChain_Issue120Tests";

        private static string ConnectionString => SqlServerTestConnection.Create(Database);

        private static string MasterConnectionString => SqlServerTestConnection.MasterConnectionString();

        // Use EnsureCreated HasData ids — no IDENTITY_INSERT needed
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
                    $"SQL Server integration environment unavailable for #120 concurrency tests. " +
                    $"Database={Database}. {ex.Message}",
                    ex);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SqlServer_SameProductionRun_ConcurrentExecute_MutatesOnce()
        {
            int preparedItemId;
            int recipeId;
            int productionRunId;

            await using (var seed = CreateContext())
            {
                preparedItemId = await SeedPreparedItemAsync(seed, "PI-SQL-120-A");
                recipeId = await SeedRecipeAsync(
                    seed,
                    code: "RCP-SQL-120-A",
                    preparedItemId: preparedItemId,
                    outputQty: 4.5m,
                    outputUnitId: UnitL,
                    inputIngredientQty: 500m);
                await PutStoreInPreparedItemModeAsync(seed);
                await EnsureIngredientStockAsync(seed, available: 5000m, reserved: 0m);
                await ClearCanonicalOutputsAsync(seed, preparedItemId);
                productionRunId = await SeedConfirmedRunAsync(seed, recipeId, runs: 1m);
            }

            await using var ctx1 = CreateContext();
            await using var ctx2 = CreateContext();
            var exec1 = CreateExecutionService(ctx1);
            var exec2 = CreateExecutionService(ctx2);

            var results = await Task.WhenAll(
                exec1.ExecuteAsync(productionRunId, StaffId, StoreId),
                exec2.ExecuteAsync(productionRunId, StaffId, StoreId));

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message + " " + r.ErrorCode));
            Assert.Contains(results, r => r.Data!.WasReplay);
            Assert.Contains(results, r => !r.Data!.WasReplay);

            await using var verify = CreateContext();
            var run = await verify.ProductionRuns.SingleAsync(r => r.ProductionRunId == productionRunId);
            Assert.Equal(ProductionRunStatus.Completed, run.Status);

            var ingredient = await verify.StoreInventories.SingleAsync(x =>
                x.StoreId == StoreId && x.IngredientId == IngredientId);
            // 5000 - 500 = 4500 once
            Assert.Equal(4500m, ingredient.AvailableQty);

            var output = await verify.StoreInventories.SingleAsync(x =>
                x.StoreId == StoreId
                && x.PreparedItemId == preparedItemId
                && x.RecipeId == null
                && x.BtpIdentityState == BtpIdentityState.Canonical);
            Assert.Equal(4500m, output.AvailableQty);

            var txCount = await verify.InventoryTransactions.CountAsync(t => t.ProductionRunId == productionRunId);
            Assert.Equal(2, txCount); // one OUT + one IN
            Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t =>
                t.ProductionRunId == productionRunId && t.Type == InventoryTransactionTypeEnum.PRODUCTION_OUT));
            Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t =>
                t.ProductionRunId == productionRunId && t.Type == InventoryTransactionTypeEnum.PRODUCTION_IN));
        }

        [Fact]
        public async Task SqlServer_TwoRuns_SameMissingCanonical_CreatesOneRow_SumsOutput()
        {
            // Two confirmed runs of the same active recipe (one active recipe per PreparedItem is enforced).
            // Both credit the same missing canonical output row concurrently.
            int preparedItemId;
            int recipeId;
            int runIdA;
            int runIdB;

            await using (var seed = CreateContext())
            {
                preparedItemId = await SeedPreparedItemAsync(seed, "PI-SQL-120-B");
                recipeId = await SeedRecipeAsync(
                    seed,
                    code: "RCP-SQL-120-B",
                    preparedItemId: preparedItemId,
                    outputQty: 4.5m,
                    outputUnitId: UnitL,
                    inputIngredientQty: 100m);
                await PutStoreInPreparedItemModeAsync(seed);
                await EnsureIngredientStockAsync(seed, available: 5000m, reserved: 0m);
                await ClearCanonicalOutputsAsync(seed, preparedItemId);
                runIdA = await SeedConfirmedRunAsync(seed, recipeId, runs: 1m);
                runIdB = await SeedConfirmedRunAsync(seed, recipeId, runs: 1m);
            }

            Assert.Equal(0, await CreateContext().StoreInventories.CountAsync(x =>
                x.StoreId == StoreId
                && x.PreparedItemId == preparedItemId
                && x.BtpIdentityState == BtpIdentityState.Canonical));

            await using var ctx1 = CreateContext();
            await using var ctx2 = CreateContext();
            var exec1 = CreateExecutionService(ctx1);
            var exec2 = CreateExecutionService(ctx2);

            var results = await Task.WhenAll(
                exec1.ExecuteAsync(runIdA, StaffId, StoreId),
                exec2.ExecuteAsync(runIdB, StaffId, StoreId));

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message + " " + r.ErrorCode));

            await using var verify = CreateContext();
            var canonical = await verify.StoreInventories
                .Where(x => x.StoreId == StoreId
                            && x.PreparedItemId == preparedItemId
                            && x.BtpIdentityState == BtpIdentityState.Canonical)
                .ToListAsync();
            Assert.Single(canonical);
            // 4.5 l × 2 runs = 9.0 l = 9000 ml
            Assert.Equal(9000m, canonical[0].AvailableQty);
            Assert.Null(canonical[0].RecipeId);

            Assert.Equal(ProductionRunStatus.Completed,
                (await verify.ProductionRuns.SingleAsync(r => r.ProductionRunId == runIdA)).Status);
            Assert.Equal(ProductionRunStatus.Completed,
                (await verify.ProductionRuns.SingleAsync(r => r.ProductionRunId == runIdB)).Status);

            Assert.Equal(4, await verify.InventoryTransactions.CountAsync(t =>
                t.ProductionRunId == runIdA || t.ProductionRunId == runIdB));

            // Input deducted once per run: 100g × 2 = 200g from 5000
            var ingredient = await verify.StoreInventories.SingleAsync(x =>
                x.StoreId == StoreId && x.IngredientId == IngredientId);
            Assert.Equal(4800m, ingredient.AvailableQty);
        }

        [Fact]
        public async Task SqlServer_V2ConcurrentAccept_PostsInventoryAndRestockOnce()
        {
            int productionRunId;
            int preparedItemId;
            await using (var seed = CreateContext())
            {
                preparedItemId = await SeedPreparedItemAsync(seed, "PI-SQL-120-V2");
                var recipeId = await SeedRecipeAsync(
                    seed,
                    "RCP-SQL-120-V2",
                    preparedItemId,
                    4.5m,
                    UnitL,
                    500m);
                await PutStoreInPreparedItemModeAsync(seed);
                await EnsureIngredientStockAsync(seed, 5_000m, 0m);
                await ClearCanonicalOutputsAsync(seed, preparedItemId);

                var now = DateTime.UtcNow;
                var restock = new RestockRequest
                {
                    StoreId = StoreId,
                    PreparedItemId = preparedItemId,
                    RequestedQuantity = 4_500m,
                    RequestedProcurementQuantity = 4_500m,
                    ProcurementUnitId = UnitMl,
                    Status = RestockRequestStatuses.Processing,
                    Priority = RestockRequestPriorities.High,
                    SourceType = RestockRequestSourceTypes.StockAlert,
                    SourcingStatus = RestockSourcingStatuses.FullyAllocated,
                    SourcingDecision = RestockSourcingDecisionTypes.Production,
                    CreatedByStaffId = StaffId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                seed.RestockRequests.Add(restock);
                var run = new ProductionRun
                {
                    StoreId = StoreId,
                    RecipeId = recipeId,
                    RequestedRunCount = 1m,
                    ContractVersion = 2,
                    PlannedBatchCount = 1,
                    ExpectedOutputPerBatchBase = 4_500m,
                    ExpectedOutputBase = 4_500m,
                    OutputBaseUnitId = UnitMl,
                    YieldVarianceTolerancePercent = 5m,
                    RequestKey = Guid.NewGuid(),
                    RequestFingerprint = new string('V', 64),
                    Status = ProductionRunStatus.AwaitingAcceptance,
                    ValuationStatus = ProductionValuationStatus.Pending,
                    CreatedByStaffId = StaffId,
                    CreatedAt = now,
                    ConfirmedAt = now,
                    ActualRecordedByStaffId = StaffId,
                    ActualRecordedAtUtc = now
                };
                seed.ProductionRuns.Add(run);
                await seed.SaveChangesAsync();
                productionRunId = run.ProductionRunId;

                seed.RestockSourcingAllocations.Add(new RestockSourcingAllocation
                {
                    RestockRequestId = restock.RestockRequestId,
                    DecisionType = RestockSourcingDecisionTypes.Production,
                    ProcurementQuantity = 4_500m,
                    ProcurementUnitId = UnitMl,
                    Status = RestockSourcingAllocationStatuses.Active,
                    SourceDocumentType = "PRODUCTION_RUN",
                    SourceDocumentId = run.ProductionRunId,
                    ProductionRunId = run.ProductionRunId,
                    CreatedByStaffId = StaffId,
                    CreatedAtUtc = now
                });
                seed.ProductionRunInputActuals.Add(new ProductionRunInputActual
                {
                    ProductionRunId = run.ProductionRunId,
                    IngredientId = IngredientId,
                    BaseUnitId = UnitGram,
                    PlannedBaseQuantity = 500m,
                    ActualBaseQuantity = 500m,
                    ConfirmedByStaffId = StaffId,
                    ConfirmedAtUtc = now
                });
                seed.ProductionRunOutputs.Add(new ProductionRunOutput
                {
                    ProductionRunId = run.ProductionRunId,
                    BaseUnitId = UnitMl,
                    ExpectedOutputBase = 4_500m,
                    ActualProducedBase = 4_500m,
                    AcceptedOutputBase = 4_500m,
                    RejectedOutputBase = 0m,
                    VariancePercent = 0m,
                    RecordedByStaffId = StaffId,
                    RecordedAtUtc = now
                });
                await seed.SaveChangesAsync();
            }

            await using var firstContext = CreateContext();
            await using var secondContext = CreateContext();
            var results = await Task.WhenAll(
                CreateAcceptanceService(firstContext).AcceptAsync(productionRunId, StaffId),
                CreateAcceptanceService(secondContext).AcceptAsync(productionRunId, StaffId));

            Assert.All(results, result => Assert.True(result.IsSuccess, result.Message + " " + result.ErrorCode));
            Assert.Single(results.Where(x => x.Data!.WasReplay));
            Assert.Single(results.Where(x => !x.Data!.WasReplay));

            await using var verify = CreateContext();
            Assert.Equal(ProductionRunStatus.Completed,
                await verify.ProductionRuns.Where(x => x.ProductionRunId == productionRunId).Select(x => x.Status).SingleAsync());
            Assert.Equal(2, await verify.InventoryTransactions.CountAsync(x => x.ProductionRunId == productionRunId));
            Assert.Single(await verify.RestockFulfillmentPostings
                .Where(x => x.SourceDocumentType == RestockFulfillmentDocumentTypes.ProductionRun
                    && x.SourceDocumentId == productionRunId)
                .ToListAsync());
            Assert.Single(await verify.InventoryCostLayers
                .Where(x => x.PreparedItemId == preparedItemId && x.SourceProductionRunId == productionRunId)
                .ToListAsync());
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new AppDbContext(options);
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

        private static ProductionRunAcceptanceService CreateAcceptanceService(AppDbContext context)
        {
            var permissions = new Mock<IAdminPermissionService>();
            permissions.Setup(x => x.HasPermissionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>()))
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
            var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
            var caps = new IInventoryWriterCapabilityProvider[] { new ProductionPreparedWriterCapabilityProvider() };
            var writer = new InventoryWriterModeService(context, physical, caps);
            return new ProductionRunAcceptanceService(
                context,
                permissions.Object,
                writer,
                new StoreInventoryWriteResolver(context, writer),
                new InventoryCostLayerConsumptionService(context),
                new RestockFulfillmentPostingService(context),
                caps,
                NullLogger<ProductionRunAcceptanceService>.Instance);
        }

        private static async Task PutStoreInPreparedItemModeAsync(AppDbContext context)
        {
            var cfg = await context.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId);
            cfg.WriterMode = InventoryWriterMode.PreparedItem;
            cfg.HasEverActivatedPreparedItem = true;
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


           // #132 cost evidence for successful execute paths
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

        private static async Task ClearCanonicalOutputsAsync(AppDbContext context, int preparedItemId)
        {
            var leftovers = await context.StoreInventories
                .Where(x => x.StoreId == StoreId && x.PreparedItemId == preparedItemId)
                .ToListAsync();
            if (leftovers.Count == 0)
                return;

            var invIds = leftovers.Select(x => x.StoreInventoryId).ToList();
            var txs = await context.InventoryTransactions
                .Where(t => invIds.Contains(t.StoreInventoryId))
                .ToListAsync();
            context.InventoryTransactions.RemoveRange(txs);
            context.StoreInventories.RemoveRange(leftovers);
            await context.SaveChangesAsync();
        }

        private static async Task<int> SeedPreparedItemAsync(AppDbContext context, string code)
        {
            var pi = new PreparedItem
            {
                Code = code,
                Name = "SQL Cold Brew " + code,
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
