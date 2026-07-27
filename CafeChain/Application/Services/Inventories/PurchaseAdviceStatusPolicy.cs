using CafeChain.Application.Constants;
using CafeChain.Models.Inventories.Procurement;

namespace CafeChain.Application.Services.Inventories;

public static class PurchaseAdviceStatusPolicy
{
    public static string DeriveLineStatus(PurchaseAdviceLine line, string headerStatus)
    {
        if (headerStatus is PurchaseAdviceStatuses.Rejected or PurchaseAdviceStatuses.Cancelled)
            return headerStatus;

        var requested = Requested(line);
        var allocated = Allocated(line);
        var fulfilled = Fulfilled(line);
        if (allocated <= 0)
            return headerStatus;
        if (allocated < requested)
            return PurchaseAdviceStatuses.PartiallyAllocated;
        if (fulfilled <= 0)
            return PurchaseAdviceStatuses.FullyAllocated;
        return fulfilled < requested
            ? PurchaseAdviceStatuses.PartiallyFulfilled
            : PurchaseAdviceStatuses.Completed;
    }

    public static string DeriveHeaderStatus(PurchaseAdvice advice)
    {
        if (advice.Status is PurchaseAdviceStatuses.Rejected or PurchaseAdviceStatuses.Cancelled)
            return advice.Status;
        if (advice.Lines.Count == 0)
            return advice.Status;
        if (advice.Lines.All(x => Fulfilled(x) >= Requested(x)))
            return PurchaseAdviceStatuses.Completed;
        if (advice.Lines.Any(x => Fulfilled(x) > 0))
            return PurchaseAdviceStatuses.PartiallyFulfilled;
        if (advice.Lines.All(x => Allocated(x) >= Requested(x)))
            return PurchaseAdviceStatuses.FullyAllocated;
        if (advice.Lines.Any(x => Allocated(x) > 0))
            return PurchaseAdviceStatuses.PartiallyAllocated;
        return advice.Status;
    }

    private static bool UsesProcurementContract(PurchaseAdviceLine line) =>
        line.RequestedProcurementQuantity.HasValue
        && line.RequestedProcurementQuantity.Value > 0
        && line.ProcurementUnitId.HasValue;

    private static decimal Requested(PurchaseAdviceLine line) =>
        UsesProcurementContract(line)
            ? line.RequestedProcurementQuantity!.Value
            : line.RequestedPurchaseBaseQuantity;

    private static decimal Allocated(PurchaseAdviceLine line) =>
        UsesProcurementContract(line)
            ? line.AllocatedToPoProcurementQuantity
            : line.AllocatedToPoBaseQuantity;

    private static decimal Fulfilled(PurchaseAdviceLine line) =>
        UsesProcurementContract(line)
            ? line.AcceptedProcurementQuantity + line.ClosedProcurementQuantity
            : line.AcceptedBaseQuantity + line.ClosedBaseQuantity;
}
