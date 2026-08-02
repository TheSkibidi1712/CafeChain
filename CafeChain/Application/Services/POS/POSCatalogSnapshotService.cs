using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.POS
{
    public sealed class POSCatalogSnapshotService : IPOSCatalogSnapshotService
    {
        private readonly AppDbContext _context;
        private readonly IStoreMenuAvailabilityEvaluator _availability;
        private readonly IPOSIceCustomizationService? _iceCustomization;

        public POSCatalogSnapshotService(
            AppDbContext context,
            IStoreMenuAvailabilityEvaluator availability,
            IPOSIceCustomizationService? iceCustomization = null)
        {
            _context = context;
            _availability = availability;
            _iceCustomization = iceCustomization;
        }

        public async Task<POSCatalogSnapshotDto> BuildAsync(
            int storeId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default)
        {
            if (storeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(storeId));

            for (var attempt = 0; attempt < 3; attempt++)
            {
                var versionBefore = await ReadVersionAsync(storeId, cancellationToken);
                var content = await BuildContentAsync(storeId, asOfUtc, cancellationToken);
                var versionAfter = await ReadVersionAsync(storeId, cancellationToken);
                if (versionBefore != versionAfter)
                    continue;

                var hash = ComputeHash(content.Categories, content.MenuItems);
                var version = await ApplyPayloadHashAsync(storeId, hash, asOfUtc, cancellationToken);
                return new POSCatalogSnapshotDto
                {
                    StoreId = storeId,
                    Version = version,
                    GeneratedAtUtc = asOfUtc,
                    Categories = content.Categories,
                    MenuItems = content.MenuItems
                };
            }

            throw new InvalidOperationException("Catalog đang được cập nhật liên tục. Vui lòng thử lại.");
        }

        private async Task<(IReadOnlyList<POSCategoryDto> Categories, IReadOnlyList<POSMenuItemDto> MenuItems)> BuildContentAsync(
            int storeId,
            DateTime asOfUtc,
            CancellationToken cancellationToken)
        {
            var menuRows = await _context.StoreMenuItems.AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.DrinkSize).ThenInclude(x => x.Size)
                .Include(x => x.DrinkSize).ThenInclude(x => x.Drink).ThenInclude(x => x.DrinkImages)
                .Where(x => x.StoreId == storeId
                    && x.IsEnabled
                    && x.PublishedAtUtc.HasValue
                    && (!x.EffectiveFromUtc.HasValue || x.EffectiveFromUtc.Value <= asOfUtc)
                    && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc.Value > asOfUtc)
                    && x.DrinkSize.Active
                    && x.DrinkSize.Size.Active
                    && x.DrinkSize.Drink.Active)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.DrinkSize.Drink.Name)
                .ThenBy(x => x.DrinkSize.Size.Name)
                .ThenBy(x => x.DrinkSizeId)
                .ToListAsync(cancellationToken);

            var availability = new Dictionary<int, StoreMenuAvailabilityDto>();
            var iceEligibility = new Dictionary<int, POSIceEligibilityDto>();
            foreach (var row in menuRows)
            {
                availability[row.DrinkSizeId] = await _availability.EvaluateAsync(storeId, row.DrinkSizeId, asOfUtc, cancellationToken);
                if (_iceCustomization != null)
                {
                    var iceResult = await _iceCustomization.GetEligibilityAsync(
                        storeId,
                        row.DrinkSize.DrinkId,
                        row.DrinkSize.SizeId,
                        cancellationToken);
                    if (iceResult.IsSuccess && iceResult.Data != null)
                        iceEligibility[row.DrinkSizeId] = iceResult.Data;
                }
            }

            var storeToppings = await (
                from storeTopping in _context.StoreToppings.AsNoTracking()
                join drinkTopping in _context.DrinkToppings.AsNoTracking()
                    on storeTopping.ToppingId equals drinkTopping.ToppingId
                where storeTopping.StoreId == storeId
                    && storeTopping.Active
                    && storeTopping.Topping.Active
                    && drinkTopping.Active
                select new
                {
                    drinkTopping.DrinkId,
                    Topping = new POSToppingDto
                    {
                        Id = storeTopping.ToppingId,
                        Name = storeTopping.Topping.Name,
                        Price = storeTopping.Topping.Price,
                        ImageUrl = storeTopping.Topping.ImageUrl
                    }
                })
                .OrderBy(x => x.DrinkId)
                .ThenBy(x => x.Topping.Name)
                .ToListAsync(cancellationToken);
            var toppingsByDrink = storeToppings
                .GroupBy(x => x.DrinkId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Topping).ToList());

            var drinkSizeIds = menuRows.Select(x => x.DrinkSizeId).Distinct().ToArray();
            var policiesByDrinkSize = await _context.DrinkSizeToppingPolicies.AsNoTracking()
                .Where(x => drinkSizeIds.Contains(x.DrinkSizeId) && x.IsActive)
                .OrderBy(x => x.DrinkSizeId)
                .ThenBy(x => x.ToppingId)
                .Select(x => new
                {
                    x.DrinkSizeId,
                    Policy = new POSToppingPolicyDto
                    {
                        ToppingId = x.ToppingId,
                        IsDefaultSelected = x.IsDefaultSelected,
                        IsRequired = x.IsRequired,
                        PriceTreatment = x.PriceTreatment,
                        QuantityPerDrink = x.QuantityPerDrink
                    }
                })
                .ToListAsync(cancellationToken);
            var toppingPolicies = policiesByDrinkSize
                .GroupBy(x => x.DrinkSizeId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Policy).ToList());

            var menuItems = menuRows
                .GroupBy(x => x.DrinkSize.DrinkId)
                .Select(group =>
                {
                    var first = group.First();
                    var sizes = group.Select(row =>
                    {
                        var state = availability[row.DrinkSizeId];
                        iceEligibility.TryGetValue(row.DrinkSizeId, out var ice);
                        return new POSMenuItemSizeDto
                        {
                            StoreMenuItemId = row.StoreMenuItemId,
                            DrinkSizeId = row.DrinkSizeId,
                            SizeId = row.DrinkSize.SizeId,
                            SizeName = row.DrinkSize.Size.Name,
                            Price = row.GetEffectivePrice(),
                            GlobalPrice = row.DrinkSize.Price,
                            StoreOverride = row.PriceOverride,
                            PriceSource = row.GetPriceSource(),
                            IsAvailable = state.IsSellable,
                            AvailabilityStatus = state.OperationalStatus,
                            AvailabilityReason = state.Reason,
                            SupportsIceCustomization = ice?.SupportsIceCustomization == true,
                            BaseIceQuantityBaseUnit = ice?.BaseIceQuantityBaseUnit,
                            ToppingPolicies = toppingPolicies.GetValueOrDefault(row.DrinkSizeId)
                                ?? new List<POSToppingPolicyDto>()
                        };
                    }).ToList();
                    var best = sizes.FirstOrDefault(x => x.AvailabilityStatus == StoreMenuAvailabilityStatuses.Available)
                        ?? sizes.FirstOrDefault(x => x.AvailabilityStatus == StoreMenuAvailabilityStatuses.LowStock)
                        ?? sizes.First();
                    var image = first.DrinkSize.Drink.DrinkImages
                        .OrderByDescending(x => x.IsDefault)
                        .ThenBy(x => x.DrinkImageId)
                        .Select(x => x.ImageUrl)
                        .FirstOrDefault();
                    return new POSMenuItemDto
                    {
                        Id = first.DrinkSize.DrinkId,
                        Name = first.DrinkSize.Drink.Name,
                        CategoryId = first.DrinkSize.Drink.CategoryId ?? 0,
                        Image = image,
                        Price = sizes.Where(x => x.IsAvailable).Select(x => x.Price).DefaultIfEmpty(sizes.Min(x => x.Price)).Min(),
                        IsAvailable = sizes.Any(x => x.IsAvailable),
                        AvailabilityStatus = best.AvailabilityStatus,
                        AvailabilityReason = best.AvailabilityReason,
                        Sizes = sizes,
                        AvailableToppings = toppingsByDrink.GetValueOrDefault(first.DrinkSize.DrinkId) ?? new List<POSToppingDto>()
                    };
                })
                .OrderBy(x => menuRows.First(y => y.DrinkSize.DrinkId == x.Id).DisplayOrder)
                .ThenBy(x => x.Name)
                .ToList();

            var categoryIds = menuItems.Select(x => x.CategoryId).Where(x => x > 0).Distinct().ToArray();
            var categoryRows = await _context.DrinkCategories.AsNoTracking()
                .Where(x => x.Active && categoryIds.Contains(x.CategoryId))
                .OrderBy(x => x.Name)
                .Select(x => new { x.CategoryId, x.Name, x.Icon })
                .ToListAsync(cancellationToken);
            var categories = categoryRows.Select(x => new POSCategoryDto
            {
                Id = x.CategoryId,
                Name = x.Name,
                Icon = x.Icon,
                Count = menuItems.Count(item => item.CategoryId == x.CategoryId)
            }).ToList();

            return (categories, menuItems);
        }

        private async Task<long> ReadVersionAsync(int storeId, CancellationToken cancellationToken) =>
            await _context.PosCatalogStates.AsNoTracking()
                .Where(x => x.StoreId == storeId)
                .Select(x => (long?)x.Version)
                .SingleOrDefaultAsync(cancellationToken) ?? 0L;

        private async Task<long> ApplyPayloadHashAsync(
            int storeId,
            string payloadHash,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken)
        {
            var state = await _context.PosCatalogStates.SingleOrDefaultAsync(x => x.StoreId == storeId, cancellationToken);
            if (state == null)
            {
                state = new PosCatalogState
                {
                    StoreId = storeId,
                    Version = 1,
                    PayloadHash = payloadHash,
                    UpdatedAtUtc = updatedAtUtc
                };
                _context.PosCatalogStates.Add(state);
            }
            else if (state.PayloadHash == null)
            {
                state.PayloadHash = payloadHash;
                state.UpdatedAtUtc = updatedAtUtc;
            }
            else if (!string.Equals(state.PayloadHash, payloadHash, StringComparison.Ordinal))
            {
                state.Version++;
                state.PayloadHash = payloadHash;
                state.UpdatedAtUtc = updatedAtUtc;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return state.Version;
        }

        private static string ComputeHash(
            IReadOnlyList<POSCategoryDto> categories,
            IReadOnlyList<POSMenuItemDto> menuItems)
        {
            var json = JsonSerializer.Serialize(new { categories, menuItems });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        }
    }
}
