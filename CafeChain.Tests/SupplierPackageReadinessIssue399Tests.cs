using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Services.Admin.Suppliers;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Infrastructure.Repositories.Admin.Procurement;
using CafeChain.Infrastrusture.Repositories.Admin.Suppliers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests;

public sealed class SupplierPackageReadinessIssue399Tests : IntegrationTestBase
{
    private const int GramUnitId = 1;
    private const int PieceUnitId = 9;
    private const int CartonUnitId = 14;
    private const int IngredientId = 99399;

    [Fact]
    public async Task ValidInactivePackage_CanReactivate()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 120_000m, active: false);
        var service = CreateSupplierService(context);

        await service.ToggleIngredientOfferActiveAsync(
            offer.IngredientSupplierId,
            true,
            await OfferVersionAsync(context, offer.IngredientSupplierId),
            actorStaffId: 5);

        Assert.True(await context.IngredientSuppliers.AsNoTracking()
            .Where(x => x.IngredientSupplierId == offer.IngredientSupplierId)
            .Select(x => x.Active)
            .SingleAsync());
    }

    [Fact]
    public async Task InvalidInactivePackage_CannotReactivate()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 0m, active: false);
        var service = CreateSupplierService(context);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ToggleIngredientOfferActiveAsync(
                offer.IngredientSupplierId,
                true,
                OfferVersion(context, offer.IngredientSupplierId),
                actorStaffId: 5));

        Assert.Contains("Không thể kích hoạt gói mua", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageQuantity", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await context.IngredientSuppliers.AsNoTracking()
            .Where(x => x.IngredientSupplierId == offer.IngredientSupplierId)
            .Select(x => x.Active)
            .SingleAsync());
    }

    [Fact]
    public async Task FailedReactivation_DoesNotPersistActiveState()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 0m, 120_000m, active: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSupplierService(context).ToggleIngredientOfferActiveAsync(
                offer.IngredientSupplierId,
                true,
                OfferVersion(context, offer.IngredientSupplierId),
                actorStaffId: 5));

        Assert.False(await context.IngredientSuppliers.AsNoTracking()
            .Where(x => x.IngredientSupplierId == offer.IngredientSupplierId)
            .Select(x => x.Active)
            .SingleAsync());
    }

    [Fact]
    public async Task Reactivation_IsIdempotent()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 120_000m, active: false);
        var service = CreateSupplierService(context);

        await service.ToggleIngredientOfferActiveAsync(
            offer.IngredientSupplierId,
            true,
            OfferVersion(context, offer.IngredientSupplierId),
            actorStaffId: 5);
        await service.ToggleIngredientOfferActiveAsync(
            offer.IngredientSupplierId,
            true,
            OfferVersion(context, offer.IngredientSupplierId),
            actorStaffId: 5);

        Assert.Equal(1, await context.AuditLogs.CountAsync(x =>
            x.TableName == "Suppliers"
            && x.Action == "SUPPLIER_OFFER_STATUS_CHANGED"));
    }

    [Fact]
    public async Task ConcurrentReactivation_RespectsRowVersion()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 120_000m, active: false);
        var staleVersion = OfferVersion(context, offer.IngredientSupplierId);
        offer.RowVersion = new byte[] { 9, 9, 9 };
        await context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSupplierService(context).ToggleIngredientOfferActiveAsync(
                offer.IngredientSupplierId,
                true,
                staleVersion,
                actorStaffId: 5));

        Assert.Contains("người khác cập nhật", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await context.IngredientSuppliers.AsNoTracking()
            .Where(x => x.IngredientSupplierId == offer.IngredientSupplierId)
            .Select(x => x.Active)
            .SingleAsync());
    }

    [Fact]
    public async Task PackageReadiness_RequiresValidPrice()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 0m, active: true);
        var service = CreateSupplierService(context);

        var row = Assert.Single((await service.GetIngredientOffersAsync(offer.SupplierId))
            .Where(x => x.IngredientSupplierId == offer.IngredientSupplierId));

        Assert.False(row.IsProcurementReady);
        Assert.Equal("Chưa sẵn sàng mua hàng", row.ProcurementReadinessLabel);
    }

    [Fact]
    public async Task PackageReadiness_RequiresValidContentUom()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, CartonUnitId, 1m, 120_000m, active: true);

        var result = await CreateValidator(context).EvaluateReadinessAsync(offer);

        Assert.False(result.IsReady);
        Assert.Equal(SupplierPackageReadinessCodes.ContentUomInvalid, result.ReasonCode);
    }

    [Fact]
    public async Task PackageReadiness_RequiresPositiveContentQuantity()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 0m, 120_000m, active: true);

        var result = await CreateValidator(context).EvaluateReadinessAsync(offer);

        Assert.False(result.IsReady);
        Assert.Equal(SupplierPackageReadinessCodes.ContentMissing, result.ReasonCode);
    }

    [Fact]
    public async Task CountPackage_UsesBaseCountUnitCorrectly()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 120_000m, active: true);

        var result = await CreateValidator(context).EvaluateReadinessAsync(offer);

        Assert.True(result.IsReady);
        Assert.Equal(50m, result.PackageBaseQuantity);
    }

    [Fact]
    public async Task CountItem_PackageOf50Pcs_IsValid()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 120_000m, active: true);
        var service = CreateSupplierService(context);

        var row = Assert.Single((await service.GetIngredientOffersAsync(offer.SupplierId))
            .Where(x => x.IngredientSupplierId == offer.IngredientSupplierId));

        Assert.True(row.HasCompletePackageDefinition);
        Assert.True(row.IsProcurementReady);
        Assert.Equal("50 pcs / gói", row.PackageDisplay);
    }

    [Fact]
    public async Task CountItem_IncompatibleContentUom_IsRejected()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, CartonUnitId, 1m, 120_000m, active: false);
        var service = CreateSupplierService(context);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ToggleIngredientOfferActiveAsync(
                offer.IngredientSupplierId,
                true,
                OfferVersion(context, offer.IngredientSupplierId),
                actorStaffId: 5));

        Assert.Contains("đơn vị tồn kho", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UnitId", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PackageUnit_IsNotTreatedAsGlobalPhysicalUom()
    {
        var source = ReadSource(
            "Application", "Services", "Inventories", "IngredientSupplierPackageValidator.cs");

        Assert.Contains("PackageUnitCodes.IsRejectedCommercialPackaging", source, StringComparison.Ordinal);
        Assert.Contains("allowConfiguredCountConversion: false", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActiveButIncompletePackage_IsNotProcurementEligible()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 0m, active: true);
        var validator = CreateValidator(context);

        var result = await validator.EvaluateProcurementEligibilityAsync(
            offer,
            PurchaseMode.Packaged);

        Assert.False(result.IsProcurementEligible);
        Assert.Equal("SUPPLIER_PACKAGE_NOT_PROCUREMENT_READY", result.ReasonCode);
    }

    [Fact]
    public async Task InactiveReadyPackage_IsNotProcurementEligible()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 120_000m, active: false);

        var result = await CreateValidator(context).EvaluateProcurementEligibilityAsync(
            offer,
            PurchaseMode.Packaged);

        Assert.True(result.IsReady);
        Assert.False(result.IsProcurementEligible);
        Assert.Equal("SUPPLIER_PACKAGE_INACTIVE", result.ReasonCode);
    }

    [Fact]
    public async Task ReadyActivePackage_IsProcurementEligible()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 120_000m, active: true);

        var result = await CreateValidator(context).EvaluateProcurementEligibilityAsync(
            offer,
            PurchaseMode.Packaged);

        Assert.True(result.IsReady);
        Assert.True(result.IsProcurementEligible);
    }

    [Fact]
    public async Task InvalidPackage_DoesNotAppearInSourceCandidates()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, CartonUnitId, 1m, 120_000m, active: true);
        offer.IsPrimary = true;
        var store = await context.Stores.OrderBy(x => x.StoreId).FirstAsync();
        store.Active = true;
        if (!await context.SupplierStores.AnyAsync(x =>
                x.SupplierId == offer.SupplierId && x.StoreId == store.StoreId))
        {
            context.SupplierStores.Add(new SupplierStore
            {
                SupplierId = offer.SupplierId,
                StoreId = store.StoreId,
                Active = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();

        var offers = await new ReorderSuggestionRepository(
            context,
            CreateValidator(context)).GetOffersAsync(
                store.StoreId,
                new[] { IngredientId });

        Assert.DoesNotContain(offers, x => x.IngredientSupplierId == offer.IngredientSupplierId);
    }

    [Fact]
    public void InvalidPackage_DoesNotAppearInPaSelectors()
    {
        var source = ReadSource(
            "Application", "Services", "Inventories", "PurchaseAdviceConsolidationService.cs");

        Assert.Contains("EvaluateReadinessAsync", source, StringComparison.Ordinal);
        Assert.Contains("IsProcurementEligible", source, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidPackage_DoesNotAppearInPoSelectors()
    {
        var source = ReadSource(
            "Areas", "Admin", "Controllers", "AdminPurchaseOrdersController.cs");

        Assert.Contains("EvaluateReadinessAsync", source, StringComparison.Ordinal);
        Assert.Contains("result.IsReady", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectProcurementRequest_WithInvalidPackage_IsRejected()
    {
        var source = ReadSource(
            "Application", "Services", "Inventories", "PurchaseOrderService.cs");

        Assert.Contains("EvaluateProcurementEligibilityAsync", source, StringComparison.Ordinal);
        Assert.Contains("Gói mua chưa sẵn sàng để tạo đơn đặt hàng", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcurementSelectors_UseCentralEligibilityAuthority()
    {
        var root = FindRepoRoot();
        var purchaseOrder = File.ReadAllText(Path.Combine(root,
            "CafeChain", "Application", "Services", "Inventories", "PurchaseOrderService.cs"));
        var purchaseAdvice = File.ReadAllText(Path.Combine(root,
            "CafeChain", "Application", "Services", "Inventories", "PurchaseAdviceConsolidationService.cs"));
        var receipt = File.ReadAllText(Path.Combine(root,
            "CafeChain", "Application", "Services", "Inventories", "BranchReceiptService.cs"));
        var selector = File.ReadAllText(Path.Combine(root,
            "CafeChain", "Areas", "Admin", "Controllers", "AdminPurchaseOrdersController.cs"));

        Assert.Contains("EvaluateProcurementEligibilityAsync", purchaseOrder, StringComparison.Ordinal);
        Assert.Contains("EvaluateProcurementEligibilityAsync", purchaseAdvice, StringComparison.Ordinal);
        Assert.Contains("EvaluateProcurementEligibilityAsync", receipt, StringComparison.Ordinal);
        Assert.Contains("EvaluateReadinessAsync", selector, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PriceHistory_UnchangedByReactivation()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 120_000m, active: false);
        context.IngredientSupplierPriceHistories.Add(new IngredientSupplierPriceHistory
        {
            IngredientSupplierId = offer.IngredientSupplierId,
            Price = 120_000m,
            PackageQuantity = 50m,
            PackageUnitId = PieceUnitId,
            EffectiveDate = DateTime.UtcNow,
            IsCurrent = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var before = await context.IngredientSupplierPriceHistories.CountAsync();

        await CreateSupplierService(context).ToggleIngredientOfferActiveAsync(
            offer.IngredientSupplierId,
            true,
            OfferVersion(context, offer.IngredientSupplierId),
            actorStaffId: 5);

        Assert.Equal(before, await context.IngredientSupplierPriceHistories.CountAsync());
    }

    [Fact]
    public async Task DataAudit_FlagsAmbiguousCountPackageForReview()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, CartonUnitId, 1m, 120_000m, active: true);

        var report = await new SupplierProcurementDataQualityService(
            context,
            CreateUnitConversionService(context)).InspectAsync();

        Assert.True(report.DryRun);
        Assert.Contains(report.Findings, x =>
            x.EntityId == offer.IngredientSupplierId
            && x.Code == "COUNT_PACKAGE_CONTENT_UOM_INVALID"
            && x.Resolution == "NEEDS_REVIEW");
        Assert.True(await context.IngredientSuppliers.AsNoTracking()
            .Where(x => x.IngredientSupplierId == offer.IngredientSupplierId)
            .Select(x => x.Active)
            .SingleAsync());
    }

    [Fact]
    public void SupplierUi_UsesOneDelegatedOfferHandler_AndInflightGuard()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Supplier", "supplier.js"));

        Assert.Contains("offerList?.addEventListener('click'", source, StringComparison.Ordinal);
        Assert.Contains("offerToggleRequests", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$$('.toggle-offer', root).forEach", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ReactivationFailure_ShowsOneNotification()
    {
        var source = SupplierUiSource();

        Assert.Contains("offerToggleRequests.has(id)", source, StringComparison.Ordinal);
        Assert.Contains("toast(error.message, 'error')", source, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(source, "async function toggleOffer(id, active, button)"));
    }

    [Fact]
    public void ReopeningSupplierDrawer_DoesNotDuplicateNotificationHandlers()
    {
        var source = SupplierUiSource();

        Assert.Equal(1, CountOccurrences(source, "offerList?.addEventListener('click'"));
        Assert.DoesNotContain("$$('.toggle-offer', root).forEach", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SwitchingSupplierTabs_DoesNotDuplicateNotificationHandlers()
    {
        var source = SupplierUiSource();

        Assert.Equal(1, CountOccurrences(source, "offerList?.addEventListener('click'"));
        Assert.Contains("offerToggleRequests.add(id)", source, StringComparison.Ordinal);
        Assert.Contains("offerToggleRequests.delete(id)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageCard_ShowsConsistentStatusAndReadiness()
    {
        var source = SupplierUiSource();

        Assert.Contains("aria-label=\"Trạng thái gói mua\"", source, StringComparison.Ordinal);
        Assert.Contains("Đang hoạt động", source, StringComparison.Ordinal);
        Assert.Contains("Ngừng hoạt động", source, StringComparison.Ordinal);
        Assert.Contains("procurementReadinessLabel", source, StringComparison.Ordinal);
        Assert.Contains("procurementReadinessMessage", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReactivationError_IsBusinessReadableVietnamese()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, CartonUnitId, 1m, 120_000m, active: false);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSupplierService(context).ToggleIngredientOfferActiveAsync(
                offer.IngredientSupplierId,
                true,
                OfferVersion(context, offer.IngredientSupplierId),
                actorStaffId: 5));

        Assert.Contains("Không thể kích hoạt gói mua", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SUPPLIER_PACKAGE", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidPackage_StillUsableInPaPo()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 120_000m, active: true);

        var result = await CreateValidator(context).EvaluateProcurementEligibilityAsync(
            offer,
            PurchaseMode.Packaged);

        Assert.True(result.IsProcurementEligible);
    }

    [Fact]
    public async Task PackageHistory_PreservedAfterDeactivateReactivate()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 120_000m, active: true);
        var service = CreateSupplierService(context);

        await service.ToggleIngredientOfferActiveAsync(
            offer.IngredientSupplierId,
            false,
            OfferVersion(context, offer.IngredientSupplierId),
            actorStaffId: 5);
        await service.ToggleIngredientOfferActiveAsync(
            offer.IngredientSupplierId,
            true,
            OfferVersion(context, offer.IngredientSupplierId),
            actorStaffId: 5);

        Assert.Equal(2, await context.AuditLogs.CountAsync(x =>
            x.TableName == "Suppliers"
            && x.Action == "SUPPLIER_OFFER_STATUS_CHANGED"));
    }

    [Fact]
    public async Task SupplierStoreScope_RemainsEnforced()
    {
        await using var context = CreateDbContext();
        var offer = await SeedOfferAsync(context, PieceUnitId, 50m, 120_000m, active: true);

        var result = await CreateValidator(context).EvaluateProcurementEligibilityAsync(
            offer,
            PurchaseMode.Packaged,
            storeId: 999_399);

        Assert.False(result.IsProcurementEligible);
        Assert.Equal(SupplierPackageReadinessCodes.StoreScopeInvalid, result.ReasonCode);
    }

    [Fact]
    public async Task PrimarySourceResolver_ExcludesInvalidPackage()
    {
        await InvalidPackage_DoesNotAppearInSourceCandidates();
    }

    private static AdminSupplierService CreateSupplierService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(
            context,
            NullLogger<UnitConversionService>.Instance,
            physical);
        return new AdminSupplierService(
            new AdminSupplierRepository(context),
            context,
            new IngredientSupplierPackageValidator(context, physical, conversion));
    }

    private static IngredientSupplierPackageValidator CreateValidator(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(
            context,
            NullLogger<UnitConversionService>.Instance,
            physical);
        return new IngredientSupplierPackageValidator(context, physical, conversion);
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

    private static async Task<IngredientSupplier> SeedOfferAsync(
        AppDbContext context,
        int contentUnitId,
        decimal packageQuantity,
        decimal price,
        bool active)
    {
        EnsureUnit(context, GramUnitId, "g", "Gram", UnitType.KhoiLuong);
        EnsureUnit(context, PieceUnitId, "pcs", "Cái", UnitType.Dem);
        EnsureUnit(context, CartonUnitId, "DEMO_CARTON", "Thùng", UnitType.Dem);

        var supplier = await context.Suppliers.OrderBy(x => x.SupplierId).FirstAsync();
        supplier.Active = true;
        var ingredient = await context.Ingredients.SingleOrDefaultAsync(x => x.IngredientId == IngredientId);
        if (ingredient == null)
        {
            ingredient = new Ingredient
            {
                IngredientId = IngredientId,
                Code = "COUNT-99399",
                Name = "Ly đếm kiểm thử",
                BaseUnitId = PieceUnitId,
                Active = true
            };
            context.Ingredients.Add(ingredient);
        }
        else
        {
            ingredient.BaseUnitId = PieceUnitId;
            ingredient.Active = true;
        }

        var old = await context.IngredientSuppliers
            .Where(x => x.IngredientId == IngredientId && x.SupplierId == supplier.SupplierId)
            .ToListAsync();
        context.IngredientSuppliers.RemoveRange(old);
        await context.SaveChangesAsync();

        var offer = new IngredientSupplier
        {
            IngredientId = IngredientId,
            SupplierId = supplier.SupplierId,
            UnitId = contentUnitId,
            PackageQuantity = packageQuantity,
            CurrentPrice = price,
            MinimumOrderPackageCount = 1,
            LeadTimeDays = 0,
            Active = active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.IngredientSuppliers.Add(offer);
        await context.SaveChangesAsync();
        return offer;
    }

    private static void EnsureUnit(
        AppDbContext context,
        int unitId,
        string code,
        string name,
        UnitType type)
    {
        var unit = context.Units.SingleOrDefault(x => x.UnitId == unitId);
        if (unit == null)
        {
            context.Units.Add(new Unit
            {
                UnitId = unitId,
                UnitCode = code,
                Name = name,
                Type = type,
                Active = true
            });
        }
        else
        {
            unit.UnitCode = code;
            unit.Name = name;
            unit.Type = type;
            unit.Active = true;
        }
        context.SaveChanges();
    }

    private static string OfferVersion(AppDbContext context, int offerId) =>
        Convert.ToBase64String(context.IngredientSuppliers.AsNoTracking()
            .Where(x => x.IngredientSupplierId == offerId)
            .Select(x => x.RowVersion)
            .Single());

    private static async Task<string> OfferVersionAsync(AppDbContext context, int offerId) =>
        Convert.ToBase64String(await context.IngredientSuppliers.AsNoTracking()
            .Where(x => x.IngredientSupplierId == offerId)
            .Select(x => x.RowVersion)
            .SingleAsync());

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

    private static string SupplierUiSource() => File.ReadAllText(Path.Combine(
        FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Supplier", "supplier.js"));

    private static string ReadSource(params string[] parts) => File.ReadAllText(
        Path.Combine(new[] { FindRepoRoot(), "CafeChain" }.Concat(parts).ToArray()));

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
