using System.Reflection;
using System.Text.Json;
using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Services.Admin.Suppliers;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Infrastrusture.Repositories.Admin.Suppliers;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests;

public class SupplierProcurementUomHardeningTests : IntegrationTestBase
{
    private const int UnitGram = 1;
    private const int UnitKilogram = 2;
    private const int UnitMilliliter = 3;
    private const int UnitLiter = 4;

    [Fact]
    public async Task VolumeIngredient_LoosePurchase_AllowsBaseMilliliter()
    {
        await using var context = CreateDbContext();
        var supplier = await context.Suppliers.AsNoTracking().OrderBy(x => x.SupplierId).FirstAsync();
        var ingredientId = 9801;
        EnsureUnit(context, UnitMilliliter, "ml", UnitType.TheTich);
        EnsureUnit(context, UnitLiter, "l", UnitType.TheTich);
        context.Ingredients.Add(new Ingredient
        {
            IngredientId = ingredientId,
            Code = "UOM9801",
            Name = "Tương kiểm thử",
            BaseUnitId = UnitMilliliter,
            Active = true
        });
        await context.SaveChangesAsync();

        var service = CreateSupplierService(context);
        var offerId = await service.CreateIngredientOfferAsync(new AdminIngredientSupplierSaveDTO
        {
            SupplierId = supplier.SupplierId,
            IngredientId = ingredientId,
            UnitId = UnitMilliliter,
            PackageQuantity = 200m,
            CurrentPrice = 168_000m,
            MinimumOrderPackageCount = 2,
            AllowsLoosePurchase = true,
            LooseProcurementUnitId = UnitMilliliter,
            CurrentProcurementUnitPrice = 840m,
            Active = true
        });

        var saved = await context.IngredientSuppliers.AsNoTracking()
            .SingleAsync(x => x.IngredientSupplierId == offerId);
        Assert.Equal(UnitMilliliter, saved.LooseProcurementUnitId);
    }

    [Fact]
    public async Task ProcurementUnitOptions_ForVolume_OnlyExposeMilliliterAndLiter()
    {
        await using var context = CreateDbContext();
        const int ingredientId = 9803;
        EnsureUnit(context, UnitMilliliter, "ml", UnitType.TheTich);
        EnsureUnit(context, UnitLiter, "l", UnitType.TheTich);
        EnsureUnit(context, 6, "cup", UnitType.TheTich);
        context.Ingredients.Add(new Ingredient
        {
            IngredientId = ingredientId,
            Code = "UOM9803",
            Name = "Siro kiểm thử",
            BaseUnitId = UnitMilliliter,
            Active = true
        });
        context.UnitConversions.Add(new UnitConversion
        {
            IngredientId = ingredientId,
            FromUnitId = 6,
            FromQuantity = 1m,
            ToUnitId = UnitMilliliter,
            ToQuantity = 240m,
            Active = true
        });
        await context.SaveChangesAsync();

        var allOptions = await CreateUnitConversionService(context)
            .GetActiveUnitOptionsAsync(ingredientId);
        Assert.True(allOptions.IsSuccess, allOptions.Message);

        var procurementOptions = ProcurementUnitPolicy.Filter(allOptions.Data);
        Assert.Equal(new[] { "ml", "l" }, procurementOptions.Select(x => x.UnitCode));
        Assert.DoesNotContain(procurementOptions, x => x.UnitCode == "cup");
    }

    [Theory]
    [InlineData(UnitType.KhoiLuong, "g", UnitType.TheTich, "l")]
    [InlineData(UnitType.TheTich, "ml", UnitType.KhoiLuong, "kg")]
    [InlineData(UnitType.TheTich, "ml", UnitType.Dem, "can")]
    public void ProcurementUnitPolicy_RejectsCrossDimensionOrPackagingUnits(
        UnitType baseType,
        string baseCode,
        UnitType candidateType,
        string candidateCode)
    {
        var baseUnit = new Unit { UnitCode = baseCode, Type = baseType };
        var candidate = new Unit { UnitCode = candidateCode, Type = candidateType };

        Assert.False(ProcurementUnitPolicy.IsAllowed(baseUnit, candidate));
    }

    [Fact]
    public void ProcurementUnitPolicy_AllowsConfiguredCountUnitAndHidesTechnicalTypeFromJson()
    {
        var options = new List<CafeChain.Application.DTOs.Inventories.InventoryUnitOptionDTO>
        {
            new()
            {
                UnitId = 9,
                UnitCode = "pcs",
                UnitName = "Cái",
                UnitType = UnitType.Dem,
                ConversionFactorToBase = 1m,
                IsBaseUnit = true
            },
            new()
            {
                UnitId = 14,
                UnitCode = "carton",
                UnitName = "Thùng",
                UnitType = UnitType.Dem,
                ConversionFactorToBase = 1000m
            }
        };

        var filtered = ProcurementUnitPolicy.Filter(options);
        Assert.Equal(2, filtered.Count);
        Assert.DoesNotContain("unitType", JsonSerializer.Serialize(filtered), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoosePurchase_CanDeriveLiterPriceFromPackage()
    {
        await using var context = CreateDbContext();
        var supplier = await context.Suppliers.AsNoTracking().OrderBy(x => x.SupplierId).FirstAsync();
        const int ingredientId = 9802;
        EnsureUnit(context, UnitMilliliter, "ml", UnitType.TheTich);
        EnsureUnit(context, UnitLiter, "l", UnitType.TheTich);
        context.Ingredients.Add(new Ingredient
        {
            IngredientId = ingredientId,
            Code = "UOM9802",
            Name = "Sốt kiểm thử",
            BaseUnitId = UnitMilliliter,
            Active = true
        });
        await context.SaveChangesAsync();

        var offerId = await CreateSupplierService(context).CreateIngredientOfferAsync(
            new AdminIngredientSupplierSaveDTO
            {
                SupplierId = supplier.SupplierId,
                IngredientId = ingredientId,
                UnitId = UnitMilliliter,
                PackageQuantity = 200m,
                CurrentPrice = 168_000m,
                MinimumOrderPackageCount = 1,
                AllowsLoosePurchase = true,
                LooseProcurementUnitId = UnitLiter,
                LoosePriceMode = LoosePurchasePriceModes.Derived,
                Active = true
            });

        var saved = await context.IngredientSuppliers.AsNoTracking()
            .SingleAsync(x => x.IngredientSupplierId == offerId);
        Assert.Equal(840_000m, saved.CurrentProcurementUnitPrice);
        Assert.Equal(LoosePurchasePriceModes.Derived, saved.LoosePriceMode);
    }

    [Theory]
    [InlineData("0.4", "0.5", "0.1", "0.5", "0.1")]
    [InlineData("1.01", "0", "0.1", "1.1", "0.09")]
    public void LoosePurchase_AppliesMoqAndQuantityStep(
        string requested,
        string moq,
        string step,
        string expectedOrdered,
        string expectedSurplus)
    {
        var success = LoosePurchaseMath.TryPlan(
            decimal.Parse(requested, System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(moq, System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(step, System.Globalization.CultureInfo.InvariantCulture),
            out var plan);

        Assert.True(success);
        Assert.Equal(decimal.Parse(expectedOrdered, System.Globalization.CultureInfo.InvariantCulture), plan.OrderedQuantity);
        Assert.Equal(decimal.Parse(expectedSurplus, System.Globalization.CultureInfo.InvariantCulture), plan.RoundingSurplusQuantity);
    }

    [Fact]
    public void SupplierAuditUi_DoesNotRenderRawTechnicalPayload()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Supplier", "supplier.js"));

        Assert.DoesNotContain("item.newData", source, StringComparison.Ordinal);
        Assert.DoesNotContain("item.actorStaffId", source, StringComparison.Ordinal);
        Assert.Contains("item.changes", source, StringComparison.Ordinal);
        Assert.Contains("item.actorName", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupplierUomRepair_DryRun_FlagsAmbiguousLooseDataWithoutModification()
    {
        await using var context = CreateDbContext();
        var supplier = await context.Suppliers.OrderBy(x => x.SupplierId).FirstAsync();
        var ingredient = await context.Ingredients.Include(x => x.BaseUnit)
            .FirstAsync(x => x.Active);
        var offer = new IngredientSupplier
        {
            SupplierId = supplier.SupplierId,
            IngredientId = ingredient.IngredientId,
            UnitId = ingredient.BaseUnitId,
            PackageQuantity = 1m,
            CurrentPrice = 10_000m,
            Active = true,
            AllowsLoosePurchase = false,
            LooseProcurementUnitId = ingredient.BaseUnitId,
            CurrentProcurementUnitPrice = 9_000m,
            LooseMinimumOrderQuantity = 1m,
            LooseQuantityStep = 0.1m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.IngredientSuppliers.Add(offer);
        await context.SaveChangesAsync();

        var report = await new SupplierProcurementDataQualityService(
            context,
            CreateUnitConversionService(context)).InspectAsync();

        Assert.True(report.DryRun);
        Assert.Contains(report.Findings, x =>
            x.EntityId == offer.IngredientSupplierId
            && x.Code == "LOOSE_FIELDS_LEFTOVER"
            && x.Resolution == "NEEDS_REVIEW");
        var unchanged = await context.IngredientSuppliers.AsNoTracking()
            .SingleAsync(x => x.IngredientSupplierId == offer.IngredientSupplierId);
        Assert.Equal(9_000m, unchanged.CurrentProcurementUnitPrice);
    }

    [Fact]
    public void SupplierFrontend_DoesNotHardcodeLooseUnits()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Supplier", "supplier.js"));

        Assert.DoesNotContain("['kg', 'l', 'pcs']", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RestockBackend_DoesNotWhitelistOnlyKgLiterAndPiece()
    {
        var field = typeof(RestockRequestService).GetField(
            "AllowedProcurementUnitCodes",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.Null(field);
    }

    [Theory]
    [InlineData("CreateManual.cshtml")]
    [InlineData("CreateCentralPlanner.cshtml")]
    public void RestockCreateForm_UsesDemandUnitLabel(string fileName)
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRestockRequests", fileName));

        Assert.Contains("Đơn vị nhu cầu", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Đơn vị mua hàng", source, StringComparison.Ordinal);
    }

    [Fact]
    public void IngredientSupplier_ModelEnforcesOneActivePrimarySourcePerIngredient()
    {
        using var context = CreateDbContext();
        var entityType = context.Model.FindEntityType(typeof(IngredientSupplier));
        var index = Assert.Single(entityType!.GetIndexes(), candidate =>
            candidate.IsUnique
            && candidate.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(IngredientSupplier.IngredientId) })
            && candidate.GetFilter() == "[IsPrimary] = 1 AND [Active] = 1");

        Assert.Equal("UX_IngredientSuppliers_PrimaryByIngredient", index.GetDatabaseName());
    }

    private static AdminSupplierService CreateSupplierService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        return new AdminSupplierService(
            new AdminSupplierRepository(context),
            context,
            new IngredientSupplierPackageValidator(context, physical));
    }

    private static UnitConversionService CreateUnitConversionService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        return new UnitConversionService(
            context,
            NullLogger<UnitConversionService>.Instance,
            physical);
    }

    private static void EnsureUnit(AppDbContext context, int unitId, string code, UnitType type)
    {
        var unit = context.Units.SingleOrDefault(x => x.UnitId == unitId);
        if (unit == null)
        {
            context.Units.Add(new Unit
            {
                UnitId = unitId,
                UnitCode = code,
                Name = code,
                Type = type,
                Active = true
            });
        }
        else
        {
            unit.UnitCode = code;
            unit.Name = code;
            unit.Type = type;
            unit.Active = true;
        }
        context.SaveChanges();
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(current.FullName, "CafeChain.Tests")))
                return current.FullName;
            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
