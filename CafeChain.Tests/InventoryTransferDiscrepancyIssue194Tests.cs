using CafeChain.Application.Services.Admin.InventoryTransfers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Transfers;
using Xunit;

namespace CafeChain.Tests;

public sealed class InventoryTransferDiscrepancyIssue194Tests
{
    [Fact]
    public void PartialReceipt_LeavesWaitingRemainder_AndSecondReceiptCanComplete()
    {
        var detail = Detail(10m, 10m, 8m);
        var partial = InventoryTransferQuantityAuthority.Calculate(detail, []);

        Assert.Equal(8m, partial.DestinationAccepted);
        Assert.Equal(2m, partial.InTransitOpen);
        Assert.Equal("WAITING_FOR_REMAINDER", partial.Status);

        detail.ReceivedBaseQuantity = 10m;
        var completed = InventoryTransferQuantityAuthority.Calculate(detail, []);
        Assert.Equal(0m, completed.InTransitOpen);
        Assert.Equal("RESOLVED_ACCEPTED", completed.Status);
    }

    [Fact]
    public void Rejected_DoesNotBecomeAcceptedOrFulfilledQuantity()
    {
        var detail = Detail(10m, 10m, 8m);
        var postings = new[]
        {
            Posting(detail, InventoryTransferDiscrepancyPostingType.DESTINATION_REJECTED, 2m)
        };

        var authority = InventoryTransferQuantityAuthority.Calculate(detail, postings);

        Assert.Equal(8m, authority.DestinationAccepted);
        Assert.Equal(2m, authority.DestinationRejected);
        Assert.Equal(2m, authority.InTransitOpen);
        Assert.Equal("WAITING_FOR_REMAINDER", authority.Status);
    }

    [Fact]
    public void ReturnRequest_DoesNotIncreaseStock_AndPendingReturnIsVisible()
    {
        var detail = Detail(10m, 10m, 8m);
        var postings = new[]
        {
            Posting(detail, InventoryTransferDiscrepancyPostingType.DESTINATION_REJECTED, 2m),
            Posting(detail, InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED, 2m)
        };

        var authority = InventoryTransferQuantityAuthority.Calculate(detail, postings);

        Assert.Equal(2m, authority.PendingReturn);
        Assert.Equal(2m, authority.InTransitOpen);
        Assert.Equal("RETURN_IN_TRANSIT", authority.Status);
    }

    [Fact]
    public void ConfirmedReturn_ResolvesShortage_WithOriginalCostEvidence()
    {
        var detail = Detail(10m, 10m, 8m);
        var postings = new[]
        {
            Posting(detail, InventoryTransferDiscrepancyPostingType.DESTINATION_REJECTED, 2m, 12m),
            Posting(detail, InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED, 2m, 12m),
            Posting(detail, InventoryTransferDiscrepancyPostingType.RETURNED_TO_SOURCE, 2m, 12m)
        };

        var authority = InventoryTransferQuantityAuthority.Calculate(detail, postings);

        Assert.Equal(2m, authority.ReturnedToSource);
        Assert.Equal(0m, authority.InTransitOpen);
        Assert.Equal("RESOLVED_RETURNED", authority.Status);
        Assert.All(postings.Where(x => x.PostingType == InventoryTransferDiscrepancyPostingType.RETURNED_TO_SOURCE),
            x => Assert.Equal(12m, x.UnitCost));
    }

    [Theory]
    [InlineData(InventoryTransferDiscrepancyPostingType.WRITTEN_OFF, "RESOLVED_WRITTEN_OFF")]
    [InlineData(InventoryTransferDiscrepancyPostingType.CLOSED_SHORTAGE, "RESOLVED_CLOSED")]
    public void WriteOffOrCloseShortage_ResolvesWithoutInventoryIncrease(
        InventoryTransferDiscrepancyPostingType type,
        string expectedStatus)
    {
        var detail = Detail(10m, 10m, 8m);
        var authority = InventoryTransferQuantityAuthority.Calculate(
            detail,
            [Posting(detail, type, 2m)]);

        Assert.Equal(2m, authority.ResolvedOutsideDestination);
        Assert.Equal(0m, authority.InTransitOpen);
        Assert.Equal(expectedStatus, authority.Status);
    }

    [Fact]
    public void AllocationAuthority_PreventsOverReceiptAndTracksReturnableRejected()
    {
        var detail = Detail(10m, 10m, 8m);
        var allocation = new InventoryTransferCostAllocation
        {
            InventoryTransferCostAllocationId = 1,
            InventoryTransferDetailId = detail.InventoryTransferDetailId,
            Quantity = 10m,
            ReceivedQuantity = 8m,
            UnitCost = 12m
        };
        var rejected = Posting(detail, InventoryTransferDiscrepancyPostingType.DESTINATION_REJECTED, 2m, 12m);
        rejected.InventoryTransferCostAllocationId = allocation.InventoryTransferCostAllocationId;

        Assert.Equal(10m, InventoryTransferQuantityAuthority.AllocationClassifiedQuantity(allocation, [rejected]));
        Assert.Equal(2m, InventoryTransferQuantityAuthority.AllocationReturnableQuantity(allocation, [rejected]));
    }

    [Fact]
    public void FollowUpTransfer_UsesExplicitParentLinkage()
    {
        var parent = new InventoryTransfer { InventoryTransferId = 10, Code = "CK-001" };
        var followUp = new InventoryTransfer { InventoryTransferId = 11, ParentInventoryTransfer = parent };
        var line = new InventoryTransferDetail
        {
            InventoryTransferDetailId = 12,
            ParentInventoryTransferDetail = new InventoryTransferDetail { InventoryTransferDetailId = 5 }
        };
        followUp.Details.Add(line);

        Assert.Same(parent, followUp.ParentInventoryTransfer);
        Assert.Equal(5, line.ParentInventoryTransferDetail!.InventoryTransferDetailId);
    }

    private static InventoryTransferDetail Detail(decimal requested, decimal dispatched, decimal accepted) => new()
    {
        InventoryTransferDetailId = 7,
        BaseQuantity = requested,
        DispatchedBaseQuantity = dispatched,
        ReceivedBaseQuantity = accepted
    };

    private static InventoryTransferDiscrepancyPosting Posting(
        InventoryTransferDetail detail,
        InventoryTransferDiscrepancyPostingType type,
        decimal quantity,
        decimal unitCost = 10m) => new()
    {
        InventoryTransferDetailId = detail.InventoryTransferDetailId,
        InventoryTransferCostAllocationId = 1,
        PostingType = type,
        Quantity = quantity,
        UnitCost = unitCost,
        TotalCost = quantity * unitCost,
        RequestKey = Guid.NewGuid().ToString("N"),
        Reason = "test",
        ActorStaffId = 1
    };
}
