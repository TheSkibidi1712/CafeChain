using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Services.Inventories;
using CafeChain.Controllers.Api.v1;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace CafeChain.Tests.POS
{
    public class POSCatalogAvailabilityTests : IntegrationTestBase
    {
        private const int StoreId = 91;
        private const int CategoryId = 9100;
        private const int SizeId = 9101;
        private const int UnitId = 9102;
        private const int IngredientId = 9103;

        [Fact]
        public async Task GetMenuItems_StoreEnabledDrinkMissingInventory_ReturnsDisabledReasonInsteadOfHiding()
        {
            using var context = CreateDbContext();
            SeedBaseCatalog(context);
            var drinkId = 9104;

            context.Set<Drink>().Add(new Drink
            {
                DrinkId = drinkId,
                DrinkCode = "POS_MISSING_INV",
                Name = "POS Missing Inventory",
                Description = "Catalog availability test",
                CategoryId = CategoryId,
                ProductTypeId = 1,
                Active = true,
                CreatedAt = System.DateTime.Now
            });
            context.Set<StoreDrink>().Add(new StoreDrink
            {
                StoreDrinkId = 9105,
                StoreId = StoreId,
                DrinkId = drinkId,
                Active = true
            });
            context.Set<DrinkSize>().Add(new DrinkSize
            {
                DrinkSizeId = 9106,
                DrinkId = drinkId,
                SizeId = SizeId,
                Price = 25000m,
                Active = true
            });
            context.Set<Recipe>().Add(new Recipe
            {
                RecipeId = 9107,
                RecipeCode = "POS_RECIPE_MISSING_INV",
                Name = "POS Recipe Missing Inventory",
                Active = true,
                Status = "Active",
                DrinkId = drinkId,
                SizeId = SizeId
            });
            context.Set<RecipeDetail>().Add(new RecipeDetail
            {
                RecipeDetailId = 9108,
                RecipeId = 9107,
                IngredientId = IngredientId,
                Quantity = 10m,
                UnitId = UnitId
            });
            await context.SaveChangesAsync();

            var items = await GetMenuItemsAsync(context);
            var item = Assert.Single(items.Where(menuItem => menuItem.Id == drinkId));

            Assert.False(item.IsAvailable);
            Assert.Equal("MissingInventory", item.AvailabilityStatus);
            Assert.Equal("Chưa có tồn kho tại cửa hàng", item.AvailabilityReason);
        }

        [Fact]
        public async Task GetMenuItems_StoreEnabledDrinkMissingRecipe_ReturnsDisabledReasonInsteadOfHiding()
        {
            using var context = CreateDbContext();
            SeedBaseCatalog(context);
            var drinkId = 9110;

            context.Set<Drink>().Add(new Drink
            {
                DrinkId = drinkId,
                DrinkCode = "POS_MISSING_RECIPE",
                Name = "POS Missing Recipe",
                Description = "Catalog availability test",
                CategoryId = CategoryId,
                ProductTypeId = 1,
                Active = true,
                CreatedAt = System.DateTime.Now
            });
            context.Set<StoreDrink>().Add(new StoreDrink
            {
                StoreDrinkId = 9111,
                StoreId = StoreId,
                DrinkId = drinkId,
                Active = true
            });
            context.Set<DrinkSize>().Add(new DrinkSize
            {
                DrinkSizeId = 9112,
                DrinkId = drinkId,
                SizeId = SizeId,
                Price = 30000m,
                Active = true
            });
            await context.SaveChangesAsync();

            var items = await GetMenuItemsAsync(context);
            var item = Assert.Single(items.Where(menuItem => menuItem.Id == drinkId));

            Assert.False(item.IsAvailable);
            Assert.Equal("MissingRecipe", item.AvailabilityStatus);
            Assert.Equal("Chưa cấu hình công thức", item.AvailabilityReason);
        }

        [Fact]
        public async Task GetMenuItems_MissingUnitConversion_ReturnsMissingUnitConversionUnavailable()
        {
            using var context = CreateDbContext();
            SeedBaseCatalog(context);
            const int badDrinkId = 9120;
            const int badUnitId = 9121; // different from ingredient BaseUnit, no conversion

            context.Set<Unit>().Add(new Unit
            {
                UnitId = badUnitId,
                UnitCode = "POS_KG",
                Name = "POS kg",
                Active = true
            });
            SeedStoreDrinkWithRecipe(context, badDrinkId, 9122, 9123, IngredientId, 10m, badUnitId);
            // Inventory present so only conversion blocks availability
            context.Set<StoreInventory>().Add(new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                AvailableQty = 1000m,
                ReservedQty = 0,
                LastUpdated = System.DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            await context.SaveChangesAsync();

            var items = await GetMenuItemsAsync(context);
            var item = Assert.Single(items.Where(m => m.Id == badDrinkId));

            Assert.False(item.IsAvailable);
            Assert.Equal("MissingUnitConversion", item.AvailabilityStatus);
            Assert.Equal("Thiếu quy đổi đơn vị nguyên liệu", item.AvailabilityReason);
        }

        [Fact]
        public async Task GetMenuItems_InvalidConversionFactor_ReturnsUnavailableNotRawQuantity()
        {
            using var context = CreateDbContext();
            SeedBaseCatalog(context);
            const int badDrinkId = 9130;
            const int badUnitId = 9131;

            context.Set<Unit>().Add(new Unit
            {
                UnitId = badUnitId,
                UnitCode = "POS_BAD",
                Name = "POS bad",
                Active = true
            });
            context.Set<UnitConversion>().Add(new UnitConversion
            {
                IngredientId = IngredientId,
                FromUnitId = badUnitId,
                FromQuantity = 0m,
                ToUnitId = UnitId,
                ToQuantity = 1000m
            });
            SeedStoreDrinkWithRecipe(context, badDrinkId, 9132, 9133, IngredientId, 10m, badUnitId);
            context.Set<StoreInventory>().Add(new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                AvailableQty = 5m, // would be enough if raw 10 used wrongly after 0-factor path
                ReservedQty = 0,
                LastUpdated = System.DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            await context.SaveChangesAsync();

            var items = await GetMenuItemsAsync(context);
            var item = Assert.Single(items.Where(m => m.Id == badDrinkId));

            Assert.False(item.IsAvailable);
            Assert.Equal("MissingUnitConversion", item.AvailabilityStatus);
            Assert.Equal("Thiếu quy đổi đơn vị nguyên liệu", item.AvailabilityReason);
            // Must not treat as available via raw-qty fallback (10 > 5 would be InsufficientStock)
            Assert.NotEqual("InsufficientStock", item.AvailabilityStatus);
        }

        [Fact]
        public async Task GetMenuItems_OneBadConversion_DoesNotHideOtherDrink()
        {
            using var context = CreateDbContext();
            SeedBaseCatalog(context);
            const int badDrinkId = 9140;
            const int goodDrinkId = 9141;
            const int badUnitId = 9142;

            context.Set<Unit>().Add(new Unit
            {
                UnitId = badUnitId,
                UnitCode = "POS_X",
                Name = "POS x",
                Active = true
            });
            SeedStoreDrinkWithRecipe(context, badDrinkId, 9143, 9144, IngredientId, 10m, badUnitId);
            SeedStoreDrinkWithRecipe(context, goodDrinkId, 9145, 9146, IngredientId, 10m, UnitId);
            context.Set<StoreInventory>().Add(new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                AvailableQty = 1000m,
                ReservedQty = 0,
                LastUpdated = System.DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            await context.SaveChangesAsync();

            var items = await GetMenuItemsAsync(context);
            var bad = Assert.Single(items.Where(m => m.Id == badDrinkId));
            var good = Assert.Single(items.Where(m => m.Id == goodDrinkId));

            Assert.False(bad.IsAvailable);
            Assert.Equal("MissingUnitConversion", bad.AvailabilityStatus);
            Assert.True(good.IsAvailable);
            Assert.Equal("Available", good.AvailabilityStatus);
        }

        private static void SeedBaseCatalog(CafeChain.Data.AppDbContext context)
        {
            context.Set<DrinkCategory>().Add(new DrinkCategory
            {
                CategoryId = CategoryId,
                CategoryCode = "POS_CAT",
                Name = "POS Catalog",
                Icon = "C",
                Active = true
            });
            context.Set<Size>().Add(new Size
            {
                SizeId = SizeId,
                SizeCode = "POS_M",
                Name = "POS Medium",
                Description = "Medium",
                Active = true
            });
            context.Set<Unit>().Add(new Unit
            {
                UnitId = UnitId,
                UnitCode = "POS_ML",
                Name = "POS ml",
                Active = true
            });
            context.Set<Ingredient>().Add(new Ingredient
            {
                IngredientId = IngredientId,
                Code = "POS_MILK",
                Name = "POS Milk",
                BaseUnitId = UnitId,
                Active = true
            });
        }

        private static void SeedStoreDrinkWithRecipe(
            CafeChain.Data.AppDbContext context,
            int drinkId,
            int storeDrinkId,
            int recipeId,
            int ingredientId,
            decimal quantity,
            int unitId)
        {
            context.Set<Drink>().Add(new Drink
            {
                DrinkId = drinkId,
                DrinkCode = $"POS_D{drinkId}",
                Name = $"POS Drink {drinkId}",
                Description = "Catalog availability test",
                CategoryId = CategoryId,
                ProductTypeId = 1,
                Active = true,
                CreatedAt = System.DateTime.Now
            });
            context.Set<StoreDrink>().Add(new StoreDrink
            {
                StoreDrinkId = storeDrinkId,
                StoreId = StoreId,
                DrinkId = drinkId,
                Active = true
            });
            context.Set<DrinkSize>().Add(new DrinkSize
            {
                DrinkSizeId = drinkId + 10000,
                DrinkId = drinkId,
                SizeId = SizeId,
                Price = 25000m,
                Active = true
            });
            context.Set<Recipe>().Add(new Recipe
            {
                RecipeId = recipeId,
                RecipeCode = $"POS_R{recipeId}",
                Name = $"POS Recipe {recipeId}",
                Active = true,
                Status = "Active",
                DrinkId = drinkId,
                SizeId = SizeId
            });
            context.Set<RecipeDetail>().Add(new RecipeDetail
            {
                RecipeDetailId = recipeId + 1000,
                RecipeId = recipeId,
                IngredientId = ingredientId,
                Quantity = quantity,
                UnitId = unitId
            });
        }

        private static async Task<List<POSMenuItemDto>> GetMenuItemsAsync(CafeChain.Data.AppDbContext context)
        {
            var controller = new POSCatalogController(
                context,
                new UnitConversionService(
                    context,
                    NullLogger<UnitConversionService>.Instance,
                    new PhysicalUnitConversionService(
                        context,
                        NullLogger<PhysicalUnitConversionService>.Instance)),
                NullLogger<POSCatalogController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim("StoreId", StoreId.ToString()),
                            new Claim("StaffId", "1")
                        }, "Test"))
                    }
                }
            };

            var result = await controller.GetMenuItems(null);
            var ok = Assert.IsType<OkObjectResult>(result);
            return Assert.IsAssignableFrom<List<POSMenuItemDto>>(ok.Value);
        }
    }
}
