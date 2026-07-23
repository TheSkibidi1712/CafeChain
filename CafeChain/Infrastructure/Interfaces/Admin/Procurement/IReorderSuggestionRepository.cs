using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Stores;

namespace CafeChain.Infrastructure.Interfaces.Admin.Procurement;

public sealed record ReorderStoreRow(int StoreId, string StoreName);
public sealed record ReorderUsageRow(decimal Quantity, int Count);

public interface IReorderSuggestionRepository
{
    Task<ReorderStoreRow?> GetStoreAsync(int storeId);
    Task<List<StoreInventory>> GetInventoriesAsync(int storeId);
    Task<IReadOnlyDictionary<int, ReorderUsageRow>> GetUsageAsync(
        int storeId,
        DateTime fromUtc);
    Task<List<IngredientSupplier>> GetOffersAsync(
        int storeId,
        IReadOnlyCollection<int> ingredientIds);
    Task<IReadOnlyDictionary<int, int>> GetActiveRestockRequestsAsync(int storeId);
    Task<IReadOnlyDictionary<int, decimal>> GetActivePurchaseAdviceQuantitiesAsync(int storeId);
    Task<bool> IsActiveStaffAtStoreAsync(int staffId, int storeId);
    Task<IReadOnlyList<int>> GetActiveStoreIdsAsync();
}
