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

        public async Task<ServiceResult<int>> CreateOrOpenFromReorderSuggestionAsync(
            int storeId,
            int ingredientId,
            string source)
        {
            if (storeId <= 0 || ingredientId <= 0)
                return ServiceResult<int>.Failure("Cửa hàng hoặc nguyên liệu không hợp lệ.");

            var inventoryId = await _context.StoreInventories
                .AsNoTracking()
                .Where(x => x.StoreId == storeId && x.IngredientId == ingredientId)
                .OrderBy(x => x.StoreInventoryId)
                .Select(x => (int?)x.StoreInventoryId)
                .FirstOrDefaultAsync();
            if (!inventoryId.HasValue)
                return ServiceResult<int>.Failure("Chưa có tồn kho nguyên liệu tại cửa hàng để tạo cảnh báo.");

            var evaluation = await EvaluateStoreInventoryItemAsync(inventoryId.Value, source);
            if (!evaluation.IsSuccess)
                return ServiceResult<int>.Failure(evaluation.Message ?? "Không đánh giá được cảnh báo tồn kho.");

            var alertId = await _context.StockAlerts
                .AsNoTracking()
                .Where(x => x.StoreId == storeId
                    && x.IngredientId == ingredientId
                    && StockAlertStatuses.ActiveValues.Contains(x.Status))
                .OrderBy(x => x.StockAlertId)
                .Select(x => (int?)x.StockAlertId)
                .FirstOrDefaultAsync();
            if (!alertId.HasValue)
            {
                return ServiceResult<int>.Failure(
                    "Gợi ý hiện chưa tạo cảnh báo vì tồn thực tế chưa chạm ngưỡng cảnh báo. Vui lòng kiểm tra lại ngưỡng tồn.");
            }

            return ServiceResult<int>.Success(
                alertId.Value,
                evaluation.Data?.CreatedCount > 0
                    ? "Đã tạo cảnh báo tồn kho để quản lý xác nhận."
                    : "Đã mở cảnh báo tồn kho đang hoạt động.");
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

            // BTP alerts always use canonical PreparedItem identity. Writer mode only decides
            // which StoreInventory row is authoritative, never the alert key.
            var btpRows = items.Where(i => i.IngredientId == null).ToList();
            var byPi = new Dictionary<int, List<StoreInventory>>();
            foreach (var row in btpRows)
            {
                var pi = ResolveEffectivePreparedItemId(row);
                if (!pi.HasValue)
                {
                    summary.EvaluatedCount++;
                    summary.ReviewCount++;
                    _logger.LogWarning(
                        "[StockAlert] Unmapped BTP inventory skipped StoreId={StoreId} RecipeId={RecipeId}",
                        storeId,
                        row.RecipeId);
                    continue;
                }

                if (!byPi.TryGetValue(pi.Value, out var list))
                {
                    list = new List<StoreInventory>();
                    byPi[pi.Value] = list;
                }
                list.Add(row);
            }

            foreach (var (piId, rows) in byPi)
            {
                await EvaluatePreparedItemGroupAsync(storeId, piId, rows, source, summary);
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
                        && StockAlertStatuses.ActiveValues.Contains(a.Status)
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

            var preparedItemId = ResolveEffectivePreparedItemId(first);
            if (preparedItemId.HasValue)
            {
                await EvaluatePreparedItemGroupAsync(storeId, preparedItemId.Value, rows, source, summary);
                return;
            }

            summary.EvaluatedCount++;
            summary.ReviewCount++;
            _logger.LogWarning(
                "[StockAlert] BTP inventory has no canonical PreparedItem StoreId={StoreId} RecipeId={RecipeId}",
                storeId,
                first.RecipeId);
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
                    StockAlertStatuses.ActiveValues.Contains(a.Status) &&
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
            var qty = CalculateUsableQuantity(item);

            StockAlert? openAlert = existingOpen;
            if (openAlert == null && openLookup != null)
                openAlert = await openLookup();

            var isManualDemand = openAlert != null
                && openAlert.Source == StockAlertSources.SalesReport
                && (openAlert.AlertType == StockAlertTypes.ManualReview || !min.HasValue);
            if (isManualDemand && openAlert!.ThresholdSnapshot.HasValue)
            {
                if (qty < openAlert.ThresholdSnapshot.Value)
                {
                    return;
                }

                var previousStatus = openAlert.Status;
                var previousType = openAlert.AlertType;
                var previousSeverity = openAlert.Severity;
                openAlert.Status = StockAlertStatuses.Resolved;
                openAlert.UpdatedAt = DateTime.UtcNow;
                openAlert.ResolvedAt = DateTime.UtcNow;
                openAlert.ResolvedReason = "Stock reached the verified manual demand target";
                openAlert.CurrentQtySnapshot = qty;
                openAlert.Source = source;
                AddTransition(
                    openAlert,
                    item,
                    previousStatus,
                    previousType,
                    previousSeverity,
                    source,
                    "Tồn khả dụng đã đạt mục tiêu bổ sung thủ công.");
                summary.ResolvedCount++;
                return;
            }

            if (!min.HasValue)
            {
                summary.SkippedUnconfiguredCount++;
                return;
            }

            if (qty >= min.Value)
            {
                if (openAlert != null)
                {
                    var previousStatus = openAlert.Status;
                    var previousType = openAlert.AlertType;
                    var previousSeverity = openAlert.Severity;
                    openAlert.Status = StockAlertStatuses.Resolved;
                    openAlert.UpdatedAt = DateTime.UtcNow;
                    openAlert.ResolvedAt = DateTime.UtcNow;
                    openAlert.ResolvedReason = "Stock replenished above MinStockLevel";
                    openAlert.CurrentQtySnapshot = qty;
                    openAlert.ThresholdSnapshot = min;
                    openAlert.Source = source;
                    openAlert.Note = AppendNote(openAlert.Note, $"Resolved via {source}");
                    AddTransition(
                        openAlert,
                        item,
                        previousStatus,
                        previousType,
                        previousSeverity,
                        source,
                        "Tồn khả dụng đã cao hơn ngưỡng tối thiểu.");
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
                    CurrentQtySnapshot = CalculateUsableQuantity(item),
                    ThresholdSnapshot = item.MinStockLevel,
                    Source = source,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                createIdentity(alert);
                _context.StockAlerts.Add(alert);
                AddTransition(
                    alert,
                    item,
                    previousStatus: null,
                    previousAlertType: null,
                    previousSeverity: null,
                    source,
                    "Phát hiện tồn kho dưới ngưỡng.");
                summary.CreatedCount++;
                await Task.CompletedTask;
                return;
            }

            // Reuse existing OPEN — do not rewrite identity tuple (no backfill convert).
            var changed =
                openAlert.AlertType != alertType ||
                openAlert.Severity != severity ||
                openAlert.CurrentQtySnapshot != CalculateUsableQuantity(item) ||
                openAlert.ThresholdSnapshot != item.MinStockLevel ||
                openAlert.Source != source;

            if (!changed)
                return;

            var wasLow = openAlert.AlertType == StockAlertTypes.LowStock;
            var nowOut = alertType == StockAlertTypes.OutOfStock;
            var previousType = openAlert.AlertType;
            var previousSeverity = openAlert.Severity;

            openAlert.AlertType = alertType;
            openAlert.Severity = severity;
            openAlert.CurrentQtySnapshot = CalculateUsableQuantity(item);
            openAlert.ThresholdSnapshot = item.MinStockLevel;
            openAlert.Source = source;
            openAlert.UpdatedAt = now;
            if (wasLow && nowOut)
                openAlert.Note = AppendNote(openAlert.Note, $"Escalated LOW_STOCK → OUT_OF_STOCK via {source}");

            if (previousType != alertType || previousSeverity != severity)
            {
                AddTransition(
                    openAlert,
                    item,
                    openAlert.Status,
                    previousType,
                    previousSeverity,
                    source,
                    wasLow && nowOut
                        ? "Tồn khả dụng giảm từ LOW_STOCK xuống OUT_OF_STOCK."
                        : "Trạng thái tồn kho thay đổi trong khi cảnh báo vẫn đang hoạt động.");
            }

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
                var notifications = CaptureNotificationTransitions();
                await _context.SaveChangesAsync();
                await NotifyTransitionsAsync(notifications);
            }
            catch (DbUpdateException ex) when (IsUniqueOpenAlertConflict(ex))
            {
                _logger.LogWarning(
                    ex,
                    "[StockAlert] Unique OPEN conflict StoreId={StoreId} — treating as concurrent create race",
                    summary.StoreId);

                // Discard poisoned inserts, reload the winner and apply any LOW -> OUT escalation.
                _context.ChangeTracker.Clear();
                ResetSummaryCounts(summary);
                await ReevaluateStoreAfterRaceAsync(summary);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "[StockAlert] Concurrent active alert update StoreId={StoreId}; reloading winner",
                    summary.StoreId);
                _context.ChangeTracker.Clear();
                ResetSummaryCounts(summary);
                await ReevaluateStoreAfterRaceAsync(summary);
            }
        }

        private static bool IsUniqueOpenAlertConflict(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("UX_StockAlert_Active_", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("UX_StockAlert_Open_", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ReevaluateStoreAfterRaceAsync(StockAlertEvaluationResultDto summary)
        {
            var items = await _context.StoreInventories
                .Include(i => i.Recipe)
                .Include(i => i.PreparedItem)
                .Where(i => i.StoreId == summary.StoreId)
                .ToListAsync();
            var mode = await ReadWriterModeOnceAsync(summary.StoreId);
            await EvaluateAllGroupsAsync(summary.StoreId, mode, summary.Source, summary, items);
            var notifications = CaptureNotificationTransitions();
            await _context.SaveChangesAsync();
            await NotifyTransitionsAsync(notifications);
        }

        private static void ResetSummaryCounts(StockAlertEvaluationResultDto summary)
        {
            summary.CreatedCount = 0;
            summary.UpdatedCount = 0;
            summary.ResolvedCount = 0;
            summary.SkippedUnconfiguredCount = 0;
            summary.EvaluatedCount = 0;
            summary.ReviewCount = 0;
        }

        private void AddTransition(
            StockAlert alert,
            StoreInventory inventory,
            string? previousStatus,
            string? previousAlertType,
            string? previousSeverity,
            string source,
            string reason)
        {
            _context.StockAlertTransitions.Add(new StockAlertTransition
            {
                StockAlert = alert,
                PreviousStatus = previousStatus,
                NewStatus = alert.Status,
                PreviousAlertType = previousAlertType,
                NewAlertType = alert.AlertType,
                PreviousSeverity = previousSeverity,
                NewSeverity = alert.Severity,
                OnHandSnapshot = inventory.AvailableQty,
                ReservedSnapshot = inventory.ReservedQty,
                AvailableSnapshot = CalculateUsableQuantity(inventory),
                MinLevelSnapshot = inventory.MinStockLevel,
                SourceType = string.IsNullOrWhiteSpace(source) ? StockAlertSources.Auto : source.Trim(),
                Reason = reason,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        private List<StockAlertTransition> CaptureNotificationTransitions() =>
            _context.ChangeTracker.Entries<StockAlertTransition>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .Where(t =>
                    t.PreviousStatus == null
                    || (t.PreviousAlertType == StockAlertTypes.LowStock
                        && t.NewAlertType == StockAlertTypes.OutOfStock))
                .ToList();

        private async Task NotifyTransitionsAsync(IReadOnlyCollection<StockAlertTransition> transitions)
        {
            foreach (var transition in transitions)
            {
                var alert = transition.StockAlert;
                var type = transition.PreviousStatus == null
                    ? StaffNotificationTypes.StockAlertCreated
                    : StaffNotificationTypes.StockAlertEscalated;
                var recipients = await _context.Staffs
                    .AsNoTracking()
                    .Where(s =>
                        s.StoreId == alert.StoreId &&
                        s.Active &&
                        s.Account != null &&
                        s.Account.Active &&
                        s.Account.AccountRoles.Any(ar =>
                            ar.Role != null && ar.Role.Active && ar.Role.Name == RoleConstants.StoreManager))
                    .Select(s => s.StaffId)
                    .Distinct()
                    .ToListAsync();

                foreach (var recipientId in recipients)
                {
                    var exists = await _context.StaffNotifications.AsNoTracking().AnyAsync(n =>
                        n.RecipientStaffId == recipientId &&
                        n.Type == type &&
                        n.EntityType == StaffNotificationEntityTypes.StockAlert &&
                        n.EntityId == alert.StockAlertId);
                    if (exists)
                        continue;

                    _context.StaffNotifications.Add(new CafeChain.Models.Operations.StaffNotification
                    {
                        StoreId = alert.StoreId,
                        RecipientStaffId = recipientId,
                        Type = type,
                        Title = type == StaffNotificationTypes.StockAlertCreated
                            ? "Cảnh báo tồn kho mới"
                            : "Cảnh báo tồn kho đã chuyển mức khẩn cấp",
                        Body = $"Cảnh báo #{alert.StockAlertId}: {alert.AlertType}, tồn khả dụng {transition.AvailableSnapshot:N3}.",
                        EntityType = StaffNotificationEntityTypes.StockAlert,
                        EntityId = alert.StockAlertId,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow,
                        EmailAttempted = false,
                        EmailSent = false
                    });
                }
            }

            if (_context.ChangeTracker.Entries<CafeChain.Models.Operations.StaffNotification>()
                .Any(e => e.State == EntityState.Added))
            {
                await _context.SaveChangesAsync();
            }
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

        private static decimal CalculateUsableQuantity(StoreInventory item) =>
            item.AvailableQty - item.ReservedQty;

        private static string? AppendNote(string? existing, string addition)
        {
            if (string.IsNullOrWhiteSpace(existing))
                return addition;
            var combined = $"{existing}; {addition}";
            return combined.Length <= 500 ? combined : combined[..500];
        }
    }
}
