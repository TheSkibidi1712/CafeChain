using CafeChain.Application.Constants;
using CafeChain.Models.Inventories.Procurement;

namespace CafeChain.Application.Services.Inventories;

public static class PurchaseAdviceStatusPolicy
{
    public static string DeriveLineStatus(PurchaseAdviceLine line, string headerStatus)
    {
        if (headerStatus is PurchaseAdviceStatuses.Rejected or PurchaseAdviceStatuses.Cancelled)
            return headerStatus;
        if (line.AllocatedToPoBaseQuantity <= 0)
            return headerStatus;
        if (line.AllocatedToPoBaseQuantity < line.RequestedPurchaseBaseQuantity)
            return PurchaseAdviceStatuses.PartiallyAllocated;
        var fulfilled = line.AcceptedBaseQuantity + line.ClosedBaseQuantity;
        if (fulfilled <= 0)
            return PurchaseAdviceStatuses.FullyAllocated;
        return fulfilled < line.RequestedPurchaseBaseQuantity
            ? PurchaseAdviceStatuses.PartiallyFulfilled
            : PurchaseAdviceStatuses.Completed;
    }

    public static string DeriveHeaderStatus(PurchaseAdvice advice)
    {
        if (advice.Status is PurchaseAdviceStatuses.Rejected or PurchaseAdviceStatuses.Cancelled)
            return advice.Status;
        if (advice.Lines.Count == 0)
            return advice.Status;
        if (advice.Lines.All(x => x.AcceptedBaseQuantity + x.ClosedBaseQuantity >= x.RequestedPurchaseBaseQuantity))
            return PurchaseAdviceStatuses.Completed;
        if (advice.Lines.Any(x => x.AcceptedBaseQuantity + x.ClosedBaseQuantity > 0))
            return PurchaseAdviceStatuses.PartiallyFulfilled;
        if (advice.Lines.All(x => x.AllocatedToPoBaseQuantity >= x.RequestedPurchaseBaseQuantity))
            return PurchaseAdviceStatuses.FullyAllocated;
        if (advice.Lines.Any(x => x.AllocatedToPoBaseQuantity > 0))
            return PurchaseAdviceStatuses.PartiallyAllocated;
        return advice.Status;
    }
}
