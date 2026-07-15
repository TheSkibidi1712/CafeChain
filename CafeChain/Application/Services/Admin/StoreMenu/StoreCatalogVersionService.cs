using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.StoreMenu
{
    public sealed class StoreCatalogVersionService : IStoreCatalogVersionService
    {
        private readonly AppDbContext _context;

        public StoreCatalogVersionService(AppDbContext context) => _context = context;

        public async Task<IReadOnlyDictionary<int, long>> InvalidateAsync(
            IEnumerable<int> storeIds,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var ids = storeIds.Where(x => x > 0).Distinct().OrderBy(x => x).ToArray();
            if (ids.Length == 0)
                return new Dictionary<int, long>();

            var states = await _context.PosCatalogStates
                .Where(x => ids.Contains(x.StoreId))
                .ToDictionaryAsync(x => x.StoreId, cancellationToken);
            var versions = new Dictionary<int, long>(ids.Length);
            foreach (var storeId in ids)
            {
                if (!states.TryGetValue(storeId, out var state))
                {
                    state = new PosCatalogState
                    {
                        StoreId = storeId,
                        Version = 1,
                        UpdatedAtUtc = updatedAtUtc
                    };
                    _context.PosCatalogStates.Add(state);
                }
                else
                {
                    state.Version++;
                    state.UpdatedAtUtc = updatedAtUtc;
                }
                state.PayloadHash = null;
                versions[storeId] = state.Version;
            }
            return versions;
        }

        public async Task<PosCatalogVersionDto> GetAsync(int storeId, CancellationToken cancellationToken = default)
        {
            var state = await _context.PosCatalogStates.AsNoTracking()
                .SingleOrDefaultAsync(x => x.StoreId == storeId, cancellationToken);
            return state == null
                ? new PosCatalogVersionDto { StoreId = storeId, Version = 0, UpdatedAtUtc = DateTime.UnixEpoch }
                : new PosCatalogVersionDto { StoreId = storeId, Version = state.Version, UpdatedAtUtc = state.UpdatedAtUtc };
        }
    }
}
