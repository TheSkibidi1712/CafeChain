using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.DTOs.Systems;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.InventoryTransfers;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Stores;
using CafeChain.Models.Systems;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class InventoryTransferPreparedItemTests
{
    [Fact]
    public async Task PreparedItemConfirm_PreservesIdentityCostAndLineAudit_AndReplayIsNoOp()
    {
        const int transferId = 21;
        const int detailId = 55;
        const int preparedItemId = 9;
        const int restockRequestId = 31;
        const int unitId = 3;
        var unit = new Unit { UnitId = unitId, UnitCode = "ml", Name = "ml", Active = true };
        var item = new PreparedItem
        {
            PreparedItemId = preparedItemId,
            Code = "PI-TEST",
            Name = "Sốt nền",
            BaseUnitId = unitId,
            BaseUnit = unit,
            Active = true
        };
        var detail = new InventoryTransferDetail
        {
            InventoryTransferDetailId = detailId,
            InventoryTransferId = transferId,
            PreparedItemId = preparedItemId,
            PreparedItem = item,
            IngredientId = null,
            RestockRequestId = restockRequestId,
            UnitId = unitId,
            Unit = unit,
            Quantity = 30m,
            BaseQuantity = 30m,
            UnitPrice = 4m
        };
        var transfer = new InventoryTransfer
        {
            InventoryTransferId = transferId,
            Code = "CK-TEST-21",
            RequestKey = "draft-key",
            FromStoreId = 1,
            ToStoreId = 2,
            Status = InventoryTransferStatus.DRAFT,
            CreatedByStaffId = 7,
            CreatedAt = DateTime.UtcNow,
            Details = [detail]
        };
        var source = CanonicalInventory(101, 1, preparedItemId, 100m);
        var destination = CanonicalInventory(102, 2, preparedItemId, 0m);
        var sourceLayer = new InventoryCostLayer
        {
            InventoryCostLayerId = 201,
            StoreId = 1,
            PreparedItemId = preparedItemId,
            Quantity = 100m,
            RemainingQuantity = 100m,
            UnitCost = 4m,
            CreatedAt = DateTime.UtcNow
        };

        var repository = new Mock<IAdminInventoryTransferRepository>();
        repository.Setup(x => x.GetTransferByIdAsync(transferId)).ReturnsAsync(transfer);
        repository.Setup(x => x.GetPreparedItemAsync(preparedItemId)).ReturnsAsync(item);
        repository.Setup(x => x.GetAccountIdForStaffAsync(7)).ReturnsAsync(70);
        repository.Setup(x => x.GetOrCreatePreparedItemInventoryForUpdateAsync(1, preparedItemId, 70, It.IsAny<string>()))
            .ReturnsAsync(source);
        repository.Setup(x => x.GetOrCreatePreparedItemInventoryForUpdateAsync(2, preparedItemId, 70, It.IsAny<string>()))
            .ReturnsAsync(destination);
        repository.Setup(x => x.GetAvailablePreparedItemCostLayersAsync(1, preparedItemId))
            .ReturnsAsync([sourceLayer]);

        var dedup = new Mock<IRequestDeduplicationService>();
        dedup.Setup(x => x.BeginAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                7,
                It.IsAny<object>(),
                transferId))
            .ReturnsAsync(() => new RequestDeduplicationBeginResult
            {
                CanProcess = true,
                Entry = new RequestDeduplication { RequestDeduplicationId = 1 }
            });

        var negative = new Mock<INegativeInventoryService>();
        negative.Setup(x => x.ValidateIssueAsync(source, 30m, item.Name))
            .ReturnsAsync(new NegativeStockValidationResult
            {
                IsAllowed = true,
                BeforeQty = 100m,
                IssueQuantity = 30m,
                AfterQty = 70m,
                StockStatus = InventoryStockStatus.NORMAL
            });

        var posting = new Mock<IRestockFulfillmentPostingService>();
        posting.Setup(x => x.RegisterAsync(It.IsAny<RegisterRestockFulfillmentPostingCommand>()))
            .ReturnsAsync(ServiceResult<RestockFulfillmentPostingResult>.Success(new()
            {
                FulfilledQuantity = 30m,
                TargetQuantity = 30m,
                RequestStatus = RestockRequestStatuses.Completed
            }));

        var alerts = new Mock<IStockAlertService>();
        alerts.Setup(x => x.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(ServiceResult<StockAlertEvaluationResultDto>.Success(new()));
        var user = new Mock<IUserContext>();
        user.SetupGet(x => x.StaffId).Returns(7);

        var transactions = new List<InventoryTransaction>();
        var destinationLayers = new List<InventoryCostLayer>();
        repository.Setup(x => x.AddInventoryTransactionAsync(It.IsAny<InventoryTransaction>()))
            .Callback<InventoryTransaction>(transactions.Add)
            .Returns(Task.CompletedTask);
        repository.Setup(x => x.AddCostLayerAsync(It.IsAny<InventoryCostLayer>()))
            .Callback<InventoryCostLayer>(destinationLayers.Add)
            .Returns(Task.CompletedTask);

        var service = new AdminInventoryTransferService(
            repository.Object,
            dedup.Object,
            negative.Object,
            posting.Object,
            alerts.Object,
            user.Object);

        var first = await service.ConfirmAsync(transferId, "confirm-1");
        var replay = await service.ConfirmAsync(transferId, "confirm-2");

        Assert.Equal(InventoryTransferStatus.COMPLETED, first.Status);
        Assert.Equal(InventoryTransferStatus.COMPLETED, replay.Status);
        Assert.Equal(70m, source.AvailableQty);
        Assert.Equal(30m, destination.AvailableQty);
        Assert.Equal(70m, sourceLayer.RemainingQuantity);
        var inbound = Assert.Single(transactions, tx => tx.Type == InventoryTransactionTypeEnum.IN_TRANSFER);
        Assert.Equal(detailId, inbound.InventoryTransferDetailId);
        Assert.Equal(destination.StoreInventoryId, inbound.StoreInventoryId);
        var outbound = Assert.Single(transactions, tx => tx.Type == InventoryTransactionTypeEnum.OUT_TRANSFER);
        Assert.Equal(detailId, outbound.InventoryTransferDetailId);
        Assert.Equal(source.StoreInventoryId, outbound.StoreInventoryId);
        var destinationLayer = Assert.Single(destinationLayers);
        Assert.Null(destinationLayer.IngredientId);
        Assert.Equal(preparedItemId, destinationLayer.PreparedItemId);
        Assert.Equal(30m, destinationLayer.Quantity);
        Assert.Equal(4m, destinationLayer.UnitCost);
        posting.Verify(x => x.RegisterAsync(It.Is<RegisterRestockFulfillmentPostingCommand>(c =>
            c.SourceDocumentType == RestockFulfillmentDocumentTypes.InventoryTransfer
            && c.SourceDocumentId == transferId
            && c.SourceDocumentLineId == detailId
            && c.RestockRequestId == restockRequestId
            && c.PreparedItemId == preparedItemId
            && c.IngredientId == null)), Times.Once);
        repository.Verify(x => x.AddInventoryTransactionAsync(It.IsAny<InventoryTransaction>()), Times.Exactly(2));
        repository.Verify(x => x.AddCostLayerAsync(It.IsAny<InventoryCostLayer>()), Times.Once);
    }

    private static StoreInventory CanonicalInventory(
        int inventoryId,
        int storeId,
        int preparedItemId,
        decimal quantity) => new()
    {
        StoreInventoryId = inventoryId,
        StoreId = storeId,
        PreparedItemId = preparedItemId,
        IngredientId = null,
        RecipeId = null,
        BtpIdentityState = BtpIdentityState.Canonical,
        QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
        AvailableQty = quantity,
        ReservedQty = 0m,
        LastUpdated = DateTime.UtcNow
    };
}
