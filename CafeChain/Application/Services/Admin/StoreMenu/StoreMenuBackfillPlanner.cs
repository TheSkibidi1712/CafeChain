using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.StoreMenu
{
    public sealed class StoreMenuBackfillPlanner : IStoreMenuBackfillPlanner
    {
        private readonly AppDbContext _context;

        public StoreMenuBackfillPlanner(AppDbContext context) => _context = context;

        public async Task<IReadOnlyList<StoreMenuBackfillCandidateDto>> BuildPlanAsync(CancellationToken cancellationToken = default)
        {
            var existingKeys = await _context.StoreMenuItems.AsNoTracking()
                .Select(x => new { x.StoreId, x.DrinkSizeId })
                .ToListAsync(cancellationToken);
            var existing = existingKeys.Select(x => (x.StoreId, x.DrinkSizeId)).ToHashSet();

            var legacy = await (
                from storeDrink in _context.StoreDrinks.AsNoTracking()
                join drinkSize in _context.DrinkSizes.AsNoTracking()
                    on storeDrink.DrinkId equals drinkSize.DrinkId
                where storeDrink.Store.Active
                    && storeDrink.Drink.Active
                    && drinkSize.Active
                    && drinkSize.Size.Active
                select new
                {
                    storeDrink.StoreId,
                    storeDrink.StoreDrinkId,
                    storeDrink.DrinkId,
                    storeDrink.Active,
                    drinkSize.DrinkSizeId,
                    DrinkName = storeDrink.Drink.Name,
                    SizeName = drinkSize.Size.Name
                })
                .OrderBy(x => x.StoreId)
                .ThenBy(x => x.DrinkName)
                .ThenBy(x => x.SizeName)
                .ThenBy(x => x.DrinkSizeId)
                .ToListAsync(cancellationToken);

            var orderByStore = new Dictionary<int, int>();
            var result = new List<StoreMenuBackfillCandidateDto>();
            foreach (var row in legacy)
            {
                if (existing.Contains((row.StoreId, row.DrinkSizeId)))
                    continue;

                orderByStore.TryGetValue(row.StoreId, out var displayOrder);
                result.Add(new StoreMenuBackfillCandidateDto
                {
                    StoreId = row.StoreId,
                    DrinkId = row.DrinkId,
                    LegacyStoreDrinkId = row.StoreDrinkId,
                    DrinkSizeId = row.DrinkSizeId,
                    IsEnabled = row.Active,
                    DisplayOrder = displayOrder
                });
                orderByStore[row.StoreId] = displayOrder + 1;
            }

            return result;
        }

        public async Task<IReadOnlyList<StoreMenuBackfillCandidateDto>> BuildStoreProvisioningPlanAsync(
            int storeId,
            CancellationToken cancellationToken = default)
        {
            var existingIds = await _context.StoreMenuItems.AsNoTracking()
                .Where(x => x.StoreId == storeId)
                .Select(x => x.DrinkSizeId)
                .ToListAsync(cancellationToken);

            var existing = existingIds.ToHashSet();
            var legacyByDrink = await _context.StoreDrinks.AsNoTracking()
                .Where(x => x.StoreId == storeId)
                .ToDictionaryAsync(x => x.DrinkId, cancellationToken);
            var startOrder = await _context.StoreMenuItems.AsNoTracking()
                .Where(x => x.StoreId == storeId)
                .Select(x => (int?)x.DisplayOrder)
                .MaxAsync(cancellationToken) ?? -1;

            var catalog = await _context.DrinkSizes.AsNoTracking()
                .Where(x => x.Active
                    && x.Drink.Active
                    && x.Size.Active)
                .OrderBy(x => x.Drink.Name)
                .ThenBy(x => x.Size.Name)
                .ThenBy(x => x.DrinkSizeId)
                .Select(x => new
                {
                    x.DrinkId,
                    x.DrinkSizeId
                })
                .ToListAsync(cancellationToken);

            var nextOrder = startOrder + 1;
            return catalog
                .Where(x => !existing.Contains(x.DrinkSizeId))
                .Select(x =>
                {
                    legacyByDrink.TryGetValue(x.DrinkId, out var legacy);
                    return new StoreMenuBackfillCandidateDto
                    {
                        StoreId = storeId,
                        DrinkId = x.DrinkId,
                        LegacyStoreDrinkId = legacy?.StoreDrinkId,
                        DrinkSizeId = x.DrinkSizeId,
                        IsEnabled = false,
                        DisplayOrder = nextOrder++
                    };
                })
                .ToList();
        }
    }
}
