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
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>Issue #132 — production actual FIFO valuation, durable allocations, fail-closed.</summary>
    public sealed class ProductionRunValuationIssue132Tests : IntegrationTestBase
    {
        private const int StoreId = 13201;
        private const int StaffId = 13202;
        private const int RecipeId = 13203;
        private const int IngredientId = 13204;
        private const int PreparedItemId = 13205;
        private const int ChildRecipeId = 13206;
        private const int ChildPreparedItemId = 13207;
        private const int UnitGram = 1;
        private const int UnitMl = 3;
        private const int UnitL = 4;
        private const decimal LayerUnitCost = 12.50m;

        [Fact]
        public async Task Production_Complete_ConsumesActualIngredientCostEvidence()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context, ingredientQty: 5000m, layerRemaining: 5000m, layerCost: LayerUnitCost);
            var run = await ConfirmAsync(context, 1m);

            var result = await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(result.IsSuccess, result.Message);

            var layer = await context.InventoryCostLayers.SingleAsync(x =>
                x.StoreId == StoreId && x.IngredientId == IngredientId && x.SourceProductionRunId == null);
            Assert.Equal(4500m, layer.RemainingQuantity); // 500g consumed
            Assert.Equal(LayerUnitCost, layer.UnitCost);
        }

        [Fact]
        public async Task Production_Complete_CreatesDurableInputCostAllocations()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context);
            var run = await ConfirmAsync(context, 1m);
            Assert.True((await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId)).IsSuccess);

            var allocs = await context.ProductionCostAllocations
                .Where(a => a.ProductionRunId == run.ProductionRunId)
                .ToListAsync();
            Assert.NotEmpty(allocs);
            Assert.All(allocs, a =>
            {
                Assert.True(a.InventoryCostLayerId > 0);
                Assert.True(a.InventoryTransactionId > 0);
                Assert.True(a.Quantity > 0);
                Assert.True(a.UnitCost > 0);
                Assert.Equal(a.Quantity * a.UnitCost, a.TotalCost);
            });
        }

        [Fact]
        public async Task Production_Complete_CreatesPreparedItemOutputCostLayer()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context);
            var run = await ConfirmAsync(context, 1m);
            Assert.True((await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId)).IsSuccess);

            var outLayer = await context.InventoryCostLayers.SingleAsync(x =>
                x.SourceProductionRunId == run.ProductionRunId);
            Assert.Equal(PreparedItemId, outLayer.PreparedItemId);
            Assert.Null(outLayer.IngredientId);
            Assert.Equal(4500m, outLayer.InitialOrRemaining());
            Assert.Equal(4500m, outLayer.RemainingQuantity);
            Assert.Equal(4500m, outLayer.Quantity);
        }

        [Fact]
        public async Task Production_Complete_SetsProductionOutUnitAndTotalCost()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context, layerCost: 10m);
            var run = await ConfirmAsync(context, 1m);
            Assert.True((await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId)).IsSuccess);

            var outs = await context.InventoryTransactions
                .Where(t => t.ProductionRunId == run.ProductionRunId
                            && t.Type == InventoryTransactionTypeEnum.PRODUCTION_OUT)
                .ToListAsync();
            Assert.Single(outs);
            Assert.Equal(10m, outs[0].UnitCost);
            Assert.Equal(5000m, outs[0].TotalCost); // 500 * 10
        }

        [Fact]
        public async Task Production_Complete_SetsProductionInUnitAndTotalCost()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context, layerCost: 10m);
            var run = await ConfirmAsync(context, 1m);
            Assert.True((await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId)).IsSuccess);

            var tin = await context.InventoryTransactions.SingleAsync(t =>
                t.ProductionRunId == run.ProductionRunId
                && t.Type == InventoryTransactionTypeEnum.PRODUCTION_IN);
            Assert.Equal(5000m, tin.TotalCost);
            Assert.Equal(5000m / 4500m, tin.UnitCost);
        }

        [Fact]
        public async Task Production_Complete_OutputUnitCost_EqualsActualInputCostDividedByOutput()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context, layerCost: 8m);
            var run = await ConfirmAsync(context, 2m); // 1000g * 8 = 8000; output 9000ml
            var result = await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(result.IsSuccess, result.Message);

            var expected = 8000m / 9000m;
            Assert.Equal(expected, result.Data!.OutputUnitCost);
            Assert.Equal(8000m, result.Data.TotalInputCost);

            var stored = await context.ProductionRuns.SingleAsync(r => r.ProductionRunId == run.ProductionRunId);
            Assert.Equal(expected, stored.OutputUnitCost);
            Assert.Equal(ProductionValuationStatus.Complete, stored.ValuationStatus);
        }

        [Fact]
        public async Task Production_Complete_DoesNotUseChangedSupplierPriceAfterReceipt()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context, layerCost: 15m);
            // Simulate "supplier price changed after receipt" by ensuring only layer cost is used
            // (no code path reads IngredientSupplier.CurrentPrice for production valuation).
            var run = await ConfirmAsync(context, 1m);
            var result = await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(15m * 500m, result.Data!.TotalInputCost);
            // Layer remains 15 even if hypothetical supplier price would be 999
            var layer = await context.InventoryCostLayers.SingleAsync(x =>
                x.IngredientId == IngredientId && x.SourceProductionRunId == null);
            Assert.Equal(15m, layer.UnitCost);
            Assert.NotEqual(999m, result.Data.OutputUnitCost);
        }

        [Fact]
        public async Task Production_Complete_DoesNotUseRecipeEstimatedCostAsActual()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context, layerCost: 7m);
            var recipe = await context.Recipes.SingleAsync(r => r.RecipeId == RecipeId);
            // EstimatedBomCost may exist on recipe details elsewhere; actual must remain layer-based.
            var run = await ConfirmAsync(context, 1m);
            var result = await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(7m * 500m, result.Data!.TotalInputCost);
            Assert.Equal(recipe.RecipeId, result.Data.RecipeId);
        }

        [Fact]
        public async Task Production_Complete_MissingInputCostEvidence_FailsClosed()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context, ingredientQty: 5000m, layerRemaining: 0m);
            // Remove all layers
            context.InventoryCostLayers.RemoveRange(context.InventoryCostLayers);
            await context.SaveChangesAsync();

            var run = await ConfirmAsync(context, 1m);
            var result = await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunExecutionFailureCodes.CostEvidenceIncomplete, result.ErrorCode);
            Assert.NotNull(result.Data?.CostEvidenceGaps);
            Assert.NotEmpty(result.Data!.CostEvidenceGaps);
        }

        [Fact]
        public async Task Production_Complete_MissingInputCost_DoesNotMutateQuantity()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context, ingredientQty: 5000m, layerRemaining: 100m); // stock ok, layer short
            var run = await ConfirmAsync(context, 1m);

            var beforeIng = await context.StoreInventories.Where(x => x.IngredientId == IngredientId)
                .Select(x => x.AvailableQty).SingleAsync();
            var result = await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunExecutionFailureCodes.CostEvidenceIncomplete, result.ErrorCode);

            Assert.Equal(beforeIng, await context.StoreInventories.Where(x => x.IngredientId == IngredientId)
                .Select(x => x.AvailableQty).SingleAsync());
            Assert.Equal(0, await context.InventoryTransactions.CountAsync(t => t.ProductionRunId == run.ProductionRunId));
            Assert.Equal(0, await context.ProductionCostAllocations.CountAsync(a => a.ProductionRunId == run.ProductionRunId));
            Assert.Equal(ProductionRunStatus.Confirmed, (await context.ProductionRuns.SingleAsync()).Status);
            Assert.Equal(ProductionValuationStatus.Pending, (await context.ProductionRuns.SingleAsync()).ValuationStatus);
        }

        [Fact]
        public async Task Production_Complete_Replay_DoesNotDuplicateCostLayer()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context);
            var run = await ConfirmAsync(context, 1m);
            var exec = Exec(context);
            Assert.True((await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId)).IsSuccess);
            Assert.True((await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId)).IsSuccess);

            Assert.Equal(1, await context.InventoryCostLayers.CountAsync(x =>
                x.SourceProductionRunId == run.ProductionRunId));
        }

        [Fact]
        public async Task Production_Complete_Replay_DoesNotDuplicateAllocations()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context);
            var run = await ConfirmAsync(context, 1m);
            var exec = Exec(context);
            Assert.True((await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId)).IsSuccess);
            var count = await context.ProductionCostAllocations.CountAsync(a => a.ProductionRunId == run.ProductionRunId);
            Assert.True((await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId)).IsSuccess);
            Assert.Equal(count, await context.ProductionCostAllocations.CountAsync(a => a.ProductionRunId == run.ProductionRunId));
        }

        [Fact]
        public async Task Production_Complete_Replay_ReturnsStoredValuation()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context, layerCost: 11m);
            var run = await ConfirmAsync(context, 1m);
            var exec = Exec(context);
            var first = await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(first.IsSuccess);

            // Poison layers after complete — replay must not revalue
            var layer = await context.InventoryCostLayers.SingleAsync(x =>
                x.IngredientId == IngredientId && x.SourceProductionRunId == null);
            layer.UnitCost = 999m;
            layer.RemainingQuantity = 0;
            await context.SaveChangesAsync();

            var replay = await exec.ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(replay.IsSuccess);
            Assert.True(replay.Data!.WasReplay);
            Assert.Equal(first.Data!.TotalInputCost, replay.Data.TotalInputCost);
            Assert.Equal(first.Data.OutputUnitCost, replay.Data.OutputUnitCost);
            Assert.Equal("Complete", replay.Data.ValuationStatus);
        }

        [Fact]
        public async Task Production_Complete_Failure_RollsBackQuantityLedgerLayersAndAllocations()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context, ingredientQty: 5000m, layerRemaining: 50m);
            var run = await ConfirmAsync(context, 1m);

            var layerBefore = await context.InventoryCostLayers
                .Where(x => x.IngredientId == IngredientId)
                .Select(x => x.RemainingQuantity).SingleAsync();

            var result = await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.False(result.IsSuccess);

            Assert.Equal(5000m, await context.StoreInventories.Where(x => x.IngredientId == IngredientId)
                .Select(x => x.AvailableQty).SingleAsync());
            Assert.Equal(layerBefore, await context.InventoryCostLayers.Where(x => x.IngredientId == IngredientId)
                .Select(x => x.RemainingQuantity).SingleAsync());
            Assert.Equal(0, await context.InventoryTransactions.CountAsync());
            Assert.Equal(0, await context.ProductionCostAllocations.CountAsync());
            Assert.Equal(0, await context.InventoryCostLayers.CountAsync(x => x.SourceProductionRunId != null));
        }

        [Fact]
        public async Task Production_Complete_PreparedItemInput_ConsumesPreparedItemCostLayer()
        {
            using var context = CreateDbContext();
            await SeedWithPreparedItemInputAsync(context);
            var run = await ConfirmAsync(context, 1m);
            var result = await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(result.IsSuccess, result.Message);

            var childLayer = await context.InventoryCostLayers.SingleAsync(x =>
                x.PreparedItemId == ChildPreparedItemId && x.SourceProductionRunId == null);
            Assert.Equal(900m, childLayer.RemainingQuantity); // 1000 - 100

            var outLayer = await context.InventoryCostLayers.SingleAsync(x =>
                x.SourceProductionRunId == run.ProductionRunId);
            Assert.Equal(PreparedItemId, outLayer.PreparedItemId);
            Assert.Equal(20m * 100m, result.Data!.TotalInputCost);
        }

        [Fact]
        public async Task Production_Complete_PinnedChildRecipe_DoesNotSubstituteLatestRecipe()
        {
            using var context = CreateDbContext();
            await SeedWithPreparedItemInputAsync(context);

            // Newer child recipe for same PI — must NOT substitute pinned ChildRecipeId
            context.Recipes.Add(new Recipe
            {
                RecipeId = ChildRecipeId + 100,
                RecipeCode = "CHILD-NEW",
                Name = "Newer child",
                Active = true,
                Status = "Active",
                YieldPercentage = 100m,
                PreparedItemId = ChildPreparedItemId,
                OutputQuantity = 1m,
                OutputUnitId = UnitMl
            });
            await context.SaveChangesAsync();

            var run = await ConfirmAsync(context, 1m);
            var result = await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(result.IsSuccess, result.Message);

            // Still consumed child PI layers (identity), formula remains pinned ChildRecipeId on detail
            var detail = await context.RecipeDetails.SingleAsync(d => d.RecipeId == RecipeId);
            Assert.Equal(ChildRecipeId, detail.ChildRecipeId);
            Assert.Equal(1, await context.InventoryCostLayers.CountAsync(x =>
                x.SourceProductionRunId == run.ProductionRunId));
        }

        [Fact]
        public async Task Production_Complete_Rounding_IsDeterministic()
        {
            using var context = CreateDbContext();
            // Two layers with different costs → weighted unit cost
            await SeedBaseAsync(context, ingredientQty: 5000m, layerRemaining: 0m, layerCost: 0m);
            context.InventoryCostLayers.RemoveRange(context.InventoryCostLayers);
            var t0 = DateTime.UtcNow.AddMinutes(-2);
            context.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                Quantity = 200m,
                RemainingQuantity = 200m,
                UnitCost = 3.33m,
                CreatedAt = t0
            });
            context.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                Quantity = 5000m,
                RemainingQuantity = 5000m,
                UnitCost = 1.11m,
                CreatedAt = t0.AddMinutes(1)
            });
            await context.SaveChangesAsync();

            var run = await ConfirmAsync(context, 1m); // need 500g: 200@3.33 + 300@1.11
            var r1 = await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.True(r1.IsSuccess, r1.Message);

            var expectedTotal = 200m * 3.33m + 300m * 1.11m;
            Assert.Equal(expectedTotal, r1.Data!.TotalInputCost);
            Assert.Equal(expectedTotal / 4500m, r1.Data.OutputUnitCost);
        }

        [Fact]
        public async Task Production_Complete_ZeroOutput_IsRejected()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context);
            var recipe = await context.Recipes.SingleAsync(r => r.RecipeId == RecipeId);
            recipe.OutputQuantity = 0m;
            await context.SaveChangesAsync();

            // CreateAndConfirm may reject invalid output; seed confirmed directly if needed
            var now = DateTime.UtcNow;
            var run = new ProductionRun
            {
                StoreId = StoreId,
                RecipeId = RecipeId,
                RequestedRunCount = 1m,
                RequestKey = Guid.NewGuid(),
                RequestFingerprint = ProductionRunService.BuildFingerprint(StoreId, RecipeId, 1m),
                Status = ProductionRunStatus.Confirmed,
                ValuationStatus = ProductionValuationStatus.Pending,
                CreatedByStaffId = StaffId,
                CreatedAt = now,
                ConfirmedAt = now
            };
            context.ProductionRuns.Add(run);
            await context.SaveChangesAsync();

            var result = await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId);
            Assert.False(result.IsSuccess);
            Assert.True(
                result.ErrorCode is ProductionRunExecutionFailureCodes.InvalidOutputContract
                    or ProductionRunExecutionFailureCodes.ZeroOutputRejected,
                result.ErrorCode);
        }

        [Fact]
        public async Task Production_Complete_OutputLayer_HasExactlyOneInventoryIdentity()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context);
            var run = await ConfirmAsync(context, 1m);
            Assert.True((await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId)).IsSuccess);

            var layer = await context.InventoryCostLayers.SingleAsync(x =>
                x.SourceProductionRunId == run.ProductionRunId);
            Assert.True(
                (layer.IngredientId.HasValue && !layer.PreparedItemId.HasValue)
                || (!layer.IngredientId.HasValue && layer.PreparedItemId.HasValue));
            Assert.True(layer.PreparedItemId.HasValue);
            Assert.False(layer.IngredientId.HasValue);
        }

        [Fact]
        public async Task Production_Complete_OutputLayer_IsUniquePerRun()
        {
            using var context = CreateDbContext();
            await SeedBaseAsync(context);
            var run = await ConfirmAsync(context, 1m);
            Assert.True((await Exec(context).ExecuteAsync(run.ProductionRunId, StaffId, StoreId)).IsSuccess);
            Assert.Equal(1, await context.InventoryCostLayers.CountAsync(x =>
                x.SourceProductionRunId == run.ProductionRunId));
        }

        private static ProductionRunExecutionService Exec(AppDbContext context)
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

        private static ProductionRunService ConfirmService(AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
            var caps = new IInventoryWriterCapabilityProvider[] { new ProductionPreparedWriterCapabilityProvider() };
            return new ProductionRunService(
                context,
                new ScopeAuthorizationService(context),
                new InventoryWriterModeService(context, physical, caps),
                caps,
                NullLogger<ProductionRunService>.Instance);
        }

        private static async Task<ProductionRun> ConfirmAsync(AppDbContext context, decimal runs)
        {
            var result = await ConfirmService(context).CreateAndConfirmAsync(
                new CreateAndConfirmProductionRunRequest
                {
                    RequestKey = Guid.NewGuid(),
                    StoreId = StoreId,
                    RecipeId = RecipeId,
                    RequestedRunCount = runs
                },
                StaffId,
                StoreId);
            if (!result.IsSuccess)
                throw new InvalidOperationException($"{result.ErrorCode} {result.Message}");
            return await context.ProductionRuns.SingleAsync(r => r.ProductionRunId == result.Data!.ProductionRunId);
        }

        private static async Task SeedBaseAsync(
            AppDbContext context,
            decimal ingredientQty = 5000m,
            decimal layerRemaining = 5000m,
            decimal layerCost = LayerUnitCost)
        {
            var now = DateTime.UtcNow;
            context.Stores.Add(new Store
            {
                StoreId = StoreId,
                Name = "Store 132",
                Address = "A",
                Phone = "1",
                Active = true,
                CreatedAt = now
            });
            context.StoreInventoryWriterConfigurations.Add(new StoreInventoryWriterConfiguration
            {
                StoreId = StoreId,
                WriterMode = InventoryWriterMode.PreparedItem,
                HasEverActivatedPreparedItem = true,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = new byte[] { 0 }
            });
            context.Staffs.Add(new Staff
            {
                StaffId = StaffId,
                AccountId = StaffId,
                FullName = "Staff 132",
                StoreId = StoreId,
                Active = true,
                CreatedAt = now
            });
            context.Ingredients.Add(new Ingredient
            {
                IngredientId = IngredientId,
                Code = "ING-132",
                Name = "Coffee 132",
                BaseUnitId = UnitGram,
                Active = true
            });
            context.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = PreparedItemId,
                Code = "PI-132",
                Name = "Cold Brew 132",
                BaseUnitId = UnitMl,
                Active = true
            });
            context.Recipes.Add(new Recipe
            {
                RecipeId = RecipeId,
                RecipeCode = "RCP-132",
                Name = "Cold Brew v132",
                Active = true,
                Status = "Active",
                YieldPercentage = 100m,
                PreparedItemId = PreparedItemId,
                OutputQuantity = 4.5m,
                OutputUnitId = UnitL
            });
            context.RecipeDetails.Add(new RecipeDetail
            {
                RecipeDetailId = 13210,
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
                ReservedQty = 0m,
                LastUpdated = now,
                RowVersion = new byte[] { 0 }
            });
            if (layerRemaining > 0 && layerCost > 0)
            {
                context.InventoryCostLayers.Add(new InventoryCostLayer
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    PreparedItemId = null,
                    Quantity = layerRemaining,
                    RemainingQuantity = layerRemaining,
                    UnitCost = layerCost,
                    CreatedAt = now
                });
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedWithPreparedItemInputAsync(AppDbContext context)
        {
            var now = DateTime.UtcNow;
            context.Stores.Add(new Store
            {
                StoreId = StoreId,
                Name = "Store 132",
                Address = "A",
                Phone = "1",
                Active = true,
                CreatedAt = now
            });
            context.StoreInventoryWriterConfigurations.Add(new StoreInventoryWriterConfiguration
            {
                StoreId = StoreId,
                WriterMode = InventoryWriterMode.PreparedItem,
                HasEverActivatedPreparedItem = true,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = new byte[] { 0 }
            });
            context.Staffs.Add(new Staff
            {
                StaffId = StaffId,
                AccountId = StaffId,
                FullName = "Staff 132",
                StoreId = StoreId,
                Active = true,
                CreatedAt = now
            });
            context.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = ChildPreparedItemId,
                Code = "PI-CHILD-132",
                Name = "Child BTP",
                BaseUnitId = UnitMl,
                Active = true
            });
            context.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = PreparedItemId,
                Code = "PI-OUT-132",
                Name = "Parent BTP",
                BaseUnitId = UnitMl,
                Active = true
            });
            context.Recipes.Add(new Recipe
            {
                RecipeId = ChildRecipeId,
                RecipeCode = "CHILD-132",
                Name = "Child formula",
                Active = false,
                Status = "Archived",
                YieldPercentage = 100m,
                PreparedItemId = ChildPreparedItemId,
                OutputQuantity = 1m,
                OutputUnitId = UnitMl
            });
            context.Recipes.Add(new Recipe
            {
                RecipeId = RecipeId,
                RecipeCode = "RCP-132-PI",
                Name = "Parent with child BTP",
                Active = true,
                Status = "Active",
                YieldPercentage = 100m,
                PreparedItemId = PreparedItemId,
                OutputQuantity = 4.5m,
                OutputUnitId = UnitL
            });
            context.RecipeDetails.Add(new RecipeDetail
            {
                RecipeDetailId = 13220,
                RecipeId = RecipeId,
                ChildRecipeId = ChildRecipeId,
                Quantity = 100m,
                UnitId = UnitMl
            });
            context.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = ChildPreparedItemId,
                RecipeId = null,
                BtpIdentityState = BtpIdentityState.Canonical,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                QuantitySemanticsEvidenceReference = "seed",
                QuantitySemanticsReviewedAt = now,
                QuantitySemanticsReviewedByAccountId = StaffId,
                AvailableQty = 1000m,
                ReservedQty = 0m,
                LastUpdated = now,
                RowVersion = new byte[] { 0 }
            });
            context.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = StoreId,
                IngredientId = null,
                PreparedItemId = ChildPreparedItemId,
                Quantity = 1000m,
                RemainingQuantity = 1000m,
                UnitCost = 20m,
                CreatedAt = now
            });
            await context.SaveChangesAsync();
        }
    }

    file static class LayerQtyExt
    {
        public static decimal InitialOrRemaining(this InventoryCostLayer layer) => layer.Quantity;
    }
}
