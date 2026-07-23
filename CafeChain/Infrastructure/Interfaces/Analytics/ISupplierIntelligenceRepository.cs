using CafeChain.Models.Inventories.Suppliers;

namespace CafeChain.Infrastructure.Interfaces.Analytics;

public interface ISupplierIntelligenceRepository
{
    Task<List<IngredientSupplier>> GetOffersAsync(int storeId, int ingredientId, CancellationToken cancellationToken);
}
