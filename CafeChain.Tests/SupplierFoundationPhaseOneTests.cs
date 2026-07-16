using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Suppliers;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Infrastrusture.Repositories.Admin.Suppliers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests;

/// <summary>
/// Supplier Foundation Phase 1 contracts: package offers, price history, store scope,
/// and server-owned receipt/FIFO snapshots.
/// </summary>
public class SupplierFoundationPhaseOneTests : IntegrationTestBase
{
    private const int StoreId = 1670;
    private const int OtherStoreId = 1671;
    private const int IngredientId = 16701;
    private const int CanUnitId = 16711;
    private const int ActorStaffId = 16710;
    private static readonly string[] WarehouseRoles = { RoleConstants.AccountantWarehouse };

    [Fact]
    public async Task SupplierContactPhone_RoundTrips_WithoutContactPhoneJoinEntity()
    {
        using var ctx = CreateDbContext();
        var supplierId = await FirstSupplierIdAsync(ctx);
        var service = CreateSupplierService(ctx);

        await service.AddContactAsync(new AdminSupplierContactCreateDTO
        {
            SupplierId = supplierId,
            Name = "Lan thu mua",
            Phone = "0909123456",
            Email = "lan.sourcing@test.local",
            Position = "Thu mua"
        });

        var detail = await service.GetByIdAsync(supplierId);
        var contact = Assert.Single(detail!.Contacts, x => x.Email == "lan.sourcing@test.local");
        Assert.Equal("0909123456", contact.Phone);
        Assert.DoesNotContain(ctx.Model.GetEntityTypes(), x => x.ClrType.Name == "SupplierContactPhone");
    }

    [Fact]
    public async Task IngredientOffer_ValidatesTerms_AndKeepsOnePrimaryPerIngredient()
    {
        using var ctx = CreateDbContext();
        await SeedCatalogAsync(ctx);
        var supplierIds = await ctx.Suppliers.OrderBy(x => x.SupplierId).Select(x => x.SupplierId).Take(2).ToListAsync();
        Assert.Equal(2, supplierIds.Count);
        var service = CreateSupplierService(ctx);

        var invalidMoq = NewOffer(supplierIds[0], minimumPackages: 0);
        var moqError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateIngredientOfferAsync(invalidMoq));
        Assert.Contains("MOQ", moqError.Message);

        var invalidLeadTime = NewOffer(supplierIds[0], leadTimeDays: -1);
        var leadTimeError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateIngredientOfferAsync(invalidLeadTime));
        Assert.Contains("không được âm", leadTimeError.Message);

        var firstId = await service.CreateIngredientOfferAsync(NewOffer(supplierIds[0]));
        var secondId = await service.CreateIngredientOfferAsync(NewOffer(supplierIds[1]));

        var offers = await ctx.IngredientSuppliers
            .Where(x => x.IngredientId == IngredientId)
            .OrderBy(x => x.IngredientSupplierId)
            .ToListAsync();
        Assert.Equal(2, offers.Count);
        Assert.False(offers.Single(x => x.IngredientSupplierId == firstId).IsPrimary);
        Assert.True(offers.Single(x => x.IngredientSupplierId == secondId).IsPrimary);
        Assert.Equal(24m, offers[1].PackageQuantity);
        Assert.Equal(648_000m, offers[1].CurrentPrice);
        Assert.Equal(2, offers[1].MinimumOrderPackageCount);
        Assert.Equal(2, offers[1].LeadTimeDays);

        var history = await ctx.IngredientSupplierPriceHistories
            .SingleAsync(x => x.IngredientSupplierId == secondId);
        Assert.True(history.IsCurrent);
        Assert.Equal(648_000m, history.Price);
        Assert.Equal(24m, history.PackageQuantity);
        Assert.Equal(CanUnitId, history.PackageUnitId);
    }

    [Fact]
    public async Task PriceChange_ClosesOldCurrent_CreatesAuditSnapshot_AndUpdatesOffer()
    {
        using var ctx = CreateDbContext();
        await SeedCatalogAsync(ctx);
        var supplierId = await FirstSupplierIdAsync(ctx);
        var service = CreateSupplierService(ctx);
        var offerId = await service.CreateIngredientOfferAsync(NewOffer(supplierId));

        await service.ChangeIngredientOfferPriceAsync(new AdminIngredientSupplierPriceChangeDTO
        {
            IngredientSupplierId = offerId,
            PackagePrice = 672_000m,
            PackageQuantity = 24m,
            PackageUnitId = CanUnitId,
            Reason = "Điều chỉnh giá tháng mới",
            RowVersion = await OfferVersionAsync(ctx, offerId)
        }, ActorStaffId);

        var offer = await ctx.IngredientSuppliers.SingleAsync(x => x.IngredientSupplierId == offerId);
        Assert.Equal(672_000m, offer.CurrentPrice);

        var history = await ctx.IngredientSupplierPriceHistories
            .Where(x => x.IngredientSupplierId == offerId)
            .OrderBy(x => x.IngredientSupplierPriceHistoryId)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Single(history, x => x.IsCurrent);
        Assert.False(history[0].IsCurrent);
        Assert.True(history[1].IsCurrent);
        Assert.Equal(ActorStaffId, history[1].CreatedByStaffId);
        Assert.Equal("Điều chỉnh giá tháng mới", history[1].Note);
    }

    [Fact]
    public async Task SupplierStore_ScopesDropdown_AndUniquePairIsEnforced()
    {
        using var ctx = CreateDbContext();
        await SeedCatalogAsync(ctx);
        var supplierId = await FirstSupplierIdAsync(ctx);
        var service = CreateSupplierService(ctx);

        await service.SaveSupplierStoreAsync(new AdminSupplierStoreSaveDTO
        {
            SupplierId = supplierId,
            StoreId = StoreId,
            Active = true,
            LeadTimeOverrideDays = 1,
            DeliverySchedule = "Thứ 2, 4, 6"
        });

        var receiptService = CreateReceiptService(ctx);
        var inScope = await receiptService.GetSupplierOptionsAsync(
            StoreId, ActorStaffId, null, WarehouseRoles);
        var outOfScope = await receiptService.GetSupplierOptionsAsync(
            OtherStoreId, ActorStaffId, null, WarehouseRoles);
        Assert.True(inScope.IsSuccess, inScope.Message);
        Assert.Contains(inScope.Data!, x => x.SupplierId == supplierId);
        Assert.True(outOfScope.IsSuccess, outOfScope.Message);
        Assert.DoesNotContain(outOfScope.Data!, x => x.SupplierId == supplierId);

        ctx.SupplierStores.Add(new SupplierStore
        {
            SupplierId = supplierId,
            StoreId = StoreId,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Receipt_UsesPackageSnapshot_CreatesFifoOnce_AndIgnoresLaterPriceChange()
    {
        using var ctx = CreateDbContext();
        await SeedCatalogAsync(ctx);
        var supplierId = await FirstSupplierIdAsync(ctx);
        var supplierService = CreateSupplierService(ctx);
        await supplierService.SaveSupplierStoreAsync(new AdminSupplierStoreSaveDTO
        {
            SupplierId = supplierId,
            StoreId = StoreId,
            Active = true
        });
        var offerId = await supplierService.CreateIngredientOfferAsync(NewOffer(supplierId));
        var requestId = await SeedProcessingRequestAsync(ctx, requestedQuantity: 48m);
        var receiptService = CreateReceiptService(ctx);

        var offerOptions = await receiptService.GetOfferOptionsAsync(
            StoreId, supplierId, requestId, ActorStaffId, null, WarehouseRoles);
        var option = Assert.Single(offerOptions.Data!);
        Assert.Equal(offerId, option.IngredientSupplierId);
        Assert.Equal(24m, option.PackageQuantity);
        Assert.Equal(648_000m, option.PackagePrice);

        var draft = await receiptService.CreateDraftAsync(new CreateBranchReceiptRequest
        {
            StoreId = StoreId,
            SupplierId = supplierId,
            ReceiptKey = "supplier-phase-one-48-cans",
            Lines =
            {
                new CreateBranchReceiptLineInput
                {
                    RestockRequestId = requestId,
                    IngredientSupplierId = offerId,
                    ActualReceivedQuantity = 2m,
                    InputUnitId = CanUnitId,
                    ActualPackagePrice = 1m
                }
            }
        }, ActorStaffId, WarehouseRoles);

        Assert.True(draft.IsSuccess, draft.Message);
        var draftLine = Assert.Single(draft.Data!.Lines);
        Assert.Equal(48m, draftLine.ReceivedBaseQuantity);
        Assert.Equal(648_000m, draftLine.ActualPackagePrice);
        Assert.Equal(24m, draftLine.PackageQuantitySnapshot);
        Assert.Equal(CanUnitId, draftLine.PackageUnitIdSnapshot);
        Assert.Equal(27_000m, draftLine.BaseUnitCostSnapshot);
        Assert.Equal(1_296_000m, draftLine.LineTotalCost);

        await supplierService.ChangeIngredientOfferPriceAsync(new AdminIngredientSupplierPriceChangeDTO
        {
            IngredientSupplierId = offerId,
            PackagePrice = 720_000m,
            PackageQuantity = 24m,
            PackageUnitId = CanUnitId,
            Reason = "Giá mới sau khi lập phiếu",
            RowVersion = await OfferVersionAsync(ctx, offerId)
        }, ActorStaffId);

        var confirmed = await receiptService.ConfirmAsync(
            draft.Data.BranchReceiptId, ActorStaffId, StoreId, WarehouseRoles, draft.Data.RowVersion);
        Assert.True(confirmed.IsSuccess, confirmed.Message);
        Assert.False(confirmed.Data!.WasReplay);

        var transaction = await ctx.InventoryTransactions.SingleAsync(x =>
            x.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN);
        Assert.Equal(48m, transaction.Quantity);
        Assert.Equal(27_000m, transaction.UnitCost);
        Assert.Equal(1_296_000m, transaction.TotalCost);

        var fifo = await ctx.InventoryCostLayers.SingleAsync();
        Assert.Equal(48m, fifo.Quantity);
        Assert.Equal(48m, fifo.RemainingQuantity);
        Assert.Equal(27_000m, fifo.UnitCost);

        var replay = await receiptService.ConfirmAsync(
            draft.Data.BranchReceiptId, ActorStaffId, StoreId, WarehouseRoles, draft.Data.RowVersion);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.True(replay.Data!.WasReplay);
        Assert.Equal(1, await ctx.InventoryTransactions.CountAsync(x =>
            x.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN));
        Assert.Equal(1, await ctx.InventoryCostLayers.CountAsync());
        Assert.DoesNotContain(ctx.Model.GetEntityTypes(), x => x.ClrType.Name == "InventoryDebt");
    }

    [Fact]
    public async Task InactiveOffer_IsNotSelectable_AndCannotCreateNewReceipt()
    {
        using var ctx = CreateDbContext();
        await SeedCatalogAsync(ctx);
        var supplierId = await FirstSupplierIdAsync(ctx);
        var supplierService = CreateSupplierService(ctx);
        await supplierService.SaveSupplierStoreAsync(new AdminSupplierStoreSaveDTO
        {
            SupplierId = supplierId,
            StoreId = StoreId,
            Active = true
        });
        var offerId = await supplierService.CreateIngredientOfferAsync(NewOffer(supplierId));
        await supplierService.ToggleIngredientOfferActiveAsync(
            offerId,
            false,
            await OfferVersionAsync(ctx, offerId));
        var requestId = await SeedProcessingRequestAsync(ctx, 48m);
        var receiptService = CreateReceiptService(ctx);

        var options = await receiptService.GetOfferOptionsAsync(
            StoreId, supplierId, requestId, ActorStaffId, null, WarehouseRoles);
        Assert.True(options.IsSuccess, options.Message);
        Assert.Empty(options.Data!);

        var draft = await receiptService.CreateDraftAsync(new CreateBranchReceiptRequest
        {
            StoreId = StoreId,
            SupplierId = supplierId,
            ReceiptKey = "inactive-offer-rejected",
            Lines =
            {
                new CreateBranchReceiptLineInput
                {
                    RestockRequestId = requestId,
                    IngredientSupplierId = offerId,
                    ActualReceivedQuantity = 2m,
                    InputUnitId = CanUnitId,
                    ActualPackagePrice = 648_000m
                }
            }
        }, ActorStaffId, WarehouseRoles);
        Assert.False(draft.IsSuccess);
        Assert.Equal(BranchReceiptErrorCodes.OfferNotAvailable, draft.ErrorCode);
    }

    private static AdminIngredientSupplierSaveDTO NewOffer(
        int supplierId,
        int? minimumPackages = 2,
        int? leadTimeDays = 2) => new()
        {
            SupplierId = supplierId,
            IngredientId = IngredientId,
            UnitId = CanUnitId,
            PackageQuantity = 24m,
            CurrentPrice = 648_000m,
            MinimumOrderPackageCount = minimumPackages,
            LeadTimeDays = leadTimeDays,
            IsPrimary = true,
            Active = true,
            Note = "24 lon / thùng"
        };

    private static async Task<int> FirstSupplierIdAsync(AppDbContext ctx) =>
        await ctx.Suppliers.OrderBy(x => x.SupplierId).Select(x => x.SupplierId).FirstAsync();

    private static AdminSupplierService CreateSupplierService(AppDbContext ctx)
    {
        var physical = new PhysicalUnitConversionService(
            ctx, NullLogger<PhysicalUnitConversionService>.Instance);
        var validator = new IngredientSupplierPackageValidator(ctx, physical);
        return new AdminSupplierService(new AdminSupplierRepository(ctx), ctx, validator);
    }

    private static BranchReceiptService CreateReceiptService(AppDbContext ctx)
    {
        var physical = new PhysicalUnitConversionService(
            ctx, NullLogger<PhysicalUnitConversionService>.Instance);
        var unit = new UnitConversionService(
            ctx, NullLogger<UnitConversionService>.Instance, physical);
        var mode = new Mock<IInventoryWriterModeService>();
        var resolver = new Mock<IStoreInventoryWriteResolver>();
        var alerts = new Mock<IStockAlertService>();
        alerts
            .Setup(x => x.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(ServiceResult<CafeChain.Application.DTOs.POS.StockAlertEvaluationResultDto>.Success(
                new CafeChain.Application.DTOs.POS.StockAlertEvaluationResultDto()));

        return new BranchReceiptService(
            ctx,
            unit,
            physical,
            mode.Object,
            resolver.Object,
            new RestockFulfillmentPostingService(ctx),
            alerts.Object,
            new ScopeAuthorizationService(ctx),
            NullLogger<BranchReceiptService>.Instance);
    }

    private static async Task SeedCatalogAsync(AppDbContext ctx)
    {
        if (!await ctx.Units.AnyAsync(x => x.UnitId == CanUnitId))
        {
            ctx.Units.Add(new Unit
            {
                UnitId = CanUnitId,
                UnitCode = "lon_count",
                Name = "Lon",
                Type = UnitType.Dem,
                Active = true
            });
        }

        if (!await ctx.Stores.AnyAsync(x => x.StoreId == StoreId))
        {
            ctx.Stores.AddRange(
                new Store
                {
                    StoreId = StoreId,
                    Name = "Supplier Phase One Store",
                    Address = "Test",
                    Phone = "0900001670",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Store
                {
                    StoreId = OtherStoreId,
                    Name = "Supplier Out Of Scope Store",
                    Address = "Test",
                    Phone = "0900001671",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
        }

        if (!await ctx.Ingredients.AnyAsync(x => x.IngredientId == IngredientId))
        {
            ctx.Ingredients.Add(new Ingredient
            {
                IngredientId = IngredientId,
                Code = "ING-SUP-167",
                Name = "Sữa lon kiểm thử Supplier",
                BaseUnitId = CanUnitId,
                Active = true
            });
        }

        await ctx.SaveChangesAsync();
    }

    private static async Task<int> SeedProcessingRequestAsync(
        AppDbContext ctx,
        decimal requestedQuantity)
    {
        var now = DateTime.UtcNow;
        var alert = new StockAlert
        {
            StoreId = StoreId,
            IngredientId = IngredientId,
            AlertType = StockAlertTypes.LowStock,
            Severity = StockAlertSeverities.Warning,
            Status = StockAlertStatuses.Confirmed,
            Source = StockAlertSources.ManualCheck,
            CurrentQtySnapshot = 0,
            ThresholdSnapshot = requestedQuantity,
            CreatedAt = now,
            UpdatedAt = now
        };
        ctx.StockAlerts.Add(alert);
        await ctx.SaveChangesAsync();

        var request = new RestockRequest
        {
            StockAlertId = alert.StockAlertId,
            StoreId = StoreId,
            IngredientId = IngredientId,
            RequestedQuantity = requestedQuantity,
            Status = RestockRequestStatuses.Processing,
            Priority = RestockRequestPriorities.Normal,
            CreatedByStaffId = ActorStaffId,
            CreatedAt = now,
            UpdatedAt = now,
            HandledByStaffId = ActorStaffId,
            HandledAt = now
        };
        ctx.RestockRequests.Add(request);
        await ctx.SaveChangesAsync();
        return request.RestockRequestId;
    }

    private static async Task<string> OfferVersionAsync(AppDbContext context, int offerId)
    {
        var version = await context.IngredientSuppliers
            .AsNoTracking()
            .Where(x => x.IngredientSupplierId == offerId)
            .Select(x => x.RowVersion)
            .SingleAsync();
        return Convert.ToBase64String(version);
    }
}
