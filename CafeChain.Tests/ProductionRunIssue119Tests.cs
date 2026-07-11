using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>Issue #119 / 114B — ProductionRun intent + idempotency (no stock).</summary>
    public sealed class ProductionRunIssue119Tests : IntegrationTestBase
    {
        private const int StoreId = 9119;
        private const int OtherStoreId = 9120;
        private const int RecipeId = 9121;
        private const int StaffId = 9122;

        [Fact]
        public async Task FirstRequest_CreatesOneConfirmedRun()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var result = await service.CreateAndConfirmAsync(
                NewRequest(Guid.NewGuid(), RecipeId, 3m),
                StaffId,
                StoreId);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(StoreId, result.Data!.StoreId);
            Assert.Equal(RecipeId, result.Data.RecipeId);
            Assert.Equal(3m, result.Data.RequestedRunCount);
            Assert.Equal("CONFIRMED", result.Data.Status);
            Assert.False(result.Data.WasReplay);
            Assert.False(result.Data.StockApplied);
            Assert.Equal(1, await context.ProductionRuns.CountAsync());
            Assert.Equal(ProductionRunStatus.Confirmed, (await context.ProductionRuns.SingleAsync()).Status);
        }

        [Fact]
        public async Task FractionalRunCount_Accepted()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var result = await service.CreateAndConfirmAsync(
                NewRequest(Guid.NewGuid(), RecipeId, 1.5m),
                StaffId,
                StoreId);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(1.5m, result.Data!.RequestedRunCount);
        }

        [Fact]
        public async Task SameKeySamePayload_Replays()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var service = CreateService(context);
            var key = Guid.NewGuid();

            var first = await service.CreateAndConfirmAsync(NewRequest(key, RecipeId, 2.5m), StaffId, StoreId);
            var second = await service.CreateAndConfirmAsync(NewRequest(key, RecipeId, 2.5m), StaffId, StoreId);

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess, second.Message);
            Assert.True(second.Data!.WasReplay);
            Assert.Equal(first.Data!.ProductionRunId, second.Data.ProductionRunId);
            Assert.Equal(1, await context.ProductionRuns.CountAsync());
        }

        [Fact]
        public async Task SameKeyDifferentPayload_Rejected()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var service = CreateService(context);
            var key = Guid.NewGuid();

            Assert.True((await service.CreateAndConfirmAsync(NewRequest(key, RecipeId, 2m), StaffId, StoreId)).IsSuccess);
            var second = await service.CreateAndConfirmAsync(NewRequest(key, RecipeId, 5m), StaffId, StoreId);

            Assert.False(second.IsSuccess);
            Assert.Equal(ProductionRunFailureCodes.IdempotencyKeyReused, second.ErrorCode);
            Assert.Equal(1, await context.ProductionRuns.CountAsync());
        }

        [Fact]
        public async Task DifferentKeys_AllowIdenticalRuns()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var a = await service.CreateAndConfirmAsync(NewRequest(Guid.NewGuid(), RecipeId, 2m), StaffId, StoreId);
            var b = await service.CreateAndConfirmAsync(NewRequest(Guid.NewGuid(), RecipeId, 2m), StaffId, StoreId);

            Assert.True(a.IsSuccess && b.IsSuccess);
            Assert.NotEqual(a.Data!.ProductionRunId, b.Data!.ProductionRunId);
            Assert.Equal(2, await context.ProductionRuns.CountAsync());
        }

        [Fact]
        public async Task SameRequestKey_AllowedInDifferentStore()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var service = CreateService(context);
            var key = Guid.NewGuid();

            // Other staff at OtherStore
            context.Staffs.Add(new Staff
            {
                StaffId = 9999,
                AccountId = 9999,
                FullName = "Other",
                StoreId = OtherStoreId,
                Active = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var a = await service.CreateAndConfirmAsync(NewRequest(key, RecipeId, 1m, StoreId), StaffId, StoreId);
            var b = await service.CreateAndConfirmAsync(NewRequest(key, RecipeId, 1m, OtherStoreId), 9999, OtherStoreId);

            Assert.True(a.IsSuccess, a.Message);
            Assert.True(b.IsSuccess, b.Message);
            Assert.Equal(2, await context.ProductionRuns.CountAsync(x => x.RequestKey == key));
        }

        [Fact]
        public async Task Confirm_DoesNotMutateInventoryOrLedger()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            context.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = RecipeId,
                AvailableQty = 10m,
                ReservedQty = 0m,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            await context.SaveChangesAsync();
            var service = CreateService(context);

            Assert.True((await service.CreateAndConfirmAsync(
                NewRequest(Guid.NewGuid(), RecipeId, 4m), StaffId, StoreId)).IsSuccess);

            Assert.Equal(10m, await context.StoreInventories
                .Where(x => x.StoreId == StoreId && x.RecipeId == RecipeId)
                .Select(x => x.AvailableQty).SingleAsync());
            Assert.Equal(0, await context.InventoryTransactions.CountAsync());
        }

        [Fact]
        public async Task InvalidRunCount_Rejected()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var zero = await service.CreateAndConfirmAsync(NewRequest(Guid.NewGuid(), RecipeId, 0m), StaffId, StoreId);
            var over = await service.CreateAndConfirmAsync(NewRequest(Guid.NewGuid(), RecipeId, 10000m), StaffId, StoreId);

            Assert.False(zero.IsSuccess);
            Assert.Equal(ProductionRunFailureCodes.InvalidRunCount, zero.ErrorCode);
            Assert.False(over.IsSuccess);
            Assert.Equal(0, await context.ProductionRuns.CountAsync());
        }

        [Fact]
        public async Task CrossStoreWithoutScope_Rejected()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var result = await service.CreateAndConfirmAsync(
                NewRequest(Guid.NewGuid(), RecipeId, 1m, OtherStoreId),
                StaffId,
                staffHomeStoreId: StoreId);

            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunFailureCodes.StoreUnauthorized, result.ErrorCode);
        }

        [Fact]
        public async Task BlockedMode_Rejects()
        {
            using var context = CreateDbContext();
            await SeedAsync(context, InventoryWriterMode.Blocked);
            var service = CreateService(context);

            var result = await service.CreateAndConfirmAsync(
                NewRequest(Guid.NewGuid(), RecipeId, 1m), StaffId, StoreId);

            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunFailureCodes.ModeBlocked, result.ErrorCode);
            Assert.Equal(0, await context.ProductionRuns.CountAsync());
        }

        [Fact]
        public async Task PreparedItemMode_RejectsAsNotReady()
        {
            using var context = CreateDbContext();
            await SeedAsync(context, InventoryWriterMode.PreparedItem);
            var service = CreateService(context);

            var result = await service.CreateAndConfirmAsync(
                NewRequest(Guid.NewGuid(), RecipeId, 1m), StaffId, StoreId);

            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunFailureCodes.ProductionWriterNotReady, result.ErrorCode);
            Assert.Equal(0, await context.InventoryTransactions.CountAsync());
        }

        [Fact]
        public void Fingerprint_IsCultureInvariant_AndNotesDoNotAffect()
        {
            var a = ProductionRunService.BuildFingerprint(1, 2, 1m);
            var b = ProductionRunService.BuildFingerprint(1, 2, 1.0m);
            var c = ProductionRunService.BuildFingerprint(1, 2, 1.00000m);
            var d = ProductionRunService.BuildFingerprint(1, 2, 1.5m);

            Assert.Equal(a, b);
            Assert.Equal(a, c);
            Assert.NotEqual(a, d);
            Assert.Equal(64, a.Length);
        }

        [Fact]
        public async Task Notes_DoNotChangeFingerprintIdentity()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var service = CreateService(context);
            var key = Guid.NewGuid();

            var first = await service.CreateAndConfirmAsync(
                new CreateAndConfirmProductionRunRequest
                {
                    RequestKey = key,
                    StoreId = StoreId,
                    RecipeId = RecipeId,
                    RequestedRunCount = 2m,
                    Notes = "first"
                },
                StaffId,
                StoreId);

            var second = await service.CreateAndConfirmAsync(
                new CreateAndConfirmProductionRunRequest
                {
                    RequestKey = key,
                    StoreId = StoreId,
                    RecipeId = RecipeId,
                    RequestedRunCount = 2m,
                    Notes = "different notes"
                },
                StaffId,
                StoreId);

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess, second.Message);
            Assert.True(second.Data!.WasReplay);
            Assert.Equal(1, await context.ProductionRuns.CountAsync());
        }

        [Fact]
        public async Task MissingRecipe_Rejected()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var result = await service.CreateAndConfirmAsync(
                NewRequest(Guid.NewGuid(), 999999, 1m), StaffId, StoreId);

            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunFailureCodes.RecipeNotFound, result.ErrorCode);
        }

        [Fact]
        public async Task InactiveRecipe_CannotCreateNewRun()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var recipe = await context.Recipes.SingleAsync(r => r.RecipeId == RecipeId);
            recipe.Active = false;
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.CreateAndConfirmAsync(
                NewRequest(Guid.NewGuid(), RecipeId, 1m), StaffId, StoreId);

            Assert.False(result.IsSuccess);
            Assert.Equal(ProductionRunFailureCodes.RecipeNotFound, result.ErrorCode);
            Assert.Equal(0, await context.ProductionRuns.CountAsync());
        }

        [Fact]
        public async Task ArchivedAfterConfirm_StillReplays_WithoutRecipeSubstitution()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var service = CreateService(context);
            var key = Guid.NewGuid();

            var first = await service.CreateAndConfirmAsync(NewRequest(key, RecipeId, 2m), StaffId, StoreId);
            Assert.True(first.IsSuccess, first.Message);

            var recipe = await context.Recipes.SingleAsync(r => r.RecipeId == RecipeId);
            recipe.Active = false;
            recipe.Status = "Archived";
            // New version with different id must not be substituted
            context.Recipes.Add(new Recipe
            {
                RecipeId = RecipeId + 100,
                RecipeCode = "PR-119-v2",
                Name = "BTP 119 v2",
                YieldPercentage = 100m,
                Active = true,
                Status = "Active",
                ParentVersionId = RecipeId
            });
            await context.SaveChangesAsync();

            var replay = await service.CreateAndConfirmAsync(NewRequest(key, RecipeId, 2m), StaffId, StoreId);
            Assert.True(replay.IsSuccess, replay.Message);
            Assert.True(replay.Data!.WasReplay);
            Assert.Equal(RecipeId, replay.Data.RecipeId);
            Assert.Equal(first.Data!.ProductionRunId, replay.Data.ProductionRunId);
            Assert.Equal(1, await context.ProductionRuns.CountAsync());
            Assert.Equal(RecipeId, (await context.ProductionRuns.SingleAsync()).RecipeId);
        }

        /// <summary>
        /// Relational unique index (SQLite file/shared connection) — not EF InMemory.
        /// Two contexts race the same key; unique constraint yields one row + structured replay.
        /// </summary>
        [Fact]
        public async Task ConcurrentSameKey_RelationalUniqueIndex_OnlyOneRun()
        {
            await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = OFF;";
                await cmd.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var setup = new TestDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                await SeedAsync(setup);
            }

            var key = Guid.NewGuid();
            await using var ctx1 = new TestDbContext(options);
            await using var ctx2 = new TestDbContext(options);
            var s1 = CreateService(ctx1);
            var s2 = CreateService(ctx2);

            var t1 = s1.CreateAndConfirmAsync(NewRequest(key, RecipeId, 2m), StaffId, StoreId);
            var t2 = s2.CreateAndConfirmAsync(NewRequest(key, RecipeId, 2m), StaffId, StoreId);
            var results = await Task.WhenAll(t1, t2);

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message + " / " + r.ErrorCode));

            await using var verify = new TestDbContext(options);
            var rows = await verify.ProductionRuns.Where(x => x.RequestKey == key).ToListAsync();
            var winner = Assert.Single(rows);
            Assert.All(results, r => Assert.Equal(winner.ProductionRunId, r.Data!.ProductionRunId));
        }

        [Fact]
        public async Task UniqueConflict_SecondInsertPath_ReturnsReplayNot500()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var service = CreateService(context);
            var key = Guid.NewGuid();

            var first = await service.CreateAndConfirmAsync(NewRequest(key, RecipeId, 2m), StaffId, StoreId);
            Assert.True(first.IsSuccess);

            // Simulate lost-response retry after winner committed
            var second = await service.CreateAndConfirmAsync(NewRequest(key, RecipeId, 2m), StaffId, StoreId);
            Assert.True(second.IsSuccess);
            Assert.True(second.Data!.WasReplay);
            Assert.Equal(first.Data!.ProductionRunId, second.Data.ProductionRunId);
            Assert.Equal(1, await context.ProductionRuns.CountAsync());
        }

        private static ProductionRunService CreateService(AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
            var writer = new InventoryWriterModeService(
                context,
                physical,
                Array.Empty<IInventoryWriterCapabilityProvider>());
            var scope = new ScopeAuthorizationService(context);
            return new ProductionRunService(
                context,
                scope,
                writer,
                NullLogger<ProductionRunService>.Instance);
        }

        private static CreateAndConfirmProductionRunRequest NewRequest(
            Guid key,
            int recipeId,
            decimal runs,
            int? storeId = null)
            => new()
            {
                RequestKey = key,
                StoreId = storeId ?? StoreId,
                RecipeId = recipeId,
                RequestedRunCount = runs
            };

        private static async Task SeedAsync(
            AppDbContext context,
            InventoryWriterMode mode = InventoryWriterMode.LegacyRecipe)
        {
            var now = DateTime.UtcNow;
            context.Stores.Add(new Store
            {
                StoreId = StoreId,
                Name = "Store 119",
                Address = "A",
                Phone = "1",
                Active = true,
                CreatedAt = now
            });
            context.Stores.Add(new Store
            {
                StoreId = OtherStoreId,
                Name = "Other",
                Address = "B",
                Phone = "2",
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
            context.StoreInventoryWriterConfigurations.Add(new StoreInventoryWriterConfiguration
            {
                StoreId = OtherStoreId,
                WriterMode = InventoryWriterMode.LegacyRecipe,
                HasEverActivatedPreparedItem = false,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = new byte[] { 0 }
            });

            context.Staffs.Add(new Staff
            {
                StaffId = StaffId,
                AccountId = StaffId,
                FullName = "Staff 119",
                StoreId = StoreId,
                Active = true,
                CreatedAt = now
            });

            context.Recipes.Add(new Recipe
            {
                RecipeId = RecipeId,
                RecipeCode = "PR-119",
                Name = "BTP 119",
                YieldPercentage = 100m,
                Active = true,
                Status = "Active"
            });

            await context.SaveChangesAsync();
        }
    }
}
