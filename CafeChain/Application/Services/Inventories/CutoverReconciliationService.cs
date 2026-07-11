using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.Inventories.Consolidation;
using CafeChain.Application.DTOs.Inventories.Cutover;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Options;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Inventories.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #124 — read-only cutover reconciliation, PreparedItem activation, Blocked rollback, graduation summary.
    /// Never mutates inventory quantities. Never auto-closes #114.
    /// </summary>
    public sealed class CutoverReconciliationService : ICutoverReconciliationService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        private readonly AppDbContext _context;
        private readonly IInventoryWriterModeService _modeService;
        private readonly IInventorySchemaReadinessProbe _schemaProbe;
        private readonly IPhysicalUnitConversionService _physical;
        private readonly IEnumerable<IInventoryWriterCapabilityProvider> _capabilityProviders;
        private readonly InventoryWriterGlobalOptions _globalOptions;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<CutoverReconciliationService> _logger;

        public CutoverReconciliationService(
            AppDbContext context,
            IInventoryWriterModeService modeService,
            IInventorySchemaReadinessProbe schemaProbe,
            IPhysicalUnitConversionService physical,
            IEnumerable<IInventoryWriterCapabilityProvider> capabilityProviders,
            IOptions<InventoryWriterGlobalOptions> globalOptions,
            IHostEnvironment environment,
            ILogger<CutoverReconciliationService> logger)
        {
            _context = context;
            _modeService = modeService;
            _schemaProbe = schemaProbe;
            _physical = physical;
            _capabilityProviders = capabilityProviders;
            _globalOptions = globalOptions.Value;
            _environment = environment;
            _logger = logger;
        }

        public async Task<ServiceResult<CutoverReconciliationReport>> ReconcileStoreAsync(
            int storeId,
            CancellationToken cancellationToken = default)
        {
            if (!await _context.Stores.AsNoTracking().AnyAsync(x => x.StoreId == storeId, cancellationToken))
            {
                return ServiceResult<CutoverReconciliationReport>.Failure(
                    "Store not found.",
                    errorCode: CutoverFailureCodes.StoreNotFound);
            }

            var report = await BuildReportAsync(storeId, cancellationToken);
            return ServiceResult<CutoverReconciliationReport>.Success(report);
        }

        public async Task<ServiceResult<CutoverActivationResult>> ActivatePreparedItemAsync(
            CutoverActivationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.TargetMode != InventoryWriterMode.PreparedItem)
            {
                return ServiceResult<CutoverActivationResult>.Failure(
                    "TargetMode must be PreparedItem.",
                    errorCode: CutoverFailureCodes.InvalidRequest);
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return ServiceResult<CutoverActivationResult>.Failure(
                    "Reason is required.",
                    errorCode: CutoverFailureCodes.InvalidRequest);
            }

            if (!request.MaintenanceWindowAcknowledged)
            {
                return ServiceResult<CutoverActivationResult>.Failure(
                    "Operator must acknowledge maintenance window / drain.",
                    errorCode: CutoverFailureCodes.MaintenanceWindowRequired);
            }

            if (!await CanActivateAsync(request.ActorAccountId, cancellationToken))
            {
                return ServiceResult<CutoverActivationResult>.Failure(
                    "Only SystemAdmin or BusinessOwner may activate cutover.",
                    errorCode: CutoverFailureCodes.Unauthorized);
            }

            await using var tx = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            try
            {
                var configuration = await LoadConfigForUpdateAsync(request.StoreId, cancellationToken);
                if (configuration == null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return ServiceResult<CutoverActivationResult>.Failure(
                        "Missing writer configuration.",
                        errorCode: InventoryWriterFailureCodes.MissingConfiguration);
                }

                // Idempotent replay: already PreparedItem + same RequestKey + same hashes
                if (configuration.WriterMode == InventoryWriterMode.PreparedItem)
                {
                    var prior = await FindSuccessfulCutoverByRequestKeyAsync(
                        request.StoreId, request.RequestKey, cancellationToken);
                    if (prior != null)
                    {
                        var priorDoc = TryParseEvidence(prior.ReadinessSnapshotJson);
                        if (priorDoc != null
                            && string.Equals(priorDoc.ReconciliationHash, request.ExpectedReconciliationHash, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(priorDoc.ReadinessHash, request.ExpectedReadinessHash, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(priorDoc.SchemaContractHash, request.ExpectedSchemaContractHash, StringComparison.OrdinalIgnoreCase))
                        {
                            await tx.CommitAsync(cancellationToken);
                            return ServiceResult<CutoverActivationResult>.Success(new CutoverActivationResult
                            {
                                Succeeded = true,
                                WasReplay = true,
                                Message = "Cutover already applied (replay).",
                                Status = ToStatus(configuration),
                                TransitionId = prior.TransitionId
                            });
                        }

                        await tx.RollbackAsync(cancellationToken);
                        return ServiceResult<CutoverActivationResult>.Failure(
                            "RequestKey reused with different evidence hashes.",
                            errorCode: CutoverFailureCodes.IdempotencyKeyReused);
                    }

                    // Same mode without matching request evidence → not a silent success
                    if (request.RequestKey != Guid.Empty)
                    {
                        var other = await FindAnySuccessfulCutoverWithRequestKeyAsync(
                            request.StoreId, request.RequestKey, cancellationToken);
                        if (other != null)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return ServiceResult<CutoverActivationResult>.Failure(
                                "RequestKey reused with different evidence hashes.",
                                errorCode: CutoverFailureCodes.IdempotencyKeyReused);
                        }
                    }
                }
                else if (request.RequestKey != Guid.Empty)
                {
                    var existingKey = await FindAnySuccessfulCutoverWithRequestKeyAsync(
                        request.StoreId, request.RequestKey, cancellationToken);
                    if (existingKey != null)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return ServiceResult<CutoverActivationResult>.Failure(
                            "RequestKey already used.",
                            errorCode: CutoverFailureCodes.IdempotencyKeyReused);
                    }
                }

                if (configuration.WriterMode != request.ExpectedMode
                    || request.ExpectedRowVersion.Length == 0
                    || !configuration.RowVersion.SequenceEqual(request.ExpectedRowVersion))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return ServiceResult<CutoverActivationResult>.Failure(
                        "Stale writer configuration.",
                        errorCode: InventoryWriterFailureCodes.StaleConfiguration);
                }

                var allowed =
                    configuration.WriterMode is InventoryWriterMode.LegacyRecipe or InventoryWriterMode.Blocked;
                if (!allowed)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return ServiceResult<CutoverActivationResult>.Failure(
                        "Invalid mode transition for cutover activation.",
                        errorCode: InventoryWriterFailureCodes.InvalidTransition);
                }

                var schema = await _schemaProbe.ProbeAsync(cancellationToken);
                if (!schema.IsReady)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return ServiceResult<CutoverActivationResult>.Failure(
                        "Schema contract not ready.",
                        errorCode: schema.FailureCode ?? CutoverFailureCodes.SchemaContractNotReady);
                }

                if (!string.Equals(schema.ContractHash, request.ExpectedSchemaContractHash, StringComparison.OrdinalIgnoreCase))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return ServiceResult<CutoverActivationResult>.Failure(
                        "Schema contract hash stale.",
                        errorCode: CutoverFailureCodes.StaleSchemaHash);
                }

                var readiness = await _modeService.EvaluateReadinessAsync(request.StoreId);
                if (!readiness.Ready
                    || !string.Equals(readiness.ReadinessHash, request.ExpectedReadinessHash, StringComparison.OrdinalIgnoreCase))
                {
                    await tx.RollbackAsync(cancellationToken);
                    var code = readiness.Ready
                        ? CutoverFailureCodes.StaleReadinessHash
                        : InventoryWriterFailureCodes.ReadinessFailed;
                    var failed = ServiceResult<CutoverActivationResult>.Failure(
                        "Readiness failed or hash stale.",
                        errorCode: code);
                    failed.Data = new CutoverActivationResult
                    {
                        Succeeded = false,
                        FailureCode = code,
                        Readiness = readiness,
                        Message = "Readiness failed or hash stale."
                    };
                    return failed;
                }

                var recon = await BuildReportAsync(request.StoreId, cancellationToken);
                if (!recon.IsClean)
                {
                    await tx.RollbackAsync(cancellationToken);
                    var failed = ServiceResult<CutoverActivationResult>.Failure(
                        "Reconciliation is not clean.",
                        errorCode: CutoverFailureCodes.ReconciliationNotClean);
                    failed.Data = new CutoverActivationResult
                    {
                        Succeeded = false,
                        FailureCode = CutoverFailureCodes.ReconciliationNotClean,
                        Reconciliation = recon,
                        Readiness = readiness,
                        Message = "Reconciliation is not clean."
                    };
                    return failed;
                }

                if (!string.Equals(recon.ReconciliationHash, request.ExpectedReconciliationHash, StringComparison.OrdinalIgnoreCase))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return ServiceResult<CutoverActivationResult>.Failure(
                        "Reconciliation hash stale.",
                        errorCode: CutoverFailureCodes.StaleReconciliationHash);
                }

                var fromMode = configuration.WriterMode;
                configuration.WriterMode = InventoryWriterMode.PreparedItem;
                configuration.HasEverActivatedPreparedItem = true;
                configuration.UpdatedAt = DateTime.UtcNow;

                var evidence = new CutoverActivationEvidenceDocument
                {
                    Kind = "CutoverActivation",
                    RequestKey = request.RequestKey,
                    ReconciliationContractVersion = CutoverContractVersions.Reconciliation,
                    SchemaContractVersion = CutoverContractVersions.Schema,
                    ReconciliationHash = recon.ReconciliationHash,
                    ReadinessHash = readiness.ReadinessHash,
                    SchemaContractHash = schema.ContractHash,
                    EnvironmentFingerprint = recon.EnvironmentFingerprint,
                    GeneratedAtUtc = DateTime.UtcNow,
                    IsClean = true,
                    AnomalyCount = 0,
                    ConsolidationEvidenceRunId = recon.ConsolidationEvidenceRunId,
                    MaintenanceWindowAcknowledged = true,
                    Capabilities = recon.Capabilities.ToList(),
                    AnomalyCounts = new Dictionary<string, int>()
                };

                var transition = new InventoryWriterModeTransition
                {
                    StoreId = request.StoreId,
                    FromMode = fromMode,
                    ToMode = InventoryWriterMode.PreparedItem,
                    ActorAccountId = request.ActorAccountId,
                    Reason = Truncate(request.Reason.Trim(), 500),
                    ReadinessHash = readiness.ReadinessHash,
                    ReadinessSnapshotJson = JsonSerializer.Serialize(evidence, JsonOpts),
                    RequestedAt = DateTime.UtcNow,
                    AppliedAt = DateTime.UtcNow,
                    Succeeded = true,
                    FailureCode = null
                };
                _context.InventoryWriterModeTransitions.Add(transition);

                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return ServiceResult<CutoverActivationResult>.Success(new CutoverActivationResult
                {
                    Succeeded = true,
                    WasReplay = false,
                    Message = "Store activated to PreparedItem.",
                    Status = ToStatus(configuration),
                    Reconciliation = recon,
                    Readiness = readiness,
                    TransitionId = transition.TransitionId
                });
            }
            catch (Exception ex)
            {
                try { await tx.RollbackAsync(cancellationToken); } catch { /* ignore */ }
                _logger.LogError(ex, "Cutover activation failed store={StoreId}", request.StoreId);
                return ServiceResult<CutoverActivationResult>.Failure(
                    "Activation failed: " + ex.Message,
                    errorCode: "CUTOVER_ACTIVATION_EXCEPTION");
            }
        }

        public async Task<ServiceResult<CutoverActivationResult>> RollbackToBlockedAsync(
            int storeId,
            byte[] expectedRowVersion,
            InventoryWriterMode expectedMode,
            string reason,
            int actorAccountId,
            CancellationToken cancellationToken = default)
        {
            if (!await CanActivateAsync(actorAccountId, cancellationToken))
            {
                return ServiceResult<CutoverActivationResult>.Failure(
                    "Unauthorized.",
                    errorCode: CutoverFailureCodes.Unauthorized);
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return ServiceResult<CutoverActivationResult>.Failure(
                    "Reason required.",
                    errorCode: CutoverFailureCodes.InvalidRequest);
            }

            // Reuse foundation transition: PreparedItem/Legacy → Blocked (no recon hashes)
            var result = await _modeService.TransitionAsync(new InventoryWriterModeTransitionRequest
            {
                StoreId = storeId,
                ExpectedCurrentMode = expectedMode,
                ExpectedRowVersion = expectedRowVersion,
                TargetMode = InventoryWriterMode.Blocked,
                Reason = reason,
                ActorAccountId = actorAccountId
            });

            return new ServiceResult<CutoverActivationResult>
            {
                IsSuccess = result.Succeeded,
                Message = result.Message,
                ErrorCode = result.FailureCode ?? string.Empty,
                Data = new CutoverActivationResult
                {
                    Succeeded = result.Succeeded,
                    Message = result.Message,
                    FailureCode = result.FailureCode,
                    Status = result.Status,
                    Readiness = result.Readiness
                }
            };
        }

        public async Task<ServiceResult<CutoverGraduationSummary>> BuildGraduationSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            var schema = await _schemaProbe.ProbeAsync(cancellationToken);
            var configs = await _context.StoreInventoryWriterConfigurations
                .AsNoTracking()
                .OrderBy(x => x.StoreId)
                .ToListAsync(cancellationToken);

            var notPrepared = configs
                .Where(x => x.WriterMode != InventoryWriterMode.PreparedItem)
                .Select(x => x.StoreId)
                .ToList();

            var blockers = new List<string>();
            if (!schema.IsReady)
                blockers.Add("SCHEMA_CONTRACT_NOT_READY");
            if (notPrepared.Count > 0)
                blockers.Add("STORES_NOT_PREPARED_ITEM");
            if (!_globalOptions.LegacyBtpWritesDisabled)
                blockers.Add("GLOBAL_LEGACY_DISABLE_NOT_ENABLED");

            var allClean = true;
            var allEvidence = true;
            foreach (var cfg in configs.Where(x => x.WriterMode == InventoryWriterMode.PreparedItem))
            {
                var recon = await BuildReportAsync(cfg.StoreId, cancellationToken);
                if (!recon.IsClean)
                {
                    allClean = false;
                    blockers.Add($"STORE_{cfg.StoreId}_RECON_NOT_CLEAN");
                }

                if (recon.ConsolidationEvidenceRunId == null)
                {
                    allEvidence = false;
                    blockers.Add($"STORE_{cfg.StoreId}_CONSOLIDATION_EVIDENCE_MISSING");
                }
            }

            var summary = new CutoverGraduationSummary
            {
                GeneratedAtUtc = DateTime.UtcNow,
                AllActiveStoresPreparedItem = notPrepared.Count == 0 && configs.Count > 0,
                NoLegacyOrBlockedStores = notPrepared.Count == 0,
                SchemaReady = schema.IsReady,
                AllStoresHaveCleanReconciliation = allClean && configs.Count > 0,
                AllStoresHaveConsolidationEvidence = allEvidence && configs.Count > 0,
                GlobalLegacyDisableEnabled = _globalOptions.LegacyBtpWritesDisabled,
                StoreIdsNotPrepared = notPrepared,
                Blockers = blockers.Distinct().OrderBy(x => x).ToList(),
                EligibleToCloseUmbrella114 = false // never auto; operator-only
            };

            return ServiceResult<CutoverGraduationSummary>.Success(summary);
        }

        // ──────────────────── report builder ────────────────────

        private async Task<CutoverReconciliationReport> BuildReportAsync(
            int storeId,
            CancellationToken cancellationToken)
        {
            var anomalies = new List<CutoverAnomaly>();
            var limitations = new List<string>();

            var schema = await _schemaProbe.ProbeAsync(cancellationToken);
            if (!schema.IsReady)
            {
                anomalies.Add(new CutoverAnomaly
                {
                    Code = CutoverAnomalyCodes.SchemaContractNotReady,
                    EvidenceId = "schema",
                    Message = schema.FailureCode ?? "Schema contract incomplete."
                });
            }

            var cfg = await _context.StoreInventoryWriterConfigurations.AsNoTracking()
                .FirstOrDefaultAsync(x => x.StoreId == storeId, cancellationToken);

            var writerMode = cfg?.WriterMode ?? InventoryWriterMode.LegacyRecipe;
            var ever = cfg?.HasEverActivatedPreparedItem ?? false;
            var rvHex = cfg != null ? Convert.ToHexString(cfg.RowVersion) : string.Empty;

            var readiness = await _modeService.EvaluateReadinessAsync(storeId);

            var caps = new List<InventoryWriterCapabilityStatus>();
            foreach (var id in InventoryWriterCapabilityIds.Required)
            {
                var provider = _capabilityProviders.FirstOrDefault(p =>
                    p is IStoreScopedInventoryWriterCapabilityProvider s
                        ? s.CapabilityId == id
                        : p.GetStatus().CapabilityId == id);

                if (provider == null)
                {
                    anomalies.Add(A(CutoverAnomalyCodes.RequiredCapabilityMissing, id, "Capability missing."));
                    caps.Add(new InventoryWriterCapabilityStatus(id, "?", false, "MISSING", "missing"));
                    continue;
                }

                var status = provider is IStoreScopedInventoryWriterCapabilityProvider scoped
                    ? await scoped.GetStatusForStoreAsync(storeId, cancellationToken)
                    : provider.GetStatus();
                caps.Add(status);
                if (!status.Ready)
                {
                    anomalies.Add(A(
                        CutoverAnomalyCodes.RequiredCapabilityNotReady,
                        id,
                        status.BlockerMessage ?? "not ready"));
                }
            }

            // Consolidation evidence
            int? evidenceRunId = null;
            string? evidenceHash = null;
            DateTime? evidenceCompletedAt = null;
            var evidence = await _context.InventoryConsolidationRuns.AsNoTracking()
                .Where(x => x.StoreId == storeId
                    && x.Status == InventoryConsolidationRunStatus.Completed
                    && (x.RunType == InventoryConsolidationRunType.AuditNoOp
                        || x.RunType == InventoryConsolidationRunType.Consolidation))
                .OrderByDescending(x => x.CompletedAt)
                .ThenByDescending(x => x.InventoryConsolidationRunId)
                .FirstOrDefaultAsync(cancellationToken);

            if (evidence == null)
            {
                anomalies.Add(A(CutoverAnomalyCodes.ConsolidationEvidenceMissing, "none", "No completed consolidation/no-op."));
            }
            else
            {
                evidenceRunId = evidence.InventoryConsolidationRunId;
                evidenceHash = evidence.ManifestHash;
                evidenceCompletedAt = evidence.CompletedAt;
                if (evidence.StoreId != storeId)
                    anomalies.Add(A(CutoverAnomalyCodes.ConsolidationEvidenceWrongStore, evidence.InventoryConsolidationRunId.ToString(), "Wrong store."));
                if (!string.Equals(evidence.QueryContractVersion, LegacyBtpConsolidationConstants.QueryContractVersion, StringComparison.Ordinal))
                    anomalies.Add(A(CutoverAnomalyCodes.ConsolidationEvidenceStaleContract, evidence.InventoryConsolidationRunId.ToString(), "Stale contract."));
                if (evidence.Status != InventoryConsolidationRunStatus.Completed)
                    anomalies.Add(A(CutoverAnomalyCodes.ConsolidationEvidenceNotCompleted, evidence.InventoryConsolidationRunId.ToString(), "Not completed."));
                if (string.IsNullOrWhiteSpace(evidence.ReportJson)
                    || (!evidence.ReportJson.Contains("noUnresolvedConsolidatableLegacy", StringComparison.Ordinal)
                        && !evidence.ReportJson.Contains("isNoOpEligible", StringComparison.Ordinal)
                        && !evidence.ReportJson.Contains("conservationVerified", StringComparison.Ordinal)))
                {
                    anomalies.Add(A(CutoverAnomalyCodes.ConsolidationReportUnresolved, evidence.InventoryConsolidationRunId.ToString(), "Report unresolved."));
                }
            }

            var activation = await _context.InventoryWriterModeTransitions.AsNoTracking()
                .Where(x => x.StoreId == storeId
                    && x.Succeeded
                    && x.ToMode == InventoryWriterMode.PreparedItem
                    && x.AppliedAt != null)
                .OrderByDescending(x => x.AppliedAt)
                .FirstOrDefaultAsync(cancellationToken);
            DateTime? activationAt = activation?.AppliedAt;

            var rows = await _context.StoreInventories.AsNoTracking()
                .Include(x => x.Recipe)
                .Include(x => x.PreparedItem).ThenInclude(x => x!.BaseUnit)
                .Where(x => x.StoreId == storeId && x.IngredientId == null)
                .OrderBy(x => x.StoreInventoryId)
                .ToListAsync(cancellationToken);

            var recipeIds = rows.Where(x => x.RecipeId != null).Select(x => x.RecipeId!.Value).Distinct().ToList();
            var recipes = recipeIds.Count == 0
                ? new List<Models.Drinks.Recipe>()
                : await _context.Recipes.AsNoTracking()
                    .Where(r => recipeIds.Contains(r.RecipeId))
                    .ToListAsync(cancellationToken);
            var recipeMap = recipes.ToDictionary(r => r.RecipeId);

            foreach (var row in rows)
            {
                if (row.BtpIdentityState == BtpIdentityState.Superseded
                    || row.SupersededByStoreInventoryId != null)
                {
                    if (row.AvailableQty != 0)
                        anomalies.Add(A(CutoverAnomalyCodes.SupersededWithNonzeroAvailable, row.StoreInventoryId.ToString(), "Superseded available nonzero."));
                    if (row.ReservedQty != 0)
                        anomalies.Add(A(CutoverAnomalyCodes.SupersededWithNonzeroReserved, row.StoreInventoryId.ToString(), "Superseded reserved nonzero."));
                    if (row.SupersededByStoreInventoryId is int targetId
                        && !rows.Any(r => r.StoreInventoryId == targetId)
                        && !await _context.StoreInventories.AsNoTracking().AnyAsync(x => x.StoreInventoryId == targetId, cancellationToken))
                    {
                        anomalies.Add(A(CutoverAnomalyCodes.SupersessionTargetMissing, row.StoreInventoryId.ToString(), "Supersession target missing."));
                    }

                    // After cutover, movements on superseded flagged later
                    continue;
                }

                if (row.RecipeId.HasValue && !row.PreparedItemId.HasValue)
                {
                    anomalies.Add(A(CutoverAnomalyCodes.RecipeOnlyBtpRow, row.StoreInventoryId.ToString(), "Recipe-only BTP row."));
                    if (row.RecipeId is int rid && recipeMap.TryGetValue(rid, out var rec) && rec.PreparedItemId == null)
                        anomalies.Add(A(CutoverAnomalyCodes.PreparedItemMappingMissing, row.StoreInventoryId.ToString(), "Recipe unmapped."));
                }

                if (row.RecipeId.HasValue && recipeMap.TryGetValue(row.RecipeId.Value, out var recipe))
                {
                    if (recipe.PreparedItemId == null)
                        anomalies.Add(A(CutoverAnomalyCodes.PreparedItemMappingMissing, row.StoreInventoryId.ToString(), "Mapping missing."));
                    if (recipe.OutputQuantity is null or <= 0 || !recipe.OutputUnitId.HasValue)
                        anomalies.Add(A(CutoverAnomalyCodes.InvalidRecipeOutputContract, row.StoreInventoryId.ToString(), "Invalid output contract."));
                    else if (recipe.PreparedItemId is int piId)
                    {
                        var pi = row.PreparedItemId == piId
                            ? row.PreparedItem
                            : await _context.PreparedItems.AsNoTracking().Include(x => x.BaseUnit)
                                .FirstOrDefaultAsync(x => x.PreparedItemId == piId, cancellationToken);
                        if (pi == null || !pi.Active)
                            anomalies.Add(A(CutoverAnomalyCodes.PreparedItemInactive, piId.ToString(), "Inactive PI."));
                        else if (pi.BaseUnit == null || !pi.BaseUnit.Active)
                            anomalies.Add(A(CutoverAnomalyCodes.PreparedItemBaseUnitMissing, piId.ToString(), "Base unit missing."));
                        else if (recipe.OutputUnitId is int ou)
                        {
                            var conv = await _physical.ConvertAsync(1m, ou, pi.BaseUnitId);
                            if (!conv.IsSuccess)
                                anomalies.Add(A(CutoverAnomalyCodes.OutputUnitConversionMissing, row.StoreInventoryId.ToString(), "Unit conversion missing."));
                        }
                    }
                }

                if (row.QuantitySemanticsStatus is InventoryQuantitySemanticsStatus.Unknown
                    or InventoryQuantitySemanticsStatus.Incompatible
                    or null)
                {
                    if (row.IngredientId == null)
                        anomalies.Add(A(CutoverAnomalyCodes.UnknownQuantitySemantics, row.StoreInventoryId.ToString(), "Unknown semantics."));
                }

                if (evidenceCompletedAt.HasValue
                    && row.LastUpdated > evidenceCompletedAt.Value
                    && (row.RecipeId.HasValue || row.BtpIdentityState == BtpIdentityState.Legacy))
                {
                    anomalies.Add(A(CutoverAnomalyCodes.LegacyRowCreatedAfterEvidence, row.StoreInventoryId.ToString(), "Legacy activity after evidence."));
                }
            }

            // Multi-canonical / collision
            var byPi = rows
                .Where(r => r.BtpIdentityState != BtpIdentityState.Superseded)
                .Select(r => new
                {
                    Row = r,
                    Pi = r.PreparedItemId ?? (r.RecipeId.HasValue && recipeMap.TryGetValue(r.RecipeId.Value, out var rec)
                        ? rec.PreparedItemId
                        : null)
                })
                .Where(x => x.Pi != null)
                .GroupBy(x => x.Pi!.Value);

            foreach (var g in byPi)
            {
                var canons = g.Where(x => x.Row.BtpIdentityState == BtpIdentityState.Canonical).ToList();
                if (canons.Count > 1)
                    anomalies.Add(A(CutoverAnomalyCodes.MultipleCanonicalRows, g.Key.ToString(), "Multiple canonical."));
                var legacies = g.Where(x => x.Row.BtpIdentityState != BtpIdentityState.Canonical).ToList();
                if (canons.Count >= 1 && legacies.Count >= 1)
                    anomalies.Add(A(CutoverAnomalyCodes.CanonicalAndActiveLegacyCollision, g.Key.ToString(), "Canonical+legacy collision."));
            }

            // Production / POS movements (post-activation only when activationAt known)
            var inventoryIds = rows.Select(r => r.StoreInventoryId).ToList();
            if (inventoryIds.Count > 0)
            {
                var movements = await _context.InventoryTransactions.AsNoTracking()
                    .Where(t => inventoryIds.Contains(t.StoreInventoryId))
                    .OrderBy(t => t.InventoryTransactionId)
                    .ToListAsync(cancellationToken);

                var invById = rows.ToDictionary(r => r.StoreInventoryId);

                foreach (var m in movements)
                {
                    if (!invById.TryGetValue(m.StoreInventoryId, out var inv))
                        continue;

                    var afterCutover = activationAt.HasValue && m.CreatedAt >= activationAt.Value;

                    if (m.Type is InventoryTransactionTypeEnum.PRODUCTION_IN or InventoryTransactionTypeEnum.PRODUCTION_OUT)
                    {
                        if (inv.RecipeId.HasValue && !inv.PreparedItemId.HasValue && afterCutover)
                            anomalies.Add(A(CutoverAnomalyCodes.ProductionMovementRecipeIdentity, m.InventoryTransactionId.ToString(), "Production on recipe-only."));
                        if (!m.ProductionRunId.HasValue && afterCutover)
                            anomalies.Add(A(CutoverAnomalyCodes.ProductionMovementWithoutRunLink, m.InventoryTransactionId.ToString(), "Production without run link."));
                        if (inv.BtpIdentityState == BtpIdentityState.Superseded && afterCutover)
                            anomalies.Add(A(CutoverAnomalyCodes.ProductionWriteToSupersededRow, m.InventoryTransactionId.ToString(), "Production write superseded."));
                        if (m.Type == InventoryTransactionTypeEnum.PRODUCTION_IN
                            && m.ProductionRunId.HasValue
                            && afterCutover)
                        {
                            await DetectProductionQuantityAsync(m, inv, anomalies, cancellationToken);
                        }
                    }

                    if (m.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION)
                    {
                        if (afterCutover
                            && inv.IngredientId == null
                            && inv.RecipeId.HasValue
                            && (inv.PreparedItemId == null
                                || inv.BtpIdentityState != BtpIdentityState.Canonical))
                        {
                            anomalies.Add(A(CutoverAnomalyCodes.PosSalesMovementRecipeOnlyIdentity, m.InventoryTransactionId.ToString(), "POS recipe-key BTP."));
                        }

                        if (afterCutover && !m.ReferenceOrderId.HasValue && inv.IngredientId == null)
                            anomalies.Add(A(CutoverAnomalyCodes.PosMovementWithoutReferenceOrder, m.InventoryTransactionId.ToString(), "POS without order."));
                        if (afterCutover && inv.BtpIdentityState == BtpIdentityState.Superseded)
                            anomalies.Add(A(CutoverAnomalyCodes.PosWriteToSupersededRow, m.InventoryTransactionId.ToString(), "POS write superseded."));
                    }
                }

                // Duplicate sales deduction sets: same order + same inventory + type
                var dups = movements
                    .Where(m => m.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION && m.ReferenceOrderId.HasValue)
                    .GroupBy(m => new { m.ReferenceOrderId, m.StoreInventoryId, m.Type })
                    .Where(g => g.Count() > 1);
                foreach (var d in dups)
                {
                    anomalies.Add(A(
                        CutoverAnomalyCodes.DuplicateSalesDeductionSet,
                        d.Key.ReferenceOrderId + ":" + d.Key.StoreInventoryId,
                        "Duplicate sales deduction set."));
                }
            }

            // Alerts / restock
            var alerts = await _context.StockAlerts.AsNoTracking()
                .Where(a => a.StoreId == storeId && a.IngredientId == null)
                .ToListAsync(cancellationToken);
            var restocks = await _context.RestockRequests.AsNoTracking()
                .Where(r => r.StoreId == storeId && r.IngredientId == null)
                .ToListAsync(cancellationToken);

            foreach (var a in alerts.Where(x => x.Status == StockAlertStatuses.Open || x.Status == StockAlertStatuses.Confirmed))
            {
                if (a.RecipeId.HasValue && a.PreparedItemId == null && activationAt.HasValue)
                {
                    if (a.CreatedAt >= activationAt.Value || a.UpdatedAt >= activationAt.Value)
                        anomalies.Add(A(CutoverAnomalyCodes.OpenRecipeOnlyBtpAlertAfterCutover, a.StockAlertId.ToString(), "Recipe-only alert post-cutover."));
                }
            }

            var openPiAlerts = alerts
                .Where(a => a.Status == StockAlertStatuses.Open && a.PreparedItemId.HasValue)
                .GroupBy(a => a.PreparedItemId!.Value)
                .Where(g => g.Count() > 1);
            foreach (var g in openPiAlerts)
                anomalies.Add(A(CutoverAnomalyCodes.DuplicateOpenPreparedItemAlert, g.Key.ToString(), "Duplicate open PI alert."));

            foreach (var group in alerts
                .Where(a => a.Status is StockAlertStatuses.Open or StockAlertStatuses.Confirmed)
                .GroupBy(a => a.PreparedItemId ?? a.RecipeId))
            {
                var hasRecipeOnly = group.Any(a => a.RecipeId.HasValue && a.PreparedItemId == null);
                var hasPi = group.Any(a => a.PreparedItemId.HasValue);
                if (hasRecipeOnly && hasPi)
                    anomalies.Add(A(CutoverAnomalyCodes.AlertIdentityCollision, group.Key?.ToString() ?? "?", "Alert identity collision."));
            }

            foreach (var r in restocks.Where(x => x.Status == "SUBMITTED" || x.Status == "PROCESSING"))
            {
                if (r.RecipeId.HasValue && r.PreparedItemId == null && activationAt.HasValue)
                {
                    if (r.CreatedAt >= activationAt.Value || r.UpdatedAt >= activationAt.Value)
                        anomalies.Add(A(CutoverAnomalyCodes.SubmittedRecipeOnlyBtpRestockAfterCutover, r.RestockRequestId.ToString(), "Recipe-only restock post-cutover."));
                }
            }

            // Deduplicate anomalies by code+evidence
            anomalies = anomalies
                .GroupBy(a => a.Code + "|" + a.EvidenceId)
                .Select(g => g.First())
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .ThenBy(a => a.EvidenceId, StringComparer.Ordinal)
                .ToList();

            var counts = anomalies.GroupBy(a => a.Code)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

            var env = BuildEnvironmentFingerprint();
            var fingerprints = rows
                .OrderBy(r => r.StoreInventoryId)
                .Select(r => new
                {
                    r.StoreInventoryId,
                    r.AvailableQty,
                    r.ReservedQty,
                    r.PreparedItemId,
                    r.RecipeId,
                    Identity = (int?)r.BtpIdentityState,
                    Sem = (int?)r.QuantitySemanticsStatus,
                    r.SupersededByStoreInventoryId
                })
                .ToList();

            var hashPayload = new
            {
                contract = CutoverContractVersions.Reconciliation,
                storeId,
                env,
                schemaHash = schema.ContractHash,
                writerMode = (int)writerMode,
                rvHex,
                ever,
                caps = caps.OrderBy(c => c.CapabilityId).Select(c => new
                {
                    c.CapabilityId,
                    c.ContractVersion,
                    c.Ready,
                    c.BlockerCode
                }),
                evidenceRunId,
                evidenceHash,
                fingerprints,
                anomalies = anomalies.Select(a => new { a.Code, a.EvidenceId }),
                counts = counts.OrderBy(kv => kv.Key).Select(kv => new { kv.Key, kv.Value }),
                activation?.TransitionId,
                activationAt
            };

            var reconHash = Sha256Hex(JsonSerializer.Serialize(hashPayload, JsonOpts));

            return new CutoverReconciliationReport
            {
                StoreId = storeId,
                ReconciliationContractVersion = CutoverContractVersions.Reconciliation,
                EnvironmentFingerprint = env,
                GeneratedAtUtc = DateTime.UtcNow,
                WriterMode = writerMode,
                HasEverActivatedPreparedItem = ever,
                ConfigRowVersionHex = rvHex,
                Schema = schema,
                Capabilities = caps,
                ConsolidationEvidenceRunId = evidenceRunId,
                ConsolidationEvidenceHash = evidenceHash,
                TotalBtpRows = rows.Count,
                CanonicalCount = rows.Count(r => r.BtpIdentityState == BtpIdentityState.Canonical),
                LegacyCount = rows.Count(r => r.BtpIdentityState == BtpIdentityState.Legacy
                    || (r.RecipeId.HasValue && r.BtpIdentityState != BtpIdentityState.Superseded && r.BtpIdentityState != BtpIdentityState.Canonical)),
                SupersededCount = rows.Count(r => r.BtpIdentityState == BtpIdentityState.Superseded),
                Anomalies = anomalies,
                AnomalyCounts = counts,
                IsClean = anomalies.Count == 0,
                ReconciliationHash = reconHash,
                ReadinessHash = readiness.ReadinessHash,
                LatestActivationTransitionId = activation?.TransitionId,
                LatestActivationAtUtc = activationAt,
                Limitations = limitations
            };
        }

        private async Task DetectProductionQuantityAsync(
            Models.Inventories.Transactions.InventoryTransaction m,
            Models.Stores.StoreInventory inv,
            List<CutoverAnomaly> anomalies,
            CancellationToken cancellationToken)
        {
            var run = await _context.ProductionRuns.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ProductionRunId == m.ProductionRunId, cancellationToken);
            if (run == null)
                return;

            var recipe = await _context.Recipes.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RecipeId == run.RecipeId, cancellationToken);
            if (recipe?.OutputQuantity is null or <= 0 || !recipe.OutputUnitId.HasValue || !recipe.PreparedItemId.HasValue)
                return;

            var pi = await _context.PreparedItems.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PreparedItemId == recipe.PreparedItemId.Value, cancellationToken);
            if (pi == null)
                return;

            var conv = await _physical.ConvertAsync(
                recipe.OutputQuantity.Value * run.RequestedRunCount,
                recipe.OutputUnitId.Value,
                pi.BaseUnitId);
            if (!conv.IsSuccess)
            {
                anomalies.Add(A(CutoverAnomalyCodes.OutputUnitConversionMissing, m.InventoryTransactionId.ToString(), "Prod conversion missing."));
                return;
            }

            var expected = Math.Round(conv.Data, 3, MidpointRounding.AwayFromZero);
            if (Math.Abs(m.Quantity - expected) > 0.001m)
            {
                if (Math.Abs(m.Quantity - run.RequestedRunCount) < 0.001m && Math.Abs(expected - run.RequestedRunCount) > 0.001m)
                {
                    anomalies.Add(A(
                        CutoverAnomalyCodes.ProductionOutputBatchCountSuspect,
                        m.InventoryTransactionId.ToString(),
                        $"Qty={m.Quantity} equals run count but expected {expected}."));
                }
                else
                {
                    anomalies.Add(A(
                        CutoverAnomalyCodes.ProductionOutputQuantityMismatch,
                        m.InventoryTransactionId.ToString(),
                        $"Qty={m.Quantity} expected {expected}."));
                }
            }

            if (m.Type == InventoryTransactionTypeEnum.PRODUCTION_IN
                && inv.BtpIdentityState != BtpIdentityState.Canonical
                && inv.PreparedItemId != null)
            {
                anomalies.Add(A(CutoverAnomalyCodes.ProductionOutputNotCanonical, m.InventoryTransactionId.ToString(), "Output not canonical."));
            }
        }

        // ──────────────────── helpers ────────────────────

        private string BuildEnvironmentFingerprint()
        {
            string server;
            string db;
            try
            {
                var c = _context.Database.GetDbConnection();
                server = c.DataSource ?? "local";
                db = c.Database ?? "unknown";
            }
            catch
            {
                server = "local";
                db = "unknown";
            }

            // Never include user/password/token
            var raw = $"server={server}|db={db}|env={_environment.EnvironmentName}|schema={CutoverContractVersions.Schema}";
            return Sha256Hex(raw)[..32];
        }

        private async Task<bool> CanActivateAsync(int accountId, CancellationToken ct)
        {
            return accountId > 0 && await _context.Accounts.AsNoTracking()
                .Where(x => x.AccountId == accountId && x.Active)
                .SelectMany(x => x.AccountRoles)
                .AnyAsync(x => x.Role != null && x.Role.Active
                    && (x.Role.Name == RoleConstants.SystemAdmin || x.Role.Name == RoleConstants.BusinessOwner), ct);
        }

        private async Task<StoreInventoryWriterConfiguration?> LoadConfigForUpdateAsync(int storeId, CancellationToken ct)
        {
            if (_context.Database.IsSqlServer())
            {
                return await _context.StoreInventoryWriterConfigurations
                    .FromSqlInterpolated(
                        $"SELECT * FROM StoreInventoryWriterConfigurations WITH (UPDLOCK, HOLDLOCK) WHERE StoreId = {storeId}")
                    .SingleOrDefaultAsync(ct);
            }

            return await _context.StoreInventoryWriterConfigurations
                .SingleOrDefaultAsync(x => x.StoreId == storeId, ct);
        }

        private async Task<InventoryWriterModeTransition?> FindSuccessfulCutoverByRequestKeyAsync(
            int storeId, Guid requestKey, CancellationToken ct)
        {
            if (requestKey == Guid.Empty) return null;
            var key = requestKey.ToString();
            var rows = await _context.InventoryWriterModeTransitions.AsNoTracking()
                .Where(x => x.StoreId == storeId
                    && x.Succeeded
                    && x.ToMode == InventoryWriterMode.PreparedItem
                    && x.ReadinessSnapshotJson != null
                    && x.ReadinessSnapshotJson.Contains(key))
                .OrderByDescending(x => x.AppliedAt)
                .ToListAsync(ct);
            return rows.FirstOrDefault(x =>
            {
                var doc = TryParseEvidence(x.ReadinessSnapshotJson);
                return doc != null && doc.RequestKey == requestKey;
            });
        }

        private Task<InventoryWriterModeTransition?> FindAnySuccessfulCutoverWithRequestKeyAsync(
            int storeId, Guid requestKey, CancellationToken ct)
            => FindSuccessfulCutoverByRequestKeyAsync(storeId, requestKey, ct);

        private static CutoverActivationEvidenceDocument? TryParseEvidence(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<CutoverActivationEvidenceDocument>(json, JsonOpts);
            }
            catch
            {
                return null;
            }
        }

        private static CutoverAnomaly A(string code, string evidenceId, string message)
            => new() { Code = code, EvidenceId = evidenceId, Message = message };

        private static string Sha256Hex(string input)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s[..max];

        private static InventoryWriterModeStatusDto ToStatus(StoreInventoryWriterConfiguration x) => new()
        {
            StoreId = x.StoreId,
            WriterMode = x.WriterMode,
            HasEverActivatedPreparedItem = x.HasEverActivatedPreparedItem,
            RowVersion = x.RowVersion.ToArray(),
            UpdatedAt = x.UpdatedAt
        };
    }
}
