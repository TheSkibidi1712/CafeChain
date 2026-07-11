using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories.Consolidation;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>
    /// Issue #123 — SQL Server concurrency proofs on dedicated CafeChain_Issue123Tests.
    /// </summary>
    public sealed class LegacyBtpConsolidationSqlServerIssue123Tests : IAsyncLifetime
    {
        private const string Server = @"DESKTOP-K038H12\SQLEXPRESS";
        private const string Database = "CafeChain_Issue123Tests";

        private static string ConnectionString =>
            $"Server={Server};Database={Database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        private static string MasterConnectionString =>
            $"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True";

        private const int StoreId = 1;
        private const int StaffId = 1;
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
                    $"SQL Server integration environment unavailable for #123 concurrency tests. " +
                    $"Server={Server}, Database={Database}. {ex.Message}",
                    ex);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SqlServer_ConcurrentExecute_SameRequestKey_MutatesOnce()
        {
            var (sourceId, targetId, key, dryHash, sourceAvail) = await SeedReadyRunAsync("A");

            await using var ctx1 = CreateContext();
            await using var ctx2 = CreateContext();
            var r1 = CreateService(ctx1).ExecuteAsync(Exec(key, dryHash));
            var r2 = CreateService(ctx2).ExecuteAsync(Exec(key, dryHash));
            var results = await Task.WhenAll(r1, r2);

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message + " " + r.ErrorCode));
            Assert.Contains(results, r => r.Data!.WasReplay);
            Assert.Contains(results, r => !r.Data!.WasReplay);

            await using var verify = CreateContext();
            Assert.Equal(1, await verify.InventoryConsolidationRuns.CountAsync(x =>
                x.RequestKey == key && x.Status == InventoryConsolidationRunStatus.Completed));
            Assert.Equal(1, await verify.InventoryTransactions.CountAsync(x =>
                x.Type == InventoryTransactionTypeEnum.CONSOLIDATION_OUT && x.StoreInventoryId == sourceId));
            Assert.Equal(1, await verify.InventoryTransactions.CountAsync(x =>
                x.Type == InventoryTransactionTypeEnum.CONSOLIDATION_IN && x.StoreInventoryId == targetId));
            var source = await verify.StoreInventories.SingleAsync(x => x.StoreInventoryId == sourceId);
            var target = await verify.StoreInventories.SingleAsync(x => x.StoreInventoryId == targetId);
            Assert.Equal(0m, source.AvailableQty);
            Assert.Equal(sourceAvail, target.AvailableQty);
            Assert.Equal(BtpIdentityState.Superseded, source.BtpIdentityState);
        }

        [Fact]
        public async Task SqlServer_ConcurrentExecute_OverlappingManifests_OneWinsOtherStale()
        {
            int preparedItemId;
            int recipeId;
            int sourceId;
            int targetId;
            await using (var seed = CreateContext())
            {
                preparedItemId = await SeedPreparedItemAsync(seed, "PI-SQL-123-OVL");
                recipeId = await SeedRecipeAsync(seed, "RCP-SQL-123-OVL", preparedItemId);
                await PutBlockedAsync(seed);
                sourceId = await SeedCompatibilityAsync(seed, recipeId, preparedItemId, 20m);
                targetId = await SeedCanonicalAsync(seed, preparedItemId, 0m);
            }

            var key1 = Guid.NewGuid();
            var key2 = Guid.NewGuid();
            ConsolidationRunResultDto dry1, dry2;
            await using (var dctx = CreateContext())
            {
                dry1 = (await CreateService(dctx).DryRunAsync(Req(key1, Manifest(preparedItemId, sourceId, targetId)))).Data!;
                dry2 = (await CreateService(dctx).DryRunAsync(Req(key2, Manifest(preparedItemId, sourceId, targetId)))).Data!;
            }

            await using var ctx1 = CreateContext();
            await using var ctx2 = CreateContext();
            var results = await Task.WhenAll(
                CreateService(ctx1).ExecuteAsync(Exec(key1, dry1.DryRunHash!)),
                CreateService(ctx2).ExecuteAsync(Exec(key2, dry2.DryRunHash!)));

            Assert.Contains(results, r => r.IsSuccess && !r.Data!.WasReplay);
            // Other may succeed as replay only if same key — different keys: one success, one fail/stale
            var successNonReplay = results.Count(r => r.IsSuccess && r.Data is { WasReplay: false });
            Assert.Equal(1, successNonReplay);

            await using var verify = CreateContext();
            var source = await verify.StoreInventories.SingleAsync(x => x.StoreInventoryId == sourceId);
            var target = await verify.StoreInventories.SingleAsync(x => x.StoreInventoryId == targetId);
            Assert.Equal(0m, source.AvailableQty);
            Assert.Equal(20m, target.AvailableQty);
            Assert.Equal(1, await verify.InventoryTransactions.CountAsync(x =>
                x.Type == InventoryTransactionTypeEnum.CONSOLIDATION_OUT));
            Assert.Equal(1, await verify.InventoryTransactions.CountAsync(x =>
                x.Type == InventoryTransactionTypeEnum.CONSOLIDATION_IN));
        }

        [Fact]
        public async Task SqlServer_ConcurrentCreateCanonicalTarget_CreatesOneTarget()
        {
            int preparedItemId;
            int recipeId;
            int sourceId;
            await using (var seed = CreateContext())
            {
                preparedItemId = await SeedPreparedItemAsync(seed, "PI-SQL-123-CRT");
                recipeId = await SeedRecipeAsync(seed, "RCP-SQL-123-CRT", preparedItemId);
                await PutBlockedAsync(seed);
                sourceId = await SeedCompatibilityAsync(seed, recipeId, preparedItemId, 7m);
            }

            var key1 = Guid.NewGuid();
            var key2 = Guid.NewGuid();
            string hash1, hash2;
            await using (var dctx = CreateContext())
            {
                var m1 = CreateTargetManifest(preparedItemId, sourceId);
                var m2 = CreateTargetManifest(preparedItemId, sourceId);
                hash1 = (await CreateService(dctx).DryRunAsync(Req(key1, m1))).Data!.DryRunHash!;
                hash2 = (await CreateService(dctx).DryRunAsync(Req(key2, m2))).Data!.DryRunHash!;
            }

            await using var ctx1 = CreateContext();
            await using var ctx2 = CreateContext();
            var results = await Task.WhenAll(
                CreateService(ctx1).ExecuteAsync(Exec(key1, hash1)),
                CreateService(ctx2).ExecuteAsync(Exec(key2, hash2)));

            Assert.Contains(results, r => r.IsSuccess && r.Data is { WasReplay: false });

            await using var verify = CreateContext();
            var canons = await verify.StoreInventories.Where(x =>
                x.StoreId == StoreId
                && x.PreparedItemId == preparedItemId
                && x.BtpIdentityState == BtpIdentityState.Canonical).ToListAsync();
            Assert.Single(canons);
            Assert.Equal(7m, canons[0].AvailableQty);
        }

        [Fact]
        public async Task SqlServer_Execute_WithReverseSourceOrder_UsesDeterministicLocks()
        {
            int preparedItemId;
            int recipeA;
            int sourceA, sourceB;
            int targetId;
            await using (var seed = CreateContext())
            {
                preparedItemId = await SeedPreparedItemAsync(seed, "PI-SQL-123-REV");
                // Only one Active recipe per PreparedItem (IX_Recipes_OneActive_PreparedItem).
                // Second source is PreparedItem-only non-canonical (Legacy) with same PI.
                recipeA = await SeedRecipeAsync(seed, "RCP-SQL-123-RA", preparedItemId);
                await PutBlockedAsync(seed);
                sourceA = await SeedCompatibilityAsync(seed, recipeA, preparedItemId, 3m);
                sourceB = await SeedNonCanonicalPreparedOnlyAsync(seed, preparedItemId, 4m);
                targetId = await SeedCanonicalAsync(seed, preparedItemId, 0m);
            }

            // Two manifests with reversed source order, different request keys, overlapping sources
            var key1 = Guid.NewGuid();
            var key2 = Guid.NewGuid();
            string h1, h2;
            await using (var dctx = CreateContext())
            {
                var m1 = MultiSourceManifest(preparedItemId, new[] { sourceA, sourceB }, targetId);
                var m2 = MultiSourceManifest(preparedItemId, new[] { sourceB, sourceA }, targetId);
                h1 = (await CreateService(dctx).DryRunAsync(Req(key1, m1))).Data!.DryRunHash!;
                h2 = (await CreateService(dctx).DryRunAsync(Req(key2, m2))).Data!.DryRunHash!;
            }

            await using var ctx1 = CreateContext();
            await using var ctx2 = CreateContext();
            // Concurrent execute must not throw unhandled; loser may fail stale/exception (caught).
            var results = await Task.WhenAll(
                CreateService(ctx1).ExecuteAsync(Exec(key1, h1)),
                CreateService(ctx2).ExecuteAsync(Exec(key2, h2)));

            Assert.Contains(results, r => r.IsSuccess && r.Data is { WasReplay: false });
            await using var verify = CreateContext();
            var target = await verify.StoreInventories.SingleAsync(x => x.StoreInventoryId == targetId);
            Assert.Equal(7m, target.AvailableQty);
            Assert.Equal(0m, await verify.StoreInventories.Where(x => x.StoreInventoryId == sourceA).Select(x => x.AvailableQty).SingleAsync());
            Assert.Equal(0m, await verify.StoreInventories.Where(x => x.StoreInventoryId == sourceB).Select(x => x.AvailableQty).SingleAsync());
            // Exactly one Completed consolidation for this overlap (loser blocked/failed, not second transfer)
            Assert.Equal(1, await verify.InventoryTransactions.CountAsync(x =>
                x.Type == InventoryTransactionTypeEnum.CONSOLIDATION_IN
                && x.StoreInventoryId == targetId));
        }

        [Fact]
        public async Task SqlServer_CompletedRun_Replay_DoesNotCreateSecondMovements()
        {
            var (sourceId, targetId, key, dryHash, _) = await SeedReadyRunAsync("RPL");
            await using (var ctx = CreateContext())
            {
                var first = await CreateService(ctx).ExecuteAsync(Exec(key, dryHash));
                Assert.True(first.IsSuccess, first.Message);
            }

            int moves;
            await using (var v = CreateContext())
                moves = await v.InventoryTransactions.CountAsync(x => x.InventoryConsolidationRunId != null);

            await using var ctx2 = CreateContext();
            var replay = await CreateService(ctx2).ExecuteAsync(Exec(key, dryHash));
            Assert.True(replay.IsSuccess);
            Assert.True(replay.Data!.WasReplay);

            await using var verify = CreateContext();
            Assert.Equal(moves, await verify.InventoryTransactions.CountAsync(x => x.InventoryConsolidationRunId != null));
        }

        [Fact]
        public async Task SqlServer_NoOpEvidence_SameRequestKey_CreatesOneCompletedEvidence()
        {
            await using (var seed = CreateContext())
            {
                await PutBlockedAsync(seed);
            }

            var key = Guid.NewGuid();
            string auditHash;
            await using (var a = CreateContext())
            {
                auditHash = (await CreateService(a).AuditStoreAsync(StoreId)).Data!.AuditHash;
            }

            await using var ctx1 = CreateContext();
            await using var ctx2 = CreateContext();
            var req = new ConsolidationNoOpRequest
            {
                StoreId = StoreId,
                RequestKey = key,
                RequestedByStaffId = StaffId,
                ApprovedByStaffId = StaffId,
                ExplicitApproval = true,
                ExpectedAuditHash = auditHash
            };
            var results = await Task.WhenAll(
                CreateService(ctx1).CreateNoOpEvidenceAsync(req),
                CreateService(ctx2).CreateNoOpEvidenceAsync(req));

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message));
            await using var verify = CreateContext();
            Assert.Equal(1, await verify.InventoryConsolidationRuns.CountAsync(x =>
                x.RequestKey == key && x.RunType == InventoryConsolidationRunType.AuditNoOp));
        }

        // ───────── helpers ─────────

        private async Task<(int sourceId, int targetId, Guid key, string dryHash, decimal sourceAvail)> SeedReadyRunAsync(string tag)
        {
            int preparedItemId, recipeId, sourceId, targetId;
            const decimal sourceAvail = 15m;
            await using (var seed = CreateContext())
            {
                preparedItemId = await SeedPreparedItemAsync(seed, $"PI-SQL-123-{tag}");
                recipeId = await SeedRecipeAsync(seed, $"RCP-SQL-123-{tag}", preparedItemId);
                await PutBlockedAsync(seed);
                sourceId = await SeedCompatibilityAsync(seed, recipeId, preparedItemId, sourceAvail);
                targetId = await SeedCanonicalAsync(seed, preparedItemId, 0m);
            }

            var key = Guid.NewGuid();
            string dryHash;
            await using (var dctx = CreateContext())
            {
                var dry = await CreateService(dctx).DryRunAsync(Req(key, Manifest(preparedItemId, sourceId, targetId)));
                Assert.True(dry.IsSuccess, dry.Message);
                dryHash = dry.Data!.DryRunHash!;
            }

            return (sourceId, targetId, key, dryHash, sourceAvail);
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new AppDbContext(options);
        }

        private static ILegacyBtpConsolidationService CreateService(AppDbContext ctx)
            => new LegacyBtpConsolidationService(
                ctx,
                new TestHostEnvironment(),
                NullLogger<LegacyBtpConsolidationService>.Instance);

        private static ConsolidationExecuteRequest Exec(Guid key, string hash)
            => new()
            {
                StoreId = StoreId,
                RequestKey = key,
                ExpectedDryRunHash = hash,
                ExecutedByStaffId = StaffId,
                ActorRole = RoleConstants.SystemAdmin
            };

        private static ConsolidationDryRunRequest Req(Guid key, ConsolidationManifestDto manifest)
            => new()
            {
                StoreId = StoreId,
                RequestKey = key,
                Manifest = manifest,
                RequestedByStaffId = StaffId,
                ApprovedByStaffId = StaffId
            };

        private static ConsolidationManifestDto Manifest(int preparedItemId, int sourceId, int targetId)
            => new()
            {
                StoreId = StoreId,
                Groups = new[]
                {
                    new ConsolidationGroupManifestDto
                    {
                        StoreId = StoreId,
                        PreparedItemId = preparedItemId,
                        SourceStoreInventoryIds = new[] { sourceId },
                        TargetStoreInventoryId = targetId,
                        CreateCanonicalTarget = false,
                        ApprovedMinStockLevel = 5m,
                        ThresholdDecisionProvided = true,
                        ActorApprovalStaffId = StaffId,
                        EvidenceReference = "sql"
                    }
                }
            };

        private static ConsolidationManifestDto CreateTargetManifest(int preparedItemId, int sourceId)
            => new()
            {
                StoreId = StoreId,
                Groups = new[]
                {
                    new ConsolidationGroupManifestDto
                    {
                        StoreId = StoreId,
                        PreparedItemId = preparedItemId,
                        SourceStoreInventoryIds = new[] { sourceId },
                        CreateCanonicalTarget = true,
                        ApprovedMinStockLevel = 5m,
                        ThresholdDecisionProvided = true,
                        ActorApprovalStaffId = StaffId,
                        EvidenceReference = "sql-create"
                    }
                }
            };

        private static ConsolidationManifestDto MultiSourceManifest(int preparedItemId, int[] sources, int targetId)
            => new()
            {
                StoreId = StoreId,
                Groups = new[]
                {
                    new ConsolidationGroupManifestDto
                    {
                        StoreId = StoreId,
                        PreparedItemId = preparedItemId,
                        SourceStoreInventoryIds = sources,
                        TargetStoreInventoryId = targetId,
                        CreateCanonicalTarget = false,
                        ApprovedMinStockLevel = 5m,
                        ThresholdDecisionProvided = true,
                        ActorApprovalStaffId = StaffId,
                        EvidenceReference = "sql-multi"
                    }
                }
            };

        private static async Task PutBlockedAsync(AppDbContext ctx)
        {
            var cfg = await ctx.StoreInventoryWriterConfigurations.FirstOrDefaultAsync(x => x.StoreId == StoreId);
            if (cfg == null)
            {
                ctx.StoreInventoryWriterConfigurations.Add(new StoreInventoryWriterConfiguration
                {
                    StoreId = StoreId,
                    WriterMode = InventoryWriterMode.Blocked,
                    HasEverActivatedPreparedItem = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                cfg.WriterMode = InventoryWriterMode.Blocked;
                cfg.UpdatedAt = DateTime.UtcNow;
            }

            await ctx.SaveChangesAsync();
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

        private static async Task<int> SeedRecipeAsync(AppDbContext ctx, string code, int preparedItemId)
        {
            var r = new Recipe
            {
                RecipeCode = code,
                Name = code,
                Active = true,
                Status = "Active",
                PreparedItemId = preparedItemId,
                OutputQuantity = 1m,
                OutputUnitId = UnitMl
            };
            ctx.Recipes.Add(r);
            await ctx.SaveChangesAsync();
            return r.RecipeId;
        }

        private static async Task<int> SeedCompatibilityAsync(
            AppDbContext ctx, int recipeId, int preparedItemId, decimal avail)
        {
            var row = new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = recipeId,
                PreparedItemId = preparedItemId,
                BtpIdentityState = BtpIdentityState.Legacy,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.ManualReview,
                QuantitySemanticsEvidenceReference = "sql",
                QuantitySemanticsReviewedAt = DateTime.UtcNow,
                QuantitySemanticsReviewedByAccountId = 1,
                AvailableQty = avail,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow
            };
            ctx.StoreInventories.Add(row);
            await ctx.SaveChangesAsync();
            return row.StoreInventoryId;
        }

        private static async Task<int> SeedCanonicalAsync(AppDbContext ctx, int preparedItemId, decimal avail)
        {
            var row = new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = preparedItemId,
                BtpIdentityState = BtpIdentityState.Canonical,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                QuantitySemanticsEvidenceReference = "sql-canon",
                QuantitySemanticsReviewedAt = DateTime.UtcNow,
                QuantitySemanticsReviewedByAccountId = 1,
                AvailableQty = avail,
                ReservedQty = 0,
                MinStockLevel = 1m,
                LastUpdated = DateTime.UtcNow
            };
            ctx.StoreInventories.Add(row);
            await ctx.SaveChangesAsync();
            return row.StoreInventoryId;
        }

        private static async Task<int> SeedNonCanonicalPreparedOnlyAsync(
            AppDbContext ctx, int preparedItemId, decimal avail)
        {
            var row = new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = preparedItemId,
                RecipeId = null,
                BtpIdentityState = BtpIdentityState.Legacy,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.ManualReview,
                QuantitySemanticsEvidenceReference = "sql-noncanon",
                QuantitySemanticsReviewedAt = DateTime.UtcNow,
                QuantitySemanticsReviewedByAccountId = 1,
                AvailableQty = avail,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow
            };
            ctx.StoreInventories.Add(row);
            await ctx.SaveChangesAsync();
            return row.StoreInventoryId;
        }

        private sealed class TestHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Development";
            public string ApplicationName { get; set; } = "CafeChain.Tests";
            public string ContentRootPath { get; set; } = ".";
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
