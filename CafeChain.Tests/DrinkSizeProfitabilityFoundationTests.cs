using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Services.Admin.Profitability;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeChain.Tests;

public sealed class DrinkSizeProfitabilityFoundationTests : IntegrationTestBase
{
    private const int StoreId = 9101;
    private const int DrinkId = 9102;
    private const int SizeId = 9103;
    private const int DrinkSizeId = 9104;
    private const int UnitId = 9105;
    private const int IngredientId = 9106;
    private const int RecipeId = 9107;
    private const int ToppingId = 9108;
    private const int ToppingRecipeId = 9109;
    private const int OwnerStaffId = 9110;
    private const int CashierStaffId = 9111;

    [Fact]
    public async Task DrinkSizeRecipe_ExactResolver_IsDeterministic()
    {
        await SeedBaseAsync();
        await using var context = CreateDbContext();
        var resolver = CreateResolver(context);

        var result = await resolver.ResolveExactAsync(DrinkId, SizeId, DateTime.UtcNow);

        Assert.True(result.IsReady);
        Assert.Equal(RecipeId, result.Recipe!.RecipeId);
        Assert.Equal(DrinkSizeRecipeHealthStatuses.ExactReady, result.Status);
    }

    [Fact]
    public async Task DrinkSizeRecipe_GenericFallback_IsFlaggedNotUsedForProfitability()
    {
        await SeedBaseAsync(includeExactRecipe: false);
        await using var context = CreateDbContext();
        context.Recipes.Add(CreateRecipe(RecipeId + 20, sizeId: null));
        await context.SaveChangesAsync();

        var result = await CreateResolver(context).ResolveExactAsync(DrinkId, SizeId, DateTime.UtcNow);

        Assert.Equal(DrinkSizeRecipeHealthStatuses.GenericFallbackOnly, result.Status);
        Assert.Null(result.Recipe);
        Assert.True(result.HasGenericFallback);
    }

    [Fact]
    public async Task DrinkSizeRecipe_MultipleActive_IsRejected()
    {
        await SeedBaseAsync();
        await using var context = CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS UX_Recipes_OneActive_Drink_Size");
        var duplicate = CreateRecipe(RecipeId + 1, SizeId);
        duplicate.RecipeCode = "PF_DUPLICATE";
        duplicate.EffectiveDate = DateTime.UtcNow.AddHours(-1);
        context.Recipes.Add(duplicate);
        context.RecipeDetails.Add(new RecipeDetail
        {
            RecipeDetailId = 9199, RecipeId = duplicate.RecipeId, IngredientId = IngredientId, Quantity = 1, UnitId = UnitId
        });
        await context.SaveChangesAsync();

        var result = await CreateResolver(context).ResolveExactAsync(DrinkId, SizeId, DateTime.UtcNow);

        Assert.Equal(DrinkSizeRecipeHealthStatuses.MultipleActiveRecipe, result.Status);
        Assert.Null(result.Recipe);
    }

    [Fact]
    public async Task DefaultTopping_IncludedInRecipe_NotDoubleCounted()
    {
        await SeedBaseAsync(policy: (ToppingPriceTreatments.IncludedInBasePrice, ToppingCostTreatments.IncludedInDrinkRecipe));
        var row = await PreviewRowAsync();

        Assert.Equal(10m, row.EstimatedCost);
        Assert.Equal(0m, row.DefaultToppingPriceImpact);
        Assert.Equal(0m, row.DefaultToppings.Single().CostImpact);
    }

    [Fact]
    public async Task DefaultTopping_FreeSeparateCost_AddsCostOnly()
    {
        await SeedBaseAsync(policy: (ToppingPriceTreatments.IncludedInBasePrice, ToppingCostTreatments.AddToppingRecipeCost));
        var row = await PreviewRowAsync();

        Assert.Equal(15m, row.EstimatedCost);
        Assert.Equal(0m, row.DefaultToppingPriceImpact);
        Assert.Equal(5m, row.DefaultToppings.Single().CostImpact);
    }

    [Fact]
    public async Task DefaultTopping_PaidAddOn_AddsPriceAndCostOnce()
    {
        await SeedBaseAsync(policy: (ToppingPriceTreatments.AddToppingPrice, ToppingCostTreatments.AddToppingRecipeCost));
        var row = await PreviewRowAsync();

        Assert.Equal(15m, row.EstimatedCost);
        Assert.Equal(5_000m, row.DefaultToppingPriceImpact);
        Assert.Equal(35_000m, row.EffectiveSellingPrice);
    }

    [Fact]
    public async Task DefaultTopping_PaidButIncludedInBom_AddsPriceWithoutDoubleCountingCost()
    {
        await SeedBaseAsync(policy: (ToppingPriceTreatments.AddToppingPrice, ToppingCostTreatments.IncludedInDrinkRecipe));
        var row = await PreviewRowAsync();

        Assert.Equal(10m, row.EstimatedCost);
        Assert.Equal(5_000m, row.DefaultToppingPriceImpact);
        Assert.Equal(35_000m, row.EffectiveSellingPrice);
        Assert.Equal(0m, row.DefaultToppings.Single().CostImpact);
    }

    [Fact]
    public async Task ToppingQuantity_MultipliesSalesAndCostImpact()
    {
        await SeedBaseAsync(
            policy: (ToppingPriceTreatments.AddToppingPrice, ToppingCostTreatments.AddToppingRecipeCost),
            policyQuantity: 2m);
        var row = await PreviewRowAsync();

        Assert.Equal(20m, row.EstimatedCost);
        Assert.Equal(10_000m, row.DefaultToppingPriceImpact);
        Assert.Equal(40_000m, row.EffectiveSellingPrice);
        Assert.Equal(10m, row.DefaultToppings.Single().CostImpact);
    }

    [Fact]
    public async Task DefaultTopping_MissingPolicy_ReturnsIncomplete()
    {
        await SeedBaseAsync(addLegacyDefault: true);
        var row = await PreviewRowAsync();

        Assert.Equal(ProfitabilityCostStatuses.MissingDefaultToppingPolicy, row.CostStatus);
        Assert.Null(row.EstimatedCost);
        Assert.Equal(10m, row.KnownCost);
    }

    [Fact]
    public async Task CostPreview_UsesIngredientLayers()
    {
        await SeedBaseAsync();
        var row = await PreviewRowAsync();

        Assert.Equal(10m, row.EstimatedCost);
        Assert.Equal(10m, row.Components.Single().KnownCost);
        Assert.Equal(ProfitabilityCostStatuses.Complete, row.Components.Single().Status);
    }

    [Fact]
    public async Task CostPreview_UsesPreparedItemLayers()
    {
        await SeedBaseAsync(usePreparedItem: true);
        var row = await PreviewRowAsync();

        Assert.Equal(24m, row.EstimatedCost);
        Assert.Equal("PreparedItem", row.Components.Single().ItemType);
        Assert.Equal(24m, row.Components.Single().KnownCost);
    }

    [Fact]
    public async Task CostCompleteness_SplitsBaseBomPreparedItemToppingAndConversion()
    {
        await SeedBaseAsync(usePreparedItem: true);
        var row = await PreviewRowAsync();

        Assert.Equal(4, row.CostSections.Count);
        Assert.Equal(ProfitabilityCostStatuses.Complete,
            row.CostSections.Single(x => x.Section == ProfitabilityCostSections.PreparedItem).Status);
        Assert.Equal(ProfitabilityCostStatuses.Complete,
            row.CostSections.Single(x => x.Section == ProfitabilityCostSections.UnitConversion).Status);
        Assert.Equal(ProfitabilityCostStatuses.Complete,
            row.CostSections.Single(x => x.Section == ProfitabilityCostSections.DefaultTopping).Status);
        Assert.Equal(ProfitabilityCostStatuses.Complete,
            row.CostSections.Single(x => x.Section == ProfitabilityCostSections.BaseBom).Status);
        Assert.Contains("không có nguyên liệu trực tiếp", row.CostSections
            .Single(x => x.Section == ProfitabilityCostSections.BaseBom).Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AmbiguousPreparedItemOutput_DoesNotReportCompleteCost()
    {
        await SeedBaseAsync(usePreparedItem: true, preparedOutputConfirmed: false);
        var row = await PreviewRowAsync();

        Assert.Null(row.EstimatedCost);
        Assert.NotEqual(ProfitabilityCostStatuses.Complete, row.CostStatus);
        Assert.Contains("Bán thành phẩm", row.CostMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CostPreview_DoesNotMutateLayers()
    {
        await SeedBaseAsync();
        await PreviewRowAsync();
        await PreviewRowAsync();
        await using var context = CreateDbContext();

        var layer = await context.InventoryCostLayers.SingleAsync(x => x.IngredientId == IngredientId && x.StoreId == StoreId);
        Assert.Equal(100m, layer.RemainingQuantity);
    }

    [Fact]
    public async Task CostPreview_IncompleteNeverReturnsZero()
    {
        await SeedBaseAsync(layerQuantity: 0);
        var row = await PreviewRowAsync();

        Assert.Null(row.EstimatedCost);
        Assert.Equal(ProfitabilityCostStatuses.Incomplete, row.CostStatus);
        Assert.NotEqual(ProfitabilityCostStatuses.Complete, row.Components.Single().Status);
    }

    [Fact]
    public void PriceFormula_MarginAndMarkupAreDistinct()
    {
        var service = new PriceSuggestionService();
        var margin = service.Calculate(new PriceSuggestionRequest { EstimatedCost = 60, CurrentSellingPrice = 100, TargetMode = ProfitabilityTargetModes.Margin, TargetValue = 40, RoundingMode = ProfitabilityRoundingModes.None });
        var markup = service.Calculate(new PriceSuggestionRequest { EstimatedCost = 60, CurrentSellingPrice = 100, TargetMode = ProfitabilityTargetModes.Markup, TargetValue = 40, RoundingMode = ProfitabilityRoundingModes.None });

        Assert.Equal(100m, margin.RawSuggestedPrice);
        Assert.Equal(84m, markup.RawSuggestedPrice);
        Assert.NotEqual(margin.RawSuggestedPrice, markup.RawSuggestedPrice);
    }

    [Fact]
    public void Rounding_RecalculatesEffectiveMargin()
    {
        var result = new PriceSuggestionService().Calculate(new PriceSuggestionRequest
        {
            EstimatedCost = 12_345, CurrentSellingPrice = 20_000, TargetMode = ProfitabilityTargetModes.Margin,
            TargetValue = 35, RoundingMode = ProfitabilityRoundingModes.Ceiling1000
        });

        Assert.True(result.IsValid);
        Assert.Equal(19_000m, result.RoundedSuggestedPrice);
        Assert.Equal(decimal.Round((19_000m - 12_345m) / 19_000m * 100m, 2), result.EffectiveMarginPercent);
    }

    [Fact]
    public void PriceFormula_ProfitAmountAndMarginValidationAreCorrect()
    {
        var service = new PriceSuggestionService();
        var profit = service.Calculate(new PriceSuggestionRequest
        {
            EstimatedCost = 60,
            CurrentSellingPrice = 100,
            TargetMode = ProfitabilityTargetModes.ProfitAmount,
            TargetValue = 25,
            RoundingMode = ProfitabilityRoundingModes.None
        });
        var invalidMargin = service.Calculate(new PriceSuggestionRequest
        {
            EstimatedCost = 60,
            CurrentSellingPrice = 100,
            TargetMode = ProfitabilityTargetModes.Margin,
            TargetValue = 100,
            RoundingMode = ProfitabilityRoundingModes.None
        });

        Assert.True(profit.IsValid);
        Assert.Equal(85m, profit.RoundedSuggestedPrice);
        Assert.False(invalidMargin.IsValid);
    }

    [Fact]
    public async Task Profitability_UsesPriceMinusCostAndCorrectDenominators()
    {
        await SeedBaseAsync();
        var row = await PreviewRowAsync();

        Assert.Equal(29_990m, row.GrossProfit);
        Assert.Equal(decimal.Round(29_990m / 30_000m * 100m, 2), row.GrossMarginPercent);
        Assert.Equal(decimal.Round(29_990m / 10m * 100m, 2), row.MarkupPercent);
    }

    [Fact]
    public async Task Cashier_CannotViewCosting()
    {
        await SeedBaseAsync();
        await using var context = CreateDbContext();
        var role = await context.Roles.FirstAsync(x => x.Name == RoleConstants.SalesStaff);
        context.Accounts.Add(new Account
        {
            AccountId = CashierStaffId,
            Email = "profit-cashier@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Staffs.Add(new Staff
        {
            StaffId = CashierStaffId,
            AccountId = CashierStaffId,
            FullName = "Profit Cashier",
            StoreId = StoreId,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            EmployeeStatus = 2
        });
        context.AccountRoles.Add(new AccountRole { AccountId = CashierStaffId, RoleId = role.RoleId });
        await context.SaveChangesAsync();

        var result = await CreateProfitabilityService(context)
            .PreviewAsync(StoreId, DrinkId, DateTime.UtcNow, CashierStaffId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROFITABILITY_FORBIDDEN", result.ErrorCode);
    }

    [Fact]
    public async Task PriceUpdate_RejectsClientCostFields()
    {
        await SeedBaseAsync();
        await using var context = CreateDbContext();
        var request = JsonSerializer.Deserialize<UpdateDrinkSizePriceRequest>("""
            {"drinkSizeId":9104,"newSellingPrice":32000,"expectedRowVersion":"AA==","cost":1,"margin":99}
            """, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var result = await CreatePricingService(context).UpdatePriceAsync(request, StoreId, OwnerStaffId);

        Assert.False(result.IsSuccess);
        Assert.Equal("CLIENT_AUTHORITY_FIELD_REJECTED", result.ErrorCode);
    }

    [Fact]
    public async Task PriceUpdate_WritesAudit_And_InvalidatesCatalog()
    {
        await SeedBaseAsync();
        await using var context = CreateDbContext();
        var rowVersion = Convert.ToBase64String((await context.DrinkSizes.AsNoTracking().SingleAsync(x => x.DrinkSizeId == DrinkSizeId)).RowVersion);
        var request = new UpdateDrinkSizePriceRequest { DrinkSizeId = DrinkSizeId, NewSellingPrice = 32_000, ExpectedRowVersion = rowVersion, Reason = "Điều chỉnh theo mô hình lợi nhuận" };

        var result = await CreatePricingService(context).UpdatePriceAsync(request, StoreId, OwnerStaffId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(32_000m, (await context.DrinkSizes.FindAsync(DrinkSizeId))!.Price);
        var audit = await context.DrinkSizePriceAudits.SingleAsync(x => x.DrinkSizeId == DrinkSizeId);
        Assert.Equal(30_000m, audit.OldPrice);
        Assert.Equal(32_000m, audit.NewPrice);
        Assert.Equal(1, (await context.PosCatalogStates.SingleAsync()).Version);
    }

    [Fact]
    public async Task PriceUpdate_RequiresReasonForCompleteCost()
    {
        await SeedBaseAsync();
        await using var context = CreateDbContext();
        var rowVersion = Convert.ToBase64String((await context.DrinkSizes.AsNoTracking()
            .SingleAsync(x => x.DrinkSizeId == DrinkSizeId)).RowVersion);

        var result = await CreatePricingService(context).UpdatePriceAsync(new UpdateDrinkSizePriceRequest
        {
            DrinkSizeId = DrinkSizeId,
            NewSellingPrice = 32_000,
            ExpectedRowVersion = rowVersion
        }, StoreId, OwnerStaffId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PRICE_CHANGE_REASON_REQUIRED", result.ErrorCode);
        Assert.Empty(await context.DrinkSizePriceAudits.ToListAsync());
    }

    [Fact]
    public async Task PriceUpdate_IncompleteCostRequiresExplicitConfirmation()
    {
        await SeedBaseAsync(layerQuantity: 0);
        await using var context = CreateDbContext();
        var rowVersion = Convert.ToBase64String((await context.DrinkSizes.AsNoTracking()
            .SingleAsync(x => x.DrinkSizeId == DrinkSizeId)).RowVersion);

        var result = await CreatePricingService(context).UpdatePriceAsync(new UpdateDrinkSizePriceRequest
        {
            DrinkSizeId = DrinkSizeId,
            NewSellingPrice = 32_000,
            ExpectedRowVersion = rowVersion,
            Reason = "Điều chỉnh có xác nhận thiếu giá vốn"
        }, StoreId, OwnerStaffId);

        Assert.False(result.IsSuccess);
        Assert.Equal("INCOMPLETE_COST_CONFIRMATION_REQUIRED", result.ErrorCode);
        Assert.Empty(await context.DrinkSizePriceAudits.ToListAsync());
    }

    [Fact]
    public async Task PriceUpdate_ConcurrentEdit_RejectsStaleRowVersion()
    {
        await SeedBaseAsync();
        await using var context = CreateDbContext();
        var request = new UpdateDrinkSizePriceRequest
        {
            DrinkSizeId = DrinkSizeId,
            NewSellingPrice = 31_000,
            ExpectedRowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9 }),
            Reason = "Stale test"
        };

        var result = await CreatePricingService(context).UpdatePriceAsync(request, StoreId, OwnerStaffId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PRICE_CHANGED_BY_ANOTHER_USER", result.ErrorCode);
        Assert.Empty(await context.DrinkSizePriceAudits.ToListAsync());
    }

    [Fact]
    public async Task DefaultToppingPolicy_Update_WritesOldAndNewAudit()
    {
        await SeedBaseAsync(addLegacyDefault: true);
        await using var context = CreateDbContext();
        var service = new DrinkSizeToppingPolicyService(context);

        var created = await service.UpsertAsync(new UpsertDrinkSizeToppingPolicyRequest
        {
            DrinkSizeId = DrinkSizeId, ToppingId = ToppingId, IsDefaultSelected = true,
            PriceTreatment = ToppingPriceTreatments.IncludedInBasePrice,
            CostTreatment = ToppingCostTreatments.AddToppingRecipeCost,
            QuantityPerDrink = 1, IsActive = true, Reason = "Khởi tạo policy"
        }, OwnerStaffId);
        Assert.True(created.IsSuccess, created.Message);

        var updated = await service.UpsertAsync(new UpsertDrinkSizeToppingPolicyRequest
        {
            PolicyId = created.Data.PolicyId, DrinkSizeId = DrinkSizeId, ToppingId = ToppingId,
            IsDefaultSelected = true, PriceTreatment = ToppingPriceTreatments.AddToppingPrice,
            CostTreatment = ToppingCostTreatments.AddToppingRecipeCost, QuantityPerDrink = 1,
            IsActive = true, ExpectedRowVersion = created.Data.RowVersion, Reason = "Chuyển sang topping tính phí"
        }, OwnerStaffId);

        Assert.True(updated.IsSuccess, updated.Message);
        var audits = await context.DrinkSizeToppingPolicyAudits.OrderBy(x => x.DrinkSizeToppingPolicyAuditId).ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Null(audits[0].OldDataJson);
        Assert.Contains(ToppingPriceTreatments.IncludedInBasePrice, audits[1].OldDataJson);
        Assert.Contains(ToppingPriceTreatments.AddToppingPrice, audits[1].NewDataJson);
    }

    [Fact]
    public async Task DefaultToppingPolicy_RequiresReason()
    {
        await SeedBaseAsync(addLegacyDefault: true);
        await using var context = CreateDbContext();

        var result = await new DrinkSizeToppingPolicyService(context).UpsertAsync(
            new UpsertDrinkSizeToppingPolicyRequest
            {
                DrinkSizeId = DrinkSizeId,
                ToppingId = ToppingId,
                IsDefaultSelected = true,
                PriceTreatment = ToppingPriceTreatments.IncludedInBasePrice,
                CostTreatment = ToppingCostTreatments.AddToppingRecipeCost,
                QuantityPerDrink = 1,
                IsActive = true
            }, OwnerStaffId);

        Assert.False(result.IsSuccess);
        Assert.Contains("lý do", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await context.DrinkSizeToppingPolicyAudits.ToListAsync());
    }

    [Fact]
    public void ProfitabilityUi_UsesVietnameseBusinessTermsAndLabelsRoundingField()
    {
        var root = FindRepoRoot();
        var view = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminDrinkProfitability", "Index.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "js", "Admin", "Profitability", "drink-profitability.js"));
        var renderedScope = view + script;

        Assert.Contains("Quy tắc làm tròn giá", view);
        Assert.Contains("Biên lợi nhuận gộp (Margin)", view);
        Assert.Contains("Tỷ lệ cộng giá (Markup)", view);
        Assert.DoesNotContain("Contract theo DrinkSize", renderedScope, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("policy active", renderedScope, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">DRINK_RECIPE<", renderedScope, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("compatibility", renderedScope, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MenuAndPreparedItemUi_DoNotRenderTechnicalCostingTerms()
    {
        var root = FindRepoRoot();
        var storeMenu = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "js", "Admin", "StoreMenu", "store-menu.js"));
        var inventory = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminStoreInventory", "Partials", "_InventoryTablePartial.cshtml"));
        var controller = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Controllers", "AdminDrinkProfitabilityController.cs"));

        Assert.DoesNotContain("Publish SKU", storeMenu, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Dùng giá global", storeMenu, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Dữ liệu StoreMenuItem", storeMenu, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Dòng compatibility", inventory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Layer #", inventory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cost layer", inventory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NEEDS_REVIEW", controller);
        Assert.Contains("Cấu hình topping cũ cần được xác nhận lại", controller);
    }

    [Fact]
    public async Task ActualSalesCogs_RemainsUnchanged()
    {
        await SeedBaseAsync();
        await using var context = CreateDbContext();
        var before = await context.SalesCostAllocations.CountAsync();

        _ = await CreateProfitabilityService(context).PreviewAsync(StoreId, DrinkId, DateTime.UtcNow, OwnerStaffId);

        Assert.Equal(before, await context.SalesCostAllocations.CountAsync());
    }

    [Fact]
    public void LockedPayment_IsNotRepriced()
    {
        var root = FindRepoRoot();
        var layout = File.ReadAllText(Path.Combine(root, "CafeChain.Frontend", "src", "POSLayout.tsx"));
        var sync = File.ReadAllText(Path.Combine(root, "CafeChain.Frontend", "src", "services", "OfflineSyncService.ts"));

        Assert.Contains("cartSnapshot: snapshotCart(cart)", layout);
        Assert.Contains("pendingPayment.cartSnapshot", layout);
        Assert.DoesNotContain("setCart", sync);
    }

    [Fact]
    public void OfflineOrder_PreservesPriceSnapshot()
    {
        var root = FindRepoRoot();
        var sync = File.ReadAllText(Path.Combine(root, "CafeChain.Frontend", "src", "services", "OfflineSyncService.ts"));

        Assert.Contains("cartSnapshot: order.cartSnapshot", sync);
        Assert.Contains("paymentSnapshot: order.paymentSnapshot", sync);
        Assert.Contains("unitPrice: i.unitPrice", sync);
        Assert.Contains("totalPrice: i.unitPrice * i.quantity", sync);
    }

    private async Task<DrinkSizeProfitabilityRowDto> PreviewRowAsync()
    {
        await using var context = CreateDbContext();
        var result = await CreateProfitabilityService(context).PreviewAsync(StoreId, DrinkId, DateTime.UtcNow, OwnerStaffId);
        Assert.True(result.IsSuccess, result.Message);
        return result.Data.Sizes.Single(x => x.DrinkSizeId == DrinkSizeId);
    }

    private async Task SeedBaseAsync(
        bool includeExactRecipe = true,
        (string Price, string Cost)? policy = null,
        bool addLegacyDefault = false,
        bool usePreparedItem = false,
        decimal layerQuantity = 100,
        bool preparedOutputConfirmed = true,
        decimal policyQuantity = 1m)
    {
        await using var context = CreateDbContext();
        context.Stores.Add(new Store { StoreId = StoreId, Name = "PF Store", Address = "Test", Phone = "0900000000", Active = true, CreatedAt = DateTime.UtcNow });
        context.Units.Add(new Unit { UnitId = UnitId, UnitCode = "pf-unit", Name = "PF unit", Active = true });
        context.Sizes.Add(new Size { SizeId = SizeId, SizeCode = "PF-M", Name = "PF Size M", Description = "Profitability", Active = true });
        context.Drinks.Add(new Drink { DrinkId = DrinkId, DrinkCode = "PF-DRINK", Name = "Profit Drink", Description = "Test", ProductTypeId = 1, Active = true, CreatedAt = DateTime.UtcNow });
        context.DrinkSizes.Add(new DrinkSize { DrinkSizeId = DrinkSizeId, DrinkId = DrinkId, SizeId = SizeId, Price = 30_000, Active = true, UpdatedAtUtc = DateTime.UtcNow });
        context.StoreMenuItems.Add(new StoreMenuItem
        {
            StoreMenuItemId = 9140,
            StoreId = StoreId,
            DrinkSizeId = DrinkSizeId,
            IsEnabled = true,
            PublishedAtUtc = DateTime.UtcNow.AddDays(-1),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        context.Ingredients.Add(new Ingredient { IngredientId = IngredientId, Code = "PF-ING", Name = "Ingredient", BaseUnitId = UnitId, Active = true });

        if (includeExactRecipe)
        {
            var recipe = CreateRecipe(RecipeId, SizeId);
            context.Recipes.Add(recipe);
            if (!usePreparedItem)
            {
                context.RecipeDetails.Add(new RecipeDetail { RecipeDetailId = RecipeId, RecipeId = RecipeId, IngredientId = IngredientId, Quantity = 1, UnitId = UnitId });
                context.InventoryCostLayers.Add(new InventoryCostLayer { InventoryCostLayerId = RecipeId, IngredientId = IngredientId, StoreId = StoreId, Quantity = 100, RemainingQuantity = layerQuantity, UnitCost = 10, CreatedAt = DateTime.UtcNow.AddDays(-1) });
            }
            else
            {
                const int preparedId = 9120;
                const int childRecipeId = 9121;
                context.PreparedItems.Add(new PreparedItem { PreparedItemId = preparedId, Code = "PF-BTP", Name = "BTP", BaseUnitId = UnitId, Active = true });
                context.Recipes.Add(new Recipe
                {
                    RecipeId = childRecipeId,
                    RecipeCode = "PF-CHILD",
                    Name = "BTP recipe",
                    Active = true,
                    Status = "Active",
                    PreparedItemId = preparedId,
                    OutputQuantity = preparedOutputConfirmed ? 10 : null,
                    OutputUnitId = preparedOutputConfirmed ? UnitId : null,
                    EffectiveDate = DateTime.UtcNow.AddDays(-1)
                });
                context.RecipeDetails.Add(new RecipeDetail { RecipeDetailId = RecipeId, RecipeId = RecipeId, ChildRecipeId = childRecipeId, Quantity = 2, UnitId = UnitId });
                context.InventoryCostLayers.Add(new InventoryCostLayer { InventoryCostLayerId = RecipeId, PreparedItemId = preparedId, StoreId = StoreId, Quantity = 100, RemainingQuantity = layerQuantity, UnitCost = 12, CreatedAt = DateTime.UtcNow.AddDays(-1) });
            }
        }

        if (policy.HasValue || addLegacyDefault)
        {
            context.Toppings.Add(new Topping { ToppingId = ToppingId, ToppingCode = "PF-TOP", Name = "Topping", Price = 5_000, Active = true });
            context.DrinkToppings.Add(new DrinkTopping { DrinkToppingId = ToppingId, DrinkId = DrinkId, ToppingId = ToppingId, Active = true });
            context.DrinkDefaultToppings.Add(new DrinkDefaultTopping { DrinkDefaultToppingId = ToppingId, DrinkId = DrinkId, ToppingId = ToppingId });
            context.Recipes.Add(new Recipe { RecipeId = ToppingRecipeId, RecipeCode = "PF-TOP-RCP", Name = "Topping recipe", ToppingId = ToppingId, Active = true, Status = "Active", EffectiveDate = DateTime.UtcNow.AddDays(-1) });
            context.RecipeDetails.Add(new RecipeDetail { RecipeDetailId = ToppingRecipeId, RecipeId = ToppingRecipeId, IngredientId = IngredientId, Quantity = 0.5m, UnitId = UnitId });
            if (policy.HasValue)
            {
                context.DrinkSizeToppingPolicies.Add(new DrinkSizeToppingPolicy
                {
                    DrinkSizeToppingPolicyId = 9130, DrinkSizeId = DrinkSizeId, ToppingId = ToppingId,
                    IsDefaultSelected = true, PriceTreatment = policy.Value.Price, CostTreatment = policy.Value.Cost,
                    QuantityPerDrink = policyQuantity, IsActive = true, CreatedByStaffId = OwnerStaffId,
                    CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
                });
            }
        }

        var role = await context.Roles.FirstAsync(x => x.Name == RoleConstants.BusinessOwner);
        context.Accounts.Add(new Account { AccountId = OwnerStaffId, Email = "profit-owner@test.local", PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow });
        context.Staffs.Add(new Staff
        {
            StaffId = OwnerStaffId, AccountId = OwnerStaffId, FullName = "Profit Owner", StoreId = StoreId,
            Active = true, CreatedAt = DateTime.UtcNow, EmployeeStatus = 2});
        context.AccountRoles.Add(new AccountRole { AccountId = OwnerStaffId, RoleId = role.RoleId });
        await context.SaveChangesAsync();
    }

    private static Recipe CreateRecipe(int id, int? sizeId) => new()
    {
        RecipeId = id, RecipeCode = $"PF-RCP-{id}", Name = "Exact recipe", DrinkId = DrinkId,
        SizeId = sizeId, Active = true, Status = "Active", EffectiveDate = DateTime.UtcNow.AddDays(-1)
    };

    private static DrinkSizeRecipeResolver CreateResolver(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
        return new DrinkSizeRecipeResolver(context, conversion, physical);
    }

    private static DrinkSizeProfitabilityQueryService CreateProfitabilityService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
        var normalizer = new RecipeOutputNormalizer(context, physical);
        var estimated = new EstimatedBomCostService(context, conversion, physical, normalizer, NullLogger<EstimatedBomCostService>.Instance);
        return new DrinkSizeProfitabilityQueryService(context, new DrinkSizeRecipeResolver(context, conversion, physical), conversion, physical, estimated, new ScopeAuthorizationService(context));
    }

    private static DrinkSizePricingService CreatePricingService(AppDbContext context) =>
        new(context, CreateProfitabilityService(context));

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "CafeChain.Frontend")) &&
                Directory.Exists(Path.Combine(current.FullName, "CafeChain")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy repository root cho source-contract test.");
    }
}
