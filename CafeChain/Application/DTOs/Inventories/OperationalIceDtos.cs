namespace CafeChain.Application.DTOs.Inventories;

public sealed class SaveIcePolicyRequest
{
    public int StoreId { get; init; }
    public int IngredientId { get; init; }
    public int DisplayUnitId { get; init; }
    public decimal SuggestedDailyQuantity { get; init; }
    public decimal SuggestedShiftQuantity { get; init; }
    public bool AllowSupplementalIssue { get; init; } = true;
    public bool AllowSameDayCarryOver { get; init; } = true;
    public bool RequireVarianceApproval { get; init; } = true;
    public decimal VarianceApprovalQuantityThreshold { get; init; }
    public decimal VarianceApprovalPercentThreshold { get; init; }
}

public sealed class CreateOperationalShiftRequest
{
    public int StoreId { get; init; }
    public DateTime BusinessDate { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }
    public int? ShiftLeadId { get; init; }
}

public sealed class OpenIceAllocationRequest
{
    public int OperationalShiftId { get; init; }
    public decimal InitialIssuedQuantity { get; init; }
    public IReadOnlyList<int> WorkShiftIds { get; init; } = Array.Empty<int>();
}

public sealed class LinkOperationalWorkShiftRequest
{
    public int OperationalShiftId { get; init; }
    public int WorkShiftId { get; init; }
}

public sealed class RequestSupplementalIceRequest
{
    public int IceAllocationId { get; init; }
    public decimal Quantity { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class DecideSupplementalIceRequest
{
    public Guid SupplementalIssuePublicId { get; init; }
    public bool Approve { get; init; }
    public string? RejectionReason { get; init; }
}

public sealed class OperationalShiftSummaryDto
{
    public int OperationalShiftId { get; init; }
    public int StoreId { get; init; }
    public DateTime BusinessDate { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int? ShiftLeadId { get; init; }
}

public sealed class IceAllocationDto
{
    public int IceAllocationId { get; init; }
    public Guid PublicId { get; init; }
    public int OperationalShiftId { get; init; }
    public int StoreId { get; init; }
    public int IngredientId { get; init; }
    public decimal InitialIssuedQuantity { get; init; }
    public decimal SupplementalIssuedQuantity { get; init; }
    public decimal TheoreticalUsageQuantity { get; init; }
    public decimal ReservedOutstandingQuantity { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class IceSupplementalIssueDto
{
    public Guid PublicId { get; init; }
    public int IceAllocationId { get; init; }
    public decimal Quantity { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool ReservationApplied { get; init; }
}
