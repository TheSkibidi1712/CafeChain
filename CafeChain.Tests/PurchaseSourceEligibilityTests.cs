using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CafeChain.Tests;

public sealed class PurchaseSourceEligibilityTests : IntegrationTestBase
{
    private const int StoreId = 38901;
    private const int IngredientId = 38901;
    private const int PreparedItemId = 38901;

    [Fact]
    public async Task RawIngredientPurchasePath_RemainsEligible()
    {
        using var context = CreateDbContext();
        await SeedItemsAsync(context);
        var service = new PurchaseSourceEligibilityService(context);

        var result = await service.EvaluateAsync(new PurchaseSourceEligibilityRequest
        {
            StoreId = StoreId,
            IngredientId = IngredientId
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.Eligible);
        Assert.Equal(PurchaseEligibilityReasonCodes.Eligible, result.Data.ReasonCode);
    }

    [Fact]
    public async Task PreparedItemWithoutCanPurchase_IsRejected()
    {
        using var context = CreateDbContext();
        await SeedItemsAsync(context);
        var service = new PurchaseSourceEligibilityService(context);

        var result = await service.EvaluateAsync(PreparedItemRequest());

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.Eligible);
        Assert.Equal(PurchaseEligibilityReasonCodes.CapabilityMissing, result.Data.ReasonCode);
    }

    [Fact]
    public async Task PreparedItemWithCanPurchaseButNoSupplierPackage_IsRejected()
    {
        using var context = CreateDbContext();
        await SeedItemsAsync(context);
        context.InventoryItemSourceCapabilities.Add(new InventoryItemSourceCapability
        {
            PreparedItemId = PreparedItemId,
            CanPurchase = true,
            Active = true,
            EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
            CreatedByStaffId = 1,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var service = new PurchaseSourceEligibilityService(context);

        var result = await service.EvaluateAsync(PreparedItemRequest());

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.Eligible);
        Assert.Equal(PurchaseEligibilityReasonCodes.PackageMissing, result.Data.ReasonCode);
        Assert.DoesNotContain(result.Data.ReasonCode, result.Data.Message, StringComparison.Ordinal);
    }

    private static PurchaseSourceEligibilityRequest PreparedItemRequest() => new()
    {
        StoreId = StoreId,
        PreparedItemId = PreparedItemId
    };

    private static async Task SeedItemsAsync(CafeChain.Data.AppDbContext context)
    {
        var gram = await context.Units.SingleAsync(x => x.UnitCode == "g");
        context.Stores.Add(new Store
        {
            StoreId = StoreId,
            Name = "Cửa hàng purchase eligibility",
            Address = "Test",
            Phone = "000",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Ingredients.Add(new Ingredient
        {
            IngredientId = IngredientId,
            Code = "ING-38901",
            Name = "Nguyên liệu giữ luồng mua hiện hữu",
            BaseUnitId = gram.UnitId,
            Active = true
        });
        context.PreparedItems.Add(new PreparedItem
        {
            PreparedItemId = PreparedItemId,
            Code = "BTP-38901",
            Name = "Bán thành phẩm kiểm tra mua ngoài",
            BaseUnitId = gram.UnitId,
            Active = true
        });
        await context.SaveChangesAsync();
    }
}
