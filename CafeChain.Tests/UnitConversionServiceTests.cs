using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Shared unit conversion — physical first, ACTIVE ingredient rows, no raw quantity.</summary>
    public class UnitConversionServiceTests : IntegrationTestBase
    {
        private const int UnitG = 1;
        private const int UnitKg = 2;
        private const int UnitMl = 3;
        private const int UnitL = 4;
        private const int UnitOz = 5;
        private const int UnitBottle = 10;
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
            // Physical would be 1000; zero ingredient row conflicts
            SeedConversion(ctx, IngredientId, UnitKg, 0m, UnitG, 1000m, active: true);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 1m, UnitKg);

            Assert.False(result.IsSuccess);
            Assert.True(
                result.ErrorCode == UnitConversionErrorCodes.ConflictingConversion
                || result.Message.Contains("không hợp lệ")
                || result.Message.Contains("xung đột"),
                result.Message);
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
            Assert.True(
                result.ErrorCode == UnitConversionErrorCodes.ConflictingConversion
                || result.Message.Contains("không hợp lệ")
                || result.Message.Contains("xung đột"),
                result.Message);
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
            Assert.True(
                result.ErrorCode == UnitConversionErrorCodes.ConflictingConversion
                || result.Message.Contains("không hợp lệ")
                || result.Message.Contains("xung đột"),
                result.Message);
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
            Assert.True(
                result.ErrorCode == UnitConversionErrorCodes.ConflictingConversion
                || result.Message.Contains("không hợp lệ")
                || result.Message.Contains("xung đột"),
                result.Message);
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
        public async Task Physical_KgToG_WithoutIngredientRow_Succeeds()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            EnsureMassVolumeUnits(ctx);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 2m, UnitKg);

            Assert.True(result.IsSuccess);
            Assert.Equal(2000m, result.Data);
        }

        [Fact]
        public async Task Inactive_Unit_IsNotHidden_ByIngredientFallback()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            EnsureMassVolumeUnits(ctx);
            // Even with a matching ingredient row, inactive unit must fail closed (no fallback).
            SeedConversion(ctx, IngredientId, UnitKg, 1m, UnitG, 1000m, active: true);
            var kg = ctx.Units.First(u => u.UnitId == UnitKg);
            kg.Active = false;
            ctx.SaveChanges();
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 1m, UnitKg);

            Assert.False(result.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.InactiveUnit, result.ErrorCode);
            Assert.NotEqual(1000m, result.Data);
        }

        [Fact]
        public async Task Matching_ActiveIngredient_KgToG_Compatible()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            EnsureMassVolumeUnits(ctx);
            SeedConversion(ctx, IngredientId, UnitKg, 1m, UnitG, 1000m, active: true);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 2m, UnitKg);

            Assert.True(result.IsSuccess);
            Assert.Equal(2000m, result.Data);
        }

        [Fact]
        public async Task Conflicting_ActiveIngredient_KgToG_FailsClosed()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            EnsureMassVolumeUnits(ctx);
            SeedConversion(ctx, IngredientId, UnitKg, 1m, UnitG, 999m, active: true);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 2m, UnitKg);

            Assert.False(result.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.ConflictingConversion, result.ErrorCode);
            Assert.NotEqual(1998m, result.Data);
            Assert.NotEqual(2000m, result.Data);
        }

        [Fact]
        public async Task Conflicting_ReverseRow_FailsClosed()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            EnsureMassVolumeUnits(ctx);
            // Reverse maps g→kg with wrong ratio (should be 1000 g = 1 kg)
            SeedConversion(ctx, IngredientId, UnitG, 500m, UnitKg, 1m, active: true);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 2m, UnitKg);

            Assert.False(result.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.ConflictingConversion, result.ErrorCode);
        }

        [Fact]
        public async Task Inactive_ConflictingRow_IsIgnored()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            EnsureMassVolumeUnits(ctx);
            SeedConversion(ctx, IngredientId, UnitKg, 1m, UnitG, 999m, active: false);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 2m, UnitKg);

            Assert.True(result.IsSuccess);
            Assert.Equal(2000m, result.Data);
        }

        [Fact]
        public async Task BottleToMl_PhysicalFails_ActiveIngredientSucceeds()
        {
            using var ctx = CreateDbContext();
            EnsureMassVolumeUnits(ctx);
            EnsureUnit(ctx, UnitBottle, "bottle", UnitType.Dem);
            SeedIngredient(ctx, IngredientId, UnitMl);
            SeedConversion(ctx, IngredientId, UnitBottle, 1m, UnitMl, 750m, active: true);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 2m, UnitBottle);

            Assert.True(result.IsSuccess);
            Assert.Equal(1500m, result.Data);
        }

        [Fact]
        public async Task Inactive_BottleToMl_DoesNotSucceed()
        {
            using var ctx = CreateDbContext();
            EnsureMassVolumeUnits(ctx);
            EnsureUnit(ctx, UnitBottle, "bottle", UnitType.Dem);
            SeedIngredient(ctx, IngredientId, UnitMl);
            SeedConversion(ctx, IngredientId, UnitBottle, 1m, UnitMl, 750m, active: false);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 2m, UnitBottle);

            Assert.False(result.IsSuccess);
            Assert.Contains("Thiếu quy đổi", result.Message);
        }

        [Fact]
        public async Task Oz_IsNotAutomaticallyGlobal()
        {
            using var ctx = CreateDbContext();
            EnsureMassVolumeUnits(ctx);
            EnsureUnit(ctx, UnitOz, "oz", UnitType.TheTich);
            SeedIngredient(ctx, IngredientId, UnitMl);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 1m, UnitOz);

            Assert.False(result.IsSuccess);
            Assert.Contains("Thiếu quy đổi", result.Message);
        }

        [Fact]
        public async Task NoRawQuantityFallback_WhenUnitsDiffer()
        {
            using var ctx = CreateDbContext();
            SeedIngredient(ctx, IngredientId, UnitG);
            EnsureMassVolumeUnits(ctx);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(IngredientId, 77m, UnitMl);

            Assert.False(result.IsSuccess);
            Assert.NotEqual(77m, result.Data);
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

        private static UnitConversionService CreateService(CafeChain.Data.AppDbContext ctx)
        {
            var physical = new PhysicalUnitConversionService(
                ctx,
                NullLogger<PhysicalUnitConversionService>.Instance);
            return new UnitConversionService(
                ctx,
                NullLogger<UnitConversionService>.Instance,
                physical);
        }

        private static void SeedIngredient(CafeChain.Data.AppDbContext ctx, int id, int baseUnitId)
        {
            EnsureMassVolumeUnits(ctx);

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

        private static void EnsureMassVolumeUnits(CafeChain.Data.AppDbContext ctx)
        {
            EnsureUnit(ctx, UnitG, "g", UnitType.KhoiLuong);
            EnsureUnit(ctx, UnitKg, "kg", UnitType.KhoiLuong);
            EnsureUnit(ctx, UnitMl, "ml", UnitType.TheTich);
            EnsureUnit(ctx, UnitL, "l", UnitType.TheTich);
        }

        private static void EnsureUnit(CafeChain.Data.AppDbContext ctx, int unitId, string code, UnitType type)
        {
            var existing = ctx.Units.FirstOrDefault(u => u.UnitId == unitId);
            if (existing != null)
            {
                if (string.IsNullOrWhiteSpace(existing.UnitCode))
                    existing.UnitCode = code;
                existing.Type = type;
                existing.Active = true;
                ctx.SaveChanges();
                return;
            }

            if (ctx.Units.Any(u => u.UnitCode.ToLower() == code.ToLower()))
                return;

            ctx.Units.Add(new Unit
            {
                UnitId = unitId,
                UnitCode = code,
                Name = code,
                Type = type,
                Active = true
            });
            ctx.SaveChanges();
        }

        private static void SeedConversion(
            CafeChain.Data.AppDbContext ctx,
            int ingredientId,
            int fromUnitId,
            decimal fromQty,
            int toUnitId,
            decimal toQty,
            bool active = true)
        {
            ctx.UnitConversions.Add(new UnitConversion
            {
                IngredientId = ingredientId,
                FromUnitId = fromUnitId,
                FromQuantity = fromQty,
                ToUnitId = toUnitId,
                ToQuantity = toQty,
                Active = active
            });
            ctx.SaveChanges();
        }
    }
}
