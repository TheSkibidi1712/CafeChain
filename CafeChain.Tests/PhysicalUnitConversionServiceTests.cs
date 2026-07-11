using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Issue #110 — global physical conversion (UnitCode, not fixed UnitId).</summary>
    public class PhysicalUnitConversionServiceTests : IntegrationTestBase
    {
        [Fact]
        public async Task SameActiveUnit_ReturnsUnchanged()
        {
            using var ctx = CreateDbContext();
            var g = EnsureUnit(ctx, "g", UnitType.KhoiLuong, unitId: 9001);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(42m, g, g);

            Assert.True(result.IsSuccess);
            Assert.Equal(42m, result.Data);
        }

        [Fact]
        public async Task Kg_To_G()
        {
            using var ctx = CreateDbContext();
            var g = EnsureUnit(ctx, "g", UnitType.KhoiLuong, 9001);
            var kg = EnsureUnit(ctx, "kg", UnitType.KhoiLuong, 9002);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(2m, kg, g);

            Assert.True(result.IsSuccess);
            Assert.Equal(2000m, result.Data);
        }

        [Fact]
        public async Task G_To_Kg()
        {
            using var ctx = CreateDbContext();
            var g = EnsureUnit(ctx, "g", UnitType.KhoiLuong, 9001);
            var kg = EnsureUnit(ctx, "kg", UnitType.KhoiLuong, 9002);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(2000m, g, kg);

            Assert.True(result.IsSuccess);
            Assert.Equal(2m, result.Data);
        }

        [Fact]
        public async Task L_To_Ml()
        {
            using var ctx = CreateDbContext();
            var ml = EnsureUnit(ctx, "ml", UnitType.TheTich, 9003);
            var l = EnsureUnit(ctx, "l", UnitType.TheTich, 9004);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(1.5m, l, ml);

            Assert.True(result.IsSuccess);
            Assert.Equal(1500m, result.Data);
        }

        [Fact]
        public async Task Ml_To_L()
        {
            using var ctx = CreateDbContext();
            var ml = EnsureUnit(ctx, "ml", UnitType.TheTich, 9003);
            var l = EnsureUnit(ctx, "l", UnitType.TheTich, 9004);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(1500m, ml, l);

            Assert.True(result.IsSuccess);
            Assert.Equal(1.5m, result.Data);
        }

        [Fact]
        public async Task Mass_To_Volume_Rejected()
        {
            using var ctx = CreateDbContext();
            var g = EnsureUnit(ctx, "g", UnitType.KhoiLuong, 9001);
            var ml = EnsureUnit(ctx, "ml", UnitType.TheTich, 9003);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(1m, g, ml);

            Assert.False(result.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.IncompatibleDimension, result.ErrorCode);
        }

        [Fact]
        public async Task Volume_To_Mass_Rejected()
        {
            using var ctx = CreateDbContext();
            var g = EnsureUnit(ctx, "g", UnitType.KhoiLuong, 9001);
            var ml = EnsureUnit(ctx, "ml", UnitType.TheTich, 9003);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(1m, ml, g);

            Assert.False(result.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.IncompatibleDimension, result.ErrorCode);
        }

        [Fact]
        public async Task Pcs_To_Ml_Rejected()
        {
            using var ctx = CreateDbContext();
            var pcs = EnsureUnit(ctx, "pcs", UnitType.Dem, 9010);
            var ml = EnsureUnit(ctx, "ml", UnitType.TheTich, 9003);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(1m, pcs, ml);

            Assert.False(result.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.IncompatibleDimension, result.ErrorCode);
        }

        [Fact]
        public async Task Bottle_To_Ml_Rejected_ByPhysicalService()
        {
            using var ctx = CreateDbContext();
            var bottle = EnsureUnit(ctx, "bottle", UnitType.Dem, 9011);
            var ml = EnsureUnit(ctx, "ml", UnitType.TheTich, 9003);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(1m, bottle, ml);

            Assert.False(result.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.IncompatibleDimension, result.ErrorCode);
        }

        [Fact]
        public async Task Inactive_SourceUnit_Rejected()
        {
            using var ctx = CreateDbContext();
            var g = EnsureUnit(ctx, "g", UnitType.KhoiLuong, 9001, active: true);
            var kg = EnsureUnit(ctx, "kg", UnitType.KhoiLuong, 9002, active: false);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(1m, kg, g);

            Assert.False(result.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.InactiveUnit, result.ErrorCode);
        }

        [Fact]
        public async Task Inactive_TargetUnit_Rejected()
        {
            using var ctx = CreateDbContext();
            var g = EnsureUnit(ctx, "g", UnitType.KhoiLuong, 9001, active: false);
            var kg = EnsureUnit(ctx, "kg", UnitType.KhoiLuong, 9002, active: true);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(1m, kg, g);

            Assert.False(result.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.InactiveUnit, result.ErrorCode);
        }

        [Fact]
        public async Task Unknown_Unit_Rejected()
        {
            using var ctx = CreateDbContext();
            var g = EnsureUnit(ctx, "g", UnitType.KhoiLuong, 9001);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(1m, 999999, g);

            Assert.False(result.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.InvalidUnit, result.ErrorCode);
        }

        [Fact]
        public async Task Unsupported_SameDimension_Pair_MissingPhysical()
        {
            using var ctx = CreateDbContext();
            var oz = EnsureUnit(ctx, "oz", UnitType.TheTich, 9005);
            var ml = EnsureUnit(ctx, "ml", UnitType.TheTich, 9003);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(1m, oz, ml);

            Assert.False(result.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.MissingPhysicalConversion, result.ErrorCode);
        }

        [Fact]
        public async Task Decimal_Overflow_SafeFailure()
        {
            using var ctx = CreateDbContext();
            var g = EnsureUnit(ctx, "g", UnitType.KhoiLuong, 9001);
            var kg = EnsureUnit(ctx, "kg", UnitType.KhoiLuong, 9002);
            var svc = CreateService(ctx);

            var result = await svc.ConvertAsync(decimal.MaxValue, kg, g);

            Assert.False(result.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.ConversionOverflow, result.ErrorCode);
        }

        [Fact]
        public async Task Reverse_RoundTrip_IsConsistent()
        {
            using var ctx = CreateDbContext();
            var g = EnsureUnit(ctx, "g", UnitType.KhoiLuong, 9001);
            var kg = EnsureUnit(ctx, "kg", UnitType.KhoiLuong, 9002);
            var svc = CreateService(ctx);

            var toG = await svc.ConvertAsync(3.25m, kg, g);
            Assert.True(toG.IsSuccess);
            var back = await svc.ConvertAsync(toG.Data, g, kg);

            Assert.True(back.IsSuccess);
            Assert.Equal(3.25m, back.Data);
        }

        [Fact]
        public void Registry_Uses_Normalized_UnitCode_Not_UnitId()
        {
            // Pure registry: factors never encode database UnitId values.
            Assert.True(PhysicalUnitConversionRegistry.TryGetPairFactor(
                "KG", "G", UnitType.KhoiLuong, UnitType.KhoiLuong, out var factorUpper));
            Assert.Equal(1000m, factorUpper);

            Assert.True(PhysicalUnitConversionRegistry.TryGetPairFactor(
                "kg", "g", UnitType.KhoiLuong, UnitType.KhoiLuong, out var factorLower));
            Assert.Equal(1000m, factorLower);

            Assert.False(PhysicalUnitConversionRegistry.TryGetPairFactor(
                "oz", "ml", UnitType.TheTich, UnitType.TheTich, out _));
        }

        [Fact]
        public async Task Service_Resolves_By_UnitCode_Regardless_Of_Which_Ids_Hold_Those_Codes()
        {
            using var ctx = CreateDbContext();
            // Seeded UnitIds may be 1=g and 2=kg; service must still work purely via UnitCode.
            var g = EnsureUnit(ctx, "g", UnitType.KhoiLuong, 1);
            var kg = EnsureUnit(ctx, "kg", UnitType.KhoiLuong, 2);
            var gEntity = ctx.Units.First(u => u.UnitId == g);
            var kgEntity = ctx.Units.First(u => u.UnitId == kg);
            Assert.Equal("g", PhysicalUnitConversionRegistry.NormalizeUnitCode(gEntity.UnitCode));
            Assert.Equal("kg", PhysicalUnitConversionRegistry.NormalizeUnitCode(kgEntity.UnitCode));

            var svc = CreateService(ctx);
            var result = await svc.ConvertAsync(1m, kg, g);

            Assert.True(result.IsSuccess);
            Assert.Equal(1000m, result.Data);
        }

        private static PhysicalUnitConversionService CreateService(CafeChain.Data.AppDbContext ctx) =>
            new(ctx, NullLogger<PhysicalUnitConversionService>.Instance);

        private static int EnsureUnit(
            CafeChain.Data.AppDbContext ctx,
            string unitCode,
            UnitType type,
            int unitId,
            bool active = true)
        {
            var existing = ctx.Units.FirstOrDefault(u => u.UnitId == unitId);
            if (existing != null)
            {
                existing.UnitCode = unitCode;
                existing.Type = type;
                existing.Active = active;
                existing.Name = unitCode;
                ctx.SaveChanges();
                return unitId;
            }

            // Prefer existing seed by code if present (integration DB)
            var byCode = ctx.Units.FirstOrDefault(u =>
                u.UnitCode.ToLower() == unitCode.ToLower());
            if (byCode != null)
            {
                byCode.Type = type;
                byCode.Active = active;
                ctx.SaveChanges();
                return byCode.UnitId;
            }

            ctx.Units.Add(new Unit
            {
                UnitId = unitId,
                UnitCode = unitCode,
                Name = unitCode,
                Type = type,
                Active = active
            });
            ctx.SaveChanges();
            return unitId;
        }
    }
}
