using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>Issue #120 — PreparedItem production stock writer.</summary>
    public sealed class ProductionRunExecutionIssue120Tests : IntegrationTestBase
    {
        private const int StoreId = 12001;
        private const int StaffId = 12002;
        private const int RecipeId = 12003;
        private const int IngredientId = 12004;
        private const int PreparedItemId = 12005;
        // Seeded Unit ids (UnitConfiguration)
        private const int UnitGram = 1;
        private const int UnitMl = 3;
        private const int UnitL = 4;

        [Fact]
        public async Task ConfirmedRun_ExecutesOnce_CreditsNormalizedOutput_NotBatchCount()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.PreparedItem);
            var run = await ConfirmRunAsync(context, runs: 2m);
            var exec = CreateExecutionService(context);

            var first = await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(first.IsSuccess, first.Message);
            Assert.False(first.Data!.WasReplay);
            Assert.True(first.Data.StockApplied);
            Assert.Equal("COMPLETED", first.Data.Status);
            // OutputQuantity 4.5 l × 2 runs → 9000 ml
            Assert.Equal(9000m, first.Data.NormalizedOutputQuantity);

            var outputInv = await context.StoreInventories.SingleAsync(x =>
                x.StoreId == StoreId && x.PreparedItemId == PreparedItemId && x.RecipeId == null);
            Assert.Equal(9000m, outputInv.AvailableQty);
            Assert.Equal(BtpIdentityState.Canonical, outputInv.BtpIdentityState);
            Assert.Null(outputInv.RecipeId);

            var ingredientInv = await context.StoreInventories.SingleAsync(x =>
                x.StoreId == StoreId && x.IngredientId == IngredientId);
            // 500 g × 2 = 1000 g deducted from 5000 g
            Assert.Equal(4000m, ingredientInv.AvailableQty);

            Assert.Equal(2, await context.InventoryTransactions.CountAsync(t => t.ProductionRunId == run.ProductionRunId));
            Assert.Equal(ProductionRunStatus.Completed, (await context.ProductionRuns.SingleAsync()).Status);

            var replay = await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(replay.IsSuccess, replay.Message);
            Assert.True(replay.Data!.WasReplay);
            Assert.Equal(9000m, (await context.StoreInventories.SingleAsync(x =>
                x.StoreId == StoreId && x.PreparedItemId == PreparedItemId && x.RecipeId == null)).AvailableQty);
            Assert.Equal(2, await context.InventoryTransactions.CountAsync(t => t.ProductionRunId == run.ProductionRunId));
        }

        [Fact]
        public async Task ProductionRun_Create_AndExecute_UseSamePreparedItemModeContract()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.PreparedItem);
            var confirm = CreateConfirmService(context);
            var create = await confirm.CreateAndConfirmAsync(
                new CafeChain.Application.DTOs.Admin.Production.CreateAndConfirmProductionRunRequest
                {
                    RequestKey = Guid.NewGuid(),
                    StoreId = StoreId,
                    RecipeId = RecipeId,
                    RequestedRunCount = 1m
                },
                StaffId,
                StoreId);
            Assert.True(create.IsSuccess, create.Message);

            var exec = await CreateExecutionService(context).ExecuteAsync(create.Data!.ProductionRunId, StaffId, StoreId);
            Assert.True(exec.IsSuccess, exec.Message);
            Assert.True(exec.Data!.StockApplied);
            Assert.Equal(4500m, exec.Data.NormalizedOutputQuantity);
        }

        [Fact]
        public async Task ProductionRun_LegacyConfirmedBeforeCutover_CannotExecuteWhileLegacy()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.LegacyRecipe);
            var run = await ConfirmRunAsync(context, 1m);
            var result = await CreateExecutionService(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunExecutionFailureCodes.ModeLegacy, result.ErrorCode);
        }

        [Fact]
        public async Task ProductionRun_LegacyConfirmedBeforeCutover_ExecutesAfterPreparedItemActivation()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.LegacyRecipe);
            var run = await ConfirmRunAsync(context, 1m);

            var cfg = await context.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId);
            cfg.WriterMode = InventoryWriterMode.PreparedItem;
            cfg.HasEverActivatedPreparedItem = true;
            await context.SaveChangesAsync();

            var result = await CreateExecutionService(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(result.IsSuccess, result.Message);
            Assert.True(result.Data!.StockApplied);
        }

        [Fact]
        public async Task LegacyRecipeMode_RejectsExecution()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.LegacyRecipe);
            var run = await ConfirmRunAsync(context, runs: 1m);
            var exec = CreateExecutionService(context);

            var result = await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunExecutionFailureCodes.ModeLegacy, result.ErrorCode);
            Assert.Equal(ProductionRunStatus.Confirmed, (await context.ProductionRuns.SingleAsync()).Status);
            Assert.Equal(0, await context.InventoryTransactions.CountAsync());
        }

        [Fact]
        public async Task BlockedMode_RejectsExecution()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.Blocked);
            var run = await ConfirmRunAsync(context, runs: 1m);
            var exec = CreateExecutionService(context);

            var result = await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunExecutionFailureCodes.ModeBlocked, result.ErrorCode);
        }

        [Fact]
        public async Task InsufficientUsableStock_FailsWithoutMutation()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.PreparedItem, ingredientQty: 100m, reserved: 50m);
            var run = await ConfirmRunAsync(context, runs: 1m);
            // Need 500 g, usable = 100 - 50 = 50
            var exec = CreateExecutionService(context);

            var result = await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunExecutionFailureCodes.InsufficientStock, result.ErrorCode);
            Assert.Equal(100m, await context.StoreInventories.Where(x => x.IngredientId == IngredientId)
                .Select(x => x.AvailableQty).SingleAsync());
            Assert.Equal(0, await context.InventoryTransactions.CountAsync());
            Assert.Equal(ProductionRunStatus.Confirmed, (await context.ProductionRuns.SingleAsync()).Status);
        }

        [Fact]
        public async Task FourPointFiveLitres_OneRun_Credits4500Ml()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.PreparedItem);
            var run = await ConfirmRunAsync(context, runs: 1m);
            var exec = CreateExecutionService(context);

            var result = await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(4500m, result.Data!.NormalizedOutputQuantity);
        }

        [Fact]
        public async Task UnmappedChildRecipe_FailsClosed()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.PreparedItem);
            // Add detail with child recipe without PreparedItemId
            context.Recipes.Add(new Recipe
            {
                RecipeId = 12999,
                RecipeCode = "CHILD-U",
                Name = "Unmapped child",
                Active = true,
                Status = "Active",
                YieldPercentage = 100m
            });
            context.RecipeDetails.Add(new RecipeDetail
            {
                RecipeDetailId = 12990,
                RecipeId = RecipeId,
                ChildRecipeId = 12999,
                Quantity = 1m,
                UnitId = UnitMl
            });
            await context.SaveChangesAsync();

            var run = await ConfirmRunAsync(context, runs: 1m);
            var exec = CreateExecutionService(context);
            var result = await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunExecutionFailureCodes.UnmappedChildRecipe, result.ErrorCode);
        }

        [Fact]
        public async Task NoRecipeIdOnlyOutputRow()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.PreparedItem);
            var run = await ConfirmRunAsync(context, runs: 1m);
            await CreateExecutionService(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);

            Assert.False(await context.StoreInventories.AnyAsync(x =>
                x.StoreId == StoreId && x.RecipeId == RecipeId && x.PreparedItemId == null));
            Assert.True(await context.StoreInventories.AnyAsync(x =>
                x.StoreId == StoreId && x.PreparedItemId == PreparedItemId && x.RecipeId == null));
        }

        [Fact]
        public async Task DuplicateIngredientLines_AggregateAndOneLedgerOut()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.PreparedItem, ingredientQty: 5000m);
            // Second detail line same ingredient: 300g more → total 800g per run
            context.RecipeDetails.Add(new RecipeDetail
            {
                RecipeDetailId = 12011,
                RecipeId = RecipeId,
                IngredientId = IngredientId,
                Quantity = 300m,
                UnitId = UnitGram
            });
            await context.SaveChangesAsync();

            var run = await ConfirmRunAsync(context, runs: 1m);
            var result = await CreateExecutionService(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(result.IsSuccess, result.Message);

            Assert.Equal(4200m, await context.StoreInventories
                .Where(x => x.IngredientId == IngredientId).Select(x => x.AvailableQty).SingleAsync());

            var outs = await context.InventoryTransactions
                .Where(t => t.ProductionRunId == run.ProductionRunId
                            && t.Type == InventoryTransactionTypeEnum.PRODUCTION_OUT)
                .ToListAsync();
            Assert.Single(outs);
            Assert.Equal(800m, outs[0].Quantity);
        }

        [Fact]
        public async Task AggregateShortage_WhenEachLineFitsButSumExceeds_Fails()
        {
            using var context = CreateDbContext();
            // 600 available: line1 500 + line2 300 = 800 needed
            await SeedFullAsync(context, InventoryWriterMode.PreparedItem, ingredientQty: 600m);
            context.RecipeDetails.Add(new RecipeDetail
            {
                RecipeDetailId = 12012,
                RecipeId = RecipeId,
                IngredientId = IngredientId,
                Quantity = 300m,
                UnitId = UnitGram
            });
            await context.SaveChangesAsync();

            var run = await ConfirmRunAsync(context, runs: 1m);
            var result = await CreateExecutionService(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunExecutionFailureCodes.InsufficientStock, result.ErrorCode);
            Assert.Equal(600m, await context.StoreInventories
                .Where(x => x.IngredientId == IngredientId).Select(x => x.AvailableQty).SingleAsync());
            Assert.Equal(0, await context.InventoryTransactions.CountAsync());
        }

        [Fact]
        public async Task ReservedStock_Protected_PassAndFail()
        {
            using var context = CreateDbContext();
            // Available 100, Reserved 40 → usable 60
            await SeedFullAsync(context, InventoryWriterMode.PreparedItem, ingredientQty: 100m, reserved: 40m);
            // Override recipe detail to need 70g
            var detail = await context.RecipeDetails.SingleAsync(d => d.RecipeId == RecipeId);
            detail.Quantity = 70m;
            await context.SaveChangesAsync();

            var runFail = await ConfirmRunAsync(context, runs: 1m);
            var fail = await CreateExecutionService(context).ExecuteAsync(runFail.ProductionRunId, StaffId, StoreId);
            Assert.False(fail.IsSuccess);
            Assert.Equal(ProductionRunExecutionFailureCodes.InsufficientStock, fail.ErrorCode);
            Assert.Equal(40m, await context.StoreInventories.Where(x => x.IngredientId == IngredientId)
                .Select(x => x.ReservedQty).SingleAsync());

            detail.Quantity = 60m;
            await context.SaveChangesAsync();
            var runOk = await ConfirmRunAsync(context, runs: 1m);
            var ok = await CreateExecutionService(context).ExecuteAsync(runOk.ProductionRunId, StaffId, StoreId);
            Assert.True(ok.IsSuccess, ok.Message);
            var inv = await context.StoreInventories.SingleAsync(x => x.IngredientId == IngredientId);
            Assert.Equal(40m, inv.AvailableQty);
            Assert.Equal(40m, inv.ReservedQty);
        }

        [Fact]
        public async Task CapabilityProvider_DoesNotChangeWriterMode()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.LegacyRecipe);
            var before = await context.StoreInventoryWriterConfigurations
                .AsNoTracking().SingleAsync(x => x.StoreId == StoreId);

            var cap = new ProductionPreparedWriterCapabilityProvider().GetStatus();
            Assert.True(cap.Ready);
            Assert.Equal(InventoryWriterCapabilityIds.ProductionPreparedWriter, cap.CapabilityId);
            Assert.Equal(ProductionPreparedWriterCapabilityProvider.ContractVersion, cap.ContractVersion);

            var after = await context.StoreInventoryWriterConfigurations
                .AsNoTracking().SingleAsync(x => x.StoreId == StoreId);
            Assert.Equal(before.WriterMode, after.WriterMode);
            Assert.Equal(InventoryWriterMode.LegacyRecipe, after.WriterMode);
        }

        [Fact]
        public async Task SelfConsumption_SameInputOutputRow_Blocked()
        {
            using var context = CreateDbContext();
            await SeedFullAsync(context, InventoryWriterMode.PreparedItem);
            // Child recipe maps to same PI as parent output (Active=false to satisfy one-active-PI index).
            context.Recipes.Add(new Recipe
            {
                RecipeId = 12100,
                RecipeCode = "SELF",
                Name = "Self child",
                Active = false,
                Status = "Archived",
                YieldPercentage = 100m,
                PreparedItemId = PreparedItemId,
                OutputQuantity = 1m,
                OutputUnitId = UnitMl
            });
            // Replace ingredient detail with child of same PI
            var detail = await context.RecipeDetails.SingleAsync(d => d.RecipeId == RecipeId);
            context.RecipeDetails.Remove(detail);
            context.RecipeDetails.Add(new RecipeDetail
            {
                RecipeDetailId = 12101,
                RecipeId = RecipeId,
                ChildRecipeId = 12100,
                Quantity = 100m,
                UnitId = UnitMl
            });
            // Seed input inventory as same canonical PI row (will also be output)
            context.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                RecipeId = null,
                BtpIdentityState = BtpIdentityState.Canonical,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                QuantitySemanticsEvidenceReference = "seed",
                QuantitySemanticsReviewedAt = DateTime.UtcNow,
                QuantitySemanticsReviewedByAccountId = StaffId,
                AvailableQty = 10000m,
                ReservedQty = 0m,
                MinStockLevel = 0,
                MaxNegativeQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            await context.SaveChangesAsync();

            var run = await ConfirmRunAsync(context, runs: 1m);
            var result = await CreateExecutionService(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunExecutionFailureCodes.SelfConsumptionNotSupported, result.ErrorCode);
            Assert.Equal(0, await context.InventoryTransactions.CountAsync(t => t.ProductionRunId == run.ProductionRunId));
        }

        private static ProductionRunExecutionService CreateExecutionService(AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
            var caps = new IInventoryWriterCapabilityProvider[]
            {
                new ProductionPreparedWriterCapabilityProvider()
            };
            var writer = new InventoryWriterModeService(context, physical, caps);
            var resolver = new StoreInventoryWriteResolver(context, writer);
            var scope = new ScopeAuthorizationService(context);
            var cost = new InventoryCostLayerConsumptionService(context);
            return new ProductionRunExecutionService(
                context,
                scope,
                writer,
                resolver,
                physical,
                unit,
                cost,
                caps,
                NullLogger<ProductionRunExecutionService>.Instance);
        }

        private static ProductionRunService CreateConfirmService(AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
            var caps = new IInventoryWriterCapabilityProvider[]
            {
                new ProductionPreparedWriterCapabilityProvider()
            };
            var writer = new InventoryWriterModeService(context, physical, caps);
            return new ProductionRunService(
                context,
                new ScopeAuthorizationService(context),
                writer,
                caps,
                NullLogger<ProductionRunService>.Instance);
        }

        /// <summary>
        /// #131 — prefer real CreateAndConfirm when store is PreparedItem; fallback seed for Legacy-only scenarios.
        /// </summary>
        private static async Task<ProductionRun> ConfirmRunAsync(AppDbContext context, decimal runs)
        {
            var mode = await context.StoreInventoryWriterConfigurations
                .AsNoTracking()
                .Where(c => c.StoreId == StoreId)
                .Select(c => c.WriterMode)
                .FirstOrDefaultAsync();

            if (mode == InventoryWriterMode.PreparedItem)
            {
                var svc = CreateConfirmService(context);
                var result = await svc.CreateAndConfirmAsync(
                    new CafeChain.Application.DTOs.Admin.Production.CreateAndConfirmProductionRunRequest
                    {
                        RequestKey = Guid.NewGuid(),
                        StoreId = StoreId,
                        RecipeId = RecipeId,
                        RequestedRunCount = runs
                    },
                    StaffId,
                    StoreId);
                if (!result.IsSuccess)
                    throw new InvalidOperationException($"CreateAndConfirm failed: {result.ErrorCode} {result.Message}");

                return await context.ProductionRuns.SingleAsync(r => r.ProductionRunId == result.Data!.ProductionRunId);
            }

            // Legacy seed path for tests that assert Execute rejects Legacy mode.
            var now = DateTime.UtcNow;
            var run = new ProductionRun
            {
                StoreId = StoreId,
                RecipeId = RecipeId,
                RequestedRunCount = runs,
                RequestKey = Guid.NewGuid(),
                RequestFingerprint = ProductionRunService.BuildFingerprint(StoreId, RecipeId, runs),
                Status = ProductionRunStatus.Confirmed,
                CreatedByStaffId = StaffId,
                CreatedAt = now,
                ConfirmedAt = now
            };
            context.ProductionRuns.Add(run);
            await context.SaveChangesAsync();
            return run;
        }

        private static async Task SeedFullAsync(
            AppDbContext context,
            InventoryWriterMode mode,
            decimal ingredientQty = 5000m,
            decimal reserved = 0m)
        {
            var now = DateTime.UtcNow;
            context.Stores.Add(new Store
            {
                StoreId = StoreId,
                Name = "Store 120",
                Address = "A",
                Phone = "1",
                Active = true,
                CreatedAt = now
            });
            context.StoreInventoryWriterConfigurations.Add(new StoreInventoryWriterConfiguration
            {
                StoreId = StoreId,
                WriterMode = mode,
                HasEverActivatedPreparedItem = mode == InventoryWriterMode.PreparedItem,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = new byte[] { 0 }
            });
            context.Staffs.Add(new Staff
            {
                StaffId = StaffId,
                AccountId = StaffId,
                FullName = "Staff 120",
                StoreId = StoreId,
                Active = true,
                CreatedAt = now
            });
            context.Ingredients.Add(new Ingredient
            {
                IngredientId = IngredientId,
                Code = "ING-120",
                Name = "Coffee",
                BaseUnitId = UnitGram,
                Active = true
            });
            context.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = PreparedItemId,
                Code = "PI-120",
                Name = "Cold Brew",
                BaseUnitId = UnitMl,
                Active = true
            });
            context.Recipes.Add(new Recipe
            {
                RecipeId = RecipeId,
                RecipeCode = "RCP-120",
                Name = "Cold Brew v1",
                Active = true,
                Status = "Active",
                YieldPercentage = 100m,
                PreparedItemId = PreparedItemId,
                OutputQuantity = 4.5m,
                OutputUnitId = UnitL
            });
            context.RecipeDetails.Add(new RecipeDetail
            {
                RecipeDetailId = 12010,
                RecipeId = RecipeId,
                IngredientId = IngredientId,
                Quantity = 500m,
                UnitId = UnitGram
            });
            context.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                AvailableQty = ingredientQty,
                ReservedQty = reserved,
                LastUpdated = now,
                RowVersion = new byte[] { 0 }
            });

            // #132 — actual FIFO cost evidence for successful execute paths
            if (ingredientQty > 0)
            {
                context.InventoryCostLayers.Add(new CafeChain.Models.Inventories.Costing.InventoryCostLayer
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    PreparedItemId = null,
                    Quantity = ingredientQty,
                    RemainingQuantity = ingredientQty,
                    UnitCost = 10.00m,
                    CreatedAt = now
                });
            }

            await context.SaveChangesAsync();
        }
    }
}
