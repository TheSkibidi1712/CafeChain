using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Drinks;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    public sealed class PreparedItemInventoryWriteFoundationIssue118Tests : IntegrationTestBase
    {
        private const int StoreId = 8118;
        private const int OtherStoreId = 8119;
        private const int RecipeId = 8120;
        private const int PreparedItemId = 8121;
        private const int IngredientId = 8122;
        private const int UnitId = 8123;

        [Fact]
        public void Model_HasWriterConfigurationLifecycleAndCanonicalIndex()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CafeChainIssue118ModelOnly;Trusted_Connection=True;")
                .Options;
            using var context = new AppDbContext(options);
            var model = context.GetService<IDesignTimeModel>().Model;

            var configuration = model.FindEntityType(typeof(StoreInventoryWriterConfiguration))!;
            Assert.NotNull(configuration.FindProperty(nameof(StoreInventoryWriterConfiguration.RowVersion)));
            Assert.Equal(typeof(int), configuration.FindProperty(nameof(StoreInventoryWriterConfiguration.WriterMode))!.GetProviderClrType());

            var inventory = model.FindEntityType(typeof(StoreInventory))!;
            Assert.Contains(inventory.GetCheckConstraints(), x => x.Name == "CK_StoreInventories_BtpLifecycle");
            Assert.Contains(inventory.GetCheckConstraints(), x => x.Name == "CK_StoreInventories_NotSelfSuperseded");
            var canonical = inventory.GetIndexes().Single(x => x.GetDatabaseName() == "UX_Store_PreparedItem_Canonical");
            Assert.True(canonical.IsUnique);
            Assert.Contains("[BtpIdentityState] = 1", canonical.GetFilter());
            var compatibility = inventory.GetIndexes()
                .Single(x => x.GetDatabaseName() == "IX_Store_PreparedItem_Compatibility");
            Assert.False(compatibility.IsUnique);
        }

        [Fact]
        public async Task ExistingStoresDefaultToLegacyAndNewStoreCanPersistConfigurationAtomically()
        {
            using var context = CreateDbContext();
            var seeded = await context.StoreInventoryWriterConfigurations.AsNoTracking().SingleAsync(x => x.StoreId == 1);
            Assert.Equal(InventoryWriterMode.LegacyRecipe, seeded.WriterMode);

            await using (var transaction = await context.Database.BeginTransactionAsync())
            {
                var store = NewStore(StoreId);
                store.InventoryWriterConfiguration = NewConfiguration(StoreId);
                context.Stores.Add(store);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            Assert.True(await context.Stores.AnyAsync(x => x.StoreId == StoreId));
            Assert.True(await context.StoreInventoryWriterConfigurations.AnyAsync(x =>
                x.StoreId == StoreId && x.WriterMode == InventoryWriterMode.LegacyRecipe));
        }

        [Fact]
        public async Task MissingConfigurationFailsClosed()
        {
            using var context = CreateDbContext();
            context.Stores.Add(NewStore(StoreId));
            await context.SaveChangesAsync();
            var service = CreateModeService(context);

            await using var transaction = await context.Database.BeginTransactionAsync();
            var result = await service.AcquireSnapshotAsync(StoreId);

            Assert.False(result.IsSuccess);
            Assert.Equal(InventoryWriterFailureCodes.MissingConfiguration, result.ErrorCode);
        }

        [Fact]
        public async Task SnapshotIsBoundToStoreAndTransactionAndReadsModeOnce()
        {
            using var context = CreateDbContext();
            await AddStoreAsync(context, StoreId, InventoryWriterMode.LegacyRecipe);
            var service = CreateModeService(context);
            InventoryWriterModeSnapshot snapshot;

            await using (var transaction = await context.Database.BeginTransactionAsync())
            {
                snapshot = (await service.AcquireSnapshotAsync(StoreId)).Data!;
                Assert.True(service.IsSnapshotValidForCurrentTransaction(snapshot, StoreId));
                Assert.False(service.IsSnapshotValidForCurrentTransaction(snapshot, OtherStoreId));
                Assert.True(service.EnsureLegacyBtpWriteAllowed(snapshot, StoreId).IsSuccess);
                await transaction.CommitAsync();
            }

            Assert.False(service.IsSnapshotValidForCurrentTransaction(snapshot, StoreId));
        }

        [Theory]
        [InlineData(InventoryWriterMode.Blocked, InventoryWriterFailureCodes.ModeBlocked)]
        [InlineData(InventoryWriterMode.PreparedItem, InventoryWriterFailureCodes.LegacyWriterForbidden)]
        public async Task LegacyBtpGuardRejectsNonLegacyModes(
            InventoryWriterMode mode,
            string expectedCode)
        {
            using var context = CreateDbContext();
            await AddStoreAsync(context, StoreId, mode, hasEverActivated: mode == InventoryWriterMode.PreparedItem);
            var service = CreateModeService(context);

            await using var transaction = await context.Database.BeginTransactionAsync();
            var snapshot = (await service.AcquireSnapshotAsync(StoreId)).Data!;
            var guard = service.EnsureLegacyBtpWriteAllowed(snapshot, StoreId);

            Assert.False(guard.IsSuccess);
            Assert.Equal(expectedCode, guard.ErrorCode);
        }

        [Fact]
        public async Task IngredientResolutionRemainsAvailableWhileBtpIsBlocked()
        {
            using var context = CreateDbContext();
            await AddStoreAsync(context, StoreId, InventoryWriterMode.Blocked);
            context.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                AvailableQty = 4m,
                ReservedQty = 0m,
                LastUpdated = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            var modeService = CreateModeService(context);
            var resolver = new StoreInventoryWriteResolver(context, modeService);

            await using var transaction = await context.Database.BeginTransactionAsync();
            var snapshot = (await modeService.AcquireSnapshotAsync(StoreId)).Data!;
            var ingredient = await resolver.ResolveAsync(new StoreInventoryWriteRequest
            {
                ModeSnapshot = snapshot,
                StoreId = StoreId,
                IdentityType = InventoryWriteIdentityTypes.Ingredient,
                IngredientId = IngredientId
            });
            var btp = await resolver.ResolveAsync(new StoreInventoryWriteRequest
            {
                ModeSnapshot = snapshot,
                StoreId = StoreId,
                IdentityType = InventoryWriteIdentityTypes.LegacyRecipe,
                RecipeId = RecipeId
            });

            Assert.Equal(InventoryWriteResolutionStatuses.FoundCanonical, ingredient.Status);
            Assert.Equal(InventoryWriteResolutionStatuses.BlockedMode, btp.Status);
        }

        [Fact]
        public async Task PreparedActivationIsBlockedByMissingCapabilitiesAndHashIsDeterministic()
        {
            using var context = CreateDbContext();
            await AddStoreAsync(context, StoreId, InventoryWriterMode.LegacyRecipe);
            var service = CreateModeService(context);
            var first = await service.EvaluateReadinessAsync(StoreId);
            var second = await service.EvaluateReadinessAsync(StoreId);
            var status = (await service.GetStatusAsync(StoreId)).Data!;

            var result = await service.TransitionAsync(new InventoryWriterModeTransitionRequest
            {
                StoreId = StoreId,
                ExpectedCurrentMode = InventoryWriterMode.LegacyRecipe,
                ExpectedRowVersion = status.RowVersion,
                TargetMode = InventoryWriterMode.PreparedItem,
                ReadinessHash = first.ReadinessHash,
                Reason = "Issue #118 readiness test",
                ActorAccountId = 1
            });

            Assert.False(first.Ready);
            Assert.Equal(first.ReadinessHash, second.ReadinessHash);
            Assert.Contains(first.Blockers, x => x.Code.Contains(InventoryWriterCapabilityIds.PosPreparedWriter));
            Assert.False(result.Succeeded);
            Assert.Equal(InventoryWriterFailureCodes.ReadinessFailed, result.FailureCode);
        }

        [Fact]
        public async Task LegacyBlockedLegacyTransitionWorksBeforeFirstActivationAndWritesAppendOnlyAudit()
        {
            using var context = CreateDbContext();
            await AddStoreAsync(context, StoreId, InventoryWriterMode.LegacyRecipe);
            var service = CreateModeService(context);

            var status = (await service.GetStatusAsync(StoreId)).Data!;
            var blocked = await service.TransitionAsync(Request(status, InventoryWriterMode.Blocked));
            Assert.True(blocked.Succeeded);

            status = (await service.GetStatusAsync(StoreId)).Data!;
            var legacy = await service.TransitionAsync(Request(status, InventoryWriterMode.LegacyRecipe));
            Assert.True(legacy.Succeeded);

            var audits = await context.InventoryWriterModeTransitions
                .AsNoTracking()
                .Where(x => x.StoreId == StoreId)
                .OrderBy(x => x.TransitionId)
                .ToListAsync();
            Assert.Equal(2, audits.Count);
            Assert.All(audits, x => Assert.True(x.Succeeded));
        }

        [Fact]
        public async Task BlockedCannotReturnToLegacyAfterPreparedActivationAndPreparedNeverReturnsDirectly()
        {
            using var context = CreateDbContext();
            await AddStoreAsync(context, StoreId, InventoryWriterMode.Blocked, hasEverActivated: true);
            var service = CreateModeService(context);
            var status = (await service.GetStatusAsync(StoreId)).Data!;
            var blockedToLegacy = await service.TransitionAsync(Request(status, InventoryWriterMode.LegacyRecipe));
            Assert.False(blockedToLegacy.Succeeded);

            var configuration = await context.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId);
            configuration.WriterMode = InventoryWriterMode.PreparedItem;
            await context.SaveChangesAsync();
            status = (await service.GetStatusAsync(StoreId)).Data!;
            var preparedToLegacy = await service.TransitionAsync(Request(status, InventoryWriterMode.LegacyRecipe));
            Assert.False(preparedToLegacy.Succeeded);
            Assert.Equal(InventoryWriterFailureCodes.InvalidTransition, preparedToLegacy.FailureCode);
        }

        [Fact]
        public async Task StaleRowVersionIsRejectedAndFailureIsAudited()
        {
            using var context = CreateDbContext();
            await AddStoreAsync(context, StoreId, InventoryWriterMode.LegacyRecipe);
            var service = CreateModeService(context);
            var status = (await service.GetStatusAsync(StoreId)).Data!;

            var result = await service.TransitionAsync(new InventoryWriterModeTransitionRequest
            {
                StoreId = StoreId,
                ExpectedCurrentMode = status.WriterMode,
                ExpectedRowVersion = new byte[] { 9, 9, 9 },
                TargetMode = InventoryWriterMode.Blocked,
                Reason = "stale test",
                ActorAccountId = 1
            });

            Assert.False(result.Succeeded);
            Assert.Equal(InventoryWriterFailureCodes.StaleConfiguration, result.FailureCode);
            Assert.True(await context.InventoryWriterModeTransitions.AnyAsync(x =>
                x.StoreId == StoreId && !x.Succeeded && x.FailureCode == InventoryWriterFailureCodes.StaleConfiguration));
        }

        [Fact]
        public async Task ResolverIsReadOnlyAndLegacyModeOnlyUsesRecipeIdentity()
        {
            using var context = CreateDbContext();
            await AddStoreAsync(context, StoreId, InventoryWriterMode.LegacyRecipe);
            var modeService = CreateModeService(context);
            var resolver = new StoreInventoryWriteResolver(context, modeService);

            await using var transaction = await context.Database.BeginTransactionAsync();
            var snapshot = (await modeService.AcquireSnapshotAsync(StoreId)).Data!;
            var before = await context.StoreInventories.CountAsync();
            var resolution = await resolver.ResolveAsync(new StoreInventoryWriteRequest
            {
                ModeSnapshot = snapshot,
                StoreId = StoreId,
                IdentityType = InventoryWriteIdentityTypes.LegacyRecipe,
                RecipeId = RecipeId,
                AllowCreateIntent = true
            });

            Assert.Equal(InventoryWriteResolutionStatuses.CreateAllowed, resolution.Status);
            Assert.Equal(before, await context.StoreInventories.CountAsync());
        }

        [Fact]
        public async Task PreparedResolverRejectsUnknownCollisionAndSupersededRows()
        {
            using var context = CreateDbContext();
            await SeedPreparedItemAsync(context);
            await AddStoreAsync(context, StoreId, InventoryWriterMode.PreparedItem, hasEverActivated: true);
            var unconfirmed = NewBtpRow(BtpIdentityState.Canonical, InventoryQuantitySemanticsStatus.Unknown);
            context.StoreInventories.Add(unconfirmed);
            await context.SaveChangesAsync();
            var modeService = CreateModeService(context);
            var resolver = new StoreInventoryWriteResolver(context, modeService);

            await using var transaction = await context.Database.BeginTransactionAsync();
            var snapshot = (await modeService.AcquireSnapshotAsync(StoreId)).Data!;
            var unknown = await resolver.ResolveAsync(PreparedRequest(snapshot));
            Assert.Equal(InventoryWriteResolutionStatuses.UnknownQuantitySemantics, unknown.Status);

            unconfirmed.BtpIdentityState = BtpIdentityState.Superseded;
            unconfirmed.SupersededByStoreInventoryId = 99999;
            await context.SaveChangesAsync();
            var superseded = await resolver.ResolveAsync(PreparedRequest(snapshot));
            Assert.Equal(InventoryWriteResolutionStatuses.Superseded, superseded.Status);
        }

        [Fact]
        public async Task BlockedMixedPosDeductionRollsBackBeforeIngredientOrBtpMutation()
        {
            using var context = CreateDbContext();
            await SeedPreparedItemAsync(context);
            await AddStoreAsync(context, StoreId, InventoryWriterMode.Blocked);
            context.Ingredients.Add(new Ingredient
            {
                IngredientId = IngredientId,
                Code = "ING-118",
                Name = "Ingredient 118",
                BaseUnitId = UnitId,
                Active = true
            });
            context.Recipes.AddRange(
                new Recipe
                {
                    RecipeId = RecipeId,
                    RecipeCode = "SALE-118",
                    Name = "Sale 118",
                    DrinkId = 8117,
                    Active = true,
                    Status = "Active"
                },
                new Recipe
                {
                    RecipeId = RecipeId + 1,
                    RecipeCode = "CHILD-118",
                    Name = "Child 118",
                    Active = true,
                    Status = "Active"
                });
            context.RecipeDetails.AddRange(
                new RecipeDetail
                {
                    RecipeDetailId = 81181,
                    RecipeId = RecipeId,
                    IngredientId = IngredientId,
                    Quantity = 1m,
                    UnitId = UnitId
                },
                new RecipeDetail
                {
                    RecipeDetailId = 81182,
                    RecipeId = RecipeId,
                    ChildRecipeId = RecipeId + 1,
                    Quantity = 1m,
                    UnitId = UnitId
                });
            context.StoreInventories.AddRange(
                new StoreInventory
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    AvailableQty = 10m,
                    ReservedQty = 0m,
                    LastUpdated = DateTime.UtcNow
                },
                new StoreInventory
                {
                    StoreId = StoreId,
                    RecipeId = RecipeId + 1,
                    BtpIdentityState = BtpIdentityState.Legacy,
                    QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.Unknown,
                    AvailableQty = 10m,
                    ReservedQty = 0m,
                    LastUpdated = DateTime.UtcNow
                });
            await context.SaveChangesAsync();

            var service = CreateDeductionService(context, CreateModeService(context));
            var result = await service.DeductStockForOrderAsync(new List<POSSoldItemDto>
            {
                new() { DrinkId = 8117, Quantity = 1 }
            }, StoreId);

            Assert.False(result.IsSuccess);
            Assert.Equal(InventoryWriterFailureCodes.ModeBlocked, result.ErrorCode);
            Assert.All(await context.StoreInventories.Where(x => x.StoreId == StoreId).ToListAsync(),
                x => Assert.Equal(10m, x.AvailableQty));
            Assert.False(await context.InventoryTransactions.AnyAsync(x =>
                x.StoreInventory.StoreId == StoreId));
        }

        [Fact]
        public async Task PreparedResolverFindsOneConfirmedCanonicalRowAndRejectsUnitMismatch()
        {
            using var context = CreateDbContext();
            await SeedPreparedItemAsync(context);
            await AddStoreAsync(context, StoreId, InventoryWriterMode.PreparedItem, hasEverActivated: true);
            var canonical = NewBtpRow(BtpIdentityState.Canonical, InventoryQuantitySemanticsStatus.BaseUnitConfirmed);
            canonical.QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation;
            canonical.QuantitySemanticsEvidenceReference = "issue-118-test";
            canonical.QuantitySemanticsReviewedAt = DateTime.UtcNow;
            canonical.QuantitySemanticsReviewedByAccountId = 1;
            context.StoreInventories.Add(canonical);
            await context.SaveChangesAsync();
            var modeService = CreateModeService(context);
            var resolver = new StoreInventoryWriteResolver(context, modeService);

            await using var transaction = await context.Database.BeginTransactionAsync();
            var snapshot = (await modeService.AcquireSnapshotAsync(StoreId)).Data!;
            var found = await resolver.ResolveAsync(PreparedRequest(snapshot, UnitId));
            var mismatch = await resolver.ResolveAsync(PreparedRequest(snapshot, UnitId + 1));

            Assert.Equal(InventoryWriteResolutionStatuses.FoundCanonical, found.Status);
            Assert.Same(canonical, found.StoreInventory);
            Assert.Equal(InventoryWriteResolutionStatuses.UnitMismatch, mismatch.Status);
        }

        [Fact]
        public async Task CanonicalUniqueIndexAllowsLegacyHistoryButOnlyOneCanonicalWinner()
        {
            using var context = CreateDbContext();
            await SeedPreparedItemAsync(context);
            await AddStoreAsync(context, StoreId, InventoryWriterMode.LegacyRecipe);
            context.StoreInventories.Add(NewBtpRow(BtpIdentityState.Legacy, InventoryQuantitySemanticsStatus.Unknown));
            context.StoreInventories.Add(ConfirmedCanonical());
            await context.SaveChangesAsync();

            context.StoreInventories.Add(ConfirmedCanonical());
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        private InventoryWriterModeTransitionRequest Request(
            InventoryWriterModeStatusDto status,
            InventoryWriterMode target) => new()
            {
                StoreId = status.StoreId,
                ExpectedCurrentMode = status.WriterMode,
                ExpectedRowVersion = status.RowVersion,
                TargetMode = target,
                Reason = "Issue #118 transition test",
                ActorAccountId = 1
            };

        private static StoreInventoryWriteRequest PreparedRequest(
            InventoryWriterModeSnapshot snapshot,
            int? unitId = UnitId) => new()
            {
                ModeSnapshot = snapshot,
                StoreId = StoreId,
                IdentityType = InventoryWriteIdentityTypes.PreparedItem,
                PreparedItemId = PreparedItemId,
                NormalizedBaseUnitId = unitId
            };

        private static StoreInventory ConfirmedCanonical()
        {
            var row = NewBtpRow(BtpIdentityState.Canonical, InventoryQuantitySemanticsStatus.BaseUnitConfirmed);
            row.QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation;
            row.QuantitySemanticsEvidenceReference = "canonical-test";
            row.QuantitySemanticsReviewedAt = DateTime.UtcNow;
            row.QuantitySemanticsReviewedByAccountId = 1;
            return row;
        }

        private static StoreInventory NewBtpRow(
            BtpIdentityState state,
            InventoryQuantitySemanticsStatus semantics) => new()
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                BtpIdentityState = state,
                QuantitySemanticsStatus = semantics,
                AvailableQty = 0m,
                ReservedQty = 0m,
                LastUpdated = DateTime.UtcNow
            };

        private static Store NewStore(int id) => new()
        {
            StoreId = id,
            Name = $"Store {id}",
            Address = "Test",
            Phone = "0000000000",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };

        private static StoreInventoryWriterConfiguration NewConfiguration(
            int storeId,
            InventoryWriterMode mode = InventoryWriterMode.LegacyRecipe,
            bool hasEverActivated = false) => new()
            {
                StoreId = storeId,
                WriterMode = mode,
                HasEverActivatedPreparedItem = hasEverActivated,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        private static async Task AddStoreAsync(
            AppDbContext context,
            int storeId,
            InventoryWriterMode mode,
            bool hasEverActivated = false)
        {
            context.Stores.Add(NewStore(storeId));
            context.StoreInventoryWriterConfigurations.Add(NewConfiguration(storeId, mode, hasEverActivated));
            await context.SaveChangesAsync();
        }

        private static async Task SeedPreparedItemAsync(AppDbContext context)
        {
            context.Units.Add(new Unit
            {
                UnitId = UnitId,
                UnitCode = "g118",
                Name = "Gram 118",
                Type = Models.Enums.Unit.UnitType.KhoiLuong,
                Active = true
            });
            context.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = PreparedItemId,
                Code = "BTP-118",
                Name = "Prepared 118",
                BaseUnitId = UnitId,
                Active = true
            });
            await context.SaveChangesAsync();
        }

        private static InventoryWriterModeService CreateModeService(
            AppDbContext context,
            params IInventoryWriterCapabilityProvider[] providers)
        {
            var physical = new PhysicalUnitConversionService(
                context,
                NullLogger<PhysicalUnitConversionService>.Instance);
            return new InventoryWriterModeService(context, physical, providers);
        }

        private static InventoryDeductionService CreateDeductionService(
            AppDbContext context,
            IInventoryWriterModeService modeService)
        {
            var physical = new PhysicalUnitConversionService(
                context,
                NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
            var normalizer = new RecipeOutputNormalizer(context, physical);
            var estimated = new EstimatedBomCostService(
                context,
                unit,
                physical,
                normalizer,
                NullLogger<EstimatedBomCostService>.Instance);
            return new InventoryDeductionService(
                context,
                NullLogger<InventoryDeductionService>.Instance,
                unit,
                estimated,
                physical,
                writerModeService: modeService);
        }
    }
}
