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
    public sealed class StockAlertOpenPreparedItemSqlServerIssue122Tests : IAsyncLifetime
    {
        private const string Server = @"DESKTOP-K038H12\SQLEXPRESS";
        private const string Database = "CafeChain_Issue122Tests";

        private static string ConnectionString =>
            $"Server={Server};Database={Database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        private static string MasterConnectionString =>
            $"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True";

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
                    $"SQL Server unavailable for #122. Server={Server} Database={Database}. {ex.Message}",
                    ex);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SqlServer_ConcurrentEvaluate_SamePreparedItem_OneOpenAlert()
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

        private static async Task<int> SeedCanonicalLowStockAsync(AppDbContext ctx)
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
                AvailableQty = 0m,
                MinStockLevel = 10m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
            return pi.PreparedItemId;
        }
    }
}
