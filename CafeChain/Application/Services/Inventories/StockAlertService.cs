using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #97 — Stock alert detection + unresolved duplicate guard.
    /// Issue #122 — PreparedItem stable identity for BTP alerts (mode-aware).
    /// </summary>
    public class StockAlertService : IStockAlertService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StockAlertService> _logger;
        private readonly IInventoryWriterModeService? _writerModeService;

        public StockAlertService(
            AppDbContext context,
            ILogger<StockAlertService> logger,
            IInventoryWriterModeService? writerModeService = null)
        {
            _context = context;
            _logger = logger;
            _writerModeService = writerModeService;
        }

        public async Task<ServiceResult<StockAlertEvaluationResultDto>> EvaluateStoreInventoryItemAsync(
            int storeInventoryId,
            string source)
        {
            var item = await _context.StoreInventories
                .Include(i => i.Recipe)
                .Include(i => i.PreparedItem)
                .FirstOrDefaultAsync(i => i.StoreInventoryId == storeInventoryId);

            if (item == null)
                return ServiceResult<StockAlertEvaluationResultDto>.Failure("Không tìm thấy tồn kho.");

            var summary = NewSummary(item.StoreId, source);
            var mode = await ReadWriterModeOnceAsync(item.StoreId);
            await EvaluateIdentityGroupAsync(
                item.StoreId,
                mode,
                source,
                summary,
                new List<StoreInventory> { item });
            await SaveWithOpenAlertRaceRetryAsync(summary);
            return ServiceResult<StockAlertEvaluationResultDto>.Success(summary);
        }

        public async Task<ServiceResult<StockAlertEvaluationResultDto>> EvaluateStoreAsync(
            int storeId,
            string source)
        {
            if (storeId <= 0)
                return ServiceResult<StockAlertEvaluationResultDto>.Failure("StoreId không hợp lệ.");

            var items = await _context.StoreInventories
                .Include(i => i.Recipe)
                .Include(i => i.PreparedItem)
                .Where(i => i.StoreId == storeId)
                .ToListAsync();

            var summary = NewSummary(storeId, source);
            var mode = await ReadWriterModeOnceAsync(storeId);
            await EvaluateAllGroupsAsync(storeId, mode, source, summary, items);
            await SaveWithOpenAlertRaceRetryAsync(summary);
            return ServiceResult<StockAlertEvaluationResultDto>.Success(summary);
        }

        public async Task<ServiceResult<StockAlertEvaluationResultDto>> EvaluateAfterInventoryChangeAsync(
            int storeId,
            int? ingredientId,
            int? recipeId,
            string source)
        {
            if (storeId <= 0)
                return ServiceResult<StockAlertEvaluationResultDto>.Failure("StoreId không hợp lệ.");

            if (ingredientId.HasValue == recipeId.HasValue)
                return ServiceResult<StockAlertEvaluationResultDto>.Failure(
                    "Cần đúng một trong IngredientId hoặc RecipeId.");

            var item = await _context.StoreInventories
                .Include(i => i.Recipe)
                .Include(i => i.PreparedItem)
                .FirstOrDefaultAsync(i =>
                    i.StoreId == storeId &&
                    i.IngredientId == ingredientId &&
                    i.RecipeId == recipeId);

            var summary = NewSummary(storeId, source);
            if (item == null)
                return ServiceResult<StockAlertEvaluationResultDto>.Success(summary);

            var mode = await ReadWriterModeOnceAsync(storeId);
            await EvaluateIdentityGroupAsync(storeId, mode, source, summary, new List<StoreInventory> { item });
            await SaveWithOpenAlertRaceRetryAsync(summary);
            return ServiceResult<StockAlertEvaluationResultDto>.Success(summary);
        }

        private async Task EvaluateAllGroupsAsync(
            int storeId,
            InventoryWriterMode mode,
            string source,
            StockAlertEvaluationResultDto summary,
            List<StoreInventory> items)
        {
            // Ingredient groups
            foreach (var group in items.Where(i => i.IngredientId.HasValue).GroupBy(i => i.IngredientId!.Value))
            {
                await EvaluateIdentityGroupAsync(storeId, mode, source, summary, group.ToList());
            }

            // BTP groups
            if (mode == InventoryWriterMode.PreparedItem)
            {
                var btpRows = items.Where(i => i.IngredientId == null).ToList();
                var byPi = new Dictionary<int, List<StoreInventory>>();
                var unmapped = new List<StoreInventory>();

                foreach (var row in btpRows)
                {
                    var pi = ResolveEffectivePreparedItemId(row);
                    if (pi.HasValue)
                    {
                        if (!byPi.TryGetValue(pi.Value, out var list))
                        {
                            list = new List<StoreInventory>();
                            byPi[pi.Value] = list;
                        }

                        list.Add(row);
                    }
                    else if (row.RecipeId.HasValue)
                    {
                        // Legacy recipe without mapping — do not create Recipe-keyed alert in Prepared mode.
                        unmapped.Add(row);
                    }
                }

                foreach (var (piId, rows) in byPi)
                {
                    await EvaluatePreparedItemGroupAsync(storeId, piId, rows, source, summary);
                }

                foreach (var row in unmapped)
                {
                    summary.EvaluatedCount++;
                    summary.ReviewCount++;
                    _logger.LogWarning(
                        "[StockAlert] Prepared mode unmapped Recipe inventory skipped StoreId={StoreId} RecipeId={RecipeId}",
                        storeId, row.RecipeId);
                }
            }
            else
            {
                // LegacyRecipe or Blocked (or missing config treated as Legacy): Recipe-keyed BTP
                foreach (var group in items
                             .Where(i => i.IngredientId == null && i.RecipeId.HasValue)
                             .GroupBy(i => i.RecipeId!.Value))
                {
                    await EvaluateIdentityGroupAsync(storeId, mode, source, summary, group.ToList());
                }
            }
        }

        private async Task EvaluateIdentityGroupAsync(
            int storeId,
            InventoryWriterMode mode,
            string source,
            StockAlertEvaluationResultDto summary,
            List<StoreInventory> rows)
        {
            if (rows.Count == 0)
                return;

            var first = rows[0];
            if (first.IngredientId.HasValue)
            {
                // One authoritative ingredient row (first by id if duplicates)
                var auth = rows.OrderBy(r => r.StoreInventoryId).First();
                summary.EvaluatedCount++;
                await EvaluateThresholdAsync(
                    storeId,
                    source,
                    summary,
                    auth,
                    openLookup: () => _context.StockAlerts.FirstOrDefaultAsync(a =>
                        a.StoreId == storeId
                        && a.Status == StockAlertStatuses.Open
                        && a.IngredientId == auth.IngredientId
                        && a.RecipeId == null
                        && a.PreparedItemId == null),
                    createIdentity: (a) =>
                    {
                        a.IngredientId = auth.IngredientId;
                        a.RecipeId = null;
                        a.PreparedItemId = null;
                    });
                return;
            }

            // BTP path — only when not PreparedItem mode (handled by EvaluatePreparedItemGroupAsync)
            if (mode == InventoryWriterMode.PreparedItem)
            {
                var pi = ResolveEffectivePreparedItemId(first);
                if (pi.HasValue)
                    await EvaluatePreparedItemGroupAsync(storeId, pi.Value, rows, source, summary);
                return;
            }

            // Legacy Recipe-keyed
            var recipeAuth = rows.OrderBy(r => r.StoreInventoryId).First();
            summary.EvaluatedCount++;
            var recipeId = recipeAuth.RecipeId;
            await EvaluateThresholdAsync(
                storeId,
                source,
                summary,
                recipeAuth,
                openLookup: () => _context.StockAlerts.FirstOrDefaultAsync(a =>
                    a.StoreId == storeId
                    && a.Status == StockAlertStatuses.Open
                    && a.IngredientId == null
                    && a.RecipeId == recipeId),
                createIdentity: (a) =>
                {
                    a.IngredientId = null;
                    a.RecipeId = recipeAuth.RecipeId;
                    a.PreparedItemId = null;
                });
        }

        private async Task EvaluatePreparedItemGroupAsync(
            int storeId,
            int preparedItemId,
            List<StoreInventory> rows,
            string source,
            StockAlertEvaluationResultDto summary)
        {
            summary.EvaluatedCount++;

            var auth = SelectAuthoritativePreparedInventory(rows);
            if (auth == null)
            {
                summary.ReviewCount++;
                _logger.LogWarning(
                    "[StockAlert] PreparedItem identity review StoreId={StoreId} PreparedItemId={PreparedItemId} Candidates={Count}",
                    storeId,
                    preparedItemId,
                    rows.Count);
                return;
            }

            // Existing OPEN candidates for this stable PI (PI key or mapped Recipe key).
            var openCandidates = await FindOpenCandidatesForPreparedItemAsync(storeId, preparedItemId, rows);
            if (openCandidates.Count > 1)
            {
                summary.ReviewCount++;
                _logger.LogWarning(
                    "[StockAlert] Multiple OPEN alerts for stable PreparedItem StoreId={StoreId} PreparedItemId={PreparedItemId} AlertIds={Ids}",
                    storeId,
                    preparedItemId,
                    string.Join(",", openCandidates.Select(a => a.StockAlertId)));
                return;
            }

            StockAlert? openAlert = openCandidates.Count == 1 ? openCandidates[0] : null;

            await EvaluateThresholdAsync(
                storeId,
                source,
                summary,
                auth,
                openLookup: null,
                createIdentity: (a) =>
                {
                    // New alerts in Prepared mode are PreparedItem-only.
                    a.IngredientId = null;
                    a.RecipeId = null;
                    a.PreparedItemId = preparedItemId;
                },
                existingOpen: openAlert,
                preserveExistingIdentity: true);
        }

        private static StoreInventory? SelectAuthoritativePreparedInventory(List<StoreInventory> rows)
        {
            var nonSuperseded = rows
                .Where(r => r.BtpIdentityState != BtpIdentityState.Superseded)
                .OrderBy(r => r.StoreInventoryId)
                .ToList();

            if (nonSuperseded.Count == 0)
                return null;

            // Collision: more than one Canonical, or Canonical + Legacy together
            var canonical = nonSuperseded
                .Where(r => r.BtpIdentityState == BtpIdentityState.Canonical)
                .ToList();
            if (canonical.Count > 1)
                return null;
            if (canonical.Count == 1 && nonSuperseded.Any(r => r.BtpIdentityState == BtpIdentityState.Legacy))
                return null;

            if (canonical.Count == 1)
            {
                var c = canonical[0];
                if (c.QuantitySemanticsStatus != InventoryQuantitySemanticsStatus.BaseUnitConfirmed)
                    return null;
                if (!c.PreparedItemId.HasValue)
                    return null;
                return c;
            }

            // No canonical: single compatibility/PI row with confirmed semantics may be used
            if (nonSuperseded.Count != 1)
                return null;

            var only = nonSuperseded[0];
            if (only.QuantitySemanticsStatus != InventoryQuantitySemanticsStatus.BaseUnitConfirmed)
                return null;
            if (!only.PreparedItemId.HasValue && only.Recipe?.PreparedItemId == null)
                return null;
            return only;
        }

        private static int? ResolveEffectivePreparedItemId(StoreInventory row)
        {
            if (row.PreparedItemId.HasValue)
                return row.PreparedItemId;
            if (row.Recipe?.PreparedItemId != null)
                return row.Recipe.PreparedItemId;
            return null;
        }

        private async Task<List<StockAlert>> FindOpenCandidatesForPreparedItemAsync(
            int storeId,
            int preparedItemId,
            List<StoreInventory> rows)
        {
            var recipeIds = rows
                .Where(r => r.RecipeId.HasValue)
                .Select(r => r.RecipeId!.Value)
                .Distinct()
                .ToList();

            // Also recipes that map to this PI even if not in inventory list
            var mappedRecipeIds = await _context.Recipes.AsNoTracking()
                .Where(r => r.PreparedItemId == preparedItemId)
                .Select(r => r.RecipeId)
                .ToListAsync();
            recipeIds = recipeIds.Union(mappedRecipeIds).Distinct().ToList();

            return await _context.StockAlerts
                .Where(a =>
                    a.StoreId == storeId &&
                    a.Status == StockAlertStatuses.Open &&
                    (
                        a.PreparedItemId == preparedItemId
                        || (a.RecipeId != null && recipeIds.Contains(a.RecipeId.Value))
                    ))
                .ToListAsync();
        }

        private async Task EvaluateThresholdAsync(
            int storeId,
            string source,
            StockAlertEvaluationResultDto summary,
            StoreInventory item,
            Func<Task<StockAlert?>>? openLookup,
            Action<StockAlert> createIdentity,
            StockAlert? existingOpen = null,
            bool preserveExistingIdentity = false)
        {
            var min = item.MinStockLevel;
            var qty = item.AvailableQty;

            StockAlert? openAlert = existingOpen;
            if (openAlert == null && openLookup != null)
                openAlert = await openLookup();

            if (!min.HasValue)
            {
                summary.SkippedUnconfiguredCount++;
                return;
            }

            if (qty > min.Value)
            {
                if (openAlert != null)
                {
                    openAlert.Status = StockAlertStatuses.Resolved;
                    openAlert.UpdatedAt = DateTime.UtcNow;
                    openAlert.ResolvedAt = DateTime.UtcNow;
                    openAlert.ResolvedReason = "Stock replenished above MinStockLevel";
                    openAlert.CurrentQtySnapshot = qty;
                    openAlert.ThresholdSnapshot = min;
                    openAlert.Source = source;
                    openAlert.Note = AppendNote(openAlert.Note, $"Resolved via {source}");
                    summary.ResolvedCount++;
                }

                return;
            }

            var alertType = qty <= 0 ? StockAlertTypes.OutOfStock : StockAlertTypes.LowStock;
            var severity = qty <= 0 ? StockAlertSeverities.Urgent : StockAlertSeverities.Warning;
            await UpsertAlertAsync(
                storeId,
                item,
                openAlert,
                alertType,
                severity,
                source,
                summary,
                createIdentity,
                preserveExistingIdentity);
        }

        private async Task UpsertAlertAsync(
            int storeId,
            StoreInventory item,
            StockAlert? openAlert,
            string alertType,
            string severity,
            string source,
            StockAlertEvaluationResultDto summary,
            Action<StockAlert> createIdentity,
            bool preserveExistingIdentity)
        {
            var now = DateTime.UtcNow;

            if (openAlert == null)
            {
                var alert = new StockAlert
                {
                    StoreId = storeId,
                    AlertType = alertType,
                    Severity = severity,
                    Status = StockAlertStatuses.Open,
                    CurrentQtySnapshot = item.AvailableQty,
                    ThresholdSnapshot = item.MinStockLevel,
                    Source = source,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                createIdentity(alert);
                _context.StockAlerts.Add(alert);
                summary.CreatedCount++;
                await Task.CompletedTask;
                return;
            }

            // Reuse existing OPEN — do not rewrite identity tuple (no backfill convert).
            var changed =
                openAlert.AlertType != alertType ||
                openAlert.Severity != severity ||
                openAlert.CurrentQtySnapshot != item.AvailableQty ||
                openAlert.ThresholdSnapshot != item.MinStockLevel ||
                openAlert.Source != source;

            if (!changed)
                return;

            var wasLow = openAlert.AlertType == StockAlertTypes.LowStock;
            var nowOut = alertType == StockAlertTypes.OutOfStock;

            openAlert.AlertType = alertType;
            openAlert.Severity = severity;
            openAlert.CurrentQtySnapshot = item.AvailableQty;
            openAlert.ThresholdSnapshot = item.MinStockLevel;
            openAlert.Source = source;
            openAlert.UpdatedAt = now;
            if (wasLow && nowOut)
                openAlert.Note = AppendNote(openAlert.Note, $"Escalated LOW_STOCK → OUT_OF_STOCK via {source}");

            // preserveExistingIdentity: leave IngredientId/RecipeId/PreparedItemId as stored
            if (!preserveExistingIdentity)
            {
                // no-op for current callers that already match
            }

            summary.UpdatedCount++;
            await Task.CompletedTask;
        }

        private async Task SaveWithOpenAlertRaceRetryAsync(StockAlertEvaluationResultDto summary)
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueOpenAlertConflict(ex))
            {
                _logger.LogWarning(
                    ex,
                    "[StockAlert] Unique OPEN conflict StoreId={StoreId} — treating as concurrent create race",
                    summary.StoreId);

                // Discard poisoned inserts; winner already exists in DB.
                _context.ChangeTracker.Clear();
                if (summary.CreatedCount > 0)
                {
                    summary.CreatedCount--;
                    summary.UpdatedCount++;
                }
            }
        }

        private static bool IsUniqueOpenAlertConflict(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("UX_StockAlert_Open", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("unique", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<InventoryWriterMode> ReadWriterModeOnceAsync(int storeId)
        {
            if (_writerModeService == null)
                return InventoryWriterMode.LegacyRecipe;

            var status = await _writerModeService.GetStatusAsync(storeId);
            if (!status.IsSuccess || status.Data == null)
                return InventoryWriterMode.LegacyRecipe;

            return status.Data.WriterMode;
        }

        private static StockAlertEvaluationResultDto NewSummary(int storeId, string source) => new()
        {
            StoreId = storeId,
            Source = source
        };

        private static string? AppendNote(string? existing, string addition)
        {
            if (string.IsNullOrWhiteSpace(existing))
                return addition;
            var combined = $"{existing}; {addition}";
            return combined.Length <= 500 ? combined : combined[..500];
        }
    }
}
