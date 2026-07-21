using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Transfers;

namespace CafeChain.Application.Services.Admin.InventoryTransfers;

public sealed record InventoryTransferLineAuthority(
    decimal Requested,
    decimal Dispatched,
    decimal DestinationAccepted,
    decimal DestinationRejected,
    decimal ReturnRequested,
    decimal ReturnedToSource,
    decimal WrittenOff,
    decimal ClosedShortage)
{
    public decimal ResolvedOutsideDestination => ReturnedToSource + WrittenOff + ClosedShortage;
    public decimal InTransitOpen => Math.Max(0, Dispatched - DestinationAccepted - ResolvedOutsideDestination);
    public decimal PendingReturn => Math.Max(0, ReturnRequested - ReturnedToSource);

    public string Status => InTransitOpen <= 0
        ? ReturnedToSource == 0 && WrittenOff == 0 && ClosedShortage == 0
            ? "RESOLVED_ACCEPTED"
            : ReturnedToSource > 0 && WrittenOff == 0 && ClosedShortage == 0
            ? "RESOLVED_RETURNED"
            : WrittenOff > 0 && ReturnedToSource == 0 && ClosedShortage == 0
                ? "RESOLVED_WRITTEN_OFF"
                : ClosedShortage > 0 && ReturnedToSource == 0 && WrittenOff == 0
                    ? "RESOLVED_CLOSED"
                    : "RESOLVED_MIXED"
        : PendingReturn > 0
            ? "RETURN_IN_TRANSIT"
            : DestinationAccepted > 0 || DestinationRejected > 0
                ? "WAITING_FOR_REMAINDER"
                : "OPEN";
}

public static class InventoryTransferQuantityAuthority
{
    public static InventoryTransferLineAuthority Calculate(
        InventoryTransferDetail detail,
        IEnumerable<InventoryTransferDiscrepancyPosting> postings)
    {
        var rows = postings.Where(x => x.InventoryTransferDetailId == detail.InventoryTransferDetailId).ToList();
        return new InventoryTransferLineAuthority(
            detail.BaseQuantity,
            detail.DispatchedBaseQuantity,
            detail.ReceivedBaseQuantity,
            Sum(rows, InventoryTransferDiscrepancyPostingType.DESTINATION_REJECTED),
            Sum(rows, InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED),
            Sum(rows, InventoryTransferDiscrepancyPostingType.RETURNED_TO_SOURCE),
            Sum(rows, InventoryTransferDiscrepancyPostingType.WRITTEN_OFF),
            Sum(rows, InventoryTransferDiscrepancyPostingType.CLOSED_SHORTAGE));
    }

    public static decimal AllocationClassifiedQuantity(
        InventoryTransferCostAllocation allocation,
        IEnumerable<InventoryTransferDiscrepancyPosting> postings)
    {
        var rows = postings.Where(x =>
            x.InventoryTransferCostAllocationId == allocation.InventoryTransferCostAllocationId).ToList();
        var rejected = Sum(rows, InventoryTransferDiscrepancyPostingType.DESTINATION_REJECTED);
        var resolved = Sum(rows, InventoryTransferDiscrepancyPostingType.RETURNED_TO_SOURCE)
            + Sum(rows, InventoryTransferDiscrepancyPostingType.WRITTEN_OFF)
            + Sum(rows, InventoryTransferDiscrepancyPostingType.CLOSED_SHORTAGE);
        return allocation.ReceivedQuantity + Math.Max(rejected, resolved);
    }

    public static decimal AllocationReturnableQuantity(
        InventoryTransferCostAllocation allocation,
        IEnumerable<InventoryTransferDiscrepancyPosting> postings)
    {
        var rows = postings.Where(x =>
            x.InventoryTransferCostAllocationId == allocation.InventoryTransferCostAllocationId).ToList();
        var rejected = Sum(rows, InventoryTransferDiscrepancyPostingType.DESTINATION_REJECTED);
        var resolved = Sum(rows, InventoryTransferDiscrepancyPostingType.RETURNED_TO_SOURCE)
            + Sum(rows, InventoryTransferDiscrepancyPostingType.WRITTEN_OFF)
            + Sum(rows, InventoryTransferDiscrepancyPostingType.CLOSED_SHORTAGE);
        var returned = Sum(rows, InventoryTransferDiscrepancyPostingType.RETURNED_TO_SOURCE);
        var requested = Sum(rows, InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED);
        var pendingRequest = Math.Max(0, requested - returned);
        return Math.Max(0, rejected - resolved - pendingRequest);
    }

    public static decimal AllocationPendingReturnQuantity(
        InventoryTransferCostAllocation allocation,
        IEnumerable<InventoryTransferDiscrepancyPosting> postings)
    {
        var rows = postings.Where(x =>
            x.InventoryTransferCostAllocationId == allocation.InventoryTransferCostAllocationId).ToList();
        return Math.Max(0,
            Sum(rows, InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED)
            - Sum(rows, InventoryTransferDiscrepancyPostingType.RETURNED_TO_SOURCE));
    }

    private static decimal Sum(
        IEnumerable<InventoryTransferDiscrepancyPosting> postings,
        InventoryTransferDiscrepancyPostingType type) =>
        postings.Where(x => x.PostingType == type).Sum(x => x.Quantity);
}
