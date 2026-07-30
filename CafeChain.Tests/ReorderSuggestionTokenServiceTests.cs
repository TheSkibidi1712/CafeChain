using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Services.Inventories;
using Microsoft.AspNetCore.DataProtection;

namespace CafeChain.Tests;

public sealed class ReorderSuggestionTokenServiceTests
{
    [Fact]
    public void Decision_mapper_includes_supplier_package_moq_and_cost()
    {
        var item = new ReorderSuggestionItemDto
        {
            StoreId = 1,
            IngredientId = 2,
            SupplierId = 3,
            SupplierName = "Nhà cung cấp A",
            IngredientSupplierId = 4,
            PackageUnitId = 5,
            PackageBaseQuantity = 6,
            MinimumOrderPackageCount = 7,
            PackagePrice = 8,
            EstimatedCost = 9
        };

        var decision = ReorderSuggestionContractMapper.ToDecision(item);

        Assert.Equal(item.SupplierId, decision.SupplierId);
        Assert.Equal(item.SupplierName, decision.SupplierName);
        Assert.Equal(item.PackageUnitId, decision.PackageUnitId);
        Assert.Equal(item.PackageBaseQuantity, decision.PackageBaseQuantity);
        Assert.Equal(
            item.MinimumOrderPackageCount,
            decision.MinimumOrderPackageCount);
        Assert.Equal(item.PackagePrice, decision.SupplierPrice);
        Assert.Equal(item.EstimatedCost, decision.EstimatedCost);
    }

    [Fact]
    public void Token_is_bound_to_staff_store_and_ingredient_and_expires()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 7, 29, 2, 0, 0, TimeSpan.Zero));
        var service = new ReorderSuggestionTokenService(
            new EphemeralDataProtectionProvider(),
            clock);
        var token = service.Issue(10, 2, 7, 30, "reorder-v2", "ABC123");

        Assert.True(service.Read(token, 10, 2, 7).IsValid);
        Assert.False(service.Read(token, 11, 2, 7).IsValid);
        Assert.False(service.Read(token, 10, 3, 7).IsValid);
        Assert.False(service.Read(token + "tampered", 10, 2, 7).IsValid);

        clock.Advance(TimeSpan.FromMinutes(31));
        var expired = service.Read(token, 10, 2, 7);
        Assert.False(expired.IsValid);
        Assert.True(expired.IsExpired);
        Assert.Equal(
            ReorderSuggestionConfirmationErrorCodes.SuggestionExpired,
            expired.ErrorCode);
    }

    [Fact]
    public void Fingerprint_is_reason_order_independent_but_changes_with_business_state()
    {
        var service = new ReorderSuggestionTokenService(
            new EphemeralDataProtectionProvider(),
            new MutableTimeProvider(DateTimeOffset.UtcNow));
        var decision = new ReorderSuggestionDecisionFingerprintDto
        {
            StoreId = 1,
            IngredientId = 2,
            CalculationVersion = "reorder-v2",
            AvailableStock = 10,
            IncomingQuantity = 3,
            RawDemand = 7,
            ProcurementCoveredQuantity = 2,
            RemainingDemand = 5,
            SuggestedPackageQuantity = 2,
            FinalSuggestedQuantity = 6,
            SupplierId = 3,
            SupplierName = "Nhà cung cấp A",
            PackageUnitId = 4,
            PackageBaseQuantity = 3,
            MinimumOrderPackageCount = 2,
            SupplierPrice = 100,
            EstimatedCost = 200,
            SuggestionStatus = "NEAR_REORDER",
            ReasonCodes = ["B", "A"]
        };

        var first = service.ComputeDecisionFingerprint(decision);
        decision.ReasonCodes = ["A", "B"];
        var reordered = service.ComputeDecisionFingerprint(decision);
        decision.PackageBaseQuantity = 4;
        var changedPackage = service.ComputeDecisionFingerprint(decision);
        decision.PackageBaseQuantity = 3;
        decision.MinimumOrderPackageCount = 3;
        var changedMoq = service.ComputeDecisionFingerprint(decision);
        decision.MinimumOrderPackageCount = 2;
        decision.SupplierId = 9;
        var changedSupplier = service.ComputeDecisionFingerprint(decision);
        decision.SupplierId = 3;
        decision.SupplierPrice = 101;
        var changedPrice = service.ComputeDecisionFingerprint(decision);
        decision.SupplierPrice = 100;
        decision.PackageUnitId = 8;
        var changedPackageUnit = service.ComputeDecisionFingerprint(decision);

        Assert.Equal(first, reordered);
        Assert.NotEqual(first, changedPackage);
        Assert.NotEqual(first, changedMoq);
        Assert.NotEqual(first, changedSupplier);
        Assert.NotEqual(first, changedPrice);
        Assert.NotEqual(first, changedPackageUnit);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan value) => _utcNow += value;
    }
}
