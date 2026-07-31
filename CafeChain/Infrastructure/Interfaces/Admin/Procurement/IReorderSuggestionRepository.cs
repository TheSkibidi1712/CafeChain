using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Stores;

namespace CafeChain.Infrastructure.Interfaces.Admin.Procurement;

public sealed record ReorderStoreRow(int StoreId, string StoreName);

/// <summary>Server-side aggregate for one ingredient and one half-open window.</summary>
public sealed record ReorderUsageRow(decimal Quantity, int Count);

public sealed record ReorderInventoryRow(
    int StoreInventoryId,
    int StoreId,
    int IngredientId,
    string IngredientCode,
    string IngredientName,
    int BaseUnitId,
    string BaseUnitCode,
    decimal OnHandQuantity,
    decimal ReservedQuantity,
    decimal? MinimumStock);

public sealed record ReorderOfferRow(
    IngredientSupplier Offer,
    DateTime? PriceEffectiveAtUtc,
    decimal? HistoricalPrice,
    decimal? HistoricalPackageQuantity,
    int? HistoricalPackageUnitId,
    bool HasCurrentPriceHistory);

/// <summary>
/// A restock request with fulfillment already posted against it.
/// </summary>
public sealed record ReorderRestockRow(
    int RestockRequestId,
    int StoreId,
    int IngredientId,
    string Status,
    decimal RequestedQuantity,
    decimal ClosedRemainingQuantity,
    decimal FulfilledQuantity);

/// <summary>
/// PA line residual and its root RR.  Rows are returned regardless of PA
/// header state; the calculator decides coverage from the active reservation
/// and actual residual quantities.
/// </summary>
public sealed record ReorderPurchaseAdviceRow(
    int PurchaseAdviceLineId,
    int PurchaseAdviceId,
    int RestockRequestId,
    int StoreId,
    int IngredientId,
    string Status,
    bool IsActiveReservation,
    decimal RequestedPurchaseBaseQuantity,
    decimal AllocatedToPoBaseQuantity,
    decimal AcceptedBaseQuantity,
    decimal ClosedBaseQuantity);

/// <summary>
/// PO line quantities are kept in base units.  The status is retained so
/// draft/approved/partial lines can be distinguished without another query.
/// </summary>
public sealed record ReorderPurchaseOrderRow(
    int PurchaseOrderLineId,
    int PurchaseOrderId,
    int StoreId,
    int IngredientId,
    string Status,
    int? RestockRequestId,
    int? PurchaseAdviceLineId,
    decimal OrderedBaseQuantity,
    decimal AcceptedBaseQuantity,
    decimal ClosedRemainingQuantity);

/// <summary>
/// Exact batch allocation link.  Accepted/closed quantities are aggregated
/// from PurchaseAdviceFulfillmentPosting and therefore never double-counted.
/// </summary>
public sealed record ReorderAllocationRow(
    int PurchaseOrderLineAllocationId,
    int PurchaseAdviceLineId,
    int PurchaseOrderLineId,
    int RestockRequestId,
    decimal AllocatedBaseQuantity,
    decimal AcceptedBaseQuantity,
    decimal ClosedBaseQuantity);

public sealed record ReorderCalculationData(
    ReorderStoreRow Store,
    IReadOnlyList<ReorderInventoryRow> Inventories,
    IReadOnlyDictionary<int, ReorderUsageRow> Usage,
    IReadOnlyList<ReorderOfferRow> Offers,
    IReadOnlyList<ReorderRestockRow> RestockRequests,
    IReadOnlyList<ReorderPurchaseAdviceRow> PurchaseAdviceLines,
    IReadOnlyList<ReorderPurchaseOrderRow> PurchaseOrderLines,
    IReadOnlyList<ReorderAllocationRow> Allocations);

public interface IReorderSuggestionRepository
{
    Task<ReorderStoreRow?> GetStoreAsync(int storeId, CancellationToken cancellationToken = default);
    Task<List<StoreInventory>> GetInventoriesAsync(int storeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ReorderUsageRow>> GetUsageAsync(
        int storeId,
        DateTime fromUtc,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);
    Task<List<IngredientSupplier>> GetOffersAsync(
        int storeId,
        IReadOnlyCollection<int> ingredientIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetActiveRestockRequestsAsync(
        int storeId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, decimal>> GetActivePurchaseAdviceQuantitiesAsync(
        int storeId,
        CancellationToken cancellationToken = default);
    Task<bool> IsActiveStaffAtStoreAsync(
        int staffId,
        int storeId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetActiveStoreIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all deterministic inputs using projections and server-side usage
    /// aggregation.  The same method powers the form, dashboard and worker.
    /// </summary>
    Task<ReorderCalculationData?> GetCalculationDataAsync(
        int storeId,
        IReadOnlyCollection<int>? ingredientIds,
        DateTime analysisFromUtc,
        DateTime analysisToUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReorderCalculationData>> GetCalculationDataForStoresAsync(
        IReadOnlyCollection<int> storeIds,
        DateTime analysisFromUtc,
        DateTime analysisToUtc,
        CancellationToken cancellationToken = default);
}
