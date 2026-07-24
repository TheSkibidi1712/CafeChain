using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using CafeChain.Tests.Testing;

namespace CafeChain.Tests;

public sealed class StoreMenuPosEnforcementIssue165Tests : IntegrationTestBase
{
    [Fact]
    public async Task OnlineSnapshot_ExactStorePriceAndRequiredTopping_IsAccepted()
    {
        await using var context = CreateDbContext();
        var validator = new POSStoreMenuSaleValidator(context, new CatalogStub(BuildCatalog()));

        var result = await validator.ValidateOnlineAsync(BuildItem(), 16501, DateTime.UtcNow);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(42_000m, result.Data!.AcceptedBasePrice);
        Assert.Equal(47_000m, result.Data.AcceptedUnitPrice);
        Assert.Equal(StoreMenuPriceSources.StoreOverride, result.Data.PriceSource);
        Assert.Equal(7, result.Data.CatalogVersion);
        Assert.Single(result.Data.Toppings);
        Assert.Equal(5_000m, result.Data.Toppings[0].AcceptedPrice);
    }

    [Fact]
    public async Task OnlineSnapshot_StaleVersionOrPrice_ReturnsExactConflict()
    {
        await using var context = CreateDbContext();
        var validator = new POSStoreMenuSaleValidator(context, new CatalogStub(BuildCatalog()));
        var item = BuildItem();
        item.CatalogVersion = 6;

        var result = await validator.ValidateOnlineAsync(item, 16501, DateTime.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.Equal(POSCatalogSaleErrorCodes.SnapshotStale, result.ErrorCode);
    }

    [Fact]
    public async Task OnlineSnapshot_UnavailableSkuAndMissingRequiredTopping_AreRejected()
    {
        await using var context = CreateDbContext();
        var unavailableCatalog = BuildCatalog(isAvailable: false);
        var unavailableValidator = new POSStoreMenuSaleValidator(context, new CatalogStub(unavailableCatalog));

        var unavailable = await unavailableValidator.ValidateOnlineAsync(BuildItem(), 16501, DateTime.UtcNow);

        Assert.False(unavailable.IsSuccess);
        Assert.Equal(POSCatalogSaleErrorCodes.ItemUnavailable, unavailable.ErrorCode);

        var requiredValidator = new POSStoreMenuSaleValidator(context, new CatalogStub(BuildCatalog()));
        var missingRequired = BuildItem();
        missingRequired.Toppings.Clear();

        var required = await requiredValidator.ValidateOnlineAsync(missingRequired, 16501, DateTime.UtcNow);

        Assert.False(required.IsSuccess);
        Assert.Equal(POSCatalogSaleErrorCodes.ToppingInvalid, required.ErrorCode);
    }

    [Fact]
    public async Task OfflineSnapshot_PreservesAcceptedPrice_WhenCurrentGlobalPriceChanged()
    {
        await SeedOfflineIdentityAsync(currentGlobalPrice: 99_000m, currentToppingPrice: 20_000m);
        await using var context = CreateDbContext();
        var validator = new POSStoreMenuSaleValidator(context, new CatalogStub(BuildCatalog()));
        var item = BuildItem();

        var result = await validator.ValidateOfflineAsync(item, 16501);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(42_000m, result.Data!.AcceptedBasePrice);
        Assert.Equal(47_000m, result.Data.AcceptedUnitPrice);
        Assert.Equal(5_000m, result.Data.Toppings.Single().AcceptedPrice);
    }

    [Fact]
    public async Task OfflineCommit_PersistsAcceptedSnapshot_WithoutChangingActualCogsAuthority()
    {
        var repository = new Mock<IPOSOrderRepository>(MockBehavior.Loose);
        var shiftService = new Mock<IWorkShiftService>(MockBehavior.Loose);
        var validator = new Mock<IPOSStoreMenuSaleValidator>(MockBehavior.Strict);
        var clientOrderId = Guid.NewGuid();
        Order? captured = null;

        repository.Setup(x => x.FindOrderByClientOrderIdAsync(clientOrderId, It.IsAny<int>())).ReturnsAsync((Order?)null);
        repository.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
        repository.Setup(x => x.CommitTransactionAsync()).Returns(Task.CompletedTask);
        repository.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .Callback<Order>(order => { captured = order; order.OrderId = 16520; })
            .ReturnsAsync((Order order) => order);
        repository.Setup(x => x.CreatePaymentAsync(It.IsAny<Payment>())).Returns(Task.CompletedTask);
        repository.Setup(x => x.SaveChangesAsync()).Returns(Task.CompletedTask);
        shiftService.Setup(x => x.GetShiftByIdAsync(16530)).ReturnsAsync(new WorkShift
        {
            ShiftId = 16530,
            UserId = 16531,
            StoreId = 16501,
            Status = "Open"
        });
        validator.Setup(x => x.ValidateOfflineAsync(It.IsAny<POSOrderItemDto>(), 16501, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<POSAcceptedSaleLineDto>.Success(BuildAcceptedLine()));

        var service = new POSOrderService(
            repository.Object,
            shiftService.Object,
            Mock.Of<IAdminVoucherService>(),
            Mock.Of<IPrintDispatcher>(),
            Mock.Of<IPayOSService>(),
            Mock.Of<ILogger<POSOrderService>>(),
            validator.Object,
            null,
            AllowAllOrderAccessAuthorizationService.Instance);
        var dto = new POSOrderCommitDto
        {
            ClientOrderId = clientOrderId,
            Items = new List<POSOrderItemDto> { BuildItem() },
            Payments = new List<PaymentLineDto> { new() { PaymentMethodId = 1, Amount = 47_000m } },
            PaymentMethodId = 1,
            ReceivedAmount = 50_000m,
            SkipPrint = true
        };

        var result = await service.CommitOfflineSyncedOrderAsync(dto, 16531, 16501, 16530, DateTime.UtcNow);

        Assert.True(result.IsSuccess, result.Message);
        var detail = Assert.Single(captured!.OrderDetails);
        Assert.Equal(16506, detail.StoreMenuItemId);
        Assert.Equal(16505, detail.DrinkSizeId);
        Assert.Equal(42_000m, detail.AcceptedBasePrice);
        Assert.Equal(47_000m, detail.Price);
        Assert.Equal(StoreMenuPriceSources.StoreOverride, detail.PriceSource);
        Assert.Equal(7, detail.AcceptedCatalogVersion);
        Assert.Equal(SalesCostStatus.Pending, detail.CostStatus);
        Assert.Null(detail.UnitCogs);
        Assert.Null(detail.TotalCogs);
    }

    [Fact]
    public async Task DuplicateClientOrderId_ReturnsBeforeSnapshotValidationOrSideEffects()
    {
        var repository = new Mock<IPOSOrderRepository>(MockBehavior.Loose);
        var validator = new Mock<IPOSStoreMenuSaleValidator>(MockBehavior.Strict);
        var clientOrderId = Guid.NewGuid();
        repository.Setup(x => x.FindOrderByClientOrderIdAsync(clientOrderId, It.IsAny<int>())).ReturnsAsync(new Order
        {
            OrderId = 16540,
            ClientOrderId = clientOrderId,
            StoreId = 16501,
            StaffId = 16531,
            WorkShiftId = 16530,
            Total = 47_000m,
            SubTotal = 47_000m
        });
        var shiftService = new Mock<IWorkShiftService>(MockBehavior.Loose);
        shiftService.Setup(x => x.GetShiftByIdAsync(16530)).ReturnsAsync(new WorkShift
        {
            ShiftId = 16530,
            UserId = 16531,
            StoreId = 16501,
            Status = "Closed"
        });
        var service = new POSOrderService(
            repository.Object,
            shiftService.Object,
            Mock.Of<IAdminVoucherService>(),
            Mock.Of<IPrintDispatcher>(),
            Mock.Of<IPayOSService>(),
            Mock.Of<ILogger<POSOrderService>>(),
            validator.Object,
            null,
            AllowAllOrderAccessAuthorizationService.Instance);

        var result = await service.CommitOfflineSyncedOrderAsync(new POSOrderCommitDto
        {
            ClientOrderId = clientOrderId,
            Items = new List<POSOrderItemDto> { BuildItem() },
            ReceivedAmount = 50_000m,
            PaymentMethodId = 1
        }, 16531, 16501, 16530, DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(true, result.Data!.GetType().GetProperty("isIdempotent")?.GetValue(result.Data));
        validator.VerifyNoOtherCalls();
        repository.Verify(x => x.BeginTransactionAsync(), Times.Never);
        repository.Verify(x => x.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    private async Task SeedOfflineIdentityAsync(decimal currentGlobalPrice, decimal currentToppingPrice)
    {
        await using var context = CreateDbContext();
        context.Drinks.Add(new Drink
        {
            DrinkId = 16503, DrinkCode = "POS165", Name = "POS 165", Description = "Test",
            ProductTypeId = 1, Active = true, CreatedAt = DateTime.UtcNow
        });
        context.Sizes.Add(new Size { SizeId = 16504, SizeCode = "M165", Name = "Size 165", Description = "Test", Active = true });
        context.DrinkSizes.Add(new DrinkSize
        {
            DrinkSizeId = 16505, DrinkId = 16503, SizeId = 16504,
            Price = currentGlobalPrice, Active = true, UpdatedAtUtc = DateTime.UtcNow
        });
        context.StoreMenuItems.Add(new StoreMenuItem
        {
            StoreMenuItemId = 16506, StoreId = 16501, DrinkSizeId = 16505,
            IsEnabled = true, PriceOverride = 88_000m, PublishedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });
        context.Toppings.Add(new Topping
        {
            ToppingId = 16507, ToppingCode = "TOP165", Name = "Trân châu", Price = currentToppingPrice, Active = true
        });
        context.DrinkToppings.Add(new DrinkTopping { DrinkToppingId = 16508, DrinkId = 16503, ToppingId = 16507, Active = true });
        context.StoreToppings.Add(new StoreTopping { StoreToppingId = 16509, StoreId = 16501, ToppingId = 16507, Active = true });
        await context.SaveChangesAsync();
    }

    private static POSOrderItemDto BuildItem() => new()
    {
        DrinkId = 16503,
        SizeId = 16504,
        StoreMenuItemId = 16506,
        DrinkSizeId = 16505,
        AcceptedBasePrice = 42_000m,
        AcceptedUnitPrice = 47_000m,
        PriceSource = StoreMenuPriceSources.StoreOverride,
        CatalogVersion = 7,
        Quantity = 1,
        Toppings = new List<POSOrderToppingDto>
        {
            new() { ToppingId = 16507, Name = "Trân châu", AcceptedPrice = 5_000m }
        }
    };

    private static POSAcceptedSaleLineDto BuildAcceptedLine() => new()
    {
        StoreMenuItemId = 16506,
        DrinkSizeId = 16505,
        DrinkId = 16503,
        SizeId = 16504,
        DrinkName = "POS 165",
        SizeName = "M",
        AcceptedBasePrice = 42_000m,
        AcceptedUnitPrice = 47_000m,
        PriceSource = StoreMenuPriceSources.StoreOverride,
        CatalogVersion = 7,
        Toppings = new[]
        {
            new POSAcceptedSaleToppingDto { ToppingId = 16507, Name = "Trân châu", AcceptedPrice = 5_000m }
        }
    };

    private static POSCatalogSnapshotDto BuildCatalog(bool isAvailable = true) => new()
    {
        StoreId = 16501,
        Version = 7,
        GeneratedAtUtc = DateTime.UtcNow,
        Categories = Array.Empty<POSCategoryDto>(),
        MenuItems = new[]
        {
            new POSMenuItemDto
            {
                Id = 16503,
                Name = "POS 165",
                CategoryId = 1,
                Price = 42_000m,
                IsAvailable = isAvailable,
                AvailableToppings = new List<POSToppingDto>
                {
                    new() { Id = 16507, Name = "Trân châu", Price = 5_000m }
                },
                Sizes = new List<POSMenuItemSizeDto>
                {
                    new()
                    {
                        StoreMenuItemId = 16506,
                        DrinkSizeId = 16505,
                        SizeId = 16504,
                        SizeName = "M",
                        Price = 42_000m,
                        GlobalPrice = 40_000m,
                        StoreOverride = 42_000m,
                        PriceSource = StoreMenuPriceSources.StoreOverride,
                        IsAvailable = isAvailable,
                        AvailabilityStatus = isAvailable ? StoreMenuAvailabilityStatuses.Available : StoreMenuAvailabilityStatuses.OutOfStock,
                        AvailabilityReason = isAvailable ? null : "Hết nguyên liệu",
                        ToppingPolicies = new List<POSToppingPolicyDto>
                        {
                            new()
                            {
                                ToppingId = 16507,
                                IsDefaultSelected = true,
                                IsRequired = true,
                                PriceTreatment = ToppingPriceTreatments.AddToppingPrice,
                                QuantityPerDrink = 1m
                            }
                        }
                    }
                }
            }
        }
    };

    private sealed class CatalogStub : IPOSCatalogSnapshotService
    {
        private readonly POSCatalogSnapshotDto _snapshot;
        public CatalogStub(POSCatalogSnapshotDto snapshot) => _snapshot = snapshot;

        public Task<POSCatalogSnapshotDto> BuildAsync(
            int storeId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default) => Task.FromResult(_snapshot);
    }
}
