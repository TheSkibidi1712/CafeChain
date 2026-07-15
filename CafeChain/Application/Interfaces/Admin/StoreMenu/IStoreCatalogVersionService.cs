using CafeChain.Application.DTOs.Admin.Profitability;

namespace CafeChain.Application.Interfaces.Admin.StoreMenu
{
    public interface IStoreCatalogVersionService
    {
        Task<IReadOnlyDictionary<int, long>> InvalidateAsync(
            IEnumerable<int> storeIds,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default);

        Task<PosCatalogVersionDto> GetAsync(int storeId, CancellationToken cancellationToken = default);
    }
}
