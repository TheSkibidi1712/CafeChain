namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IReorderIncomingQuantityProvider
    {
        Task<IReadOnlyDictionary<int, decimal>> GetIncomingBaseQuantitiesAsync(
            int storeId,
            IReadOnlyCollection<int> ingredientIds);
    }
}
