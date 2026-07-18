using System;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Services.Admin.InventoryDocuments;
using CafeChain.Application.Services.Admin.Suppliers;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Infrastrusture.Repositories.Admin.Suppliers;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Issue #111 — PackageQuantity, validation, history, document autofill safety.</summary>
    public class IngredientSupplierPackageQuantityIssue111Tests : IntegrationTestBase
    {
        private const int UnitG = 1;
        private const int UnitKg = 2;
        private const int UnitMl = 3;
        private const int UnitL = 4;
        private const int UnitPcs = 9;
        private const int UnitBottle = 10;

        [Fact]
        public async Task NewActiveOffer_WithoutPackageQuantity_Fails()
        {
            using var ctx = CreateDbContext();
            var svc = CreateSupplierService(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
                {
                    SupplierId = supplierId,
                    IngredientId = ingredientId,
                    UnitId = UnitKg,
                    PackageQuantity = null,
                    CurrentPrice = 1000,
                    Active = true
                }));
        }

        [Fact]
        public async Task LegacyActiveNull_CanBeRead_AndIsIncomplete()
        {
            using var ctx = CreateDbContext();
            var svc = CreateSupplierService(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId);
            var offer = new IngredientSupplier
            {
                IngredientId = ingredientId,
                SupplierId = supplierId,
                UnitId = UnitKg,
                PackageQuantity = null,
                CurrentPrice = 5000,
                Active = true,
                IsPrimary = true
            };
            ctx.IngredientSuppliers.Add(offer);
            ctx.SaveChanges();

            var list = await svc.GetIngredientOffersAsync(supplierId);
            var row = list.Single(x => x.IngredientId == ingredientId);
            Assert.Null(row.PackageQuantity);
            Assert.False(row.HasCompletePackageDefinition);
        }

        [Fact]
        public async Task PackageQuantity_Zero_Fails()
        {
            using var ctx = CreateDbContext();
            var validator = CreateValidator(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId);

            var result = await validator.ValidateAsync(
                ingredientId, supplierId, UnitKg, 0m, 1000m, true, requirePackageQuantity: true);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task PackageQuantity_Negative_Fails()
        {
            using var ctx = CreateDbContext();
            var validator = CreateValidator(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId);

            var result = await validator.ValidateAsync(
                ingredientId, supplierId, UnitKg, -1m, 1000m, true, requirePackageQuantity: true);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task Kg_Content_With_G_Base_Succeeds()
        {
            using var ctx = CreateDbContext();
            var svc = CreateSupplierService(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId, baseUnitId: UnitG);

            var id = await svc.CreateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
            {
                SupplierId = supplierId,
                IngredientId = ingredientId,
                UnitId = UnitKg,
                PackageQuantity = 1m,
                CurrentPrice = 140000,
                Active = true
            });

            Assert.True(id > 0);
            var offer = await ctx.IngredientSuppliers.FindAsync(id);
            Assert.Equal(1m, offer!.PackageQuantity);
        }

        [Fact]
        public async Task L_Content_With_Ml_Base_Succeeds()
        {
            using var ctx = CreateDbContext();
            var svc = CreateSupplierService(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId, baseUnitId: UnitMl);

            var id = await svc.CreateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
            {
                SupplierId = supplierId,
                IngredientId = ingredientId,
                UnitId = UnitL,
                PackageQuantity = 1m,
                CurrentPrice = 95000,
                Active = true
            });

            Assert.True(id > 0);
        }

        [Fact]
        public async Task Pcs_Content_With_Pcs_Base_Succeeds()
        {
            using var ctx = CreateDbContext();
            EnsureUnit(ctx, UnitPcs, "pcs", UnitType.Dem);
            var svc = CreateSupplierService(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId, baseUnitId: UnitPcs);

            var id = await svc.CreateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
            {
                SupplierId = supplierId,
                IngredientId = ingredientId,
                UnitId = UnitPcs,
                PackageQuantity = 100m,
                CurrentPrice = 50000,
                Active = true
            });

            Assert.True(id > 0);
        }

        [Fact]
        public async Task Bottle_ContentUnit_Rejected_ForNewOffer()
        {
            using var ctx = CreateDbContext();
            EnsureUnit(ctx, UnitBottle, "bottle", UnitType.Dem);
            var validator = CreateValidator(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId, baseUnitId: UnitMl);

            var result = await validator.ValidateAsync(
                ingredientId, supplierId, UnitBottle, 1m, 1000m, true, requirePackageQuantity: true);

            Assert.False(result.IsSuccess);
            Assert.Contains("đóng gói", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Mass_Content_For_VolumeBase_Fails()
        {
            using var ctx = CreateDbContext();
            var validator = CreateValidator(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId, baseUnitId: UnitMl);

            var result = await validator.ValidateAsync(
                ingredientId, supplierId, UnitKg, 1m, 1000m, true, requirePackageQuantity: true);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task Volume_Content_For_MassBase_Fails()
        {
            using var ctx = CreateDbContext();
            var validator = CreateValidator(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId, baseUnitId: UnitG);

            var result = await validator.ValidateAsync(
                ingredientId, supplierId, UnitMl, 1m, 1000m, true, requirePackageQuantity: true);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task Unique_IngredientSupplier_Enforced()
        {
            using var ctx = CreateDbContext();
            var svc = CreateSupplierService(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId);

            await svc.CreateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
            {
                SupplierId = supplierId,
                IngredientId = ingredientId,
                UnitId = UnitKg,
                PackageQuantity = 1m,
                CurrentPrice = 1000,
                Active = true
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
                {
                    SupplierId = supplierId,
                    IngredientId = ingredientId,
                    UnitId = UnitKg,
                    PackageQuantity = 1m,
                    CurrentPrice = 2000,
                    Active = true
                }));
        }

        [Fact]
        public async Task PriceChange_CreatesNewCurrentHistory_AndKeepsOldSnapshot()
        {
            using var ctx = CreateDbContext();
            var svc = CreateSupplierService(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId);

            var id = await svc.CreateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
            {
                SupplierId = supplierId,
                IngredientId = ingredientId,
                UnitId = UnitKg,
                PackageQuantity = 1m,
                CurrentPrice = 1000,
                Active = true
            });

            await svc.UpdateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
            {
                IngredientSupplierId = id,
                SupplierId = supplierId,
                IngredientId = ingredientId,
                UnitId = UnitKg,
                PackageQuantity = 1m,
                CurrentPrice = 2000,
                Active = true,
                RowVersion = await OfferVersionAsync(ctx, id)
            });

            var histories = await ctx.Set<IngredientSupplierPriceHistory>()
                .Where(h => h.IngredientSupplierId == id)
                .OrderBy(h => h.IngredientSupplierPriceHistoryId)
                .ToListAsync();

            Assert.Equal(2, histories.Count);
            Assert.False(histories[0].IsCurrent);
            Assert.Equal(1000m, histories[0].Price);
            Assert.Equal(1m, histories[0].PackageQuantity);
            Assert.True(histories[1].IsCurrent);
            Assert.Equal(2000m, histories[1].Price);
        }

        [Fact]
        public async Task PackageQuantityChange_CreatesHistorySnapshot()
        {
            using var ctx = CreateDbContext();
            var svc = CreateSupplierService(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId);

            var id = await svc.CreateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
            {
                SupplierId = supplierId,
                IngredientId = ingredientId,
                UnitId = UnitG,
                PackageQuantity = 500m,
                CurrentPrice = 10000,
                Active = true
            });

            await svc.UpdateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
            {
                IngredientSupplierId = id,
                SupplierId = supplierId,
                IngredientId = ingredientId,
                UnitId = UnitG,
                PackageQuantity = 1000m,
                CurrentPrice = 10000,
                Active = true,
                RowVersion = await OfferVersionAsync(ctx, id)
            });

            var current = await ctx.Set<IngredientSupplierPriceHistory>()
                .SingleAsync(h => h.IngredientSupplierId == id && h.IsCurrent);
            Assert.Equal(1000m, current.PackageQuantity);
            Assert.Equal(UnitG, current.PackageUnitId);
        }

        [Fact]
        public async Task UnitIdChange_CreatesHistorySnapshot()
        {
            using var ctx = CreateDbContext();
            var svc = CreateSupplierService(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId, baseUnitId: UnitG);

            var id = await svc.CreateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
            {
                SupplierId = supplierId,
                IngredientId = ingredientId,
                UnitId = UnitG,
                PackageQuantity = 1000m,
                CurrentPrice = 140000,
                Active = true
            });

            await svc.UpdateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
            {
                IngredientSupplierId = id,
                SupplierId = supplierId,
                IngredientId = ingredientId,
                UnitId = UnitKg,
                PackageQuantity = 1m,
                CurrentPrice = 140000,
                Active = true,
                RowVersion = await OfferVersionAsync(ctx, id)
            });

            var current = await ctx.Set<IngredientSupplierPriceHistory>()
                .SingleAsync(h => h.IngredientSupplierId == id && h.IsCurrent);
            Assert.Equal(UnitKg, current.PackageUnitId);
            Assert.Equal(1m, current.PackageQuantity);
        }

        [Fact]
        public void SupplierIngredientDto_PackageQty750_NormalizesPackagePriceToContentUnit()
        {
            var supplier = BuildOffer(packageQty: 750m, unitId: UnitMl, price: 250000m, baseUnitId: UnitMl);
            var dto = InvokeMapSupplierIngredientDto(supplier);

            Assert.Equal(250000m, dto.PackagePrice);
            Assert.Equal(250000m / 750m, dto.SuggestedUnitPrice);
            Assert.True(dto.CanAutoFillUnitPrice);
            Assert.Equal(250000m / 750m, dto.SuggestedBaseUnitCost);
        }

        [Fact]
        public void SupplierIngredientDto_PackageQty1Kg_MayAutoFillUnitPrice()
        {
            var supplier = BuildOffer(packageQty: 1m, unitId: UnitKg, price: 140000m, baseUnitId: UnitG);
            // conversion kg→g for document path uses ingredient.UnitConversions in Map — add conversion
            supplier.Ingredient.UnitConversions.Add(new UnitConversion
            {
                IngredientId = supplier.IngredientId,
                FromUnitId = UnitKg,
                FromQuantity = 1,
                ToUnitId = UnitG,
                ToQuantity = 1000,
                Active = true
            });

            var dto = InvokeMapSupplierIngredientDto(supplier);

            Assert.True(dto.CanAutoFillUnitPrice);
            Assert.Equal(140000m, dto.SuggestedUnitPrice);
            Assert.NotNull(dto.SuggestedBaseUnitCost);
        }

        [Fact]
        public void SupplierIngredientDto_MissingPackage_SuggestedUnitPriceNull()
        {
            var supplier = BuildOffer(packageQty: null, unitId: UnitKg, price: 140000m, baseUnitId: UnitG);
            var dto = InvokeMapSupplierIngredientDto(supplier);

            Assert.Null(dto.SuggestedUnitPrice);
            Assert.False(dto.CanAutoFillUnitPrice);
            Assert.False(dto.HasCompletePackageDefinition);
        }

        [Fact]
        public void SupplierIngredientDto_ZeroPackagePrice_DoesNotAutoFill()
        {
            var supplier = BuildOffer(packageQty: 1m, unitId: UnitKg, price: 0m, baseUnitId: UnitG);
            supplier.Ingredient.UnitConversions.Add(new UnitConversion
            {
                IngredientId = supplier.IngredientId,
                FromUnitId = UnitKg,
                FromQuantity = 1,
                ToUnitId = UnitG,
                ToQuantity = 1000,
                Active = true
            });
            var dto = InvokeMapSupplierIngredientDto(supplier);
            Assert.False(dto.CanAutoFillUnitPrice);
            Assert.Null(dto.SuggestedUnitPrice);
        }

        [Fact]
        public async Task FailedUpdate_AfterValidation_DoesNotCorruptOffer()
        {
            using var ctx = CreateDbContext();
            var svc = CreateSupplierService(ctx);
            SeedCore(ctx, out var supplierId, out var ingredientId);

            var id = await svc.CreateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
            {
                SupplierId = supplierId,
                IngredientId = ingredientId,
                UnitId = UnitKg,
                PackageQuantity = 1m,
                CurrentPrice = 1000,
                Active = true
            });

            // Invalid: Active with cleared package quantity while changing price (requires package)
            var rowVersion = await OfferVersionAsync(ctx, id);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.UpdateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
                {
                    IngredientSupplierId = id,
                    SupplierId = supplierId,
                    IngredientId = ingredientId,
                    UnitId = UnitKg,
                    PackageQuantity = null,
                    CurrentPrice = 9999,
                    Active = true,
                    RowVersion = rowVersion
                }));

            var offer = await ctx.IngredientSuppliers.AsNoTracking()
                .SingleAsync(x => x.IngredientSupplierId == id);
            Assert.Equal(1000m, offer.CurrentPrice);
            Assert.Equal(1m, offer.PackageQuantity);

            var currentHistories = await ctx.Set<IngredientSupplierPriceHistory>()
                .AsNoTracking()
                .Where(h => h.IngredientSupplierId == id && h.IsCurrent)
                .ToListAsync();
            Assert.Single(currentHistories);
            Assert.Equal(1000m, currentHistories[0].Price);
        }

        [Fact]
        public void ApprovedSeed_PackageQuantity_IsOne_ForMappedOffers()
        {
            using var ctx = CreateDbContext();
            // Seeds from HasData after EnsureCreated
            var coffee = ctx.IngredientSuppliers.Single(x => x.IngredientSupplierId == 3);
            var sugar = ctx.IngredientSuppliers.Single(x => x.IngredientSupplierId == 1);
            var cocoa = ctx.IngredientSuppliers.Single(x => x.IngredientSupplierId == 7);
            var milkPowder = ctx.IngredientSuppliers.Single(x => x.IngredientSupplierId == 8);
            var cream = ctx.IngredientSuppliers.Single(x => x.IngredientSupplierId == 5);

            Assert.Equal(1m, coffee.PackageQuantity);
            Assert.Equal(1m, sugar.PackageQuantity);
            Assert.Equal(1m, cocoa.PackageQuantity);
            Assert.Equal(1m, milkPowder.PackageQuantity);
            Assert.Equal(1m, cream.PackageQuantity);
        }

        [Fact]
        public void OwnerApprovedDemoSeed_FormerAmbiguousOffers_HaveExplicitPackageQuantity()
        {
            // #113 Hybrid D: model HasData now carries owner-approved demo package quantities
            // (EnsureCreated path). Migration InsertData may lag until InitialCreate regenerate.
            using var ctx = CreateDbContext();
            var syrup = ctx.IngredientSuppliers.Single(x => x.IngredientSupplierId == 4);
            var condensed = ctx.IngredientSuppliers.Single(x => x.IngredientSupplierId == 2);
            var matcha = ctx.IngredientSuppliers.Single(x => x.IngredientSupplierId == 6);
            var tea = ctx.IngredientSuppliers.Single(x => x.IngredientSupplierId == 9);

            Assert.Equal(750m, syrup.PackageQuantity);
            Assert.Equal(380m, condensed.PackageQuantity);
            Assert.Equal(500m, matcha.PackageQuantity);
            Assert.Equal(200m, tea.PackageQuantity);
        }

        [Fact]
        public void NoIngredientNameParsingHelper_Exists()
        {
            // Guardrail: package size must not be derived from Ingredient.Name
            var helpers = typeof(IngredientSupplierPackageValidator).Assembly
                .GetTypes()
                .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance))
                .Where(m => m.Name.Contains("Parse", StringComparison.OrdinalIgnoreCase)
                            && m.Name.Contains("Package", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(helpers);
        }

        // ---- helpers ----

        private static AdminSupplierService CreateSupplierService(AppDbContext ctx)
        {
            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var validator = new IngredientSupplierPackageValidator(ctx, physical);
            var repo = new AdminSupplierRepository(ctx);
            return new AdminSupplierService(repo, ctx, validator);
        }

        private static IngredientSupplierPackageValidator CreateValidator(AppDbContext ctx)
        {
            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            return new IngredientSupplierPackageValidator(ctx, physical);
        }

        private static void SeedCore(
            AppDbContext ctx,
            out int supplierId,
            out int ingredientId,
            int baseUnitId = UnitG)
        {
            EnsureUnit(ctx, UnitG, "g", UnitType.KhoiLuong);
            EnsureUnit(ctx, UnitKg, "kg", UnitType.KhoiLuong);
            EnsureUnit(ctx, UnitMl, "ml", UnitType.TheTich);
            EnsureUnit(ctx, UnitL, "l", UnitType.TheTich);

            // Reuse seeded suppliers from HasData (avoid SQL Server GETDATE defaults on new Supplier inserts).
            var supplier = ctx.Suppliers.AsNoTracking().OrderBy(s => s.SupplierId).First();
            var sid = supplier.SupplierId;
            var iid = 9102;
            supplierId = sid;
            ingredientId = iid;

            var ingredient = ctx.Ingredients.FirstOrDefault(i => i.IngredientId == iid);
            if (ingredient == null)
            {
                ingredient = new Ingredient
                {
                    IngredientId = iid,
                    Code = "ING9102",
                    Name = "Test Ingredient Package",
                    BaseUnitId = baseUnitId,
                    Active = true
                };
                ctx.Ingredients.Add(ingredient);
            }
            else
            {
                ingredient.BaseUnitId = baseUnitId;
                ingredient.Active = true;
            }

            // Remove any prior test offers for this pair to keep unique index free
            var prior = ctx.IngredientSuppliers
                .Where(x => x.IngredientId == iid && x.SupplierId == sid)
                .ToList();
            if (prior.Count > 0)
            {
                ctx.IngredientSuppliers.RemoveRange(prior);
            }

            ctx.SaveChanges();
        }

        private static void EnsureUnit(AppDbContext ctx, int unitId, string code, UnitType type)
        {
            var u = ctx.Units.FirstOrDefault(x => x.UnitId == unitId);
            if (u != null)
            {
                u.UnitCode = code;
                u.Type = type;
                u.Active = true;
                ctx.SaveChanges();
                return;
            }

            if (ctx.Units.Any(x => x.UnitCode.ToLower() == code.ToLower()))
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

        private static IngredientSupplier BuildOffer(
            decimal? packageQty,
            int unitId,
            decimal price,
            int baseUnitId)
        {
            var unit = new Unit
            {
                UnitId = unitId,
                UnitCode = unitId switch
                {
                    UnitKg => "kg",
                    UnitMl => "ml",
                    UnitG => "g",
                    UnitL => "l",
                    _ => "u"
                },
                Name = "u",
                Type = unitId is UnitKg or UnitG ? UnitType.KhoiLuong : UnitType.TheTich,
                Active = true
            };
            var baseUnit = new Unit
            {
                UnitId = baseUnitId,
                UnitCode = baseUnitId == UnitG ? "g" : "ml",
                Name = "base",
                Type = baseUnitId == UnitG ? UnitType.KhoiLuong : UnitType.TheTich,
                Active = true
            };
            var ingredient = new Ingredient
            {
                IngredientId = 1,
                Code = "X",
                Name = "X",
                BaseUnitId = baseUnitId,
                BaseUnit = baseUnit,
                Active = true,
                UnitConversions = new System.Collections.Generic.List<UnitConversion>()
            };
            return new IngredientSupplier
            {
                IngredientSupplierId = 1,
                IngredientId = 1,
                SupplierId = 1,
                UnitId = unitId,
                Unit = unit,
                PackageQuantity = packageQty,
                CurrentPrice = price,
                Ingredient = ingredient,
                Active = true,
                PriceHistories = new System.Collections.Generic.List<IngredientSupplierPriceHistory>()
            };
        }

        private static CafeChain.Application.DTOs.Admin.InventoryDocuments.Create.SupplierIngredientDTO
            InvokeMapSupplierIngredientDto(IngredientSupplier supplier)
        {
            var method = typeof(AdminInventoryDocumentCreateService)
                .GetMethod("MapSupplierIngredientDto",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);
            return (CafeChain.Application.DTOs.Admin.InventoryDocuments.Create.SupplierIngredientDTO)
                method!.Invoke(null, new object[] { supplier })!;
        }

        private static async Task<string> OfferVersionAsync(
            CafeChain.Data.AppDbContext context,
            int ingredientSupplierId)
        {
            var version = await context.IngredientSuppliers
                .AsNoTracking()
                .Where(x => x.IngredientSupplierId == ingredientSupplierId)
                .Select(x => x.RowVersion)
                .SingleAsync();
            return Convert.ToBase64String(version);
        }
    }
}
