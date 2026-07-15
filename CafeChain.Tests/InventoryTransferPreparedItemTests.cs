using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.DTOs.Systems;
using CafeChain.Application.DTOs.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.DTOs.Admin.Actor;
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
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CafeChain.Tests;

public sealed class InventoryTransferPreparedItemTests
{
    [Fact]
    public async Task PreparedItemDispatchAndPartialReceive_PreserveIdentityCostAndReplayIsNoOp()
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
        repository.Setup(x => x.GetTransferForUpdateAsync(transferId)).ReturnsAsync(transfer);
        repository.Setup(x => x.LockInventoriesAsync(
                It.IsAny<IEnumerable<(int StoreId, int? IngredientId, int? PreparedItemId)>>()))
            .Returns(Task.CompletedTask);
        repository.Setup(x => x.GetPreparedItemAsync(preparedItemId)).ReturnsAsync(item);
        repository.Setup(x => x.GetAccountIdForStaffAsync(7)).ReturnsAsync(70);
        repository.Setup(x => x.GetOrCreatePreparedItemInventoryForUpdateAsync(1, preparedItemId, 70, It.IsAny<string>()))
            .ReturnsAsync(source);
        repository.Setup(x => x.GetOrCreatePreparedItemInventoryForUpdateAsync(2, preparedItemId, 70, It.IsAny<string>()))
            .ReturnsAsync(destination);
        repository.Setup(x => x.GetAvailablePreparedItemCostLayersAsync(1, preparedItemId))
            .ReturnsAsync([sourceLayer]);
        var transferAllocations = new List<InventoryTransferCostAllocation>();
        repository.Setup(x => x.AddTransferCostAllocationsAsync(It.IsAny<IEnumerable<InventoryTransferCostAllocation>>()))
            .Callback<IEnumerable<InventoryTransferCostAllocation>>(rows =>
            {
                foreach (var row in rows)
                {
                    row.InventoryTransferCostAllocationId = transferAllocations.Count + 1;
                    transferAllocations.Add(row);
                }
            })
            .Returns(Task.CompletedTask);
        repository.Setup(x => x.GetTransferCostAllocationsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(() => transferAllocations);
        repository.Setup(x => x.AddBranchReceiptAsync(It.IsAny<BranchReceipt>()))
            .Returns(Task.CompletedTask);

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

        var issuePolicy = new Mock<IInventoryIssuePolicy>();
        issuePolicy.Setup(x => x.EvaluateAsync(It.IsAny<InventoryIssueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryIssueRequest request, CancellationToken _) => new InventoryIssueDecision(
                InventoryIssueOutcome.Allowed,
                InventoryIssueReasonCodes.NonNegativeIssueAllowed,
                request.BeforeAvailableQty,
                request.IssueQty,
                request.BeforeAvailableQty - request.IssueQty,
                0,
                0,
                false,
                false,
                string.Empty));

        var posting = new Mock<IRestockFulfillmentPostingService>();
        posting.Setup(x => x.RegisterAsync(It.IsAny<RegisterRestockFulfillmentPostingCommand>()))
            .ReturnsAsync(ServiceResult<RestockFulfillmentPostingResult>.Success(new()
            {
                FulfilledQuantity = 30m,
                TargetQuantity = 30m,
                RequestStatus = RestockRequestStatuses.Completed
            }));
        var costConsumption = new Mock<IInventoryCostLayerConsumptionService>();
        costConsumption.Setup(x => x.PlanConsumeAsync(1, null, preparedItemId, 30m, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<CostLayerConsumptionPlan>.Success(new CostLayerConsumptionPlan
            {
                StoreId = 1,
                PreparedItemId = preparedItemId,
                RequiredQuantity = 30m,
                CoveredQuantity = 30m,
                AvailableLayerQuantity = 100m,
                TotalCost = 120m,
                WeightedUnitCost = 4m,
                IsFullyCovered = true,
                Slices =
                [
                    new CostLayerAllocationSlice
                    {
                        Layer = sourceLayer,
                        InventoryCostLayerId = sourceLayer.InventoryCostLayerId,
                        Quantity = 30m,
                        UnitCost = 4m,
                        TotalCost = 120m
                    }
                ]
            }));
        costConsumption.Setup(x => x.ApplyPlan(It.IsAny<CostLayerConsumptionPlan>()))
            .Callback<CostLayerConsumptionPlan>(plan =>
            {
                foreach (var slice in plan.Slices)
                    slice.Layer.RemainingQuantity -= slice.Quantity;
            });

        var alerts = new Mock<IStockAlertService>();
        alerts.Setup(x => x.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(ServiceResult<StockAlertEvaluationResultDto>.Success(new()));
        var user = new Mock<IUserContext>();
        user.SetupGet(x => x.StaffId).Returns(7);
        var actor = new Mock<IAdminActorContextAccessor>();
        actor.Setup(x => x.Get(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns(new AdminActorContext { StaffId = 7 });
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(7, It.IsAny<int>())).ReturnsAsync(true);

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
            issuePolicy.Object,
            costConsumption.Object,
            posting.Object,
            alerts.Object,
            user.Object,
            actor.Object,
            scope.Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        var first = await service.ConfirmAsync(transferId, "confirm-1");
        var replay = await service.ConfirmAsync(transferId, "confirm-2");

        Assert.Equal(InventoryTransferStatus.DISPATCHED, first.Status);
        Assert.Equal(InventoryTransferStatus.DISPATCHED, replay.Status);
        Assert.Equal(70m, source.AvailableQty);
        Assert.Equal(0m, destination.AvailableQty);
        Assert.Equal(70m, sourceLayer.RemainingQuantity);
        var outbound = Assert.Single(transactions, tx => tx.Type == InventoryTransactionTypeEnum.OUT_TRANSFER);
        Assert.Equal(detailId, outbound.InventoryTransferDetailId);
        Assert.Equal(source.StoreInventoryId, outbound.StoreInventoryId);
        Assert.Empty(destinationLayers);
        posting.Verify(x => x.RegisterAsync(It.IsAny<RegisterRestockFulfillmentPostingCommand>()), Times.Never);
        repository.Verify(x => x.AddInventoryTransactionAsync(It.IsAny<InventoryTransaction>()), Times.Once);
        repository.Verify(x => x.AddCostLayerAsync(It.IsAny<InventoryCostLayer>()), Times.Never);

        var partial = await service.ReceiveAsync(transferId, new InventoryTransferReceiveDTO
        {
            RequestKey = "receive-1",
            Lines =
            [
                new InventoryTransferReceiveLineDTO
                {
                    InventoryTransferDetailId = detailId,
                    ReceivedBaseQuantity = 10m
                }
            ]
        });
        Assert.Equal(InventoryTransferStatus.DISPATCHED, partial.Status);
        Assert.Equal(10m, destination.AvailableQty);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReceiveAsync(
            transferId,
            new InventoryTransferReceiveDTO
            {
                RequestKey = "receive-over",
                Lines =
                [
                    new InventoryTransferReceiveLineDTO
                    {
                        InventoryTransferDetailId = detailId,
                        ReceivedBaseQuantity = 21m
                    }
                ]
            }));

        var completed = await service.ReceiveAsync(transferId, new InventoryTransferReceiveDTO
        {
            RequestKey = "receive-2",
            Lines =
            [
                new InventoryTransferReceiveLineDTO
                {
                    InventoryTransferDetailId = detailId,
                    ReceivedBaseQuantity = 20m
                }
            ]
        });
        Assert.Equal(InventoryTransferStatus.COMPLETED, completed.Status);
        Assert.Equal(30m, destination.AvailableQty);
        Assert.Equal(30m, transferAllocations.Single().ReceivedQuantity);
        Assert.Equal(2, destinationLayers.Count);
        Assert.All(destinationLayers, layer => Assert.Equal(4m, layer.UnitCost));
        Assert.Equal(2, transactions.Count(tx => tx.Type == InventoryTransactionTypeEnum.IN_TRANSFER));
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
