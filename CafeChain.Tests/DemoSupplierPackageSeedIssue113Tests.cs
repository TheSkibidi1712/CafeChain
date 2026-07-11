using System;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Auditing;
using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Inventories.Ingredients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Issue #113 Hybrid D — owner-approved demo package metadata on model HasData (EnsureCreated).
    /// Proves Configuration seed source only; does NOT prove migration InsertData / InitialCreate.
    /// </summary>
    public class DemoSupplierPackageSeedIssue113Tests : IntegrationTestBase
    {
        private const int UnitG = 1;
        private const int UnitKg = 2;
        private const int UnitMl = 3;

        private static readonly int[] OwnerApprovedOfferIds = { 2, 4, 6, 7, 8, 9 };

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

        [Fact]
        public void Seed_AllOwnerApprovedSupplierOffers_HaveCompletePackageMetadata()
        {
            using var ctx = CreateDbContext();

            AssertOffer(ctx, ingredientSupplierId: 2, packageQty: 380m, unitId: UnitMl, price: 27000m);
            AssertOffer(ctx, ingredientSupplierId: 4, packageQty: 750m, unitId: UnitMl, price: 250000m);
            AssertOffer(ctx, ingredientSupplierId: 6, packageQty: 500m, unitId: UnitG, price: 450000m);
            AssertOffer(ctx, ingredientSupplierId: 7, packageQty: 1m, unitId: UnitKg, price: 180000m);
            AssertOffer(ctx, ingredientSupplierId: 8, packageQty: 1m, unitId: UnitKg, price: 85000m);
            AssertOffer(ctx, ingredientSupplierId: 9, packageQty: 200m, unitId: UnitG, price: 120000m);
        }

        [Fact]
        public void Seed_SyntheticCondensedMilk_UsesVolumeUnitWithoutMassConversion()
        {
            using var ctx = CreateDbContext();

            var ingredient = ctx.Ingredients.Single(i => i.IngredientId == 2);
            Assert.Equal("ING00002", ingredient.Code);
            Assert.Equal("Sữa đặc demo lon 380 ml", ingredient.Name);
            Assert.Equal(UnitMl, ingredient.BaseUnitId);

            var offer = ctx.IngredientSuppliers.Single(s => s.IngredientSupplierId == 2);
            Assert.Equal(380m, offer.PackageQuantity);
            Assert.Equal(UnitMl, offer.UnitId);
            Assert.True(offer.IsPrimary);

            // No g ↔ ml conversion for this ingredient (synthetic volume assumption, no density).
            var massVolumeBridge = ctx.Set<UnitConversion>().Where(c =>
                c.IngredientId == 2
                && ((c.FromUnitId == UnitG && c.ToUnitId == UnitMl)
                    || (c.FromUnitId == UnitMl && c.ToUnitId == UnitG)
                    || (c.FromUnitId == UnitKg && c.ToUnitId == UnitMl)
                    || (c.FromUnitId == UnitMl && c.ToUnitId == UnitKg)));
            Assert.Empty(massVolumeBridge);
        }

        [Fact]
        public void Seed_TeaBox_UsesExplicitNetMassNotBagCountAsQuantity()
        {
            using var ctx = CreateDbContext();

            var ingredient = ctx.Ingredients.Single(i => i.IngredientId == 3);
            Assert.Equal("ING00003", ingredient.Code);
            Assert.Equal("Trà đen demo hộp 100 túi × 2 g", ingredient.Name);
            Assert.Equal(UnitG, ingredient.BaseUnitId);

            var offer = ctx.IngredientSuppliers.Single(s => s.IngredientSupplierId == 9);
            // 100 bags × 2 g = 200 g net — bag count is NOT the package quantity.
            Assert.Equal(200m, offer.PackageQuantity);
            Assert.NotEqual(100m, offer.PackageQuantity);
            Assert.Equal(UnitG, offer.UnitId);
            Assert.True(offer.IsPrimary);
        }

        [Fact]
        public void Seed_ApprovedPrimaryOffers_ArePrimary()
        {
            using var ctx = CreateDbContext();

            foreach (var id in OwnerApprovedOfferIds)
            {
                var offer = ctx.IngredientSuppliers.Single(s => s.IngredientSupplierId == id);
                Assert.True(offer.IsPrimary, $"IS#{id} must be primary after owner-approved demo seed.");
                Assert.True(offer.Active);
            }
        }

        [Fact]
        public async Task PurchaseAudit_FreshEnsureCreatedSeed_HasNoPackageQuantityBusinessDecisions()
        {
            // EnsureCreated model seed only — not migration-based SQL Server recreate proof.
            using var ctx = CreateDbContext();
            var report = await CreateAudit(ctx).RunAuditAsync();

            foreach (var id in OwnerApprovedOfferIds)
            {
                var offer = report.Offers.Single(o => o.IngredientSupplierId == id);
                Assert.DoesNotContain(CostIssueCodes.MissingPackageQuantity, offer.CostIssueCodes);
                Assert.NotEqual(PurchaseUnitRemediationClass.BusinessDecisionRequired, offer.Classification);
                Assert.Equal(PurchaseUnitRemediationClass.Complete, offer.Classification);
                Assert.Equal(CostCompletenessStatus.Complete, offer.CostingStatus);
            }

            Assert.DoesNotContain(
                report.Offers,
                o => o.CostIssueCodes.Contains(CostIssueCodes.MissingPackageQuantity));

            Assert.DoesNotContain(
                report.Offers,
                o => o.AuditIssueCodes.Contains(PurchaseUnitAuditIssueCodes.SoleCompleteOfferNotPrimary)
                     && OwnerApprovedOfferIds.Contains(o.IngredientSupplierId));
        }

        [Fact]
        public async Task PurchaseAudit_DemoSeed_ComputesExpectedBaseUnitCosts()
        {
            using var ctx = CreateDbContext();
            var report = await CreateAudit(ctx).RunAuditAsync();

            AssertBaseCost(report, ingredientSupplierId: 2, expected: 27000m / 380m);
            AssertBaseCost(report, ingredientSupplierId: 4, expected: 250000m / 750m);
            AssertBaseCost(report, ingredientSupplierId: 6, expected: 900m);
            AssertBaseCost(report, ingredientSupplierId: 7, expected: 180m);
            AssertBaseCost(report, ingredientSupplierId: 8, expected: 85m);
            AssertBaseCost(report, ingredientSupplierId: 9, expected: 600m);
        }

        private static void AssertOffer(
            AppDbContext ctx,
            int ingredientSupplierId,
            decimal packageQty,
            int unitId,
            decimal price)
        {
            var offer = ctx.IngredientSuppliers.Single(s => s.IngredientSupplierId == ingredientSupplierId);
            Assert.Equal(packageQty, offer.PackageQuantity);
            Assert.Equal(unitId, offer.UnitId);
            Assert.True(offer.IsPrimary);
            Assert.Equal(price, offer.CurrentPrice);
            Assert.True(offer.Active);
            Assert.True(offer.PackageQuantity > 0);
        }

        private static void AssertBaseCost(PurchaseUnitAuditReport report, int ingredientSupplierId, decimal expected)
        {
            var offer = report.Offers.Single(o => o.IngredientSupplierId == ingredientSupplierId);
            Assert.NotNull(offer.BaseUnitCost);
            Assert.Equal(expected, offer.BaseUnitCost!.Value, precision: 8);
        }
    }
}
