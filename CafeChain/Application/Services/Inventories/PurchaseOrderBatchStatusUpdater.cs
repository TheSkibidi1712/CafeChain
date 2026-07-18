using CafeChain.Application.Constants;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

internal static class PurchaseOrderBatchStatusUpdater
{
    public static async Task RefreshAsync(AppDbContext context, int? batchId)
    {
        if (!batchId.HasValue) return;

        // Receipt confirmations for different child POs still converge on one batch.
        // Serialize that aggregate update so concurrent stores do not race its RowVersion.
        if (context.Database.IsSqlServer() && context.Database.CurrentTransaction != null)
        {
            await context.PurchaseOrderBatches
                .FromSqlInterpolated(
                    $@"SELECT * FROM PurchaseOrderBatches WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                       WHERE PurchaseOrderBatchId = {batchId.Value}")
                .SingleOrDefaultAsync();
        }

        var batch = await context.PurchaseOrderBatches
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
            .SingleOrDefaultAsync(x => x.PurchaseOrderBatchId == batchId.Value);
        if (batch == null || batch.Status == PurchaseOrderBatchStatuses.Cancelled) return;

        var children = batch.ChildPurchaseOrders.ToList();
        if (children.Count == 0) return;
        var anyReceived = children.SelectMany(x => x.Lines).SelectMany(x => x.ReceiptPostings).Any(x => x.AcceptedBaseQuantity > 0);
        var allTerminal = children.All(x => x.Status is PurchaseOrderStatuses.Completed or PurchaseOrderStatuses.Cancelled);
        var next = allTerminal && children.Any(x => x.Status == PurchaseOrderStatuses.Completed)
            ? PurchaseOrderBatchStatuses.Completed
            : anyReceived
                ? PurchaseOrderBatchStatuses.PartiallyReceived
                : batch.Status;
        if (next == batch.Status) return;
        batch.Status = next;
        batch.UpdatedAtUtc = DateTime.UtcNow;
    }
}
