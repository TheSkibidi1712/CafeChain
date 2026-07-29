namespace CafeChain.Application.DTOs.Inventories;

public sealed class OperationalIcePolicySetupDto
{
    public bool IsConfigured { get; init; }
    public bool IsValid { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public IReadOnlyList<OperationalIcePolicyOptionDto> Ingredients { get; init; } = [];
    public IReadOnlyList<OperationalIcePolicyOptionDto> Units { get; init; } = [];
    public OperationalIceInventorySnapshotDto? Inventory { get; init; }
}

public sealed class OperationalIcePolicyOptionDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

public sealed class OperationalIceInventorySnapshotDto
{
    public decimal PhysicalQuantity { get; init; }
    public decimal ReservedQuantity { get; init; }
    public decimal AvailableQuantity { get; init; }
}

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
    public string CreationSource { get; init; } = "Manual";
    public int? SourceScheduleShiftId { get; init; }
}

public sealed class OperationalIceScheduleOptionDto
{
    public int ScheduleShiftId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime BusinessDate { get; init; }
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }
    public int StaffCount { get; init; }
    public int? SuggestedShiftLeadId { get; init; }
}

public sealed class OperationalIceScheduleReviewDto
{
    public int OperationalShiftId { get; init; }
    public bool IsScheduleAvailable { get; init; }
    public bool HasChanges { get; init; }
    public bool CanSync { get; init; }
    public string SavedName { get; init; } = string.Empty;
    public DateTime SavedStartAtUtc { get; init; }
    public DateTime SavedEndAtUtc { get; init; }
    public int? SavedShiftLeadId { get; init; }
    public string? CurrentName { get; init; }
    public DateTime? CurrentStartAtUtc { get; init; }
    public DateTime? CurrentEndAtUtc { get; init; }
    public int? CurrentShiftLeadId { get; init; }
    public int StaffCount { get; init; }
}

public sealed class SyncOperationalShiftScheduleRequest
{
    public int OperationalShiftId { get; init; }
}

public sealed class OperationalIceWorkShiftSuggestionDto
{
    public int WorkShiftId { get; init; }
    public string StaffName { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
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

public sealed class LinkOperationalWorkShiftsRequest
{
    public int OperationalShiftId { get; init; }
    public IReadOnlyList<int> WorkShiftIds { get; init; } = [];
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

public sealed class CloseIceAllocationRequest
{
    public int IceAllocationId { get; init; }
    public decimal ReturnedQuantity { get; init; }
    public string? ReturnCondition { get; init; }
    public int? ReturnReceivedByStaffId { get; init; }
    public string? CloseReason { get; init; }
}

public sealed class ApproveIceVarianceRequest
{
    public int IceAllocationId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class ReconcileIceVarianceRequest
{
    public int IceAllocationId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class ConfirmIceCarryOverRequest
{
    public int FromIceAllocationId { get; init; }
    public int ToIceAllocationId { get; init; }
    public decimal Quantity { get; init; }
    public int ReceivedByStaffId { get; init; }
}

public sealed class CancelIceAllocationRequest
{
    public int IceAllocationId { get; init; }
    public string Reason { get; init; } = string.Empty;
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

public sealed class IceCloseResultDto
{
    public int IceAllocationId { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal OpeningCarryQuantity { get; init; }
    public decimal TotalIssuedQuantity { get; init; }
    public decimal ReturnedQuantity { get; init; }
    public decimal ClosingCarryQuantity { get; init; }
    public decimal ActualUsageQuantity { get; init; }
    public decimal TheoreticalUsageQuantity { get; init; }
    public decimal VarianceQuantity { get; init; }
    public bool RequiresApproval { get; init; }
}

public sealed class IceCarryOverDto
{
    public Guid PublicId { get; init; }
    public int FromIceAllocationId { get; init; }
    public int ToIceAllocationId { get; init; }
    public decimal Quantity { get; init; }
    public string Status { get; init; } = string.Empty;
}
