using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #97 — Stock alert detection + unresolved duplicate guard (ADR-0004 identity).
    /// </summary>
    public class StockAlertService : IStockAlertService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StockAlertService> _logger;

        public StockAlertService(AppDbContext context, ILogger<StockAlertService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult<StockAlertEvaluationResultDto>> EvaluateStoreInventoryItemAsync(
            int storeInventoryId,
            string source)
        {
            var item = await _context.StoreInventories
                .FirstOrDefaultAsync(i => i.StoreInventoryId == storeInventoryId);

            if (item == null)
                return ServiceResult<StockAlertEvaluationResultDto>.Failure("Không tìm thấy tồn kho.");

            var summary = NewSummary(item.StoreId, source);
            await EvaluateItemCoreAsync(item, source, summary);
            await _context.SaveChangesAsync();
            return ServiceResult<StockAlertEvaluationResultDto>.Success(summary);
        }

        public async Task<ServiceResult<StockAlertEvaluationResultDto>> EvaluateStoreAsync(
            int storeId,
            string source)
        {
            if (storeId <= 0)
                return ServiceResult<StockAlertEvaluationResultDto>.Failure("StoreId không hợp lệ.");

            var items = await _context.StoreInventories
                .Where(i => i.StoreId == storeId)
                .ToListAsync();

            var summary = NewSummary(storeId, source);
            foreach (var item in items)
            {
                await EvaluateItemCoreAsync(item, source, summary);
            }

            await _context.SaveChangesAsync();
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
                .FirstOrDefaultAsync(i =>
                    i.StoreId == storeId &&
                    i.IngredientId == ingredientId &&
                    i.RecipeId == recipeId);

            var summary = NewSummary(storeId, source);
            if (item == null)
            {
                // No row — nothing to alert (do not auto-create inventory).
                return ServiceResult<StockAlertEvaluationResultDto>.Success(summary);
            }

            await EvaluateItemCoreAsync(item, source, summary);
            await _context.SaveChangesAsync();
            return ServiceResult<StockAlertEvaluationResultDto>.Success(summary);
        }

        private async Task EvaluateItemCoreAsync(
            StoreInventory item,
            string source,
            StockAlertEvaluationResultDto summary)
        {
            summary.EvaluatedCount++;

            var min = item.MinStockLevel;
            var qty = item.AvailableQty;
            var openAlert = await FindOpenAlertAsync(item.StoreId, item.IngredientId, item.RecipeId);

            // Unconfigured threshold: skip auto alerts entirely (including OUT_OF_STOCK).
            if (!min.HasValue)
            {
                summary.SkippedUnconfiguredCount++;
                // If somehow an OPEN alert exists without threshold, leave it; do not auto-resolve/create.
                return;
            }

            // NORMAL — above threshold
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
                    _logger.LogInformation(
                        "[StockAlert] RESOLVED StoreId={StoreId} IngredientId={IngredientId} RecipeId={RecipeId} Qty={Qty}",
                        item.StoreId, item.IngredientId, item.RecipeId, qty);
                }
                return;
            }

            // OUT_OF_STOCK
            if (qty <= 0)
            {
                await UpsertAlertAsync(
                    item,
                    openAlert,
                    StockAlertTypes.OutOfStock,
                    StockAlertSeverities.Urgent,
                    source,
                    summary);
                return;
            }

            // LOW_STOCK: 0 < qty <= min
            await UpsertAlertAsync(
                item,
                openAlert,
                StockAlertTypes.LowStock,
                StockAlertSeverities.Warning,
                source,
                summary);
        }

        private async Task UpsertAlertAsync(
            StoreInventory item,
            StockAlert? openAlert,
            string alertType,
            string severity,
            string source,
            StockAlertEvaluationResultDto summary)
        {
            var now = DateTime.UtcNow;

            if (openAlert == null)
            {
                _context.StockAlerts.Add(new StockAlert
                {
                    StoreId = item.StoreId,
                    IngredientId = item.IngredientId,
                    RecipeId = item.RecipeId,
                    AlertType = alertType,
                    Severity = severity,
                    Status = StockAlertStatuses.Open,
                    CurrentQtySnapshot = item.AvailableQty,
                    ThresholdSnapshot = item.MinStockLevel,
                    Source = source,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                summary.CreatedCount++;
                return;
            }

            // Duplicate guard: same OPEN alert — update/escalate in place, never insert another.
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
            {
                openAlert.Note = AppendNote(openAlert.Note, $"Escalated LOW_STOCK → OUT_OF_STOCK via {source}");
            }

            summary.UpdatedCount++;
        }

        private async Task<StockAlert?> FindOpenAlertAsync(int storeId, int? ingredientId, int? recipeId)
        {
            return await _context.StockAlerts
                .FirstOrDefaultAsync(a =>
                    a.StoreId == storeId &&
                    a.Status == StockAlertStatuses.Open &&
                    a.IngredientId == ingredientId &&
                    a.RecipeId == recipeId);
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
