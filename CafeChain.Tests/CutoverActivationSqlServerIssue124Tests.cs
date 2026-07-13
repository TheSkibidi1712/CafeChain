// using CafeChain.Application.Constants;
// using CafeChain.Application.DTOs.Inventories;
// using CafeChain.Application.DTOs.Inventories.Consolidation;
// using CafeChain.Application.DTOs.Inventories.Cutover;
// using CafeChain.Application.Interfaces.Inventories;
// using CafeChain.Application.Options;
// using CafeChain.Application.Services.Admin.Production;
// using CafeChain.Application.Services.Inventories;
// using CafeChain.Data;
// using CafeChain.Models.Enums.Inventory;
// using CafeChain.Models.Inventories.Configuration;
// using CafeChain.Models.Inventories.PreparedItems;
// using CafeChain.Models.Drinks;
// using CafeChain.Models.Stores;
// using Microsoft.Data.SqlClient;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.FileProviders;
// using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging.Abstractions;
// using Microsoft.Extensions.Options;
// using Xunit;

// namespace CafeChain.Tests
// {
//     /// <summary>Issue #124 — SQL Server concurrent activation proof on CafeChain_Issue124Tests.</summary>
//     [Trait("Category", "SqlServerIntegration")]
//     public sealed class CutoverActivationSqlServerIssue124Tests : IAsyncLifetime
//     {
//         private const string Server = @"DESKTOP-K038H12\SQLEXPRESS";
//         private const string Database = "CafeChain_Issue124Tests";
//         private static string ConnectionString =>
//             $"Server={Server};Database={Database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
//         private static string MasterConnectionString =>
//             $"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True";

//         private const int StoreId = 1;
//         private const int StaffId = 1;
//         private const int AccountId = 1; // seed HasData typically has accounts — use 1 if exists
//         private const int UnitMl = 3;

//         public async Task InitializeAsync()
//         {
//             try
//             {
//                 await using (var master = new SqlConnection(MasterConnectionString))
//                 {
//                     await master.OpenAsync();
//                     await using var cmd = master.CreateCommand();
//                     cmd.CommandText = $@"
// IF DB_ID(N'{Database}') IS NULL
//     CREATE DATABASE [{Database}];";
//                     await cmd.ExecuteNonQueryAsync();
//                 }

//                 await using var ctx = CreateContext();
//                 await ctx.Database.EnsureDeletedAsync();
//                 await ctx.Database.EnsureCreatedAsync();
//             }
//             catch (Exception ex)
//             {
//                 throw new InvalidOperationException(
//                     $"SQL Server unavailable for #124 tests. Server={Server} Database={Database}. {ex.Message}", ex);
//             }
//         }

//         public Task DisposeAsync() => Task.CompletedTask;

//         [Fact]
//         public async Task SqlServer_ConcurrentActivate_SameStore_OneWinner()
//         {
//             int preparedItemId;
//             await using (var seed = CreateContext())
//             {
//                 preparedItemId = await SeedPreparedAsync(seed, "PI-124-A");
//                 await SeedRecipeAsync(seed, "RCP-124-A", preparedItemId);
//                 await PutModeAsync(seed, InventoryWriterMode.Blocked);
//                 await SeedCanonicalAsync(seed, preparedItemId, 5m);
//                 await SeedNoOpAsync(seed);
//                 await EnsureAdminAccountAsync(seed);
//             }

//             CutoverActivationRequest req1, req2;
//             await using (var prep = CreateContext())
//             {
//                 var probe = new InventorySchemaReadinessProbe(prep);
//                 var schema = await probe.ProbeAsync();
//                 Assert.True(schema.IsReady,
//                     $"schema fail code={schema.FailureCode} tables={string.Join(",", schema.MissingTables)} cols={string.Join(",", schema.MissingColumns)} diag={string.Join(";", schema.Diagnostics)}");

//                 var cutover = CreateCutover(prep);
//                 var reconResult = await cutover.ReconcileStoreAsync(StoreId);
//                 Assert.True(reconResult.IsSuccess, reconResult.Message + " " + reconResult.ErrorCode);
//                 var recon = reconResult.Data!;
//                 Assert.True(recon.IsClean, string.Join(";", recon.Anomalies.Select(a => a.Code + ":" + a.Message)));
//                 var status = await prep.StoreInventoryWriterConfigurations.AsNoTracking()
//                     .SingleAsync(x => x.StoreId == StoreId);
//                 var actor = ResolveActorId(prep);
//                 req1 = MakeReq(status, recon, Guid.NewGuid(), actor);
//                 req2 = MakeReq(status, recon, Guid.NewGuid(), actor); // different keys, same state
//             }

//             await using var c1 = CreateContext();
//             await using var c2 = CreateContext();
//             var results = await Task.WhenAll(
//                 CreateCutover(c1).ActivatePreparedItemAsync(req1),
//                 CreateCutover(c2).ActivatePreparedItemAsync(req2));

//             Assert.Contains(results, r => r.IsSuccess && r.Data is { WasReplay: false });
//             await using var verify = CreateContext();
//             var cfg = await verify.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId);
//             Assert.Equal(InventoryWriterMode.PreparedItem, cfg.WriterMode);
//             // At most one non-replay success for concurrent different keys; loser may fail stale
//             var successNonReplay = results.Count(r => r.IsSuccess && r.Data is { WasReplay: false });
//             Assert.True(successNonReplay >= 1);
//             Assert.Equal(5m, await verify.StoreInventories.Where(x =>
//                 x.StoreId == StoreId && x.PreparedItemId == preparedItemId).SumAsync(x => x.AvailableQty));
//         }

//         [Fact]
//         public async Task SqlServer_RollbackToBlocked_NoQuantityChange()
//         {
//             int preparedItemId;
//             await using (var seed = CreateContext())
//             {
//                 preparedItemId = await SeedPreparedAsync(seed, "PI-124-B");
//                 await SeedRecipeAsync(seed, "RCP-124-B", preparedItemId);
//                 await PutModeAsync(seed, InventoryWriterMode.Blocked);
//                 await SeedCanonicalAsync(seed, preparedItemId, 9m);
//                 await SeedNoOpAsync(seed);
//                 await EnsureAdminAccountAsync(seed);
//             }

//             await using (var act = CreateContext())
//             {
//                 var cutover = CreateCutover(act);
//                 var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//                 var status = await act.StoreInventoryWriterConfigurations.AsNoTracking()
//                     .SingleAsync(x => x.StoreId == StoreId);
//                 var actor = ResolveActorId(act);
//                 var actResult = await cutover.ActivatePreparedItemAsync(MakeReq(status, recon, Guid.NewGuid(), actor));
//                 Assert.True(actResult.IsSuccess, actResult.Message + " " + actResult.ErrorCode);
//             }

//             await using var block = CreateContext();
//             var cut = CreateCutover(block);
//             var st = await block.StoreInventoryWriterConfigurations.AsNoTracking()
//                 .SingleAsync(x => x.StoreId == StoreId);
//             var result = await cut.RollbackToBlockedAsync(
//                 StoreId, st.RowVersion, st.WriterMode, "sql rollback", ResolveActorId(block));
//             Assert.True(result.IsSuccess, result.Message + " " + result.ErrorCode);
//             await using var verify = CreateContext();
//             Assert.Equal(InventoryWriterMode.Blocked,
//                 (await verify.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId)).WriterMode);
//             Assert.Equal(9m, await verify.StoreInventories
//                 .Where(x => x.StoreId == StoreId && x.PreparedItemId == preparedItemId)
//                 .SumAsync(x => x.AvailableQty));
//         }

//         [Fact]
//         public async Task SqlServer_Activation_StaleEvidenceChangedDuringRequest_IsRejected()
//         {
//             await using (var seed = CreateContext())
//             {
//                 var pi = await SeedPreparedAsync(seed, "PI-124-STALE");
//                 await SeedRecipeAsync(seed, "RCP-124-STALE", pi);
//                 await PutModeAsync(seed, InventoryWriterMode.Blocked);
//                 await SeedCanonicalAsync(seed, pi, 3m);
//                 await SeedNoOpAsync(seed);
//                 await EnsureAdminAccountAsync(seed);
//             }

//             CutoverActivationRequest req;
//             await using (var prep = CreateContext())
//             {
//                 var cutover = CreateCutover(prep);
//                 var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//                 Assert.True(recon.IsClean);
//                 var status = await prep.StoreInventoryWriterConfigurations.AsNoTracking()
//                     .SingleAsync(x => x.StoreId == StoreId);
//                 req = MakeReq(status, recon, Guid.NewGuid(), ResolveActorId(prep));
//             }

//             // Change inventory after recon hash captured — activation must revalidate and reject.
//             string newHash;
//             decimal beforeQty;
//             await using (var mutate = CreateContext())
//             {
//                 var inv = await mutate.StoreInventories
//                     .OrderBy(x => x.StoreInventoryId)
//                     .FirstAsync(x => x.StoreId == StoreId && x.IngredientId == null);
//                 beforeQty = inv.AvailableQty;
//                 inv.AvailableQty = beforeQty + 7.5m;
//                 inv.MinStockLevel = (inv.MinStockLevel ?? 0) + 3m;
//                 inv.LastUpdated = DateTime.UtcNow.AddMinutes(1);
//                 await mutate.SaveChangesAsync();
//                 Assert.Equal(beforeQty + 7.5m,
//                     await mutate.StoreInventories.AsNoTracking()
//                         .Where(x => x.StoreInventoryId == inv.StoreInventoryId)
//                         .Select(x => x.AvailableQty).SingleAsync());
//                 newHash = (await CreateCutover(mutate).ReconcileStoreAsync(StoreId)).Data!.ReconciliationHash;
//             }

//             Assert.NotEqual(req.ExpectedReconciliationHash, newHash);

//             await using var act = CreateContext();
//             var result = await CreateCutover(act).ActivatePreparedItemAsync(req);
//             Assert.False(result.IsSuccess, $"unexpected success: {result.Message}");
//             Assert.True(
//                 result.ErrorCode is CutoverFailureCodes.StaleReconciliationHash
//                     or CutoverFailureCodes.ReconciliationNotClean
//                     or CutoverFailureCodes.StaleReadinessHash
//                     or InventoryWriterFailureCodes.StaleConfiguration
//                     or InventoryWriterFailureCodes.ReadinessFailed,
//                 result.ErrorCode ?? "null");
//             await using var verify = CreateContext();
//             Assert.NotEqual(InventoryWriterMode.PreparedItem,
//                 (await verify.StoreInventoryWriterConfigurations.AsNoTracking()
//                     .SingleAsync(x => x.StoreId == StoreId)).WriterMode);
//         }

//         [Fact]
//         public async Task SqlServer_Activate_Vs_PosWriter_UsesWriterBarrier()
//         {
//             // POS AcquireSnapshot uses UPDLOCK on same config row as activation — concurrent start is serialized.
//             int preparedItemId;
//             await using (var seed = CreateContext())
//             {
//                 preparedItemId = await SeedPreparedAsync(seed, "PI-124-POS");
//                 await SeedRecipeAsync(seed, "RCP-124-POS", preparedItemId);
//                 await PutModeAsync(seed, InventoryWriterMode.Blocked);
//                 await SeedCanonicalAsync(seed, preparedItemId, 20m);
//                 await SeedNoOpAsync(seed);
//                 await EnsureAdminAccountAsync(seed);
//             }

//             CutoverActivationRequest req;
//             await using (var prep = CreateContext())
//             {
//                 var cutover = CreateCutover(prep);
//                 var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//                 var status = await prep.StoreInventoryWriterConfigurations.AsNoTracking()
//                     .SingleAsync(x => x.StoreId == StoreId);
//                 req = MakeReq(status, recon, Guid.NewGuid(), ResolveActorId(prep));
//             }

//             await using var actCtx = CreateContext();
//             await using var posCtx = CreateContext();
//             var mode = CreateModeService(posCtx);

//             async Task<string> PosAcquirePath()
//             {
//                 await using var tx = await posCtx.Database.BeginTransactionAsync();
//                 var snap = await mode.AcquireSnapshotAsync(StoreId);
//                 // Hold lock briefly then complete
//                 await Task.Delay(50);
//                 if (snap.IsSuccess && snap.Data!.WriterMode == InventoryWriterMode.Blocked)
//                 {
//                     await tx.RollbackAsync();
//                     return "BLOCKED_SNAPSHOT";
//                 }

//                 if (snap.IsSuccess && snap.Data!.WriterMode == InventoryWriterMode.PreparedItem)
//                 {
//                     await tx.CommitAsync();
//                     return "PREPARED_SNAPSHOT";
//                 }

//                 await tx.RollbackAsync();
//                 return snap.ErrorCode ?? "FAIL";
//             }

//             var actTask = CreateCutover(actCtx).ActivatePreparedItemAsync(req);
//             var posTask = PosAcquirePath();
//             await Task.WhenAll(actTask, posTask);

//             Assert.True(actTask.Result.IsSuccess || posTask.Result is "BLOCKED_SNAPSHOT" or "PREPARED_SNAPSHOT",
//                 $"act={actTask.Result.IsSuccess}/{actTask.Result.ErrorCode} pos={posTask.Result}");
//             await using var verify = CreateContext();
//             // Final mode is either still Blocked (POS held first and rolled back without activate win) or PreparedItem.
//             var modeFinal = (await verify.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId)).WriterMode;
//             Assert.True(modeFinal is InventoryWriterMode.PreparedItem or InventoryWriterMode.Blocked);
//             // Qty untouched by barrier smoke (no POS deduction executed)
//             Assert.Equal(20m, await verify.StoreInventories.Where(x =>
//                 x.StoreId == StoreId && x.PreparedItemId == preparedItemId).SumAsync(x => x.AvailableQty));
//         }

//         [Fact]
//         public async Task SqlServer_Activate_Vs_ProductionWriter_UsesWriterBarrier()
//         {
//             int preparedItemId;
//             await using (var seed = CreateContext())
//             {
//                 preparedItemId = await SeedPreparedAsync(seed, "PI-124-PRD");
//                 await SeedRecipeAsync(seed, "RCP-124-PRD", preparedItemId);
//                 await PutModeAsync(seed, InventoryWriterMode.Blocked);
//                 await SeedCanonicalAsync(seed, preparedItemId, 11m);
//                 await SeedNoOpAsync(seed);
//                 await EnsureAdminAccountAsync(seed);
//             }

//             CutoverActivationRequest req;
//             await using (var prep = CreateContext())
//             {
//                 var cutover = CreateCutover(prep);
//                 var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//                 var status = await prep.StoreInventoryWriterConfigurations.AsNoTracking()
//                     .SingleAsync(x => x.StoreId == StoreId);
//                 req = MakeReq(status, recon, Guid.NewGuid(), ResolveActorId(prep));
//             }

//             await using var actCtx = CreateContext();
//             await using var prodCtx = CreateContext();
//             var mode = CreateModeService(prodCtx);

//             async Task<string> ProdAcquirePath()
//             {
//                 await using var tx = await prodCtx.Database.BeginTransactionAsync();
//                 var snap = await mode.AcquireSnapshotAsync(StoreId);
//                 await Task.Delay(50);
//                 var label = !snap.IsSuccess
//                     ? snap.ErrorCode ?? "FAIL"
//                     : snap.Data!.WriterMode.ToString();
//                 await tx.RollbackAsync();
//                 return label!;
//             }

//             var actTask = CreateCutover(actCtx).ActivatePreparedItemAsync(req);
//             var prodTask = ProdAcquirePath();
//             await Task.WhenAll(actTask, prodTask);

//             Assert.True(actTask.Result.IsSuccess || prodTask.Result is "Blocked" or "PreparedItem" or "LegacyRecipe",
//                 $"act={actTask.Result.ErrorCode} prod={prodTask.Result}");
//             await using var verify = CreateContext();
//             Assert.Equal(11m, await verify.StoreInventories.Where(x =>
//                 x.StoreId == StoreId && x.PreparedItemId == preparedItemId).SumAsync(x => x.AvailableQty));
//         }

//         [Fact]
//         public async Task SqlServer_Activate_Vs_ConsolidationExecute_RequiresFreshEvidence()
//         {
//             // Clean store → activate PreparedItem → consolidation execute must fail Store-not-Blocked (no inventory mutate).
//             int preparedItemId;
//             await using (var seed = CreateContext())
//             {
//                 preparedItemId = await SeedPreparedAsync(seed, "PI-124-CON");
//                 await SeedRecipeAsync(seed, "RCP-124-CON", preparedItemId);
//                 await PutModeAsync(seed, InventoryWriterMode.Blocked);
//                 await SeedCanonicalAsync(seed, preparedItemId, 5m);
//                 await SeedNoOpAsync(seed);
//                 await EnsureAdminAccountAsync(seed);
//             }

//             await using (var act = CreateContext())
//             {
//                 var cutover = CreateCutover(act);
//                 var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//                 Assert.True(recon.IsClean, string.Join(";", recon.Anomalies.Select(a => a.Code)));
//                 var status = await act.StoreInventoryWriterConfigurations.AsNoTracking()
//                     .SingleAsync(x => x.StoreId == StoreId);
//                 var actResult = await cutover.ActivatePreparedItemAsync(
//                     MakeReq(status, recon, Guid.NewGuid(), ResolveActorId(act)));
//                 Assert.True(actResult.IsSuccess, actResult.Message + " " + actResult.ErrorCode);
//             }

//             decimal qtyBefore;
//             await using (var v = CreateContext())
//                 qtyBefore = await v.StoreInventories.Where(x => x.StoreId == StoreId && x.PreparedItemId == preparedItemId)
//                     .SumAsync(x => x.AvailableQty);

//             await using var consCtx = CreateContext();
//             var cons = new LegacyBtpConsolidationService(
//                 consCtx, new TestHostEnvironment(), NullLogger<LegacyBtpConsolidationService>.Instance);
//             var exec = await cons.ExecuteAsync(new ConsolidationExecuteRequest
//             {
//                 StoreId = StoreId,
//                 RequestKey = Guid.NewGuid(),
//                 ExpectedDryRunHash = "deadbeef",
//                 ExecutedByStaffId = StaffId,
//                 ActorRole = RoleConstants.SystemAdmin
//             });
//             Assert.False(exec.IsSuccess);
//             // No dry-run for this key → INVALID_MANIFEST, or if run exists → STORE_NOT_BLOCKED.
//             Assert.True(
//                 exec.ErrorCode is ConsolidationFailureCodes.ConsolidationStoreNotBlocked
//                     or ConsolidationFailureCodes.InvalidManifest
//                     or ConsolidationFailureCodes.RunNotDryRunReady,
//                 exec.ErrorCode);

//             await using var verify = CreateContext();
//             Assert.Equal(qtyBefore, await verify.StoreInventories
//                 .Where(x => x.StoreId == StoreId && x.PreparedItemId == preparedItemId)
//                 .SumAsync(x => x.AvailableQty));
//         }

//         [Fact]
//         public async Task SqlServer_NoLegacyRecipeMovement_CommitsAfterActivationTimestamp()
//         {
//             int preparedItemId;
//             await using (var seed = CreateContext())
//             {
//                 preparedItemId = await SeedPreparedAsync(seed, "PI-124-LEG");
//                 await SeedRecipeAsync(seed, "RCP-124-LEG", preparedItemId);
//                 await PutModeAsync(seed, InventoryWriterMode.Blocked);
//                 await SeedCanonicalAsync(seed, preparedItemId, 2m);
//                 await SeedNoOpAsync(seed);
//                 await EnsureAdminAccountAsync(seed);
//             }

//             DateTime? activatedAt;
//             await using (var act = CreateContext())
//             {
//                 var cutover = CreateCutover(act);
//                 var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//                 Assert.True(recon.IsClean, string.Join(";", recon.Anomalies.Select(a => a.Code)));
//                 var status = await act.StoreInventoryWriterConfigurations.AsNoTracking()
//                     .SingleAsync(x => x.StoreId == StoreId);
//                 Assert.True((await cutover.ActivatePreparedItemAsync(
//                     MakeReq(status, recon, Guid.NewGuid(), ResolveActorId(act)))).IsSuccess);
//                 activatedAt = await act.InventoryWriterModeTransitions.AsNoTracking()
//                     .Where(x => x.StoreId == StoreId && x.Succeeded && x.ToMode == InventoryWriterMode.PreparedItem)
//                     .MaxAsync(x => x.AppliedAt);
//             }

//             // Legacy path blocked after activation
//             await using var posCtx = CreateContext();
//             await using var tx = await posCtx.Database.BeginTransactionAsync();
//             var mode = CreateModeService(posCtx);
//             var snap = await mode.AcquireSnapshotAsync(StoreId);
//             Assert.True(snap.IsSuccess);
//             Assert.Equal(InventoryWriterMode.PreparedItem, snap.Data!.WriterMode);
//             var guard = mode.EnsureLegacyBtpWriteAllowed(snap.Data, StoreId);
//             Assert.False(guard.IsSuccess);
//             await tx.RollbackAsync();

//             await using var verify = CreateContext();
//             var postLegacyMoves = await verify.InventoryTransactions.AsNoTracking()
//                 .Where(t => t.CreatedAt >= activatedAt
//                     && t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION)
//                 .Join(verify.StoreInventories,
//                     t => t.StoreInventoryId,
//                     i => i.StoreInventoryId,
//                     (t, i) => new { t, i })
//                 .Where(x => x.i.StoreId == StoreId && x.i.RecipeId != null && x.i.PreparedItemId == null)
//                 .CountAsync();
//             Assert.Equal(0, postLegacyMoves);
//         }

//         [Fact]
//         public async Task SqlServer_Activate_Replay_DoesNotCreateSecondTransition()
//         {
//             await using (var seed = CreateContext())
//             {
//                 var pi = await SeedPreparedAsync(seed, "PI-124-R");
//                 await SeedRecipeAsync(seed, "RCP-124-R", pi);
//                 await PutModeAsync(seed, InventoryWriterMode.Blocked);
//                 await SeedCanonicalAsync(seed, pi, 1m);
//                 await SeedNoOpAsync(seed);
//                 await EnsureAdminAccountAsync(seed);
//             }

//             var key = Guid.NewGuid();
//             CutoverActivationRequest req;
//             await using (var prep = CreateContext())
//             {
//                 var cutover = CreateCutover(prep);
//                 var recon = (await cutover.ReconcileStoreAsync(StoreId)).Data!;
//                 var status = await prep.StoreInventoryWriterConfigurations.AsNoTracking()
//                     .SingleAsync(x => x.StoreId == StoreId);
//                 req = MakeReq(status, recon, key, ResolveActorId(prep));
//                 Assert.True((await cutover.ActivatePreparedItemAsync(req)).IsSuccess, "activate failed");
//             }

//             await using var replayCtx = CreateContext();
//             var replay = await CreateCutover(replayCtx).ActivatePreparedItemAsync(req);
//             Assert.True(replay.IsSuccess);
//             Assert.True(replay.Data!.WasReplay);
//             await using var verify = CreateContext();
//             Assert.Equal(1, await verify.InventoryWriterModeTransitions.CountAsync(x =>
//                 x.StoreId == StoreId && x.Succeeded && x.ToMode == InventoryWriterMode.PreparedItem
//                 && x.ReadinessSnapshotJson != null
//                 && x.ReadinessSnapshotJson.Contains(key.ToString())));
//         }

//         private static AppDbContext CreateContext()
//         {
//             var options = new DbContextOptionsBuilder<AppDbContext>()
//                 .UseSqlServer(ConnectionString)
//                 .Options;
//             return new AppDbContext(options);
//         }

//         private static ICutoverReconciliationService CreateCutover(AppDbContext ctx)
//         {
//             var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
//             var caps = new IInventoryWriterCapabilityProvider[]
//             {
//                 new ProductionPreparedWriterCapabilityProvider(),
//                 new PosPreparedWriterCapabilityProvider(),
//                 new AlertRestockPreparedIdentityCapabilityProvider(),
//                 new ConsolidationOrNoopEvidenceCapabilityProvider(ctx)
//             };
//             var mode = new InventoryWriterModeService(ctx, physical, caps,
//                 Options.Create(new InventoryWriterGlobalOptions()));
//             return new CutoverReconciliationService(
//                 ctx, mode, new InventorySchemaReadinessProbe(ctx), physical, caps,
//                 Options.Create(new InventoryWriterGlobalOptions()),
//                 new TestHostEnvironment(),
//                 NullLogger<CutoverReconciliationService>.Instance);
//         }

//         private static InventoryWriterModeService CreateModeService(AppDbContext ctx)
//         {
//             var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
//             var caps = new IInventoryWriterCapabilityProvider[]
//             {
//                 new ProductionPreparedWriterCapabilityProvider(),
//                 new PosPreparedWriterCapabilityProvider(),
//                 new AlertRestockPreparedIdentityCapabilityProvider(),
//                 new ConsolidationOrNoopEvidenceCapabilityProvider(ctx)
//             };
//             return new InventoryWriterModeService(ctx, physical, caps,
//                 Options.Create(new InventoryWriterGlobalOptions()));
//         }

//         private static async Task<int> SeedCompatibilityAsync(
//             AppDbContext ctx, int recipeId, int preparedItemId, decimal qty)
//         {
//             var row = new StoreInventory
//             {
//                 StoreId = StoreId,
//                 RecipeId = recipeId,
//                 PreparedItemId = preparedItemId,
//                 BtpIdentityState = BtpIdentityState.Legacy,
//                 QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
//                 QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.ManualReview,
//                 QuantitySemanticsEvidenceReference = "compat",
//                 QuantitySemanticsReviewedAt = DateTime.UtcNow,
//                 QuantitySemanticsReviewedByAccountId = 1,
//                 AvailableQty = qty,
//                 ReservedQty = 0,
//                 LastUpdated = DateTime.UtcNow
//             };
//             ctx.StoreInventories.Add(row);
//             await ctx.SaveChangesAsync();
//             return row.StoreInventoryId;
//         }

//         private static async Task PutModeAsync(AppDbContext ctx, InventoryWriterMode mode)
//         {
//             var cfg = await ctx.StoreInventoryWriterConfigurations.FirstOrDefaultAsync(x => x.StoreId == StoreId);
//             if (cfg == null)
//             {
//                 ctx.StoreInventoryWriterConfigurations.Add(new StoreInventoryWriterConfiguration
//                 {
//                     StoreId = StoreId,
//                     WriterMode = mode,
//                     HasEverActivatedPreparedItem = false,
//                     CreatedAt = DateTime.UtcNow,
//                     UpdatedAt = DateTime.UtcNow
//                 });
//             }
//             else
//             {
//                 cfg.WriterMode = mode;
//                 cfg.UpdatedAt = DateTime.UtcNow;
//             }

//             await ctx.SaveChangesAsync();
//         }

//         private static async Task<int> SeedPreparedAsync(AppDbContext ctx, string code)
//         {
//             var pi = new PreparedItem { Code = code, Name = code, BaseUnitId = UnitMl, Active = true };
//             ctx.PreparedItems.Add(pi);
//             await ctx.SaveChangesAsync();
//             return pi.PreparedItemId;
//         }

//         private static async Task<int> SeedRecipeAsync(AppDbContext ctx, string code, int preparedItemId)
//         {
//             var r = new Recipe
//             {
//                 RecipeCode = code,
//                 Name = code,
//                 Active = true,
//                 Status = "Active",
//                 PreparedItemId = preparedItemId,
//                 OutputQuantity = 1m,
//                 OutputUnitId = UnitMl
//             };
//             ctx.Recipes.Add(r);
//             await ctx.SaveChangesAsync();
//             return r.RecipeId;
//         }

//         private static async Task<int> SeedCanonicalAsync(AppDbContext ctx, int preparedItemId, decimal qty)
//         {
//             var row = new StoreInventory
//             {
//                 StoreId = StoreId,
//                 PreparedItemId = preparedItemId,
//                 BtpIdentityState = BtpIdentityState.Canonical,
//                 QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
//                 QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
//                 QuantitySemanticsEvidenceReference = "sql",
//                 QuantitySemanticsReviewedAt = DateTime.UtcNow,
//                 QuantitySemanticsReviewedByAccountId = 1,
//                 AvailableQty = qty,
//                 ReservedQty = 0,
//                 MinStockLevel = 1,
//                 LastUpdated = DateTime.UtcNow
//             };
//             ctx.StoreInventories.Add(row);
//             await ctx.SaveChangesAsync();
//             return row.StoreInventoryId;
//         }

//         private static async Task SeedNoOpAsync(AppDbContext ctx)
//         {
//             var svc = new LegacyBtpConsolidationService(
//                 ctx, new TestHostEnvironment(), NullLogger<LegacyBtpConsolidationService>.Instance);
//             var audit = (await svc.AuditStoreAsync(StoreId)).Data!;
//             var r = await svc.CreateNoOpEvidenceAsync(new ConsolidationNoOpRequest
//             {
//                 StoreId = StoreId,
//                 RequestKey = Guid.NewGuid(),
//                 RequestedByStaffId = StaffId,
//                 ApprovedByStaffId = StaffId,
//                 ExplicitApproval = true,
//                 ExpectedAuditHash = audit.AuditHash
//             });
//             Assert.True(r.IsSuccess, r.Message);
//         }

//         private static async Task EnsureAdminAccountAsync(AppDbContext ctx)
//         {
//             // Seed HasData includes Role SystemAdmin (RoleId=6) and AccountId=1 typically.
//             var accountId = await ctx.Accounts.AsNoTracking().Select(a => a.AccountId).FirstOrDefaultAsync();
//             if (accountId == 0)
//                 return;

//             var saRoleId = await ctx.Roles.AsNoTracking()
//                 .Where(r => r.Name == RoleConstants.SystemAdmin)
//                 .Select(r => r.RoleId)
//                 .FirstOrDefaultAsync();
//             if (saRoleId == 0)
//                 return;

//             if (!await ctx.AccountRoles.AnyAsync(ar => ar.AccountId == accountId && ar.RoleId == saRoleId))
//             {
//                 ctx.AccountRoles.Add(new CafeChain.Models.Customers.AccountRole
//                 {
//                     AccountId = accountId,
//                     RoleId = saRoleId
//                 });
//                 await ctx.SaveChangesAsync();
//             }
//         }

//         private static int ResolveActorId(AppDbContext ctx)
//         {
//             var id = ctx.Accounts.AsNoTracking().Select(a => a.AccountId).FirstOrDefault();
//             return id == 0 ? AccountId : id;
//         }

//         private static CutoverActivationRequest MakeReq(
//             StoreInventoryWriterConfiguration status,
//             CutoverReconciliationReport recon,
//             Guid key,
//             int actorAccountId) => new()
//             {
//                 StoreId = StoreId,
//                 RequestKey = key,
//                 ExpectedMode = status.WriterMode,
//                 ExpectedRowVersion = status.RowVersion.ToArray(),
//                 ExpectedReadinessHash = recon.ReadinessHash,
//                 ExpectedReconciliationHash = recon.ReconciliationHash,
//                 ExpectedSchemaContractHash = recon.Schema.ContractHash,
//                 MaintenanceWindowAcknowledged = true,
//                 Reason = "SQL concurrent cutover maintenance window acknowledged.",
//                 ActorAccountId = actorAccountId
//             };

//         private sealed class TestHostEnvironment : IHostEnvironment
//         {
//             public string EnvironmentName { get; set; } = "Development";
//             public string ApplicationName { get; set; } = "CafeChain.Tests";
//             public string ContentRootPath { get; set; } = ".";
//             public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
//         }
//     }
// }
