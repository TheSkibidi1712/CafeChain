using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using Moq;

namespace CafeChain.Tests;

public sealed class InventoryDocumentMoneylessCostTests
{
    [Theory]
    [InlineData(InventoryDocumentType.WASTE, 2, 10, 2)]
    [InlineData(InventoryDocumentType.STOCK_TAKE, 0, 5, 5)]
    public async Task MoneylessDocument_StoresZeroDetailMoney_ButKeepsActualLedgerCost(
        InventoryDocumentType type,
        decimal actualQuantity,
        decimal beforeQuantity,
        decimal issuedQuantity)
    {
        var repository = new Mock<IAdminInventoryDocumentRepository>();
        var policy = new Mock<IInventoryIssuePolicy>();
        var costing = new Mock<IInventoryCostLayerConsumptionService>();
        var inventory = new StoreInventory
        {
            StoreInventoryId = 41,
            StoreId = 2,
            IngredientId = 7,
            AvailableQty = beforeQuantity,
            ReservedQty = 3
        };
        var detail = new InventoryDocumentDetail
        {
            InventoryDocumentDetailId = 19,
            IngredientId = 7,
            UnitId = 1,
            Quantity = actualQuantity,
            BaseQuantity = actualQuantity,
            UnitPrice = 999,
            TotalAmount = 999,
            CostPrice = 999,
            CostAmount = 999
        };
        var document = new InventoryDocument
        {
            InventoryDocumentId = 11,
            StoreId = 2,
            Type = type,
            Purpose = type == InventoryDocumentType.WASTE
                ? InventoryDocumentPurpose.DAMAGED
                : InventoryDocumentPurpose.STOCK_TAKE,
            Details = [detail]
        };

        repository.Setup(x => x.GetStoreInventoryForUpdateAsync(2, 7))
            .ReturnsAsync(inventory);
        repository.Setup(x => x.GetNegativeApprovalForUpdateAsync(11))
            .ReturnsAsync((CafeChain.Models.Inventories.Approvals.InventoryNegativeApproval?)null);
        policy.Setup(x => x.EvaluateAsync(It.IsAny<InventoryIssueRequest>(), It.IsAny<CancellationToken>()))
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
                "strict-v1"));
        costing.Setup(x => x.PlanConsumeAsync(2, 7, null, issuedQuantity, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<CostLayerConsumptionPlan>.Success(new CostLayerConsumptionPlan
            {
                StoreId = 2,
                IngredientId = 7,
                RequiredQuantity = issuedQuantity,
                CoveredQuantity = issuedQuantity,
                AvailableLayerQuantity = issuedQuantity,
                TotalCost = issuedQuantity * 4,
                WeightedUnitCost = 4,
                IsFullyCovered = true,
                Slices = []
            }));

        InventoryTransaction? transaction = null;
        repository.Setup(x => x.AddInventoryTransactionAsync(It.IsAny<InventoryTransaction>()))
            .Callback<InventoryTransaction>(x => transaction = x)
            .Returns(Task.CompletedTask);

        var service = new AdminInventoryDocumentProcessService(
            repository.Object,
            policy.Object,
            costing.Object);

        await service.ExecuteProcessAsync(document);

        Assert.Equal(0, detail.UnitPrice);
        Assert.Equal(0, detail.TotalAmount);
        Assert.Equal(0, detail.CostPrice);
        Assert.Equal(0, detail.CostAmount);
        Assert.NotNull(transaction);
        Assert.Equal(4, transaction!.UnitCost);
        Assert.Equal(issuedQuantity * 4, transaction.TotalCost);
        Assert.Equal(beforeQuantity - issuedQuantity, transaction.AfterQty);
    }
}
