using CafeChain.Application.Services.Admin.StoreMenu;
using CafeChain.Models.Drinks;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Tests
{
    public sealed class StoreMenuIdentityIssue160Tests : IntegrationTestBase
    {
        [Fact]
        public void StoreMenuItem_DerivesLifecycleAndPriceWithoutRecipeOverride()
        {
            var now = DateTime.UtcNow;
            var item = new StoreMenuItem
            {
                DrinkSize = new DrinkSize { Price = 35_000m },
                IsEnabled = true
            };

            Assert.Equal(StoreMenuConfiguredStatuses.Draft, item.GetConfiguredStatus(now));
            item.PublishedAtUtc = now.AddMinutes(-1);
            item.EffectiveFromUtc = now.AddHours(1);
            Assert.Equal(StoreMenuConfiguredStatuses.Scheduled, item.GetConfiguredStatus(now));
            item.EffectiveFromUtc = now.AddHours(-1);
            Assert.Equal(StoreMenuConfiguredStatuses.Active, item.GetConfiguredStatus(now));
            item.IsEnabled = false;
            Assert.Equal(StoreMenuConfiguredStatuses.Paused, item.GetConfiguredStatus(now));
            item.EffectiveToUtc = now;
            Assert.Equal(StoreMenuConfiguredStatuses.Ended, item.GetConfiguredStatus(now));

            item.PriceOverride = null;
            Assert.Equal(35_000m, item.GetEffectivePrice());
            Assert.Equal(StoreMenuPriceSources.Global, item.GetPriceSource());
            item.PriceOverride = 39_000m;
            Assert.Equal(39_000m, item.GetEffectivePrice());
            Assert.Equal(StoreMenuPriceSources.StoreOverride, item.GetPriceSource());
            Assert.DoesNotContain(typeof(StoreMenuItem).GetProperties(), property => property.Name.Contains("Recipe", StringComparison.Ordinal));
        }

        [Fact]
        public async Task BackfillPlanner_IsReadOnlyDeterministicAndSkipsExistingSku()
        {
            await using var context = CreateDbContext();
            context.Stores.Add(new Store { StoreId = 501, Name = "Store", Address = "A", Phone = "1", Active = true, CreatedAt = DateTime.UtcNow });
            context.Sizes.AddRange(
                new Size { SizeId = 502, SizeCode = "SM160_M", Name = "SM160 M", Description = "M", Active = true },
                new Size { SizeId = 503, SizeCode = "SM160_L", Name = "SM160 L", Description = "L", Active = true });
            context.Drinks.Add(new Drink { DrinkId = 504, DrinkCode = "D", Name = "Drink", Description = "D", ProductTypeId = 1, Active = true, CreatedAt = DateTime.UtcNow });
            context.DrinkSizes.AddRange(
                new DrinkSize { DrinkSizeId = 505, DrinkId = 504, SizeId = 502, Price = 10, Active = true },
                new DrinkSize { DrinkSizeId = 506, DrinkId = 504, SizeId = 503, Price = 20, Active = true });
            context.StoreDrinks.Add(new StoreDrink { StoreDrinkId = 507, StoreId = 501, DrinkId = 504, Active = true });
            context.StoreMenuItems.Add(new StoreMenuItem { StoreMenuItemId = 508, StoreId = 501, DrinkSizeId = 505, IsEnabled = true, DisplayOrder = 0, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var before = await context.StoreMenuItems.CountAsync();
            var plan = await new StoreMenuBackfillPlanner(context).BuildPlanAsync();

            var candidate = Assert.Single(plan.Where(x => x.StoreId == 501));
            Assert.Equal(506, candidate.DrinkSizeId);
            Assert.True(candidate.IsEnabled);
            Assert.Equal(before, await context.StoreMenuItems.CountAsync());
        }

        [Fact]
        public async Task StoreMenuItem_StoreDrinkSizeIdentity_IsUnique()
        {
            await using var context = CreateDbContext();
            context.StoreMenuItems.Add(new StoreMenuItem { StoreMenuItemId = 601, StoreId = 1, DrinkSizeId = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
            context.StoreMenuItems.Add(new StoreMenuItem { StoreMenuItemId = 602, StoreId = 1, DrinkSizeId = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }
}
