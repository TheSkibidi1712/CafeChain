using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Inventories.Ingredients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Shared unit conversion — no silent raw quantity; invalid factors rejected.</summary>
    public class UnitConversionServiceTests : IntegrationTestBase
    {
        private const int UnitG = 1;
        private const int UnitKg = 2;
        private const int UnitMl = 3;
        private const int IngredientId = 501;

        [Fact]
        public async Task SameUnit_ReturnsSameQuantity()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 42m, UnitG);

            Assert.True(result.IsSuccess);
            Assert.Equal(42m, result.Data);
        }

        [Fact]
        public async Task DirectConversion_KgToG_Works()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            SeedConversion(ctx, IngredientId, UnitKg, 1m, UnitG, 1000m);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 2m, UnitKg);

            Assert.True(result.IsSuccess);
            Assert.Equal(2000m, result.Data);
        }

        [Fact]
        public async Task ReverseConversion_GToKg_Works()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            SeedConversion(ctx, IngredientId, UnitG, 1000m, UnitKg, 1m);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 2m, UnitKg, UnitG);

            Assert.True(result.IsSuccess);
            Assert.Equal(2000m, result.Data);
        }

        [Fact]
        public async Task MissingConversion_DoesNotReturnRawQuantity()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 50m, UnitMl);

            Assert.False(result.IsSuccess);
            Assert.Contains("Thiếu quy đổi", result.Message);
            Assert.NotEqual(50m, result.Data);
        }

        [Fact]
        public async Task FromQuantityZero_IsRejected()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            SeedConversion(ctx, IngredientId, UnitKg, 0m, UnitG, 1000m);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 1m, UnitKg);

            Assert.False(result.IsSuccess);
            Assert.Contains("không hợp lệ", result.Message);
        }

        [Fact]
        public async Task ToQuantityZero_IsRejected()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            SeedConversion(ctx, IngredientId, UnitKg, 1m, UnitG, 0m);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 1m, UnitKg);

            Assert.False(result.IsSuccess);
            Assert.Contains("không hợp lệ", result.Message);
        }

        [Fact]
        public async Task FromQuantityNegative_IsRejected()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            SeedConversion(ctx, IngredientId, UnitKg, -1m, UnitG, 1000m);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 1m, UnitKg);

            Assert.False(result.IsSuccess);
            Assert.Contains("không hợp lệ", result.Message);
        }

        [Fact]
        public async Task ToQuantityNegative_IsRejected()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            SeedConversion(ctx, IngredientId, UnitKg, 1m, UnitG, -1000m);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 1m, UnitKg);

            Assert.False(result.IsSuccess);
            Assert.Contains("không hợp lệ", result.Message);
        }

        [Fact]
        public async Task InvalidFactor_DoesNotReturnSuccessConversion()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            SeedConversion(ctx, IngredientId, UnitKg, 0m, UnitG, 0m);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 5m, UnitKg);

            Assert.False(result.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(result.Message));
        }

        [Fact]
        public async Task SeedGuardrail_AllSolidRecipeDetailsUseBaseUnitG()
        {
            using var ctx = CreateDbContext();
            var solidIds = new[] { 1, 3, 4, 5, 6, 7 };

            var solids = await ctx.Ingredients.AsNoTracking()
                .Where(i => solidIds.Contains(i.IngredientId))
                .ToListAsync();

            Assert.True(solids.Count == solidIds.Length,
                $"Expected seeded solid ingredients {string.Join(",", solidIds)}; found {solids.Count}.");

            foreach (var ing in solids)
            {
                Assert.Equal(UnitG, ing.BaseUnitId);

                var details = await ctx.RecipeDetails.AsNoTracking()
                    .Where(rd => rd.IngredientId == ing.IngredientId)
                    .ToListAsync();

                Assert.NotEmpty(details);

                foreach (var d in details)
                {
                    Assert.True(
                        d.UnitId == UnitG,
                        $"Solid IngredientId={ing.IngredientId} RecipeDetailId={d.RecipeDetailId} must use UnitId=g(1), got {d.UnitId}");
                }
            }
        }

        [Fact]
        public async Task SeedGuardrail_LiquidRecipeDetailsRemainMl()
        {
            using var ctx = CreateDbContext();
            var liquidIds = new[] { 2, 13 }; // condensed milk, water

            var liquids = await ctx.Ingredients.AsNoTracking()
                .Where(i => liquidIds.Contains(i.IngredientId))
                .ToListAsync();

            Assert.True(liquids.Count == liquidIds.Length,
                $"Expected seeded liquid ingredients {string.Join(",", liquidIds)}; found {liquids.Count}.");

            foreach (var ing in liquids)
            {
                Assert.Equal(UnitMl, ing.BaseUnitId);

                var details = await ctx.RecipeDetails.AsNoTracking()
                    .Where(rd => rd.IngredientId == ing.IngredientId)
                    .ToListAsync();

                Assert.NotEmpty(details);

                foreach (var d in details)
                {
                    Assert.True(
                        d.UnitId == UnitMl,
                        $"Liquid IngredientId={ing.IngredientId} RecipeDetailId={d.RecipeDetailId} must use UnitId=ml(3), got {d.UnitId}");
                }
            }
        }

        private static UnitConversionService CreateService(CafeChain.Data.AppDbContext ctx) =>
            new(ctx, NullLogger<UnitConversionService>.Instance);

        private static void SeedIngredient(CafeChain.Data.AppDbContext ctx, int id, int baseUnitId)
        {
            if (!ctx.Units.Any(u => u.UnitId == UnitG))
            {
                ctx.Units.Add(new Unit { UnitId = UnitG, UnitCode = "g", Name = "Gram", Active = true });
                ctx.Units.Add(new Unit { UnitId = UnitKg, UnitCode = "kg", Name = "Kilogram", Active = true });
                ctx.Units.Add(new Unit { UnitId = UnitMl, UnitCode = "ml", Name = "Milliliter", Active = true });
            }

            if (!ctx.Ingredients.Any(i => i.IngredientId == id))
            {
                ctx.Ingredients.Add(new Ingredient
                {
                    IngredientId = id,
                    Code = $"ING{id}",
                    Name = $"Ing {id}",
                    BaseUnitId = baseUnitId,
                    Active = true
                });
            }

            ctx.SaveChanges();
        }

        private static void SeedConversion(
            CafeChain.Data.AppDbContext ctx,
            int ingredientId,
            int fromUnitId,
            decimal fromQty,
            int toUnitId,
            decimal toQty)
        {
            ctx.UnitConversions.Add(new UnitConversion
            {
                IngredientId = ingredientId,
                FromUnitId = fromUnitId,
                FromQuantity = fromQty,
                ToUnitId = toUnitId,
                ToQuantity = toQty
            });
            ctx.SaveChanges();
        }
    }
}
