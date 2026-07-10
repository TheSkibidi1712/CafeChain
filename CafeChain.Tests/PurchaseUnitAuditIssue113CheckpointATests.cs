using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Auditing;
using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Issue #113 Checkpoint A — read-only purchase/unit audit infrastructure.</summary>
    public class PurchaseUnitAuditIssue113CheckpointATests : IntegrationTestBase
    {
        private const int UnitG = 1;
        private const int UnitKg = 2;
        private const int UnitMl = 3;

        private static PurchaseUnitAuditService CreateAudit(AppDbContext ctx)
        {
            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(ctx, NullLogger<UnitConversionService>.Instance, physical);
            var norm = new RecipeOutputNormalizer(ctx, physical);
            var estimated = new EstimatedBomCostService(
                ctx, unit, physical, norm, NullLogger<EstimatedBomCostService>.Instance);
            return new PurchaseUnitAuditService(
                ctx, estimated, NullLogger<PurchaseUnitAuditService>.Instance);
        }

        private static void EnsureUnits(AppDbContext ctx)
        {
            EnsureUnit(ctx, UnitG, "g", UnitType.KhoiLuong);
            EnsureUnit(ctx, UnitKg, "kg", UnitType.KhoiLuong);
            EnsureUnit(ctx, UnitMl, "ml", UnitType.TheTich);
        }

        private static void EnsureUnit(AppDbContext ctx, int id, string code, UnitType type)
        {
            var u = ctx.Units.FirstOrDefault(x => x.UnitId == id);
            if (u != null)
            {
                u.UnitCode = code;
                u.Type = type;
                u.Active = true;
                ctx.SaveChanges();
                return;
            }
            ctx.Units.Add(new Unit { UnitId = id, UnitCode = code, Name = code, Type = type, Active = true });
            ctx.SaveChanges();
        }

        [Fact]
        public async Task CompletePrimaryOffer_ClassifiedComplete()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            // Use seed coffee if present; else add fixture
            if (!ctx.IngredientSuppliers.Any(s => s.IngredientSupplierId == 3))
            {
                EnsureIngredient(ctx, 1, UnitG, "ING00001", "Coffee");
                ctx.IngredientSuppliers.Add(new IngredientSupplier
                {
                    IngredientSupplierId = 3,
                    IngredientId = 1,
                    SupplierId = 3,
                    UnitId = UnitKg,
                    PackageQuantity = 1m,
                    CurrentPrice = 140000m,
                    IsPrimary = true,
                    Active = true
                });
                await ctx.SaveChangesAsync();
            }

            var audit = CreateAudit(ctx);
            var report = await audit.RunAuditAsync();
            var coffee = report.Offers.FirstOrDefault(o => o.IngredientSupplierId == 3)
                ?? report.Offers.FirstOrDefault(o => o.IngredientId == 1 && o.IsPrimary && o.Active);

            Assert.NotNull(coffee);
            Assert.Equal(PurchaseUnitRemediationClass.Complete, coffee!.Classification);
            Assert.Equal(CostCompletenessStatus.Complete, coffee.CostingStatus);
            Assert.NotNull(coffee.BaseUnitCost);
            Assert.Equal(140m, coffee.BaseUnitCost);
        }

        [Fact]
        public async Task NullPackageQuantity_BusinessDecisionRequired()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            EnsureIngredient(ctx, 901, UnitMl, "ING901", "Ambiguous syrup");
            ctx.IngredientSuppliers.Add(new IngredientSupplier
            {
                IngredientId = 901,
                SupplierId = 1,
                UnitId = UnitMl,
                PackageQuantity = null,
                CurrentPrice = 250000m,
                IsPrimary = true,
                Active = true
            });
            await ctx.SaveChangesAsync();

            var report = await CreateAudit(ctx).RunAuditAsync();
            var row = report.Offers.Single(o => o.IngredientId == 901);
            Assert.Equal(PurchaseUnitRemediationClass.BusinessDecisionRequired, row.Classification);
            Assert.Contains(CostIssueCodes.MissingPackageQuantity, row.CostIssueCodes);
        }

        [Fact]
        public async Task SoleCompleteNonPrimary_SafeRemediationCandidate()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            EnsureIngredient(ctx, 902, UnitG, "ING902", "Cacao-like");
            ctx.IngredientSuppliers.Add(new IngredientSupplier
            {
                IngredientId = 902,
                SupplierId = 1,
                UnitId = UnitKg,
                PackageQuantity = 1m,
                CurrentPrice = 180000m,
                IsPrimary = false,
                Active = true
            });
            await ctx.SaveChangesAsync();

            var report = await CreateAudit(ctx).RunAuditAsync();
            var row = report.Offers.Single(o => o.IngredientId == 902);
            Assert.Equal(PurchaseUnitRemediationClass.SafeRemediationCandidate, row.Classification);
            Assert.Contains(PurchaseUnitAuditIssueCodes.SoleCompleteOfferNotPrimary, row.AuditIssueCodes);

            var primary = report.Primaries.Single(p => p.IngredientId == 902);
            Assert.Equal(0, primary.ActivePrimaryCount);
            Assert.Equal(1, primary.ActiveOfferCount);
        }

        [Fact]
        public async Task MultipleActivePrimary_InvalidConfiguration()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            EnsureIngredient(ctx, 903, UnitG, "ING903", "Multi primary");
            ctx.IngredientSuppliers.AddRange(
                new IngredientSupplier
                {
                    IngredientId = 903,
                    SupplierId = 1,
                    UnitId = UnitKg,
                    PackageQuantity = 1m,
                    CurrentPrice = 100000m,
                    IsPrimary = true,
                    Active = true
                },
                new IngredientSupplier
                {
                    IngredientId = 903,
                    SupplierId = 2,
                    UnitId = UnitKg,
                    PackageQuantity = 1m,
                    CurrentPrice = 110000m,
                    IsPrimary = true,
                    Active = true
                });
            await ctx.SaveChangesAsync();

            var report = await CreateAudit(ctx).RunAuditAsync();
            Assert.All(
                report.Offers.Where(o => o.IngredientId == 903),
                o => Assert.Equal(PurchaseUnitRemediationClass.InvalidConfiguration, o.Classification));

            var primary = report.Primaries.Single(p => p.IngredientId == 903);
            Assert.Equal(2, primary.ActivePrimaryCount);
            Assert.Contains(CostIssueCodes.MultiplePrimarySuppliers, primary.IssueCodes);
        }

        [Fact]
        public async Task DoesNotInferPackageFromIngredientName()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            // Name contains 750ml but package is null — must NOT become COMPLETE
            EnsureIngredient(ctx, 904, UnitMl, "ING904", "Syrup Torani Vanilla 750ml");
            ctx.IngredientSuppliers.Add(new IngredientSupplier
            {
                IngredientId = 904,
                SupplierId = 1,
                UnitId = UnitMl,
                PackageQuantity = null,
                CurrentPrice = 250000m,
                IsPrimary = true,
                Active = true
            });
            await ctx.SaveChangesAsync();

            var report = await CreateAudit(ctx).RunAuditAsync();
            var row = report.Offers.Single(o => o.IngredientId == 904);
            Assert.NotEqual(PurchaseUnitRemediationClass.Complete, row.Classification);
            Assert.Null(row.BaseUnitCost);
            Assert.False(row.PackageDefinitionComplete);
        }

        [Fact]
        public async Task Audit_IsReadOnly_DoesNotMutateOffersOrHistories()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var offerCountBefore = await ctx.IngredientSuppliers.CountAsync();
            var historyCountBefore = await ctx.IngredientSupplierPriceHistories.CountAsync();
            var primaryFlags = await ctx.IngredientSuppliers
                .AsNoTracking()
                .Select(s => new { s.IngredientSupplierId, s.IsPrimary, s.PackageQuantity, s.CurrentPrice })
                .OrderBy(s => s.IngredientSupplierId)
                .ToListAsync();

            var report = await CreateAudit(ctx).RunAuditAsync();
            Assert.NotNull(report);
            Assert.Equal("ReadOnly", report.Mode);
            Assert.False(ctx.ChangeTracker.HasChanges());

            Assert.Equal(offerCountBefore, await ctx.IngredientSuppliers.CountAsync());
            Assert.Equal(historyCountBefore, await ctx.IngredientSupplierPriceHistories.CountAsync());

            var primaryFlagsAfter = await ctx.IngredientSuppliers
                .AsNoTracking()
                .Select(s => new { s.IngredientSupplierId, s.IsPrimary, s.PackageQuantity, s.CurrentPrice })
                .OrderBy(s => s.IngredientSupplierId)
                .ToListAsync();
            Assert.Equal(primaryFlags, primaryFlagsAfter);
        }

        [Fact]
        public async Task Audit_IsDeterministicallyOrdered_AndLeavesNoTrackedChanges()
        {
            using var ctx = CreateDbContext();

            var audit = CreateAudit(ctx);
            var first = await audit.RunAuditAsync();
            var second = await audit.RunAuditAsync();

            Assert.Equal(
                first.Offers.Select(x => x.IngredientSupplierId),
                second.Offers.Select(x => x.IngredientSupplierId));
            Assert.Equal(
                first.Primaries.Select(x => x.IngredientId),
                second.Primaries.Select(x => x.IngredientId));
            Assert.Equal(
                first.PriceHistories.Select(x => x.IngredientSupplierId),
                second.PriceHistories.Select(x => x.IngredientSupplierId));
            Assert.Equal(
                first.Recipes.Select(x => x.RecipeId),
                second.Recipes.Select(x => x.RecipeId));
            Assert.False(ctx.ChangeTracker.HasChanges());
        }

        [Fact]
        public async Task PriceHistoryMissingCurrent_Flagged()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            EnsureIngredient(ctx, 905, UnitG, "ING905", "No history");
            var offer = new IngredientSupplier
            {
                IngredientId = 905,
                SupplierId = 1,
                UnitId = UnitKg,
                PackageQuantity = 1m,
                CurrentPrice = 50000m,
                IsPrimary = true,
                Active = true
            };
            ctx.IngredientSuppliers.Add(offer);
            await ctx.SaveChangesAsync();

            var report = await CreateAudit(ctx).RunAuditAsync();
            var hist = report.PriceHistories.Single(h => h.IngredientSupplierId == offer.IngredientSupplierId);
            Assert.True(hist.MissingCurrentHistory);
            Assert.Contains(PurchaseUnitAuditIssueCodes.PriceHistoryMissingCurrent, hist.IssueCodes);
        }

        [Fact]
        public async Task RecipeAudit_UsesEstimatedBomCost_NotFakeZero()
        {
            using var ctx = CreateDbContext();
            var report = await CreateAudit(ctx).RunAuditAsync();
            // Seed recipes with missing package data should be Incomplete with null TotalCost
            foreach (var r in report.Recipes.Where(x => x.Status == CostCompletenessStatus.Incomplete))
            {
                Assert.Null(r.TotalCost);
            }
        }

        [Fact]
        public async Task BaselineSeedReport_CanSerialize()
        {
            using var ctx = CreateDbContext();
            var report = await CreateAudit(ctx).RunAuditAsync();
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
            Assert.Contains("ReadOnly", json);
            Assert.Contains("113.A.1", json);
        }

        private static void EnsureIngredient(AppDbContext ctx, int id, int baseUnitId, string code, string name)
        {
            if (ctx.Ingredients.Any(i => i.IngredientId == id))
                return;
            ctx.Ingredients.Add(new Ingredient
            {
                IngredientId = id,
                Code = code,
                Name = name,
                BaseUnitId = baseUnitId,
                Active = true
            });
            ctx.SaveChanges();
        }
    }
}
