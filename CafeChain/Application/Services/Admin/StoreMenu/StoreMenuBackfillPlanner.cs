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
                    LegacyStoreDrinkId = row.StoreDrinkId,
                    DrinkSizeId = row.DrinkSizeId,
                    IsEnabled = row.Active,
                    DisplayOrder = displayOrder
                });
                orderByStore[row.StoreId] = displayOrder + 1;
            }

            return result;
        }
    }
}
