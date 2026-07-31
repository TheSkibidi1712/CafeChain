namespace CafeChain.Application.Constants;

public static class RestockRequestSourceTypes
{
    public const string ReorderSuggestion = "REORDER_SUGGESTION";
    public const string StockAlert = "StockAlert";
    public const string ManualByStore = "ManualByStore";
    public const string CentralPlanner = "CentralPlanner";
    public const string StockCountVariance = "StockCountVariance";
    public const string PromotionOrEvent = "PromotionOrEvent";
    public const string Forecast = "Forecast";
    public const string DirectPurchaseProposal = "DirectPurchaseProposal";
    public const string Legacy = "Legacy";
}

public static class RestockSourcingDecisionTypes
{
    public const string Transfer = "TRANSFER";
    public const string Purchase = "PURCHASE";
    public const string Production = "PRODUCTION";
    public const string Reject = "REJECT";

    public static readonly string[] All = { Transfer, Purchase, Production, Reject };
}

public static class RestockSourcingStatuses
{
    public const string Unallocated = "UNALLOCATED";
    public const string PartiallyAllocated = "PARTIALLY_ALLOCATED";
    public const string FullyAllocated = "FULLY_ALLOCATED";
}

public static class RestockSourcingAllocationStatuses
{
    public const string Active = "ACTIVE";
    public const string PendingPurchaseAdvice = "PENDING_PURCHASE";
    public const string Released = "RELEASED";
    public const string Cancelled = "CANCELLED";
}

public static class ProcurementUnitCodes
{
    public const string Kilogram = "kg";
    public const string Liter = "l";
    public const string Piece = "pcs";
}
