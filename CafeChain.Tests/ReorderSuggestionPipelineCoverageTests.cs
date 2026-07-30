using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Infrastructure.Interfaces.Admin.Procurement;
using CafeChain.Models.Inventories.Suppliers;
using Moq;

namespace CafeChain.Tests;

public sealed class ReorderSuggestionPipelineCoverageTests
{
    private const int StoreId = 9101;
    private const int IngredientId = 9102;
    private const int UnitId = 9103;
    private const int RestockRequestId = 9104;
    private const int PurchaseAdviceLineId = 9105;

    [Fact]
    public async Task Near_reorder_is_actionable_before_stock_falls_below_minimum()
    {
        var item = await CalculateAsync(
            onHand: 10m,
            minimumStock: 5m,
            consumption: 300m);

        Assert.Equal(10m, item.AvailableStock);
        Assert.Equal(15m, item.ReorderPoint);
        Assert.Equal(5m, item.RawDemand);
        Assert.Equal(ReorderRecommendationLevels.NearReorder, item.SuggestionStatus);
        Assert.True(item.CanConfirm);
    }

    [Fact]
    public async Task Package_rounding_then_moq_produces_final_base_quantity()
    {
        var item = await CalculateAsync(
            consumption: 390m,
            packageQuantity: 5m,
            minimumOrderPackageCount: 4);

        Assert.Equal(13m, item.RawDemand);
        Assert.Equal(13m, item.RemainingDemand);
        Assert.Equal(4m, item.SuggestedPackageCount);
        Assert.Equal(20m, item.FinalSuggestedQuantity);
    }

    [Fact]
    public async Task Missing_threshold_and_history_are_data_incomplete_and_not_confirmable()
    {
        var item = await CalculateAsync(
            minimumStock: null,
            includeUsage: false);

        Assert.Equal(
            ReorderRecommendationLevels.DataIncomplete,
            item.SuggestionStatus);
        Assert.Contains(
            ReorderSuggestionReasonCodes.MissingThreshold,
            item.ReasonCodes);
        Assert.Contains(
            ReorderSuggestionReasonCodes.InsufficientHistory,
            item.ReasonCodes);
        Assert.False(item.CanConfirm);
        Assert.Null(item.FinalSuggestedQuantity);
    }

    [Fact]
    public async Task Missing_supplier_is_data_incomplete_and_not_confirmable()
    {
        var item = await CalculateAsync(includeOffer: false);

        Assert.Equal(
            ReorderRecommendationLevels.DataIncomplete,
            item.SuggestionStatus);
        Assert.Contains(
            ReorderSuggestionReasonCodes.NoActiveSupplier,
            item.ReasonCodes);
        Assert.False(item.CanConfirm);
    }

    [Fact]
    public async Task Invalid_package_conversion_is_data_incomplete_and_not_confirmable()
    {
        var item = await CalculateAsync(conversionSucceeds: false);

        Assert.Equal(
            ReorderRecommendationLevels.DataIncomplete,
            item.SuggestionStatus);
        Assert.Contains(
            ReorderSuggestionReasonCodes.InvalidConversion,
            item.ReasonCodes);
        Assert.False(item.CanConfirm);
    }

    [Fact]
    public async Task Active_purchase_advice_covers_only_its_unallocated_residual()
    {
        var item = await CalculateAsync(
            restocks:
            [
                new(
                    RestockRequestId,
                    StoreId,
                    IngredientId,
                    RestockRequestStatuses.Processing,
                    30m,
                    0m,
                    0m)
            ],
            advice:
            [
                new(
                    PurchaseAdviceLineId,
                    9201,
                    RestockRequestId,
                    StoreId,
                    IngredientId,
                    PurchaseAdviceStatuses.UnderReview,
                    true,
                    30m,
                    0m,
                    0m,
                    0m)
            ]);

        Assert.Equal(100m, item.RawDemand);
        Assert.Equal(30m, item.ProcurementCoveredQuantity);
        Assert.Equal(70m, item.RemainingDemand);
        Assert.Equal(70m, item.FinalSuggestedQuantity);
        Assert.Equal(
            ReorderRecommendationLevels.ProcurementInProgress,
            item.SuggestionStatus);
    }

    [Fact]
    public async Task Approved_po_is_incoming_and_is_not_counted_again_as_procurement_coverage()
    {
        var item = await CalculateAsync(
            restocks:
            [
                new(
                    RestockRequestId,
                    StoreId,
                    IngredientId,
                    RestockRequestStatuses.Processing,
                    30m,
                    0m,
                    0m)
            ],
            advice:
            [
                new(
                    PurchaseAdviceLineId,
                    9201,
                    RestockRequestId,
                    StoreId,
                    IngredientId,
                    PurchaseAdviceStatuses.FullyAllocated,
                    false,
                    30m,
                    30m,
                    0m,
                    0m)
            ],
            purchaseOrders:
            [
                new(
                    9301,
                    9302,
                    StoreId,
                    IngredientId,
                    PurchaseOrderStatuses.Approved,
                    RestockRequestId,
                    PurchaseAdviceLineId,
                    30m,
                    0m,
                    0m)
            ]);

        Assert.Equal(30m, item.IncomingQuantity);
        Assert.Equal(70m, item.RawDemand);
        Assert.Equal(0m, item.ProcurementCoveredQuantity);
        Assert.Equal(70m, item.RemainingDemand);
        Assert.Equal(30m, item.IncomingQuantity + item.ProcurementCoveredQuantity);
    }

    [Fact]
    public async Task Draft_po_replaces_lower_stages_and_covers_demand_once()
    {
        var item = await CalculateAsync(
            restocks:
            [
                new(
                    RestockRequestId,
                    StoreId,
                    IngredientId,
                    RestockRequestStatuses.Processing,
                    30m,
                    0m,
                    0m)
            ],
            advice:
            [
                new(
                    PurchaseAdviceLineId,
                    9201,
                    RestockRequestId,
                    StoreId,
                    IngredientId,
                    PurchaseAdviceStatuses.FullyAllocated,
                    false,
                    30m,
                    30m,
                    0m,
                    0m)
            ],
            purchaseOrders:
            [
                new(
                    9301,
                    9302,
                    StoreId,
                    IngredientId,
                    PurchaseOrderStatuses.Draft,
                    RestockRequestId,
                    PurchaseAdviceLineId,
                    30m,
                    0m,
                    0m)
            ]);

        Assert.Equal(0m, item.IncomingQuantity);
        Assert.Equal(30m, item.ProcurementCoveredQuantity);
        Assert.Equal(70m, item.RemainingDemand);
    }

    [Fact]
    public async Task Partial_purchase_order_uses_only_unreceived_unclosed_quantity()
    {
        var item = await CalculateAsync(
            purchaseOrders:
            [
                new(
                    9301,
                    9302,
                    StoreId,
                    IngredientId,
                    PurchaseOrderStatuses.PartiallyReceived,
                    null,
                    null,
                    100m,
                    70m,
                    0m)
            ]);

        Assert.Equal(30m, item.IncomingQuantity);
        Assert.Equal(70m, item.RawDemand);
    }

    private static async Task<
        CafeChain.Application.DTOs.Admin.Procurement.ReorderSuggestionItemDto>
        CalculateAsync(
            IReadOnlyList<ReorderRestockRow>? restocks = null,
            IReadOnlyList<ReorderPurchaseAdviceRow>? advice = null,
            IReadOnlyList<ReorderPurchaseOrderRow>? purchaseOrders = null,
            IReadOnlyList<ReorderAllocationRow>? allocations = null,
            decimal onHand = 0m,
            decimal? minimumStock = 0m,
            decimal consumption = 3000m,
            bool includeUsage = true,
            decimal packageQuantity = 1m,
            int minimumOrderPackageCount = 1,
            bool includeOffer = true,
            bool conversionSucceeds = true)
    {
        var supplier = new Supplier
        {
            SupplierId = 9401,
            Code = "SUP-PIPELINE",
            Name = "Nhà cung cấp pipeline",
            Active = true
        };
        supplier.SupplierStores.Add(new SupplierStore
        {
            SupplierStoreId = 9402,
            SupplierId = supplier.SupplierId,
            StoreId = StoreId,
            Active = true,
            LeadTimeOverrideDays = 1
        });
        var offer = new IngredientSupplier
        {
            IngredientSupplierId = 9403,
            IngredientId = IngredientId,
            SupplierId = supplier.SupplierId,
            UnitId = UnitId,
            PackageQuantity = packageQuantity,
            CurrentPrice = 1m,
            MinimumOrderPackageCount = minimumOrderPackageCount,
            LeadTimeDays = 1,
            IsPrimary = true,
            Active = true,
            UpdatedAt = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
            Supplier = supplier
        };
        var data = new ReorderCalculationData(
            new ReorderStoreRow(StoreId, "Cửa hàng pipeline"),
            [
                new(
                    9501,
                    StoreId,
                    IngredientId,
                    "ING-PIPELINE",
                    "Nguyên liệu pipeline",
                    UnitId,
                    "kg",
                    onHand,
                    0m,
                    minimumStock)
            ],
            includeUsage
                ? new Dictionary<int, ReorderUsageRow>
                {
                    [IngredientId] = new(consumption, 1)
                }
                : new Dictionary<int, ReorderUsageRow>(),
            includeOffer
                ? [new ReorderOfferRow(offer, null, null, null, null, false)]
                : Array.Empty<ReorderOfferRow>(),
            restocks ?? Array.Empty<ReorderRestockRow>(),
            advice ?? Array.Empty<ReorderPurchaseAdviceRow>(),
            purchaseOrders ?? Array.Empty<ReorderPurchaseOrderRow>(),
            allocations ?? Array.Empty<ReorderAllocationRow>());

        var repository = new Mock<IReorderSuggestionRepository>();
        repository
            .Setup(x => x.GetCalculationDataAsync(
                StoreId,
                It.IsAny<IReadOnlyCollection<int>?>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);
        var conversion = new Mock<IPhysicalUnitConversionService>();
        conversion
            .Setup(x => x.ConvertAsync(It.IsAny<decimal>(), UnitId, UnitId))
            .ReturnsAsync((decimal quantity, int _, int _) =>
                conversionSucceeds
                    ? ServiceResult<decimal>.Success(quantity)
                    : ServiceResult<decimal>.Failure("Không có quy đổi."));
        var incoming = new Mock<IReorderIncomingQuantityProvider>();
        incoming
            .Setup(x => x.GetIncomingBaseQuantitiesAsync(
                StoreId,
                It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new Dictionary<int, decimal>());
        var service = new ReorderSuggestionService(
            repository.Object,
            conversion.Object,
            incoming.Object,
            Mock.Of<IScopeAuthorizationService>(),
            Mock.Of<IAIService>(),
            clock: new FixedTimeProvider());

        var result = await service.CalculateForStoreAsync(StoreId);

        Assert.True(result.IsSuccess, result.Message);
        return Assert.Single(result.Data!.Items);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
    }
}
