using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>
    /// Issue #122 — SQL Server race proof for OPEN PreparedItem alert uniqueness.
    /// DB: CafeChain_Issue122Tests on local SQLEXPRESS.
    /// </summary>
    [Trait("Category", "SqlServerIntegration")]
    public sealed class StockAlertOpenPreparedItemSqlServerIssue122Tests : IAsyncLifetime
    {
        private const string Database = "CafeChain_Issue122Tests";

        private static string ConnectionString => SqlServerTestConnection.Create(Database);

        private static string MasterConnectionString => SqlServerTestConnection.MasterConnectionString();

        private const int StoreId = 1;
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
                    $"SQL Server unavailable for #122. Database={Database}. {ex.Message}",
                    ex);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SqlServer_ConcurrentStockEvents_CreateOneAlert()
        {
            int preparedItemId;
            await using (var seed = CreateContext())
            {
                preparedItemId = await SeedCanonicalLowStockAsync(seed);
            }

            await using var ctx1 = CreateContext();
            await using var ctx2 = CreateContext();
            var results = await Task.WhenAll(
                CreateService(ctx1).EvaluateStoreAsync(StoreId, StockAlertSources.PosSale),
                CreateService(ctx2).EvaluateStoreAsync(StoreId, StockAlertSources.PosSale));

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message));

            await using var verify = CreateContext();
            var opens = await verify.StockAlerts
                .Where(a => a.StoreId == StoreId
                            && a.PreparedItemId == preparedItemId
                            && a.Status == StockAlertStatuses.Open)
                .ToListAsync();
            Assert.Single(opens);
            Assert.Null(opens[0].RecipeId);
            Assert.Null(opens[0].IngredientId);
        }

        [Fact]
        public async Task SqlServer_LowToOut_EscalatesSameAlert()
        {
            int preparedItemId;
            await using (var seed = CreateContext())
            {
                preparedItemId = await SeedCanonicalLowStockAsync(seed, availableQty: 5m);
                Assert.True((await CreateService(seed).EvaluateStoreAsync(
                    StoreId, StockAlertSources.ManualCheck)).IsSuccess);
            }

            int alertId;
            await using (var lower = CreateContext())
            {
                alertId = await lower.StockAlerts
                    .Where(a => a.StoreId == StoreId && a.PreparedItemId == preparedItemId)
                    .Select(a => a.StockAlertId)
                    .SingleAsync();
                var inventory = await lower.StoreInventories.SingleAsync(i =>
                    i.StoreId == StoreId && i.PreparedItemId == preparedItemId);
                inventory.AvailableQty = 0m;
                inventory.LastUpdated = DateTime.UtcNow;
                await lower.SaveChangesAsync();
                Assert.True((await CreateService(lower).EvaluateStoreInventoryItemAsync(
                    inventory.StoreInventoryId, StockAlertSources.PosSale)).IsSuccess);
            }

            await using var verify = CreateContext();
            var alert = await verify.StockAlerts.SingleAsync(a => a.StockAlertId == alertId);
            Assert.Equal(StockAlertTypes.OutOfStock, alert.AlertType);
            Assert.Equal(StockAlertSeverities.Urgent, alert.Severity);
            Assert.Equal(StockAlertStatuses.Open, alert.Status);
            Assert.Contains(await verify.StockAlertTransitions
                .Where(t => t.StockAlertId == alertId)
                .ToListAsync(), t => t.PreviousAlertType == StockAlertTypes.LowStock
                                   && t.NewAlertType == StockAlertTypes.OutOfStock);
        }

        [Fact]
        public async Task SqlServer_AlertRecovery_ResolvesOnce()
        {
            int preparedItemId;
            await using (var seed = CreateContext())
            {
                preparedItemId = await SeedCanonicalLowStockAsync(seed, availableQty: 2m);
                Assert.True((await CreateService(seed).EvaluateStoreAsync(
                    StoreId, StockAlertSources.ManualCheck)).IsSuccess);
            }

            int inventoryId;
            await using (var recover = CreateContext())
            {
                var inventory = await recover.StoreInventories.SingleAsync(i =>
                    i.StoreId == StoreId && i.PreparedItemId == preparedItemId);
                inventory.AvailableQty = 20m;
                inventory.LastUpdated = DateTime.UtcNow;
                await recover.SaveChangesAsync();
                inventoryId = inventory.StoreInventoryId;
                Assert.True((await CreateService(recover).EvaluateStoreInventoryItemAsync(
                    inventoryId, "TRANSFER_COMPLETED")).IsSuccess);
            }

            await using (var replay = CreateContext())
            {
                Assert.True((await CreateService(replay).EvaluateStoreInventoryItemAsync(
                    inventoryId, "SCHEDULED_RECHECK")).IsSuccess);
            }

            await using var verify = CreateContext();
            var alert = await verify.StockAlerts.SingleAsync(a =>
                a.StoreId == StoreId && a.PreparedItemId == preparedItemId);
            Assert.Equal(StockAlertStatuses.Resolved, alert.Status);
            Assert.Equal(1, await verify.StockAlertTransitions.CountAsync(t =>
                t.StockAlertId == alert.StockAlertId
                && t.NewStatus == StockAlertStatuses.Resolved));
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new AppDbContext(options);
        }

        private static StockAlertService CreateService(AppDbContext ctx)
        {
            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var caps = new IInventoryWriterCapabilityProvider[]
            {
                new ProductionPreparedWriterCapabilityProvider(),
                new PosPreparedWriterCapabilityProvider(),
                new AlertRestockPreparedIdentityCapabilityProvider()
            };
            var writer = new InventoryWriterModeService(ctx, physical, caps);
            return new StockAlertService(ctx, NullLogger<StockAlertService>.Instance, writer);
        }

        private static async Task<int> SeedCanonicalLowStockAsync(
            AppDbContext ctx,
            decimal availableQty = 0m)
        {
            var cfg = await ctx.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId);
            cfg.WriterMode = InventoryWriterMode.PreparedItem;
            cfg.HasEverActivatedPreparedItem = true;
            cfg.UpdatedAt = DateTime.UtcNow;

            var pi = new PreparedItem
            {
                Code = "PI-SQL-122-" + Guid.NewGuid().ToString("N")[..8],
                Name = "SQL PI 122",
                BaseUnitId = UnitMl,
                Active = true
            };
            ctx.PreparedItems.Add(pi);
            await ctx.SaveChangesAsync();

            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = pi.PreparedItemId,
                RecipeId = null,
                BtpIdentityState = BtpIdentityState.Canonical,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                QuantitySemanticsEvidenceReference = "sql-122",
                QuantitySemanticsReviewedAt = DateTime.UtcNow,
                QuantitySemanticsReviewedByAccountId = 1,
                AvailableQty = availableQty,
                MinStockLevel = 10m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
            return pi.PreparedItemId;
        }
    }
}
