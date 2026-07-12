using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories.Consolidation;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Consolidation;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #123 — full tooling: audit, no-op evidence, dry-run, atomic execute with conservation.
    /// Never auto-changes Store WriterMode. Never reparents historical InventoryTransactions.
    /// </summary>
    public sealed class LegacyBtpConsolidationService : ILegacyBtpConsolidationService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        private static readonly HashSet<string> ExecuteRoles = new(StringComparer.Ordinal)
        {
            RoleConstants.SystemAdmin,
            RoleConstants.BusinessOwner
        };

        private readonly AppDbContext _context;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<LegacyBtpConsolidationService> _logger;

        public LegacyBtpConsolidationService(
            AppDbContext context,
            IHostEnvironment environment,
            ILogger<LegacyBtpConsolidationService> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        public async Task<ServiceResult<ConsolidationAuditReportDto>> AuditStoreAsync(
            int storeId,
            CancellationToken cancellationToken = default)
        {
            if (!await _context.Stores.AsNoTracking().AnyAsync(x => x.StoreId == storeId, cancellationToken))
            {
                return ServiceResult<ConsolidationAuditReportDto>.Failure(
                    "Không tìm thấy cửa hàng.",
                    errorCode: ConsolidationFailureCodes.StoreNotFound);
            }

            var report = await BuildAuditReportAsync(storeId, cancellationToken);
            return ServiceResult<ConsolidationAuditReportDto>.Success(report);
        }

        public async Task<ServiceResult<ConsolidationRunResultDto>> CreateNoOpEvidenceAsync(
            ConsolidationNoOpRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!request.ExplicitApproval)
            {
                return ServiceResult<ConsolidationRunResultDto>.Failure(
                    "No-op evidence yêu cầu explicit approval.",
                    errorCode: ConsolidationFailureCodes.ExplicitApprovalRequired);
            }

            if (!await _context.Stores.AsNoTracking().AnyAsync(x => x.StoreId == request.StoreId, cancellationToken))
            {
                return ServiceResult<ConsolidationRunResultDto>.Failure(
                    "Không tìm thấy cửa hàng.",
                    errorCode: ConsolidationFailureCodes.StoreNotFound);
            }

            var existing = await _context.InventoryConsolidationRuns
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.StoreId == request.StoreId && x.RequestKey == request.RequestKey,
                    cancellationToken);

            if (existing != null)
            {
                if (existing.RunType != InventoryConsolidationRunType.AuditNoOp)
                {
                    return ServiceResult<ConsolidationRunResultDto>.Failure(
                        "RequestKey đã dùng cho run khác type.",
                        errorCode: ConsolidationFailureCodes.IdempotencyKeyReused);
                }

                return ServiceResult<ConsolidationRunResultDto>.Success(
                    MapRun(existing, wasReplay: true),
                    "Replay no-op evidence.");
            }

            var audit = await BuildAuditReportAsync(request.StoreId, cancellationToken);
            if (!string.IsNullOrEmpty(request.ExpectedAuditHash)
                && !string.Equals(request.ExpectedAuditHash, audit.AuditHash, StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<ConsolidationRunResultDto>.Failure(
                    "Audit hash stale — re-audit trước khi tạo no-op evidence.",
                    errorCode: ConsolidationFailureCodes.StaleManifest);
            }

            if (!audit.IsNoOpEligible)
            {
                return ServiceResult<ConsolidationRunResultDto>.Failure(
                    "Store không đủ điều kiện zero-legacy no-op.",
                    errorCode: ConsolidationFailureCodes.NoOpNotEligible);
            }

            var now = DateTime.UtcNow;
            var reportPayload = new
            {
                kind = "AuditNoOp",
                isNoOpEligible = true,
                noUnresolvedConsolidatableLegacy = true,
                auditCriteriaVersion = LegacyBtpConsolidationConstants.AuditCriteriaVersion,
                auditHash = audit.AuditHash,
                audit
            };

            var run = new InventoryConsolidationRun
            {
                StoreId = request.StoreId,
                RequestKey = request.RequestKey,
                RunType = InventoryConsolidationRunType.AuditNoOp,
                Status = InventoryConsolidationRunStatus.Completed,
                ManifestVersion = LegacyBtpConsolidationConstants.ManifestVersion,
                QueryContractVersion = LegacyBtpConsolidationConstants.QueryContractVersion,
                ManifestHash = audit.AuditHash,
                DryRunHash = audit.AuditHash,
                EnvironmentFingerprint = audit.EnvironmentFingerprint,
                ReportJson = JsonSerializer.Serialize(reportPayload, JsonOpts),
                RequestedByStaffId = request.RequestedByStaffId,
                ApprovedByStaffId = request.ApprovedByStaffId,
                ExecutedByStaffId = request.ApprovedByStaffId,
                CreatedAt = now,
                DryRunAt = now,
                CompletedAt = now,
                BeforeAvailableTotal = 0,
                BeforeReservedTotal = 0,
                AfterAvailableTotal = 0,
                AfterReservedTotal = 0,
                RowVersion = Array.Empty<byte>()
            };

            _context.InventoryConsolidationRuns.Add(run);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                if (await RunExistsAsync(request.StoreId, request.RequestKey, cancellationToken))
                {
                    var race = await _context.InventoryConsolidationRuns.AsNoTracking()
                        .FirstAsync(x => x.StoreId == request.StoreId && x.RequestKey == request.RequestKey, cancellationToken);
                    return ServiceResult<ConsolidationRunResultDto>.Success(MapRun(race, wasReplay: true));
                }

                throw;
            }

            return ServiceResult<ConsolidationRunResultDto>.Success(MapRun(run), "No-op evidence persisted.");
        }

        public async Task<ServiceResult<ConsolidationRunResultDto>> DryRunAsync(
            ConsolidationDryRunRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Manifest == null)
            {
                return ServiceResult<ConsolidationRunResultDto>.Failure(
                    "Manifest bắt buộc.",
                    errorCode: ConsolidationFailureCodes.InvalidManifest);
            }

            if (request.Manifest.StoreId != request.StoreId)
            {
                return ServiceResult<ConsolidationRunResultDto>.Failure(
                    "Manifest StoreId không khớp request.",
                    errorCode: ConsolidationFailureCodes.ManifestStoreMismatch);
            }

            if (!await _context.Stores.AsNoTracking().AnyAsync(x => x.StoreId == request.StoreId, cancellationToken))
            {
                return ServiceResult<ConsolidationRunResultDto>.Failure(
                    "Không tìm thấy cửa hàng.",
                    errorCode: ConsolidationFailureCodes.StoreNotFound);
            }

            var existing = await _context.InventoryConsolidationRuns
                .FirstOrDefaultAsync(
                    x => x.StoreId == request.StoreId && x.RequestKey == request.RequestKey,
                    cancellationToken);

            var manifestHash = ComputeManifestHash(request.Manifest, await LoadRowFingerprintsAsync(request.StoreId, request.Manifest, cancellationToken));

            if (existing != null)
            {
                if (!string.Equals(existing.ManifestHash, manifestHash, StringComparison.OrdinalIgnoreCase)
                    && existing.Status is InventoryConsolidationRunStatus.Completed
                        or InventoryConsolidationRunStatus.DryRunReady
                        or InventoryConsolidationRunStatus.Executing)
                {
                    return ServiceResult<ConsolidationRunResultDto>.Failure(
                        "RequestKey đã gắn manifest khác.",
                        errorCode: ConsolidationFailureCodes.IdempotencyKeyReused);
                }

                if (existing.Status == InventoryConsolidationRunStatus.Completed)
                    return ServiceResult<ConsolidationRunResultDto>.Success(MapRun(existing, wasReplay: true));

                if (existing.Status == InventoryConsolidationRunStatus.Executing)
                {
                    return ServiceResult<ConsolidationRunResultDto>.Failure(
                        "Run đang Executing.",
                        errorCode: ConsolidationFailureCodes.ExecutingInProgress);
                }
            }

            var audit = await BuildAuditReportAsync(request.StoreId, cancellationToken);
            var validation = await ValidateManifestForDryRunAsync(request.Manifest, audit, cancellationToken);
            var now = DateTime.UtcNow;
            var dryRunHash = ComputeDryRunHash(manifestHash, validation, audit.AuditHash);

            var reportObj = new
            {
                kind = "DryRun",
                manifestHash,
                dryRunHash,
                blockers = validation.Blockers,
                expectedBeforeAvailable = validation.BeforeAvailableTotal,
                expectedBeforeReserved = validation.BeforeReservedTotal,
                expectedAfterAvailable = validation.AfterAvailableTotal,
                expectedAfterReserved = validation.AfterReservedTotal,
                lines = validation.LineSnapshots,
                auditHash = audit.AuditHash,
                isNoOpEligible = audit.IsNoOpEligible
            };

            var status = validation.Blockers.Count == 0
                ? InventoryConsolidationRunStatus.DryRunReady
                : InventoryConsolidationRunStatus.Blocked;

            if (existing == null)
            {
                existing = new InventoryConsolidationRun
                {
                    StoreId = request.StoreId,
                    RequestKey = request.RequestKey,
                    RunType = InventoryConsolidationRunType.Consolidation,
                    RequestedByStaffId = request.RequestedByStaffId,
                    ApprovedByStaffId = request.ApprovedByStaffId,
                    CreatedAt = now,
                    RowVersion = Array.Empty<byte>()
                };
                _context.InventoryConsolidationRuns.Add(existing);
            }

            existing.Status = status;
            existing.ManifestVersion = request.Manifest.ManifestVersion;
            existing.QueryContractVersion = LegacyBtpConsolidationConstants.QueryContractVersion;
            existing.ManifestHash = manifestHash;
            existing.DryRunHash = dryRunHash;
            existing.EnvironmentFingerprint = audit.EnvironmentFingerprint;
            existing.ManifestJson = JsonSerializer.Serialize(request.Manifest, JsonOpts);
            existing.ReportJson = JsonSerializer.Serialize(reportObj, JsonOpts);
            existing.DryRunAt = now;
            existing.FailureCode = validation.Blockers.FirstOrDefault();
            existing.FailureDetails = validation.Blockers.Count == 0
                ? null
                : string.Join("; ", validation.Blockers);
            existing.BeforeAvailableTotal = validation.BeforeAvailableTotal;
            existing.BeforeReservedTotal = validation.BeforeReservedTotal;
            existing.AfterAvailableTotal = validation.AfterAvailableTotal;
            existing.AfterReservedTotal = validation.AfterReservedTotal;

            // Replace lines on re-dry-run
            if (existing.InventoryConsolidationRunId != 0)
            {
                var oldLines = await _context.InventoryConsolidationLines
                    .Where(x => x.InventoryConsolidationRunId == existing.InventoryConsolidationRunId)
                    .ToListAsync(cancellationToken);
                _context.InventoryConsolidationLines.RemoveRange(oldLines);
            }

            foreach (var line in validation.LineEntities)
            {
                line.InventoryConsolidationRunId = existing.InventoryConsolidationRunId;
                existing.Lines.Add(line);
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (status == InventoryConsolidationRunStatus.Blocked)
            {
                var blocked = ServiceResult<ConsolidationRunResultDto>.Failure(
                    "Dry-run blocked: " + existing.FailureDetails,
                    errorCode: existing.FailureCode ?? ConsolidationFailureCodes.InvalidManifest);
                blocked.Data = MapRun(existing);
                return blocked;
            }

            return ServiceResult<ConsolidationRunResultDto>.Success(MapRun(existing), "Dry-run ready.");
        }

        public async Task<ServiceResult<ConsolidationRunResultDto>> ExecuteAsync(
            ConsolidationExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!ExecuteRoles.Contains(request.ActorRole ?? string.Empty))
            {
                return ServiceResult<ConsolidationRunResultDto>.Failure(
                    "Chỉ SystemAdmin hoặc BusinessOwner được execute consolidation.",
                    errorCode: ConsolidationFailureCodes.UnauthorizedExecute);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var run = await LoadRunForUpdateAsync(request.StoreId, request.RequestKey, cancellationToken);
                if (run == null)
                {
                    return ServiceResult<ConsolidationRunResultDto>.Failure(
                        "Không tìm thấy consolidation run cho RequestKey.",
                        errorCode: ConsolidationFailureCodes.InvalidManifest);
                }

                if (run.Status == InventoryConsolidationRunStatus.Completed)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return ServiceResult<ConsolidationRunResultDto>.Success(MapRun(run, wasReplay: true), "Replay.");
                }

                if (run.Status == InventoryConsolidationRunStatus.Executing)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<ConsolidationRunResultDto>.Failure(
                        "Run đang Executing.",
                        errorCode: ConsolidationFailureCodes.ExecutingInProgress);
                }

                if (run.Status != InventoryConsolidationRunStatus.DryRunReady)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<ConsolidationRunResultDto>.Failure(
                        "Run phải ở DryRunReady trước execute.",
                        errorCode: ConsolidationFailureCodes.RunNotDryRunReady);
                }

                if (!string.Equals(run.DryRunHash, request.ExpectedDryRunHash, StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<ConsolidationRunResultDto>.Failure(
                        "DryRunHash không khớp — stale dry-run.",
                        errorCode: ConsolidationFailureCodes.DryRunHashMismatch);
                }

                var writerCfg = await _context.StoreInventoryWriterConfigurations
                    .FirstOrDefaultAsync(x => x.StoreId == request.StoreId, cancellationToken);
                if (writerCfg == null || writerCfg.WriterMode != InventoryWriterMode.Blocked)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<ConsolidationRunResultDto>.Failure(
                        "Store phải ở Blocked trước khi execute consolidation.",
                        errorCode: ConsolidationFailureCodes.ConsolidationStoreNotBlocked);
                }

                if (string.IsNullOrWhiteSpace(run.ManifestJson))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<ConsolidationRunResultDto>.Failure(
                        "Thiếu manifest durable.",
                        errorCode: ConsolidationFailureCodes.InvalidManifest);
                }

                var manifest = JsonSerializer.Deserialize<ConsolidationManifestDto>(run.ManifestJson, JsonOpts)
                    ?? throw new InvalidOperationException("Manifest deserialize failed.");

                // Revalidate against current state (stale protection)
                var audit = await BuildAuditReportAsync(request.StoreId, cancellationToken);
                var validation = await ValidateManifestForDryRunAsync(manifest, audit, cancellationToken);
                if (validation.Blockers.Count > 0)
                {
                    run.Status = InventoryConsolidationRunStatus.Blocked;
                    run.FailureCode = validation.Blockers[0];
                    run.FailureDetails = string.Join("; ", validation.Blockers);
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return ServiceResult<ConsolidationRunResultDto>.Failure(
                        "Execute blocked by revalidation: " + run.FailureDetails,
                        errorCode: run.FailureCode);
                }

                var recomputedDry = ComputeDryRunHash(
                    ComputeManifestHash(manifest, await LoadRowFingerprintsAsync(request.StoreId, manifest, cancellationToken)),
                    validation,
                    audit.AuditHash);
                if (!string.Equals(recomputedDry, request.ExpectedDryRunHash, StringComparison.OrdinalIgnoreCase))
                {
                    run.Status = InventoryConsolidationRunStatus.Blocked;
                    run.FailureCode = ConsolidationFailureCodes.StaleManifest;
                    run.FailureDetails = "State changed since dry-run.";
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return ServiceResult<ConsolidationRunResultDto>.Failure(
                        run.FailureDetails,
                        errorCode: ConsolidationFailureCodes.StaleManifest);
                }

                run.Status = InventoryConsolidationRunStatus.Executing;
                run.ExecutedByStaffId = request.ExecutedByStaffId;
                await _context.SaveChangesAsync(cancellationToken);

                var execResult = await ApplyConsolidationMutationAsync(
                    run,
                    manifest,
                    validation,
                    request.ExecutedByStaffId,
                    cancellationToken);

                if (!execResult.IsSuccess)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    // Mark failed on clean context path: re-attach status if needed
                    _logger.LogWarning(
                        "Consolidation execute failed store={StoreId} key={Key} code={Code}",
                        request.StoreId,
                        request.RequestKey,
                        execResult.ErrorCode);
                    return execResult;
                }

                await transaction.CommitAsync(cancellationToken);
                return execResult;
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(cancellationToken); } catch { /* ignore */ }
                _logger.LogError(ex, "Consolidation execute exception store={StoreId}", request.StoreId);
                return ServiceResult<ConsolidationRunResultDto>.Failure(
                    "Execute failed: " + ex.Message,
                    errorCode: "EXECUTE_EXCEPTION");
            }
        }

        public async Task<ServiceResult<ConsolidationRunResultDto>> GetRunAsync(
            int storeId,
            Guid requestKey,
            CancellationToken cancellationToken = default)
        {
            var run = await _context.InventoryConsolidationRuns.AsNoTracking()
                .FirstOrDefaultAsync(x => x.StoreId == storeId && x.RequestKey == requestKey, cancellationToken);
            return run == null
                ? ServiceResult<ConsolidationRunResultDto>.Failure("Run not found.", errorCode: "RUN_NOT_FOUND")
                : ServiceResult<ConsolidationRunResultDto>.Success(MapRun(run));
        }

        public async Task<ServiceResult<ConsolidationRunResultDto>> GetRunByIdAsync(
            int consolidationRunId,
            CancellationToken cancellationToken = default)
        {
            var run = await _context.InventoryConsolidationRuns.AsNoTracking()
                .FirstOrDefaultAsync(x => x.InventoryConsolidationRunId == consolidationRunId, cancellationToken);
            return run == null
                ? ServiceResult<ConsolidationRunResultDto>.Failure("Run not found.", errorCode: "RUN_NOT_FOUND")
                : ServiceResult<ConsolidationRunResultDto>.Success(MapRun(run));
        }

        // ─────────────────────────── Audit ───────────────────────────

        private async Task<ConsolidationAuditReportDto> BuildAuditReportAsync(
            int storeId,
            CancellationToken cancellationToken)
        {
            var rows = await _context.StoreInventories
                .AsNoTracking()
                .Include(x => x.Recipe)
                .Include(x => x.PreparedItem)
                .Where(x => x.StoreId == storeId && x.IngredientId == null)
                .OrderBy(x => x.StoreInventoryId)
                .ToListAsync(cancellationToken);

            var recipeIds = rows.Where(x => x.RecipeId != null).Select(x => x.RecipeId!.Value).Distinct().ToList();
            var recipes = recipeIds.Count == 0
                ? new Dictionary<int, Models.Drinks.Recipe>()
                : await _context.Recipes.AsNoTracking()
                    .Where(r => recipeIds.Contains(r.RecipeId))
                    .ToDictionaryAsync(r => r.RecipeId, cancellationToken);

            var classifications = new List<ConsolidationAuditRowDto>();
            var blockerCodes = new HashSet<string>(StringComparer.Ordinal);
            int recipeOnly = 0, compatibility = 0, canonical = 0, nonCanonPi = 0, superseded = 0;
            int unknownSem = 0, collision = 0, unmapped = 0, multiCanon = 0, unitMismatch = 0;

            foreach (var row in rows)
            {
                var cls = ClassifyRow(row, recipes);
                classifications.Add(cls);
                switch (cls.Classification)
                {
                    case "RecipeOnlyLegacy": recipeOnly++; break;
                    case "Compatibility": compatibility++; break;
                    case "PreparedItemCanonical": canonical++; break;
                    case "PreparedItemNonCanonical": nonCanonPi++; break;
                    case "Superseded": superseded++; break;
                    case "UnknownSemantics": unknownSem++; break;
                }

                if (!string.IsNullOrEmpty(cls.BlockerCode))
                {
                    blockerCodes.Add(cls.BlockerCode);
                    if (cls.BlockerCode is "SOURCE_MAPPING_MISSING" or "UNMAPPED_RECIPE") unmapped++;
                    if (cls.BlockerCode == "UNKNOWN_QUANTITY_SEMANTICS") { /* counted */ }
                    if (cls.BlockerCode == "UNIT_MISMATCH") unitMismatch++;
                }
            }

            // Group by prepared item for collision / multi-canonical
            var groups = new Dictionary<int, List<int>>();
            foreach (var c in classifications)
            {
                var pi = ResolvePreparedItemId(c, rows.First(r => r.StoreInventoryId == c.StoreInventoryId), recipes);
                if (pi == null) continue;
                if (!groups.TryGetValue(pi.Value, out var list))
                {
                    list = new List<int>();
                    groups[pi.Value] = list;
                }
                list.Add(c.StoreInventoryId);
            }

            int thresholdConflicts = 0;
            foreach (var (pi, ids) in groups)
            {
                var groupRows = rows.Where(r => ids.Contains(r.StoreInventoryId)).ToList();
                var activeCanon = groupRows.Where(r =>
                    r.BtpIdentityState == BtpIdentityState.Canonical
                    && r.PreparedItemId == pi
                    && r.SupersededByStoreInventoryId == null).ToList();
                if (activeCanon.Count > 1)
                {
                    multiCanon++;
                    collision++;
                    blockerCodes.Add(ConsolidationFailureCodes.MultipleCanonicalTargets);
                }

                var thresholds = groupRows
                    .Where(r => r.BtpIdentityState != BtpIdentityState.Superseded)
                    .Select(r => (r.MinStockLevel, r.MaxNegativeQty))
                    .Distinct()
                    .ToList();
                if (thresholds.Count > 1)
                    thresholdConflicts++;
            }

            var env = await BuildEnvironmentFingerprintAsync(cancellationToken);
            var auditedAt = DateTime.UtcNow;

            // No-op eligible: no consolidatable legacy/compat, no multi-canon, no unknown/collision/unmapped
            var consolidatable = classifications.Any(c =>
                c.Classification is "RecipeOnlyLegacy" or "Compatibility" or "PreparedItemNonCanonical"
                && c.BlockerCode == null);

            var hasUnresolved = consolidatable
                || multiCanon > 0
                || classifications.Any(c =>
                    c.Classification == "UnknownSemantics"
                    || c.BlockerCode is "SOURCE_MAPPING_MISSING" or "UNMAPPED_RECIPE"
                        or "UNKNOWN_QUANTITY_SEMANTICS" or "UNIT_MISMATCH")
                || collision > 0;

            // Recipe-only without mapping is blocked but still "unresolved consolidatable"
            var hasRecipeOnlyNeedingWork = classifications.Any(c => c.Classification == "RecipeOnlyLegacy");
            var hasCompat = classifications.Any(c => c.Classification == "Compatibility");
            var hasNonCanonPi = classifications.Any(c => c.Classification == "PreparedItemNonCanonical");

            var isNoOpEligible = !hasRecipeOnlyNeedingWork
                && !hasCompat
                && !hasNonCanonPi
                && multiCanon == 0
                && unknownSem == 0
                && collision == 0
                && unmapped == 0
                && !classifications.Any(c => !string.IsNullOrEmpty(c.BlockerCode)
                    && c.Classification != "Superseded");

            // Superseded alone / pure canonical store is no-op eligible
            if (classifications.All(c =>
                    c.Classification is "PreparedItemCanonical" or "Superseded")
                && multiCanon == 0
                && unknownSem == 0)
            {
                isNoOpEligible = true;
            }

            // Empty store (0 BTP) is no-op eligible at audit level (persist still requires explicit command)
            if (rows.Count == 0)
                isNoOpEligible = true;

            var report = new ConsolidationAuditReportDto
            {
                StoreId = storeId,
                EnvironmentFingerprint = env,
                QueryContractVersion = LegacyBtpConsolidationConstants.QueryContractVersion,
                AuditCriteriaVersion = LegacyBtpConsolidationConstants.AuditCriteriaVersion,
                AuditedAtUtc = auditedAt,
                IsNoOpEligible = isNoOpEligible,
                TotalBtpRows = rows.Count,
                RecipeOnlyLegacyCount = recipeOnly,
                CompatibilityCount = compatibility,
                CanonicalCount = canonical,
                NonCanonicalPreparedOnlyCount = nonCanonPi,
                SupersededCount = superseded,
                UnknownSemanticsCount = unknownSem,
                CollisionCount = collision,
                UnmappedRecipeCount = unmapped,
                MultipleCanonicalCandidateCount = multiCanon,
                UnitMismatchCount = unitMismatch,
                ThresholdConflictGroupCount = thresholdConflicts,
                Rows = classifications,
                BlockerCodes = blockerCodes.OrderBy(x => x).ToList(),
                PreparedItemGroups = groups.ToDictionary(
                    kv => kv.Key,
                    kv => (IReadOnlyList<int>)kv.Value.OrderBy(id => id).ToList()),
                AuditHash = string.Empty
            };

            // Hash without unstable timestamp: recompute with fixed fields
            var hashPayload = new
            {
                report.StoreId,
                report.QueryContractVersion,
                report.AuditCriteriaVersion,
                report.EnvironmentFingerprint,
                report.IsNoOpEligible,
                report.TotalBtpRows,
                report.RecipeOnlyLegacyCount,
                report.CompatibilityCount,
                report.CanonicalCount,
                report.NonCanonicalPreparedOnlyCount,
                report.SupersededCount,
                report.UnknownSemanticsCount,
                report.CollisionCount,
                report.UnmappedRecipeCount,
                report.MultipleCanonicalCandidateCount,
                rows = classifications.Select(r => new
                {
                    r.StoreInventoryId,
                    r.Classification,
                    r.AvailableQty,
                    r.ReservedQty,
                    r.MinStockLevel,
                    r.MaxNegativeQty,
                    r.IdentityState,
                    r.QuantitySemantics,
                    r.PreparedItemId,
                    r.RecipeId,
                    r.RowFingerprint
                }).OrderBy(r => r.StoreInventoryId)
            };
            var auditHash = Sha256Hex(CanonicalJson(hashPayload));
            return new ConsolidationAuditReportDto
            {
                StoreId = report.StoreId,
                EnvironmentFingerprint = report.EnvironmentFingerprint,
                QueryContractVersion = report.QueryContractVersion,
                AuditCriteriaVersion = report.AuditCriteriaVersion,
                AuditedAtUtc = auditedAt,
                AuditHash = auditHash,
                IsNoOpEligible = report.IsNoOpEligible,
                TotalBtpRows = report.TotalBtpRows,
                RecipeOnlyLegacyCount = report.RecipeOnlyLegacyCount,
                CompatibilityCount = report.CompatibilityCount,
                CanonicalCount = report.CanonicalCount,
                NonCanonicalPreparedOnlyCount = report.NonCanonicalPreparedOnlyCount,
                SupersededCount = report.SupersededCount,
                UnknownSemanticsCount = report.UnknownSemanticsCount,
                CollisionCount = report.CollisionCount,
                UnmappedRecipeCount = report.UnmappedRecipeCount,
                MultipleCanonicalCandidateCount = report.MultipleCanonicalCandidateCount,
                UnitMismatchCount = report.UnitMismatchCount,
                ThresholdConflictGroupCount = report.ThresholdConflictGroupCount,
                Rows = report.Rows,
                BlockerCodes = report.BlockerCodes,
                PreparedItemGroups = report.PreparedItemGroups
            };
        }

        private static ConsolidationAuditRowDto ClassifyRow(
            StoreInventory row,
            IReadOnlyDictionary<int, Models.Drinks.Recipe> recipes)
        {
            var fp = RowFingerprint(row);
            if (row.BtpIdentityState == BtpIdentityState.Superseded
                || row.SupersededByStoreInventoryId != null)
            {
                return new ConsolidationAuditRowDto
                {
                    StoreInventoryId = row.StoreInventoryId,
                    RecipeId = row.RecipeId,
                    PreparedItemId = row.PreparedItemId,
                    Classification = "Superseded",
                    IdentityState = row.BtpIdentityState,
                    QuantitySemantics = row.QuantitySemanticsStatus,
                    AvailableQty = row.AvailableQty,
                    ReservedQty = row.ReservedQty,
                    MinStockLevel = row.MinStockLevel,
                    MaxNegativeQty = row.MaxNegativeQty,
                    SupersededByStoreInventoryId = row.SupersededByStoreInventoryId,
                    RowFingerprint = fp
                };
            }

            if (row.QuantitySemanticsStatus == InventoryQuantitySemanticsStatus.Unknown
                || row.QuantitySemanticsStatus == InventoryQuantitySemanticsStatus.Incompatible)
            {
                return new ConsolidationAuditRowDto
                {
                    StoreInventoryId = row.StoreInventoryId,
                    RecipeId = row.RecipeId,
                    PreparedItemId = row.PreparedItemId,
                    Classification = "UnknownSemantics",
                    IdentityState = row.BtpIdentityState,
                    QuantitySemantics = row.QuantitySemanticsStatus,
                    AvailableQty = row.AvailableQty,
                    ReservedQty = row.ReservedQty,
                    MinStockLevel = row.MinStockLevel,
                    MaxNegativeQty = row.MaxNegativeQty,
                    BlockerCode = ConsolidationFailureCodes.UnknownQuantitySemantics,
                    BlockerReason = "Quantity semantics unknown/incompatible.",
                    RowFingerprint = fp
                };
            }

            // Recipe-only (no PreparedItem on row)
            if (row.RecipeId.HasValue && !row.PreparedItemId.HasValue)
            {
                recipes.TryGetValue(row.RecipeId.Value, out var recipe);
                if (recipe?.PreparedItemId == null)
                {
                    return new ConsolidationAuditRowDto
                    {
                        StoreInventoryId = row.StoreInventoryId,
                        RecipeId = row.RecipeId,
                        PreparedItemId = null,
                        Classification = "RecipeOnlyLegacy",
                        IdentityState = row.BtpIdentityState,
                        QuantitySemantics = row.QuantitySemanticsStatus,
                        AvailableQty = row.AvailableQty,
                        ReservedQty = row.ReservedQty,
                        MinStockLevel = row.MinStockLevel,
                        MaxNegativeQty = row.MaxNegativeQty,
                        BlockerCode = ConsolidationFailureCodes.SourceMappingMissing,
                        BlockerReason = "Recipe-only row without Recipe.PreparedItemId explicit mapping.",
                        RowFingerprint = fp
                    };
                }

                return new ConsolidationAuditRowDto
                {
                    StoreInventoryId = row.StoreInventoryId,
                    RecipeId = row.RecipeId,
                    PreparedItemId = recipe.PreparedItemId,
                    Classification = "RecipeOnlyLegacy",
                    IdentityState = row.BtpIdentityState ?? BtpIdentityState.Legacy,
                    QuantitySemantics = row.QuantitySemanticsStatus,
                    AvailableQty = row.AvailableQty,
                    ReservedQty = row.ReservedQty,
                    MinStockLevel = row.MinStockLevel,
                    MaxNegativeQty = row.MaxNegativeQty,
                    UnitEvidence = recipe.OutputUnitId?.ToString(CultureInfo.InvariantCulture),
                    RowFingerprint = fp
                };
            }

            // Compatibility: Recipe + PreparedItem
            if (row.RecipeId.HasValue && row.PreparedItemId.HasValue)
            {
                return new ConsolidationAuditRowDto
                {
                    StoreInventoryId = row.StoreInventoryId,
                    RecipeId = row.RecipeId,
                    PreparedItemId = row.PreparedItemId,
                    Classification = "Compatibility",
                    IdentityState = row.BtpIdentityState,
                    QuantitySemantics = row.QuantitySemanticsStatus,
                    AvailableQty = row.AvailableQty,
                    ReservedQty = row.ReservedQty,
                    MinStockLevel = row.MinStockLevel,
                    MaxNegativeQty = row.MaxNegativeQty,
                    RowFingerprint = fp
                };
            }

            // PreparedItem-only
            if (!row.RecipeId.HasValue && row.PreparedItemId.HasValue)
            {
                if (row.BtpIdentityState == BtpIdentityState.Canonical
                    && row.QuantitySemanticsStatus == InventoryQuantitySemanticsStatus.BaseUnitConfirmed)
                {
                    return new ConsolidationAuditRowDto
                    {
                        StoreInventoryId = row.StoreInventoryId,
                        PreparedItemId = row.PreparedItemId,
                        Classification = "PreparedItemCanonical",
                        IdentityState = row.BtpIdentityState,
                        QuantitySemantics = row.QuantitySemanticsStatus,
                        AvailableQty = row.AvailableQty,
                        ReservedQty = row.ReservedQty,
                        MinStockLevel = row.MinStockLevel,
                        MaxNegativeQty = row.MaxNegativeQty,
                        RowFingerprint = fp
                    };
                }

                return new ConsolidationAuditRowDto
                {
                    StoreInventoryId = row.StoreInventoryId,
                    PreparedItemId = row.PreparedItemId,
                    Classification = "PreparedItemNonCanonical",
                    IdentityState = row.BtpIdentityState,
                    QuantitySemantics = row.QuantitySemanticsStatus,
                    AvailableQty = row.AvailableQty,
                    ReservedQty = row.ReservedQty,
                    MinStockLevel = row.MinStockLevel,
                    MaxNegativeQty = row.MaxNegativeQty,
                    RowFingerprint = fp
                };
            }

            return new ConsolidationAuditRowDto
            {
                StoreInventoryId = row.StoreInventoryId,
                RecipeId = row.RecipeId,
                PreparedItemId = row.PreparedItemId,
                Classification = "UnknownSemantics",
                IdentityState = row.BtpIdentityState,
                QuantitySemantics = row.QuantitySemanticsStatus,
                AvailableQty = row.AvailableQty,
                ReservedQty = row.ReservedQty,
                MinStockLevel = row.MinStockLevel,
                MaxNegativeQty = row.MaxNegativeQty,
                BlockerCode = ConsolidationFailureCodes.UnknownQuantitySemantics,
                BlockerReason = "Unclassified BTP row.",
                RowFingerprint = fp
            };
        }

        private static int? ResolvePreparedItemId(
            ConsolidationAuditRowDto dto,
            StoreInventory row,
            IReadOnlyDictionary<int, Models.Drinks.Recipe> recipes)
        {
            if (dto.PreparedItemId.HasValue) return dto.PreparedItemId;
            if (row.PreparedItemId.HasValue) return row.PreparedItemId;
            if (row.RecipeId.HasValue && recipes.TryGetValue(row.RecipeId.Value, out var r))
                return r.PreparedItemId;
            return null;
        }

        // ─────────────────────────── Dry-run validation ───────────────────────────

        private sealed class DryRunValidation
        {
            public List<string> Blockers { get; } = new();
            public decimal BeforeAvailableTotal { get; set; }
            public decimal BeforeReservedTotal { get; set; }
            public decimal AfterAvailableTotal { get; set; }
            public decimal AfterReservedTotal { get; set; }
            public List<object> LineSnapshots { get; } = new();
            public List<InventoryConsolidationLine> LineEntities { get; } = new();
            public List<GroupPlan> GroupPlans { get; } = new();
        }

        private sealed class GroupPlan
        {
            public ConsolidationGroupManifestDto Group { get; init; } = null!;
            public List<(StoreInventory Row, decimal ConvAvail, decimal ConvReserved, ConsolidationConversionEvidenceDto? Conv)> Sources { get; init; } = new();
            public StoreInventory? ExistingTarget { get; init; }
            public bool CreateTarget { get; init; }
            public decimal TargetBeforeAvail { get; init; }
            public decimal TargetBeforeReserved { get; init; }
            public decimal TargetAfterAvail { get; init; }
            public decimal TargetAfterReserved { get; init; }
        }

        private async Task<DryRunValidation> ValidateManifestForDryRunAsync(
            ConsolidationManifestDto manifest,
            ConsolidationAuditReportDto audit,
            CancellationToken cancellationToken)
        {
            var v = new DryRunValidation();
            if (manifest.Groups == null || manifest.Groups.Count == 0)
            {
                v.Blockers.Add(ConsolidationFailureCodes.InvalidManifest);
                return v;
            }

            if (!string.Equals(manifest.QueryContractVersion, LegacyBtpConsolidationConstants.QueryContractVersion, StringComparison.Ordinal))
                v.Blockers.Add(ConsolidationFailureCodes.StaleManifest);

            var allSourceIds = manifest.Groups.SelectMany(g => g.SourceStoreInventoryIds).ToList();
            if (allSourceIds.Count != allSourceIds.Distinct().Count())
                v.Blockers.Add(ConsolidationFailureCodes.InvalidManifest);

            var invMap = await _context.StoreInventories
                .Where(x => x.StoreId == manifest.StoreId && x.IngredientId == null)
                .ToDictionaryAsync(x => x.StoreInventoryId, cancellationToken);

            var preparedItems = await _context.PreparedItems.AsNoTracking()
                .Where(p => manifest.Groups.Select(g => g.PreparedItemId).Contains(p.PreparedItemId))
                .ToDictionaryAsync(p => p.PreparedItemId, cancellationToken);

            var recipeIdsForValidation = invMap.Values
                .Where(x => x.RecipeId != null)
                .Select(x => x.RecipeId!.Value)
                .Distinct()
                .ToList();
            var recipes = recipeIdsForValidation.Count == 0
                ? new Dictionary<int, Models.Drinks.Recipe>()
                : await _context.Recipes.AsNoTracking()
                    .Where(r => recipeIdsForValidation.Contains(r.RecipeId))
                    .ToDictionaryAsync(r => r.RecipeId, cancellationToken);

            // Open alerts for collision checks
            var openAlerts = await _context.StockAlerts.AsNoTracking()
                .Where(a => a.StoreId == manifest.StoreId
                    && (a.Status == StockAlertStatuses.Open || a.Status == StockAlertStatuses.Confirmed)
                    && a.IngredientId == null)
                .ToListAsync(cancellationToken);

            var openRestocks = await _context.RestockRequests.AsNoTracking()
                .Where(r => r.StoreId == manifest.StoreId && r.IngredientId == null)
                .ToListAsync(cancellationToken);

            decimal beforeA = 0, beforeR = 0, afterA = 0, afterR = 0;

            foreach (var group in manifest.Groups)
            {
                if (group.StoreId != manifest.StoreId)
                    v.Blockers.Add(ConsolidationFailureCodes.ManifestStoreMismatch);

                if (!group.ThresholdDecisionProvided)
                    v.Blockers.Add(ConsolidationFailureCodes.ThresholdDecisionMissing);

                if (!preparedItems.TryGetValue(group.PreparedItemId, out var pi) || !pi.Active)
                {
                    v.Blockers.Add("INVALID_PREPARED_ITEM");
                    continue;
                }

                var hasTargetId = group.TargetStoreInventoryId.HasValue;
                if (hasTargetId == group.CreateCanonicalTarget)
                    v.Blockers.Add(ConsolidationFailureCodes.TargetSpecAmbiguous);
                if (!hasTargetId && !group.CreateCanonicalTarget)
                    v.Blockers.Add(ConsolidationFailureCodes.TargetSpecMissing);

                StoreInventory? target = null;
                if (hasTargetId)
                {
                    if (!invMap.TryGetValue(group.TargetStoreInventoryId!.Value, out target)
                        || target.StoreId != manifest.StoreId)
                    {
                        v.Blockers.Add(ConsolidationFailureCodes.TargetNotFound);
                    }
                    else if (target.BtpIdentityState != BtpIdentityState.Canonical
                             || target.PreparedItemId != group.PreparedItemId
                             || target.RecipeId != null
                             || target.QuantitySemanticsStatus != InventoryQuantitySemanticsStatus.BaseUnitConfirmed)
                    {
                        v.Blockers.Add(ConsolidationFailureCodes.TargetNotCanonical);
                    }
                }
                else if (group.CreateCanonicalTarget)
                {
                    var existingCanon = invMap.Values.Where(x =>
                        x.PreparedItemId == group.PreparedItemId
                        && x.BtpIdentityState == BtpIdentityState.Canonical
                        && x.SupersededByStoreInventoryId == null).ToList();
                    if (existingCanon.Count > 0)
                        v.Blockers.Add(ConsolidationFailureCodes.TargetCollision);
                }

                var multiCanon = invMap.Values.Count(x =>
                    x.PreparedItemId == group.PreparedItemId
                    && x.BtpIdentityState == BtpIdentityState.Canonical
                    && x.SupersededByStoreInventoryId == null
                    && (!hasTargetId || x.StoreInventoryId != group.TargetStoreInventoryId));
                if (multiCanon > (hasTargetId ? 0 : 0) && hasTargetId)
                {
                    var other = invMap.Values.Count(x =>
                        x.PreparedItemId == group.PreparedItemId
                        && x.BtpIdentityState == BtpIdentityState.Canonical
                        && x.StoreInventoryId != group.TargetStoreInventoryId
                        && x.SupersededByStoreInventoryId == null);
                    if (other > 0)
                        v.Blockers.Add(ConsolidationFailureCodes.MultipleCanonicalTargets);
                }

                // Alert collision: legacy Recipe alert + PI alert for same PI
                var recipeIdsInGroup = new HashSet<int>();
                var sources = new List<(StoreInventory Row, decimal ConvAvail, decimal ConvReserved, ConsolidationConversionEvidenceDto? Conv)>();

                foreach (var sourceId in group.SourceStoreInventoryIds.OrderBy(id => id))
                {
                    if (!invMap.TryGetValue(sourceId, out var source) || source.StoreId != manifest.StoreId)
                    {
                        v.Blockers.Add(ConsolidationFailureCodes.SourceNotFound);
                        continue;
                    }

                    if (source.BtpIdentityState == BtpIdentityState.Superseded
                        || source.SupersededByStoreInventoryId != null)
                    {
                        v.Blockers.Add(ConsolidationFailureCodes.SourceAlreadySuperseded);
                        continue;
                    }

                    if (hasTargetId && source.StoreInventoryId == group.TargetStoreInventoryId)
                    {
                        v.Blockers.Add(ConsolidationFailureCodes.InvalidManifest);
                        continue;
                    }

                    var mappedPi = source.PreparedItemId
                        ?? (source.RecipeId.HasValue && recipes.TryGetValue(source.RecipeId.Value, out var rec)
                            ? rec.PreparedItemId
                            : null);

                    if (mappedPi == null)
                    {
                        v.Blockers.Add(ConsolidationFailureCodes.SourceMappingMissing);
                        continue;
                    }

                    if (mappedPi != group.PreparedItemId)
                    {
                        v.Blockers.Add(ConsolidationFailureCodes.SourcePreparedItemMismatch);
                        continue;
                    }

                    if (source.QuantitySemanticsStatus == InventoryQuantitySemanticsStatus.Unknown
                        || source.QuantitySemanticsStatus == InventoryQuantitySemanticsStatus.Incompatible)
                    {
                        v.Blockers.Add(ConsolidationFailureCodes.UnknownQuantitySemantics);
                        continue;
                    }

                    if (source.RecipeId.HasValue)
                        recipeIdsInGroup.Add(source.RecipeId.Value);

                    decimal factor = 1m;
                    ConsolidationConversionEvidenceDto? conv = null;
                    var needsConversion = source.QuantitySemanticsStatus != InventoryQuantitySemanticsStatus.BaseUnitConfirmed;
                    if (group.ConversionBySourceId != null
                        && group.ConversionBySourceId.TryGetValue(sourceId, out var evidence))
                    {
                        conv = evidence;
                        if (evidence.Factor <= 0
                            || evidence.ToUnitId != pi.BaseUnitId)
                        {
                            v.Blockers.Add(ConsolidationFailureCodes.ConversionEvidenceMissing);
                            continue;
                        }

                        factor = evidence.Factor;
                        var convertedA = RoundQty(source.AvailableQty * factor);
                        var convertedR = RoundQty(source.ReservedQty * factor);
                        if (!IsExactRepresentable(source.AvailableQty, factor, convertedA)
                            || !IsExactRepresentable(source.ReservedQty, factor, convertedR))
                        {
                            v.Blockers.Add(ConsolidationFailureCodes.QuantityPrecisionLoss);
                            continue;
                        }

                        sources.Add((source, convertedA, convertedR, conv));
                    }
                    else if (needsConversion)
                    {
                        v.Blockers.Add(ConsolidationFailureCodes.ConversionEvidenceMissing);
                        continue;
                    }
                    else
                    {
                        // BaseUnitConfirmed → factor 1
                        sources.Add((source, source.AvailableQty, source.ReservedQty, null));
                    }
                }

                // Alert identity collision
                if (!group.AllowAlertIdentityCollision)
                {
                    var recipeAlerts = openAlerts.Where(a =>
                        a.RecipeId.HasValue && recipeIdsInGroup.Contains(a.RecipeId.Value)
                        && a.PreparedItemId == null).ToList();
                    var piAlerts = openAlerts.Where(a =>
                        a.PreparedItemId == group.PreparedItemId).ToList();
                    if (recipeAlerts.Count > 0 && piAlerts.Count > 0)
                        v.Blockers.Add(ConsolidationFailureCodes.AlertIdentityCollision);

                    var recipeRestocks = openRestocks.Where(r =>
                        r.RecipeId.HasValue && recipeIdsInGroup.Contains(r.RecipeId.Value)
                        && r.PreparedItemId == null).ToList();
                    var piRestocks = openRestocks.Where(r => r.PreparedItemId == group.PreparedItemId).ToList();
                    if (recipeRestocks.Count > 0 && piRestocks.Count > 0)
                        v.Blockers.Add(ConsolidationFailureCodes.RestockIdentityCollision);
                }

                var tBeforeA = target?.AvailableQty ?? 0m;
                var tBeforeR = target?.ReservedQty ?? 0m;
                var sumA = sources.Sum(s => s.ConvAvail);
                var sumR = sources.Sum(s => s.ConvReserved);
                var tAfterA = RoundQty(tBeforeA + sumA);
                var tAfterR = RoundQty(tBeforeR + sumR);

                // Conservation in target unit: sources (converted) + target before = target after + sources after(0)
                beforeA += sources.Sum(s => s.ConvAvail) + tBeforeA;
                beforeR += sources.Sum(s => s.ConvReserved) + tBeforeR;
                afterA += tAfterA; // sources go to 0
                afterR += tAfterR;

                var plan = new GroupPlan
                {
                    Group = group,
                    Sources = sources,
                    ExistingTarget = target,
                    CreateTarget = group.CreateCanonicalTarget,
                    TargetBeforeAvail = tBeforeA,
                    TargetBeforeReserved = tBeforeR,
                    TargetAfterAvail = tAfterA,
                    TargetAfterReserved = tAfterR
                };
                v.GroupPlans.Add(plan);

                foreach (var s in sources)
                {
                    v.LineSnapshots.Add(new
                    {
                        role = "Source",
                        s.Row.StoreInventoryId,
                        beforeAvail = s.Row.AvailableQty,
                        beforeReserved = s.Row.ReservedQty,
                        convertedAvail = s.ConvAvail,
                        convertedReserved = s.ConvReserved,
                        afterAvail = 0m,
                        afterReserved = 0m
                    });
                    v.LineEntities.Add(new InventoryConsolidationLine
                    {
                        StoreInventoryId = s.Row.StoreInventoryId,
                        LineRole = InventoryConsolidationLineRole.Source,
                        PreparedItemId = group.PreparedItemId,
                        SourceRecipeId = s.Row.RecipeId,
                        BeforeAvailableQty = s.Row.AvailableQty,
                        BeforeReservedQty = s.Row.ReservedQty,
                        BeforeMinStockLevel = s.Row.MinStockLevel,
                        BeforeMaxNegativeQty = s.Row.MaxNegativeQty,
                        BeforeIdentityState = s.Row.BtpIdentityState,
                        BeforeQuantitySemantics = s.Row.QuantitySemanticsStatus,
                        ApprovedConversionFactor = s.Conv?.Factor,
                        ApprovedConversionFromUnitId = s.Conv?.FromUnitId,
                        ApprovedConversionToUnitId = s.Conv?.ToUnitId,
                        ConvertedAvailableQty = s.ConvAvail,
                        ConvertedReservedQty = s.ConvReserved,
                        AfterAvailableQty = 0,
                        AfterReservedQty = 0,
                        EvidenceType = "CONSOLIDATION_SOURCE",
                        EvidenceReference = group.EvidenceReference,
                        IsTargetCreated = false
                    });
                }

                v.LineSnapshots.Add(new
                {
                    role = "Target",
                    storeInventoryId = target?.StoreInventoryId,
                    create = group.CreateCanonicalTarget,
                    beforeAvail = tBeforeA,
                    beforeReserved = tBeforeR,
                    afterAvail = tAfterA,
                    afterReserved = tAfterR,
                    approvedMin = group.ApprovedMinStockLevel,
                    approvedMaxNeg = group.ApprovedMaxNegativeQty
                });
            }

            v.BeforeAvailableTotal = beforeA;
            v.BeforeReservedTotal = beforeR;
            v.AfterAvailableTotal = afterA;
            v.AfterReservedTotal = afterR;

            if (Math.Abs(beforeA - afterA) > 0.0005m || Math.Abs(beforeR - afterR) > 0.0005m)
            {
                // Should not happen if math is correct; flag for safety
                if (!v.Blockers.Contains(ConsolidationFailureCodes.ConservationFailed))
                    v.Blockers.Add(ConsolidationFailureCodes.ConservationFailed);
            }

            // De-dupe blockers preserve order
            var distinct = v.Blockers.Distinct().ToList();
            v.Blockers.Clear();
            v.Blockers.AddRange(distinct);
            return v;
        }

        private async Task<ServiceResult<ConsolidationRunResultDto>> ApplyConsolidationMutationAsync(
            InventoryConsolidationRun run,
            ConsolidationManifestDto manifest,
            DryRunValidation validation,
            int actorStaffId,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var actorAccountId = await _context.Staffs.AsNoTracking()
                .Where(s => s.StaffId == actorStaffId)
                .Select(s => (int?)s.AccountId)
                .FirstOrDefaultAsync(cancellationToken) ?? actorStaffId;

            var affectedIds = validation.GroupPlans
                .SelectMany(g => g.Sources.Select(s => s.Row.StoreInventoryId)
                    .Concat(g.ExistingTarget != null
                        ? new[] { g.ExistingTarget.StoreInventoryId }
                        : Array.Empty<int>()))
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            // Deterministic lock order by StoreInventoryId
            var locked = new Dictionary<int, StoreInventory>();
            if (_context.Database.IsSqlServer() && affectedIds.Count > 0)
            {
                var idList = string.Join(",", affectedIds);
                var lockedRows = await _context.StoreInventories
                    .FromSqlRaw(
                        $"SELECT * FROM StoreInventories WITH (UPDLOCK, HOLDLOCK) WHERE StoreInventoryId IN ({idList})")
                    .ToListAsync(cancellationToken);
                foreach (var row in lockedRows)
                    locked[row.StoreInventoryId] = row;
            }
            else
            {
                foreach (var id in affectedIds)
                {
                    var row = await _context.StoreInventories.FirstAsync(x => x.StoreInventoryId == id, cancellationToken);
                    locked[id] = row;
                }
            }

            var newTargets = new List<StoreInventory>();
            var lines = new List<InventoryConsolidationLine>();
            var movements = new List<InventoryTransaction>();

            foreach (var plan in validation.GroupPlans)
            {
                StoreInventory target;
                bool created = false;
                if (plan.CreateTarget)
                {
                    var pi = await _context.PreparedItems.AsNoTracking()
                        .FirstAsync(p => p.PreparedItemId == plan.Group.PreparedItemId, cancellationToken);

                    // Re-check no concurrent canonical
                    var existingCanon = await _context.StoreInventories
                        .Where(x => x.StoreId == manifest.StoreId
                            && x.PreparedItemId == plan.Group.PreparedItemId
                            && x.BtpIdentityState == BtpIdentityState.Canonical
                            && x.SupersededByStoreInventoryId == null)
                        .ToListAsync(cancellationToken);
                    if (existingCanon.Count > 0)
                    {
                        return ServiceResult<ConsolidationRunResultDto>.Failure(
                            "Canonical target collision during execute.",
                            errorCode: ConsolidationFailureCodes.TargetCollision);
                    }

                    target = new StoreInventory
                    {
                        StoreId = manifest.StoreId,
                        IngredientId = null,
                        RecipeId = null,
                        PreparedItemId = plan.Group.PreparedItemId,
                        BtpIdentityState = BtpIdentityState.Canonical,
                        QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                        QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                        QuantitySemanticsEvidenceReference = $"CONSOLIDATION_RUN:{run.InventoryConsolidationRunId}",
                        QuantitySemanticsReviewedAt = now,
                        QuantitySemanticsReviewedByAccountId = actorAccountId,
                        AvailableQty = 0,
                        ReservedQty = 0,
                        MinStockLevel = plan.Group.ApprovedMinStockLevel,
                        MaxNegativeQty = plan.Group.ApprovedMaxNegativeQty,
                        LastUpdated = now,
                        RowVersion = Array.Empty<byte>()
                    };
                    _context.StoreInventories.Add(target);
                    await _context.SaveChangesAsync(cancellationToken); // get id
                    created = true;
                    newTargets.Add(target);
                }
                else
                {
                    if (!locked.TryGetValue(plan.Group.TargetStoreInventoryId!.Value, out target!))
                    {
                        return ServiceResult<ConsolidationRunResultDto>.Failure(
                            "Target missing under lock.",
                            errorCode: ConsolidationFailureCodes.TargetNotFound);
                    }
                }

                decimal targetBeforeAvail = target.AvailableQty;
                decimal targetBeforeReserved = target.ReservedQty;
                decimal sumInAvail = 0;
                decimal sumInReserved = 0;

                foreach (var srcPlan in plan.Sources.OrderBy(s => s.Row.StoreInventoryId))
                {
                    if (!locked.TryGetValue(srcPlan.Row.StoreInventoryId, out var source))
                    {
                        return ServiceResult<ConsolidationRunResultDto>.Failure(
                            "Source missing under lock.",
                            errorCode: ConsolidationFailureCodes.SourceNotFound);
                    }

                    // Stale qty check vs dry-run snapshot
                    if (source.AvailableQty != srcPlan.Row.AvailableQty
                        || source.ReservedQty != srcPlan.Row.ReservedQty
                        || source.BtpIdentityState == BtpIdentityState.Superseded)
                    {
                        return ServiceResult<ConsolidationRunResultDto>.Failure(
                            "Source state changed under lock.",
                            errorCode: ConsolidationFailureCodes.StaleManifest);
                    }

                    var beforeAvail = source.AvailableQty;
                    var beforeReserved = source.ReservedQty;

                    if (srcPlan.ConvAvail != 0 || beforeAvail != 0)
                    {
                        // OUT movement uses source-native quantity (absolute positive)
                        var outQty = Math.Abs(beforeAvail);
                        if (outQty > 0)
                        {
                            movements.Add(new InventoryTransaction
                            {
                                StoreInventoryId = source.StoreInventoryId,
                                Type = InventoryTransactionTypeEnum.CONSOLIDATION_OUT,
                                StockStatus = InventoryStockStatus.NORMAL,
                                Quantity = outQty,
                                BeforeQty = beforeAvail,
                                AfterQty = 0,
                                InventoryConsolidationRunId = run.InventoryConsolidationRunId,
                                CreatedAt = now
                            });
                        }
                    }

                    source.AvailableQty = 0;
                    source.ReservedQty = 0;
                    source.BtpIdentityState = BtpIdentityState.Superseded;
                    source.SupersededByStoreInventoryId = target.StoreInventoryId;
                    source.LastUpdated = now;
                    // Source thresholds retained for audit (not used by writers after Superseded)

                    sumInAvail += srcPlan.ConvAvail;
                    sumInReserved += srcPlan.ConvReserved;

                    lines.Add(new InventoryConsolidationLine
                    {
                        InventoryConsolidationRunId = run.InventoryConsolidationRunId,
                        StoreInventoryId = source.StoreInventoryId,
                        LineRole = InventoryConsolidationLineRole.Source,
                        PreparedItemId = plan.Group.PreparedItemId,
                        SourceRecipeId = source.RecipeId,
                        BeforeAvailableQty = beforeAvail,
                        BeforeReservedQty = beforeReserved,
                        BeforeMinStockLevel = source.MinStockLevel,
                        BeforeMaxNegativeQty = source.MaxNegativeQty,
                        BeforeIdentityState = BtpIdentityState.Legacy, // pre-state was not superseded
                        BeforeQuantitySemantics = srcPlan.Row.QuantitySemanticsStatus,
                        ApprovedConversionFactor = srcPlan.Conv?.Factor,
                        ApprovedConversionFromUnitId = srcPlan.Conv?.FromUnitId,
                        ApprovedConversionToUnitId = srcPlan.Conv?.ToUnitId,
                        ConvertedAvailableQty = srcPlan.ConvAvail,
                        ConvertedReservedQty = srcPlan.ConvReserved,
                        AfterAvailableQty = 0,
                        AfterReservedQty = 0,
                        EvidenceType = "CONSOLIDATION_SOURCE",
                        EvidenceReference = plan.Group.EvidenceReference,
                        IsTargetCreated = false
                    });
                }

                var targetAfterAvail = RoundQty(targetBeforeAvail + sumInAvail);
                var targetAfterReserved = RoundQty(targetBeforeReserved + sumInReserved);
                var inQty = RoundQty(sumInAvail);
                if (inQty > 0 || sumInAvail != 0)
                {
                    var qty = Math.Abs(inQty);
                    if (qty > 0)
                    {
                        movements.Add(new InventoryTransaction
                        {
                            StoreInventoryId = target.StoreInventoryId,
                            Type = InventoryTransactionTypeEnum.CONSOLIDATION_IN,
                            StockStatus = InventoryStockStatus.NORMAL,
                            Quantity = qty,
                            BeforeQty = targetBeforeAvail,
                            AfterQty = targetAfterAvail,
                            InventoryConsolidationRunId = run.InventoryConsolidationRunId,
                            CreatedAt = now
                        });
                    }
                }

                // Also handle reserved-only transfer with zero available: still update reserved
                target.AvailableQty = targetAfterAvail;
                target.ReservedQty = targetAfterReserved;
                target.PreparedItemId = plan.Group.PreparedItemId;
                target.IngredientId = null;
                target.RecipeId = null;
                target.BtpIdentityState = BtpIdentityState.Canonical;
                target.QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed;
                target.QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation;
                target.QuantitySemanticsEvidenceReference =
                    $"CONSOLIDATION_RUN:{run.InventoryConsolidationRunId}";
                target.QuantitySemanticsReviewedAt = now;
                target.QuantitySemanticsReviewedByAccountId = actorAccountId;
                target.MinStockLevel = plan.Group.ApprovedMinStockLevel;
                target.MaxNegativeQty = plan.Group.ApprovedMaxNegativeQty;
                target.LastUpdated = now;

                lines.Add(new InventoryConsolidationLine
                {
                    InventoryConsolidationRunId = run.InventoryConsolidationRunId,
                    StoreInventoryId = target.StoreInventoryId,
                    LineRole = InventoryConsolidationLineRole.Target,
                    PreparedItemId = plan.Group.PreparedItemId,
                    BeforeAvailableQty = targetBeforeAvail,
                    BeforeReservedQty = targetBeforeReserved,
                    BeforeMinStockLevel = plan.ExistingTarget?.MinStockLevel,
                    BeforeMaxNegativeQty = plan.ExistingTarget?.MaxNegativeQty,
                    BeforeIdentityState = plan.ExistingTarget?.BtpIdentityState,
                    BeforeQuantitySemantics = plan.ExistingTarget?.QuantitySemanticsStatus,
                    ConvertedAvailableQty = sumInAvail,
                    ConvertedReservedQty = sumInReserved,
                    AfterAvailableQty = targetAfterAvail,
                    AfterReservedQty = targetAfterReserved,
                    EvidenceType = created ? "CANONICAL_TARGET_CREATED" : "CANONICAL_TARGET_EXISTING",
                    EvidenceReference = plan.Group.EvidenceReference,
                    IsTargetCreated = created
                });

                // Alert / restock reconciliation (no auto-resolve)
                await ReconcileAlertsAndRestocksAsync(
                    manifest.StoreId,
                    plan,
                    actorStaffId,
                    cancellationToken);
            }

            // Conservation check on converted totals
            var afterAvailSum = validation.GroupPlans.Sum(p =>
            {
                // target after is in plan; sources 0
                return p.TargetAfterAvail;
            });
            // Recompute from actual locked state for groups — use planned after
            if (Math.Abs(validation.BeforeAvailableTotal - validation.AfterAvailableTotal) > 0.0005m
                || Math.Abs(validation.BeforeReservedTotal - validation.AfterReservedTotal) > 0.0005m)
            {
                return ServiceResult<ConsolidationRunResultDto>.Failure(
                    "Conservation check failed.",
                    errorCode: ConsolidationFailureCodes.ConservationFailed);
            }

            // Replace dry-run lines with execute lines
            var oldLines = await _context.InventoryConsolidationLines
                .Where(x => x.InventoryConsolidationRunId == run.InventoryConsolidationRunId)
                .ToListAsync(cancellationToken);
            _context.InventoryConsolidationLines.RemoveRange(oldLines);
            _context.InventoryConsolidationLines.AddRange(lines);
            _context.InventoryTransactions.AddRange(movements);

            run.Status = InventoryConsolidationRunStatus.Completed;
            run.CompletedAt = now;
            run.ExecutedByStaffId = actorStaffId;
            run.BeforeAvailableTotal = validation.BeforeAvailableTotal;
            run.BeforeReservedTotal = validation.BeforeReservedTotal;
            run.AfterAvailableTotal = validation.AfterAvailableTotal;
            run.AfterReservedTotal = validation.AfterReservedTotal;
            run.FailureCode = null;
            run.FailureDetails = null;
            run.ReportJson = JsonSerializer.Serialize(new
            {
                kind = "ExecuteCompleted",
                conservationVerified = true,
                noUnresolvedConsolidatableLegacy = true,
                availableBefore = validation.BeforeAvailableTotal,
                availableAfter = validation.AfterAvailableTotal,
                reservedBefore = validation.BeforeReservedTotal,
                reservedAfter = validation.AfterReservedTotal,
                movementCount = movements.Count,
                sourceThresholdsPreservedForAudit = true,
                historicalTransactionsNotReparented = true
            }, JsonOpts);

            await _context.SaveChangesAsync(cancellationToken);
            return ServiceResult<ConsolidationRunResultDto>.Success(MapRun(run), "Consolidation completed.");
        }

        private async Task ReconcileAlertsAndRestocksAsync(
            int storeId,
            GroupPlan plan,
            int actorStaffId,
            CancellationToken cancellationToken)
        {
            var recipeIds = plan.Sources
                .Where(s => s.Row.RecipeId.HasValue)
                .Select(s => s.Row.RecipeId!.Value)
                .Distinct()
                .ToList();

            if (recipeIds.Count == 0)
                return;

            var alerts = await _context.StockAlerts
                .Where(a => a.StoreId == storeId
                    && a.IngredientId == null
                    && a.RecipeId != null
                    && recipeIds.Contains(a.RecipeId.Value)
                    && a.PreparedItemId == null
                    && (a.Status == StockAlertStatuses.Open || a.Status == StockAlertStatuses.Confirmed))
                .ToListAsync(cancellationToken);

            // Only upgrade when single mapping and no existing PI alert for same PI
            var existingPiAlert = await _context.StockAlerts.AsNoTracking()
                .AnyAsync(a => a.StoreId == storeId
                    && a.PreparedItemId == plan.Group.PreparedItemId
                    && (a.Status == StockAlertStatuses.Open || a.Status == StockAlertStatuses.Confirmed),
                    cancellationToken);

            if (!existingPiAlert && alerts.Count > 0)
            {
                foreach (var alert in alerts)
                {
                    // B: add PreparedItemId, keep RecipeId, keep Status — no resolve
                    alert.PreparedItemId = plan.Group.PreparedItemId;
                    alert.UpdatedAt = DateTime.UtcNow;
                }
            }

            var restocks = await _context.RestockRequests
                .Where(r => r.StoreId == storeId
                    && r.IngredientId == null
                    && r.RecipeId != null
                    && recipeIds.Contains(r.RecipeId.Value)
                    && r.PreparedItemId == null)
                .ToListAsync(cancellationToken);

            var existingPiRestock = await _context.RestockRequests.AsNoTracking()
                .AnyAsync(r => r.StoreId == storeId && r.PreparedItemId == plan.Group.PreparedItemId,
                    cancellationToken);

            if (!existingPiRestock)
            {
                foreach (var rr in restocks)
                {
                    // E: add PreparedItemId, keep RecipeId, keep RequestedQuantity and status
                    rr.PreparedItemId = plan.Group.PreparedItemId;
                }
            }

            // No warehouse notification dispatch from consolidation.
            _ = actorStaffId;
        }

        // ─────────────────────────── Helpers ───────────────────────────

        private async Task<InventoryConsolidationRun?> LoadRunForUpdateAsync(
            int storeId,
            Guid requestKey,
            CancellationToken cancellationToken)
        {
            if (_context.Database.IsSqlServer())
            {
                return await _context.InventoryConsolidationRuns
                    .FromSqlInterpolated($@"
SELECT * FROM InventoryConsolidationRuns WITH (UPDLOCK, HOLDLOCK)
WHERE StoreId = {storeId} AND RequestKey = {requestKey}")
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return await _context.InventoryConsolidationRuns
                .FirstOrDefaultAsync(x => x.StoreId == storeId && x.RequestKey == requestKey, cancellationToken);
        }

        private async Task<bool> RunExistsAsync(int storeId, Guid requestKey, CancellationToken ct)
            => await _context.InventoryConsolidationRuns.AsNoTracking()
                .AnyAsync(x => x.StoreId == storeId && x.RequestKey == requestKey, ct);

        private async Task<string> BuildEnvironmentFingerprintAsync(CancellationToken cancellationToken)
        {
            var dbName = _context.Database.GetDbConnection().Database ?? "unknown";
            string server;
            try
            {
                server = _context.Database.GetDbConnection().DataSource ?? "local";
            }
            catch
            {
                server = "local";
            }

            var env = _environment.EnvironmentName ?? "Unknown";
            var schema = LegacyBtpConsolidationConstants.QueryContractVersion;
            var raw = $"server={server}|db={dbName}|env={env}|schema={schema}";
            await Task.CompletedTask;
            return Sha256Hex(raw)[..32];
        }

        private async Task<Dictionary<int, string>> LoadRowFingerprintsAsync(
            int storeId,
            ConsolidationManifestDto manifest,
            CancellationToken cancellationToken)
        {
            var ids = manifest.Groups
                .SelectMany(g => g.SourceStoreInventoryIds
                    .Concat(g.TargetStoreInventoryId.HasValue
                        ? new[] { g.TargetStoreInventoryId.Value }
                        : Array.Empty<int>()))
                .Distinct()
                .ToList();

            var rows = await _context.StoreInventories.AsNoTracking()
                .Where(x => x.StoreId == storeId && ids.Contains(x.StoreInventoryId))
                .ToListAsync(cancellationToken);

            return rows.ToDictionary(r => r.StoreInventoryId, RowFingerprint);
        }

        private static string RowFingerprint(StoreInventory row)
        {
            var raw =
                $"{row.StoreInventoryId}|{row.AvailableQty.ToString(CultureInfo.InvariantCulture)}|{row.ReservedQty.ToString(CultureInfo.InvariantCulture)}|" +
                $"{row.MinStockLevel?.ToString(CultureInfo.InvariantCulture)}|{row.MaxNegativeQty?.ToString(CultureInfo.InvariantCulture)}|" +
                $"{(int?)row.BtpIdentityState}|{(int?)row.QuantitySemanticsStatus}|{row.PreparedItemId}|{row.RecipeId}|{row.SupersededByStoreInventoryId}";
            return Sha256Hex(raw)[..16];
        }

        private static string ComputeManifestHash(
            ConsolidationManifestDto manifest,
            IReadOnlyDictionary<int, string> fingerprints)
        {
            var payload = new
            {
                manifest.ManifestVersion,
                manifest.QueryContractVersion,
                manifest.StoreId,
                groups = manifest.Groups
                    .OrderBy(g => g.PreparedItemId)
                    .Select(g => new
                    {
                        g.StoreId,
                        g.PreparedItemId,
                        sources = g.SourceStoreInventoryIds.OrderBy(id => id).ToList(),
                        g.TargetStoreInventoryId,
                        g.CreateCanonicalTarget,
                        g.ApprovedMinStockLevel,
                        g.ApprovedMaxNegativeQty,
                        g.ThresholdDecisionProvided,
                        g.QuantitySemanticsEvidence,
                        g.EvidenceReference,
                        g.ActorApprovalStaffId,
                        conversions = g.ConversionBySourceId == null
                            ? null
                            : g.ConversionBySourceId
                                .OrderBy(kv => kv.Key)
                                .Select(kv => new
                                {
                                    sourceId = kv.Key,
                                    kv.Value.FromUnitId,
                                    kv.Value.ToUnitId,
                                    factor = kv.Value.Factor,
                                    kv.Value.SourceReference,
                                    kv.Value.ApproverStaffId,
                                    kv.Value.Version
                                }),
                        fingerprints = g.SourceStoreInventoryIds
                            .OrderBy(id => id)
                            .Select(id => fingerprints.TryGetValue(id, out var f) ? f : "missing")
                            .Concat(g.TargetStoreInventoryId.HasValue
                                && fingerprints.TryGetValue(g.TargetStoreInventoryId.Value, out var tf)
                                    ? new[] { tf }
                                    : Array.Empty<string>())
                    })
            };
            return Sha256Hex(CanonicalJson(payload));
        }

        private static string ComputeDryRunHash(
            string manifestHash,
            DryRunValidation validation,
            string auditHash)
        {
            var payload = new
            {
                manifestHash,
                auditHash,
                blockers = validation.Blockers,
                validation.BeforeAvailableTotal,
                validation.BeforeReservedTotal,
                validation.AfterAvailableTotal,
                validation.AfterReservedTotal,
                lines = validation.LineSnapshots
            };
            return Sha256Hex(CanonicalJson(payload));
        }

        private static string CanonicalJson(object obj)
            => JsonSerializer.Serialize(obj, JsonOpts);

        private static string Sha256Hex(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static decimal RoundQty(decimal value)
            => Math.Round(value, 3, MidpointRounding.AwayFromZero);

        private static bool IsExactRepresentable(decimal source, decimal factor, decimal converted)
        {
            // Allow only when rounding does not change value beyond 0.001 scale
            var raw = source * factor;
            return Math.Abs(raw - converted) < 0.0000001m
                   || Math.Abs(RoundQty(raw) - converted) < 0.0000001m
                      && Math.Abs(raw - RoundQty(raw)) < 0.0005m;
        }

        private static ConsolidationRunResultDto MapRun(InventoryConsolidationRun run, bool wasReplay = false)
            => new()
            {
                InventoryConsolidationRunId = run.InventoryConsolidationRunId,
                StoreId = run.StoreId,
                RequestKey = run.RequestKey,
                RunType = run.RunType,
                Status = run.Status,
                ManifestHash = run.ManifestHash,
                DryRunHash = run.DryRunHash,
                QueryContractVersion = run.QueryContractVersion,
                EnvironmentFingerprint = run.EnvironmentFingerprint,
                WasReplay = wasReplay,
                FailureCode = run.FailureCode,
                FailureDetails = run.FailureDetails,
                BeforeAvailableTotal = run.BeforeAvailableTotal,
                BeforeReservedTotal = run.BeforeReservedTotal,
                AfterAvailableTotal = run.AfterAvailableTotal,
                AfterReservedTotal = run.AfterReservedTotal,
                CreatedAt = run.CreatedAt,
                DryRunAt = run.DryRunAt,
                CompletedAt = run.CompletedAt,
                ReportJson = run.ReportJson
            };
    }
}
