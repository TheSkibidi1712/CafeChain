//using CafeChain.Application.Constants;
//using CafeChain.Application.DTOs.Inventories;
//using CafeChain.Application.DTOs.Inventories.Consolidation;
//using CafeChain.Application.DTOs.Inventories.Cutover;
//using CafeChain.Application.Interfaces.Inventories;
//using CafeChain.Application.Options;
//using CafeChain.Application.Services.Admin.Production;
//using CafeChain.Application.Services.Inventories;
//using CafeChain.Data;
//using CafeChain.Models.Customers;
//using CafeChain.Models.Drinks;
//using CafeChain.Models.Enums.Inventory;
//using CafeChain.Models.Inventories.Configuration;
//using CafeChain.Models.Inventories.PreparedItems;
//using CafeChain.Models.Inventories.Transactions;
//using CafeChain.Models.Permissions;
//using CafeChain.Models.Staffs;
//using CafeChain.Models.Stores;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.FileProviders;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging.Abstractions;
//using Microsoft.Extensions.Options;
//using Xunit;

//namespace CafeChain.Tests
//{
//    /// <summary>Issue #124 — cutover reconciliation, schema probe, activation, global legacy disable.</summary>
//    public sealed class CutoverReconciliationIssue124Tests : IntegrationTestBase
//    {
//        private const int StoreId = 12401;
//        private const int StaffId = 12402;
//        private const int AccountId = 12403;
//        private const int PreparedItemId = 12405;
//        private const int RecipeId = 12406;
//        private const int UnitMl = 3;

//        private static ICutoverReconciliationService CreateCutover(
//            AppDbContext ctx,
//            bool legacyDisabled = false)
//        {
//            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
//            var caps = new IInventoryWriterCapabilityProvider[]
//            {
//                new ProductionPreparedWriterCapabilityProvider(),
//                new PosPreparedWriterCapabilityProvider(),
//                new AlertRestockPreparedIdentityCapabilityProvider(),
//                new ConsolidationOrNoopEvidenceCapabilityProvider(ctx)
//            };
//            var mode = new InventoryWriterModeService(
//                ctx,
//                physical,
//                caps,
//                Options.Create(new InventoryWriterGlobalOptions { LegacyBtpWritesDisabled = legacyDisabled }));
//            var probe = new InventorySchemaReadinessProbe(ctx);
//            return new CutoverReconciliationService(
//                ctx,
//                mode,
//                probe,
//                physical,
//                caps,
//                Options.Create(new InventoryWriterGlobalOptions { LegacyBtpWritesDisabled = legacyDisabled }),
//                new TestHostEnvironment(),
//                NullLogger<CutoverReconciliationService>.Instance);
//        }

//        private static InventoryWriterModeService CreateMode(AppDbContext ctx, bool legacyDisabled = false)
//        {
//            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
//            var caps = new IInventoryWriterCapabilityProvider[]
//            {
//                new ProductionPreparedWriterCapabilityProvider(),
//                new PosPreparedWriterCapabilityProvider(),
//                new AlertRestockPreparedIdentityCapabilityProvider(),
//                new ConsolidationOrNoopEvidenceCapabilityProvider(ctx)
//            };
//            return new InventoryWriterModeService(
//                ctx, physical, caps,
//                Options.Create(new InventoryWriterGlobalOptions { LegacyBtpWritesDisabled = legacyDisabled }));
//        }

//        [Fact]
//        public async Task Reconciliation_EnvironmentFingerprint_DoesNotExposeConnectionStringOrSecret()
//        {
//            using var ctx = CreateDbContext();
//            await SeedBaseAsync(ctx);
//            var report = (await CreateCutover(ctx).ReconcileStoreAsync(StoreId)).Data!;
//            Assert.False(string.IsNullOrEmpty(report.EnvironmentFingerprint));
//            Assert.DoesNotContain("Password", report.EnvironmentFingerprint, StringComparison.OrdinalIgnoreCase);
//            Assert.DoesNotContain("User Id", report.EnvironmentFingerprint, StringComparison.OrdinalIgnoreCase);
//            Assert.DoesNotContain(";", report.EnvironmentFingerprint);
//            Assert.Equal(32, report.EnvironmentFingerprint.Length);
//        }

//        [Fact]
//        public async Task SchemaProbe_OnEnsureCreated_IsReady()
//        {
//            using var ctx = CreateDbContext();
//            var probe = new InventorySchemaReadinessProbe(ctx);
//            var report = await probe.ProbeAsync();
//            Assert.True(report.IsReady, string.Join(",", report.MissingTables.Concat(report.MissingColumns).Concat(report.MissingIndexes)));
//            Assert.Equal(CutoverContractVersions.Schema, report.ContractVersion);
//            Assert.False(string.IsNullOrEmpty(report.ContractHash));
//        }

//        [Fact]
//        public async Task SchemaProbe_MissingConsolidationRequestKeyUnique_IsNotReady()
//        {
//            using var ctx = CreateDbContext();
//            // Drop all unique indexes on consolidation runs so semantic uniqueness fails.
//            await using (var cmd = ctx.Database.GetDbConnection().CreateCommand())
//            {
//                if (cmd.Connection!.State != System.Data.ConnectionState.Open)
//                    await cmd.Connection.OpenAsync();
//                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='InventoryConsolidationRuns'";
//                var names = new List<string>();
//                await using (var reader = await cmd.ExecuteReaderAsync())
//                {
//                    while (await reader.ReadAsync())
//                        names.Add(reader.GetString(0));
//                }

//                foreach (var name in names)
//                    await ctx.Database.ExecuteSqlRawAsync($"DROP INDEX IF EXISTS \"{name}\"");
//            }

//            var report = await new InventorySchemaReadinessProbe(ctx).ProbeAsync();
//            Assert.False(report.IsReady);
//            Assert.Contains(report.MissingIndexes, x => x.Contains("ConsolidationRuns", StringComparison.OrdinalIgnoreCase)
//                || x.Contains("RequestKey", StringComparison.OrdinalIgnoreCase)
//                || x == "UX_InventoryConsolidationRuns_Store_RequestKey");
//        }

//        [Fact]
//        public async Task SchemaProbe_MissingPreparedAlertUniqueIndex_IsNotReady()
//        {
//            using var ctx = CreateDbContext();
//            await using (var cmd = ctx.Database.GetDbConnection().CreateCommand())
//            {
//                if (cmd.Connection!.State != System.Data.ConnectionState.Open)
//                    await cmd.Connection.OpenAsync();
//                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='StockAlerts'";
//                var names = new List<string>();
//                await using (var reader = await cmd.ExecuteReaderAsync())
//                {
//                    while (await reader.ReadAsync())
//                        names.Add(reader.GetString(0));
//                }

//                foreach (var name in names.Where(n =>
//                             n.Contains("PreparedItem", StringComparison.OrdinalIgnoreCase)
//                             || n.Contains("Open", StringComparison.OrdinalIgnoreCase)
//                             || n.Contains("UX_", StringComparison.OrdinalIgnoreCase)))
//                    await ctx.Database.ExecuteSqlRawAsync($"DROP INDEX IF EXISTS \"{name}\"");
//            }

//            var report = await new InventorySchemaReadinessProbe(ctx).ProbeAsync();
//            // If any unique covering StoreId+PreparedItem remains, probe may still pass — force by dropping ALL StockAlerts indexes
//            if (report.IsReady)
//            {
//                await using var cmd = ctx.Database.GetDbConnection().CreateCommand();
//                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='StockAlerts'";
//                var names = new List<string>();
//                await using (var reader = await cmd.ExecuteReaderAsync())
//                {
//                    while (await reader.ReadAsync())
//                        names.Add(reader.GetString(0));
//                }

//                foreach (var name in names)
//                    await ctx.Database.ExecuteSqlRawAsync($"DROP INDEX IF EXISTS \"{name}\"");
//                report = await new InventorySchemaReadinessProbe(ctx).ProbeAsync();
//            }

//            Assert.False(report.IsReady);
//            Assert.Contains(report.MissingIndexes, x => x.Contains("StockAlert", StringComparison.OrdinalIgnoreCase)
//                || x.Contains("PreparedItem", StringComparison.OrdinalIgnoreCase));
//        }

//        [Fact]
//        public async Task Recon_CleanStore_WithNoOpEvidence_IsClean()
//        {
//            using var ctx = CreateDbContext();
//            await SeedCleanPreparedStoreAsync(ctx);
//            await PersistNoOpAsync(ctx);
//            var report = (await CreateCutover(ctx).ReconcileStoreAsync(StoreId)).Data!;
//            Assert.True(report.IsClean, string.Join(";", report.Anomalies.Select(a => a.Code)));
//            Assert.False(string.IsNullOrEmpty(report.ReconciliationHash));
//        }

//        [Fact]
//        public async Task Recon_DetectsRecipeOnlyBtpRow()
//        {
//            using var ctx = CreateDbContext();
//            await SeedBaseAsync(ctx);
//            await SeedPiAndRecipeAsync(ctx, mapped: false);
//            ctx.StoreInventories.Add(new StoreInventory
//            {
//                StoreId = StoreId,
//                RecipeId = RecipeId,
//                AvailableQty = 1,
//                ReservedQty = 0,
//                LastUpdated = DateTime.UtcNow,
//                RowVersion = new byte[] { 0 }
//            });
//            await ctx.SaveChangesAsync();
//            var report = (await CreateCutover(ctx).ReconcileStoreAsync(StoreId)).Data!;
//            Assert.Contains(report.Anomalies, a => a.Code == CutoverAnomalyCodes.RecipeOnlyBtpRow);
//            Assert.False(report.IsClean);
//        }

//        [Fact]
//        public async Task Recon_DetectsUnknownQuantitySemantics()
//        {
//            using var ctx = CreateDbContext();
//            await SeedBaseAsync(ctx);
//            await SeedPiAndRecipeAsync(ctx, mapped: true);
//            ctx.StoreInventories.Add(MakeCanonical(qty: 1, semantics: InventoryQuantitySemanticsStatus.Unknown));
//            await ctx.SaveChangesAsync();
//            var report = (await CreateCutover(ctx).ReconcileStoreAsync(StoreId)).Data!;
//            Assert.Contains(report.Anomalies, a => a.Code == CutoverAnomalyCodes.UnknownQuantitySemantics);
//        }

//        [Fact]
//        public async Task Recon_DetectsMissingConsolidationEvidence()
//        {
//            using var ctx = CreateDbContext();
//            await SeedCleanPreparedStoreAsync(ctx);
//            // no no-op
//            var report = (await CreateCutover(ctx).ReconcileStoreAsync(StoreId)).Data!;
//            Assert.Contains(report.Anomalies, a => a.Code == CutoverAnomalyCodes.ConsolidationEvidenceMissing);
//        }

//        [Fact]
//        public async Task Recon_StableHash_SameState()
//        {
//            using var ctx = CreateDbContext();
//            await SeedCleanPreparedStoreAsync(ctx);
//            await PersistNoOpAsync(ctx);
//            var a = (await CreateCutover(ctx).ReconcileStoreAsync(StoreId)).Data!;
//            var b = (await CreateCutover(ctx).ReconcileStoreAsync(StoreId)).Data!;
//            Assert.Equal(a.ReconciliationHash, b.ReconciliationHash);
//        }

//        [Fact]
//        public async Task Recon_HashChanges_WhenInventoryChanges()
//        {
//            using var ctx = CreateDbContext();
//            await SeedCleanPreparedStoreAsync(ctx);
//            await PersistNoOpAsync(ctx);
//            var a = (await CreateCutover(ctx).ReconcileStoreAsync(StoreId)).Data!;
//            var inv = await ctx.StoreInventories.SingleAsync(x => x.StoreId == StoreId);
//            inv.AvailableQty += 1;
//            await ctx.SaveChangesAsync();
//            var b = (await CreateCutover(ctx).ReconcileStoreAsync(StoreId)).Data!;
//            Assert.NotEqual(a.ReconciliationHash, b.ReconciliationHash);
//        }

//        [Fact]
//        public async Task Reconciliation_DoesNotChangeWriterModeOrInventory()
//        {
//            using var ctx = CreateDbContext();
//            await SeedBaseAsync(ctx, InventoryWriterMode.LegacyRecipe);
//            await SeedPiAndRecipeAsync(ctx, true);
//            ctx.StoreInventories.Add(MakeCanonical(5));
//            await ctx.SaveChangesAsync();
//            var beforeMode = (await ctx.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId)).WriterMode;
//            var beforeQty = (await ctx.StoreInventories.SingleAsync(x => x.StoreId == StoreId)).AvailableQty;
//            await CreateCutover(ctx).ReconcileStoreAsync(StoreId);
//            Assert.Equal(beforeMode, (await ctx.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId)).WriterMode);
//            Assert.Equal(beforeQty, (await ctx.StoreInventories.SingleAsync(x => x.StoreId == StoreId)).AvailableQty);
//        }

//        [Fact]
//        public async Task Cutover_Activate_WithoutMaintenanceAck_Rejected()
//        {
//            using var ctx = CreateDbContext();
//            await SeedReadyForActivationAsync(ctx);
//            var cutover = CreateCutover(ctx);
//            var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//            var status = (await CreateMode(ctx).GetStatusAsync(StoreId)).Data!;
//            var result = await cutover.ActivatePreparedItemAsync(new CutoverActivationRequest
//            {
//                StoreId = StoreId,
//                RequestKey = Guid.NewGuid(),
//                ExpectedMode = status.WriterMode,
//                ExpectedRowVersion = status.RowVersion,
//                ExpectedReadinessHash = recon.ReadinessHash,
//                ExpectedReconciliationHash = recon.ReconciliationHash,
//                ExpectedSchemaContractHash = recon.Schema.ContractHash,
//                MaintenanceWindowAcknowledged = false,
//                Reason = "no ack",
//                ActorAccountId = AccountId
//            });
//            Assert.False(result.IsSuccess);
//            Assert.Equal(CutoverFailureCodes.MaintenanceWindowRequired, result.ErrorCode);
//        }

//        [Fact]
//        public async Task Cutover_Activate_SystemAdmin_Succeeds()
//        {
//            using var ctx = CreateDbContext();
//            await SeedReadyForActivationAsync(ctx);
//            var cutover = CreateCutover(ctx);
//            var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//            Assert.True(recon.IsClean, string.Join(";", recon.Anomalies.Select(a => a.Code)));
//            var status = (await CreateMode(ctx).GetStatusAsync(StoreId)).Data!;
//            var result = await cutover.ActivatePreparedItemAsync(BuildActivate(status, recon));
//            Assert.True(result.IsSuccess, result.Message + " " + result.ErrorCode);
//            Assert.False(result.Data!.WasReplay);
//            Assert.Equal(InventoryWriterMode.PreparedItem,
//                (await ctx.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId)).WriterMode);
//            Assert.True((await ctx.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId)).HasEverActivatedPreparedItem);
//            Assert.Equal(1, await ctx.InventoryWriterModeTransitions.CountAsync(x =>
//                x.StoreId == StoreId && x.Succeeded && x.ToMode == InventoryWriterMode.PreparedItem));
//        }

//        [Fact]
//        public async Task Cutover_Activate_ReplaySameEvidence_Idempotent()
//        {
//            using var ctx = CreateDbContext();
//            await SeedReadyForActivationAsync(ctx);
//            var cutover = CreateCutover(ctx);
//            var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//            var status = (await CreateMode(ctx).GetStatusAsync(StoreId)).Data!;
//            var key = Guid.NewGuid();
//            var req = BuildActivate(status, recon, key);
//            Assert.True((await cutover.ActivatePreparedItemAsync(req)).IsSuccess);
//            var second = await cutover.ActivatePreparedItemAsync(req);
//            Assert.True(second.IsSuccess, second.Message);
//            Assert.True(second.Data!.WasReplay);
//            Assert.Equal(1, await ctx.InventoryWriterModeTransitions.CountAsync(x =>
//                x.StoreId == StoreId && x.Succeeded && x.ToMode == InventoryWriterMode.PreparedItem));
//        }

//        [Fact]
//        public async Task Cutover_Activate_StaleReconHash_Rejected()
//        {
//            using var ctx = CreateDbContext();
//            await SeedReadyForActivationAsync(ctx);
//            var cutover = CreateCutover(ctx);
//            var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//            var status = (await CreateMode(ctx).GetStatusAsync(StoreId)).Data!;
//            var req = BuildActivate(status, recon);
//            req = new CutoverActivationRequest
//            {
//                StoreId = req.StoreId,
//                RequestKey = req.RequestKey,
//                ExpectedMode = req.ExpectedMode,
//                ExpectedRowVersion = req.ExpectedRowVersion,
//                ExpectedReadinessHash = req.ExpectedReadinessHash,
//                ExpectedReconciliationHash = "deadbeef",
//                ExpectedSchemaContractHash = req.ExpectedSchemaContractHash,
//                MaintenanceWindowAcknowledged = true,
//                Reason = req.Reason,
//                ActorAccountId = req.ActorAccountId
//            };
//            var result = await cutover.ActivatePreparedItemAsync(req);
//            Assert.False(result.IsSuccess);
//            Assert.Equal(CutoverFailureCodes.StaleReconciliationHash, result.ErrorCode);
//            Assert.Equal(InventoryWriterMode.Blocked,
//                (await ctx.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId)).WriterMode);
//        }

//        [Fact]
//        public async Task Cutover_Activate_StoreManager_Rejected()
//        {
//            using var ctx = CreateDbContext();
//            await SeedReadyForActivationAsync(ctx);
//            // Use account without SA/BO role
//            var otherAccount = 99991;
//            ctx.Accounts.Add(new Account
//            {
//                AccountId = otherAccount,
//                Email = "mgr@test.local",
//                PasswordHash = "x",
//                Active = true,
//                CreatedAt = DateTime.UtcNow
//            });
//            await ctx.SaveChangesAsync();
//            var cutover = CreateCutover(ctx);
//            var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//            var status = (await CreateMode(ctx).GetStatusAsync(StoreId)).Data!;
//            var req = BuildActivate(status, recon);
//            req = new CutoverActivationRequest
//            {
//                StoreId = req.StoreId,
//                RequestKey = req.RequestKey,
//                ExpectedMode = req.ExpectedMode,
//                ExpectedRowVersion = req.ExpectedRowVersion,
//                ExpectedReadinessHash = req.ExpectedReadinessHash,
//                ExpectedReconciliationHash = req.ExpectedReconciliationHash,
//                ExpectedSchemaContractHash = req.ExpectedSchemaContractHash,
//                MaintenanceWindowAcknowledged = true,
//                Reason = req.Reason,
//                ActorAccountId = otherAccount
//            };
//            var result = await cutover.ActivatePreparedItemAsync(req);
//            Assert.False(result.IsSuccess);
//            Assert.Equal(CutoverFailureCodes.Unauthorized, result.ErrorCode);
//        }

//        [Fact]
//        public async Task Cutover_PreparedItem_ToBlocked_NoQuantityMutation()
//        {
//            using var ctx = CreateDbContext();
//            await SeedReadyForActivationAsync(ctx);
//            var cutover = CreateCutover(ctx);
//            var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//            var status = (await CreateMode(ctx).GetStatusAsync(StoreId)).Data!;
//            Assert.True((await cutover.ActivatePreparedItemAsync(BuildActivate(status, recon))).IsSuccess);
//            var qty = (await ctx.StoreInventories.SingleAsync(x => x.StoreId == StoreId)).AvailableQty;
//            status = (await CreateMode(ctx).GetStatusAsync(StoreId)).Data!;
//            var block = await cutover.RollbackToBlockedAsync(
//                StoreId, status.RowVersion, status.WriterMode, "incident", AccountId);
//            Assert.True(block.IsSuccess, block.Message);
//            Assert.Equal(InventoryWriterMode.Blocked,
//                (await ctx.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId)).WriterMode);
//            Assert.Equal(qty, (await ctx.StoreInventories.SingleAsync(x => x.StoreId == StoreId)).AvailableQty);
//        }

//        [Fact]
//        public async Task Cutover_AfterPreparedItem_CannotReturnToLegacyRecipe()
//        {
//            using var ctx = CreateDbContext();
//            await SeedReadyForActivationAsync(ctx);
//            var cutover = CreateCutover(ctx);
//            var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//            var status = (await CreateMode(ctx).GetStatusAsync(StoreId)).Data!;
//            Assert.True((await cutover.ActivatePreparedItemAsync(BuildActivate(status, recon))).IsSuccess);
//            status = (await CreateMode(ctx).GetStatusAsync(StoreId)).Data!;
//            var mode = CreateMode(ctx);
//            var result = await mode.TransitionAsync(new InventoryWriterModeTransitionRequest
//            {
//                StoreId = StoreId,
//                ExpectedCurrentMode = status.WriterMode,
//                ExpectedRowVersion = status.RowVersion,
//                TargetMode = InventoryWriterMode.LegacyRecipe,
//                Reason = "try downgrade",
//                ActorAccountId = AccountId
//            });
//            Assert.False(result.Succeeded);
//        }

//        [Fact]
//        public async Task GlobalLegacyDisable_BlocksLegacyRecipeWrite()
//        {
//            using var ctx = CreateDbContext();
//            await SeedBaseAsync(ctx, InventoryWriterMode.LegacyRecipe);
//            await ctx.Database.BeginTransactionAsync();
//            var mode = CreateMode(ctx, legacyDisabled: true);
//            var snap = await mode.AcquireSnapshotAsync(StoreId);
//            Assert.True(snap.IsSuccess);
//            var guard = mode.EnsureLegacyBtpWriteAllowed(snap.Data!, StoreId);
//            Assert.False(guard.IsSuccess);
//            Assert.Equal(CutoverFailureCodes.LegacyBtpWritesGloballyDisabled, guard.ErrorCode);
//        }

//        [Fact]
//        public async Task Cutover_Capability_OtherStoreEvidence_DoesNotReadyThisStore()
//        {
//            using var ctx = CreateDbContext();
//            await SeedBaseAsync(ctx);
//            await SeedPiAndRecipeAsync(ctx, true);
//            // evidence for different store
//            var otherStore = 12499;
//            ctx.Stores.Add(new Store
//            {
//                StoreId = otherStore,
//                Name = "Other",
//                Address = "A",
//                Phone = "1",
//                Active = true,
//                CreatedAt = DateTime.UtcNow
//            });
//            await ctx.SaveChangesAsync();
//            await PersistNoOpAsync(ctx, otherStore);
//            var cap = new ConsolidationOrNoopEvidenceCapabilityProvider(ctx);
//            Assert.False((await cap.GetStatusForStoreAsync(StoreId)).Ready);
//        }

//        [Fact]
//        public async Task Graduation_DoesNotAutoCloseUmbrella()
//        {
//            using var ctx = CreateDbContext();
//            await SeedReadyForActivationAsync(ctx);
//            var cutover = CreateCutover(ctx);
//            var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//            var status = (await CreateMode(ctx).GetStatusAsync(StoreId)).Data!;
//            await cutover.ActivatePreparedItemAsync(BuildActivate(status, recon));
//            var g = (await cutover.BuildGraduationSummaryAsync()).Data!;
//            Assert.False(g.EligibleToCloseUmbrella114);
//            Assert.Contains("operator", g.Note, StringComparison.OrdinalIgnoreCase);
//        }

//        [Fact]
//        public void Controller_CutoverActivate_DoesNotMutateInventoryDirectly()
//        {
//            var ctor = typeof(CafeChain.Areas.Admin.Controllers.AdminCutoverController)
//                .GetConstructors().Single();
//            Assert.Contains(ctor.GetParameters(), p => p.ParameterType == typeof(ICutoverReconciliationService));
//            Assert.DoesNotContain(ctor.GetParameters(), p => p.ParameterType == typeof(AppDbContext));
//        }

//        [Fact]
//        public void Controller_Cutover_ExecuteRoles_ExcludeStoreManager()
//        {
//            var field = typeof(CafeChain.Areas.Admin.Controllers.AdminCutoverController)
//                .GetField("ActivateRoles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
//            var roles = (string[])field!.GetValue(null)!;
//            Assert.Contains(RoleConstants.SystemAdmin, roles);
//            Assert.DoesNotContain(RoleConstants.StoreManager, roles);
//        }

//        // ───────── helpers ─────────

//        private static CutoverActivationRequest BuildActivate(
//            InventoryWriterModeStatusDto status,
//            CutoverReconciliationReport recon,
//            Guid? key = null) => new()
//            {
//                StoreId = StoreId,
//                RequestKey = key ?? Guid.NewGuid(),
//                TargetMode = InventoryWriterMode.PreparedItem,
//                ExpectedMode = status.WriterMode,
//                ExpectedRowVersion = status.RowVersion,
//                ExpectedReadinessHash = recon.ReadinessHash,
//                ExpectedReconciliationHash = recon.ReconciliationHash,
//                ExpectedSchemaContractHash = recon.Schema.ContractHash,
//                MaintenanceWindowAcknowledged = true,
//                Reason = "Cutover activation maintenance window acknowledged.",
//                ActorAccountId = AccountId
//            };

//        private async Task SeedReadyForActivationAsync(AppDbContext ctx)
//        {
//            await SeedCleanPreparedStoreAsync(ctx, mode: InventoryWriterMode.Blocked);
//            await PersistNoOpAsync(ctx);
//        }

//        private async Task SeedCleanPreparedStoreAsync(AppDbContext ctx, InventoryWriterMode mode = InventoryWriterMode.Blocked)
//        {
//            await SeedBaseAsync(ctx, mode);
//            await SeedPiAndRecipeAsync(ctx, mapped: true);
//            ctx.StoreInventories.Add(MakeCanonical(10m));
//            await ctx.SaveChangesAsync();
//        }

//        private async Task PersistNoOpAsync(AppDbContext ctx, int? storeId = null)
//        {
//            var sid = storeId ?? StoreId;
//            var cons = new LegacyBtpConsolidationService(
//                ctx, new TestHostEnvironment(), NullLogger<LegacyBtpConsolidationService>.Instance);
//            var audit = (await cons.AuditStoreAsync(sid)).Data!;
//            var result = await cons.CreateNoOpEvidenceAsync(new ConsolidationNoOpRequest
//            {
//                StoreId = sid,
//                RequestKey = Guid.NewGuid(),
//                RequestedByStaffId = StaffId,
//                ApprovedByStaffId = StaffId,
//                ExplicitApproval = true,
//                ExpectedAuditHash = audit.AuditHash
//            });
//            Assert.True(result.IsSuccess, result.Message);
//        }

//        private async Task SeedBaseAsync(AppDbContext ctx, InventoryWriterMode mode = InventoryWriterMode.Blocked)
//        {
//            if (!await ctx.Stores.AnyAsync(x => x.StoreId == StoreId))
//            {
//                ctx.Stores.Add(new Store
//                {
//                    StoreId = StoreId,
//                    Name = "S124",
//                    Address = "A",
//                    Phone = "1",
//                    Active = true,
//                    CreatedAt = DateTime.UtcNow
//                });
//            }

//            if (!await ctx.Roles.AnyAsync(r => r.Name == RoleConstants.SystemAdmin))
//            {
//                ctx.Roles.Add(new Role
//                {
//                    RoleId = 12490,
//                    Name = RoleConstants.SystemAdmin,
//                    Active = true,
//                    IsStoreLevel = false,
//                    CreatedAt = DateTime.UtcNow
//                });
//            }

//            if (!await ctx.Accounts.AnyAsync(a => a.AccountId == AccountId))
//            {
//                ctx.Accounts.Add(new Account
//                {
//                    AccountId = AccountId,
//                    Email = "sa124@test.local",
//                    PasswordHash = "x",
//                    Active = true,
//                    CreatedAt = DateTime.UtcNow
//                });
//                var role = await ctx.Roles.FirstAsync(r => r.Name == RoleConstants.SystemAdmin);
//                ctx.AccountRoles.Add(new AccountRole { AccountId = AccountId, RoleId = role.RoleId });
//            }

//            if (!await ctx.Staffs.AnyAsync(s => s.StaffId == StaffId))
//            {
//                ctx.Staffs.Add(new Staff
//                {
//                    StaffId = StaffId,
//                    AccountId = AccountId,
//                    FullName = "SA124",
//                    StoreId = StoreId,
//                    Active = true,
//                    CreatedAt = DateTime.UtcNow,
//                });
//            }

//            var cfg = await ctx.StoreInventoryWriterConfigurations.FirstOrDefaultAsync(x => x.StoreId == StoreId);
//            if (cfg == null)
//            {
//                ctx.StoreInventoryWriterConfigurations.Add(new StoreInventoryWriterConfiguration
//                {
//                    StoreId = StoreId,
//                    WriterMode = mode,
//                    HasEverActivatedPreparedItem = false,
//                    CreatedAt = DateTime.UtcNow,
//                    UpdatedAt = DateTime.UtcNow,
//                    RowVersion = new byte[] { 0 }
//                });
//            }
//            else
//            {
//                cfg.WriterMode = mode;
//            }

//            await ctx.SaveChangesAsync();
//        }

//        private async Task SeedPiAndRecipeAsync(AppDbContext ctx, bool mapped)
//        {
//            if (!await ctx.PreparedItems.AnyAsync(p => p.PreparedItemId == PreparedItemId))
//            {
//                ctx.PreparedItems.Add(new PreparedItem
//                {
//                    PreparedItemId = PreparedItemId,
//                    Code = "PI124",
//                    Name = "Syrup",
//                    BaseUnitId = UnitMl,
//                    Active = true
//                });
//            }

//            if (!await ctx.Recipes.AnyAsync(r => r.RecipeId == RecipeId))
//            {
//                ctx.Recipes.Add(new Recipe
//                {
//                    RecipeId = RecipeId,
//                    RecipeCode = "RCP124",
//                    Name = "R124",
//                    Active = true,
//                    Status = "Active",
//                    PreparedItemId = mapped ? PreparedItemId : null,
//                    OutputQuantity = mapped ? 1m : null,
//                    OutputUnitId = mapped ? UnitMl : null
//                });
//            }

//            await ctx.SaveChangesAsync();
//        }

//        private static StoreInventory MakeCanonical(
//            decimal qty,
//            InventoryQuantitySemanticsStatus semantics = InventoryQuantitySemanticsStatus.BaseUnitConfirmed)
//            => new()
//            {
//                StoreId = StoreId,
//                PreparedItemId = PreparedItemId,
//                BtpIdentityState = BtpIdentityState.Canonical,
//                QuantitySemanticsStatus = semantics,
//                QuantitySemanticsEvidenceType = semantics == InventoryQuantitySemanticsStatus.BaseUnitConfirmed
//                    ? QuantitySemanticsEvidenceType.SystemCanonicalCreation
//                    : null,
//                QuantitySemanticsEvidenceReference = semantics == InventoryQuantitySemanticsStatus.BaseUnitConfirmed ? "canon" : null,
//                QuantitySemanticsReviewedAt = semantics == InventoryQuantitySemanticsStatus.BaseUnitConfirmed ? DateTime.UtcNow : null,
//                QuantitySemanticsReviewedByAccountId = semantics == InventoryQuantitySemanticsStatus.BaseUnitConfirmed ? 1 : null,
//                AvailableQty = qty,
//                ReservedQty = 0,
//                MinStockLevel = 1,
//                LastUpdated = DateTime.UtcNow,
//                RowVersion = new byte[] { 0 }
//            };

//        private sealed class TestHostEnvironment : IHostEnvironment
//        {
//            public string EnvironmentName { get; set; } = "Development";
//            public string ApplicationName { get; set; } = "CafeChain.Tests";
//            public string ContentRootPath { get; set; } = ".";
//            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
//        }
//    }
//}
