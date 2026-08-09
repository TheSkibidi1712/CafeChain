using CafeChain.Application.DTOs.Admin.StoreScope;

namespace CafeChain.ViewModels.Admin.OperationalIce;

public sealed class OperationalIceIndexVM
{
    public int StoreId { get; init; }
    public DateTime BusinessDate { get; init; }
    public string? Status { get; init; }
    public IReadOnlyList<AdminStoreOptionDto> Stores { get; init; } = [];
    public IReadOnlyList<OperationalIceListRowVM> Rows { get; init; } = [];
    public IcePolicyVM? Policy { get; init; }
    public IReadOnlyList<OperationalIceOptionVM> Ingredients { get; init; } = [];
    public IReadOnlyList<OperationalIceOptionVM> Units { get; init; } = [];
    public IReadOnlyList<OperationalIceOptionVM> ShiftLeads { get; init; } = [];
    public IReadOnlyList<OperationalIceScheduleOptionVM> ScheduleOptions { get; init; } = [];
    public OperationalIceInventoryVM? Inventory { get; init; }
    public bool HasValidPolicy { get; init; }
    public string PolicyStatusMessage { get; init; } = string.Empty;
    public bool CanCreateShift { get; init; }
    public bool CanOpenShift { get; init; }
    public bool CanCancelScheduledShift { get; init; }
    public bool CanViewReport { get; init; }
    public bool CanConfigurePolicy { get; init; }
}

public sealed class OperationalIceListRowVM
{
    public int OperationalShiftId { get; init; }
    public int? IceAllocationId { get; init; }
    public string ShiftName { get; init; } = string.Empty;
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }
    public decimal SuggestedQuantity { get; init; }
    public decimal TotalIssuedQuantity { get; init; }
    public decimal TheoreticalUsageQuantity { get; init; }
    public decimal? VarianceQuantity { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool HasShiftLead { get; init; }
    public string CreationSource { get; init; } = string.Empty;
    public int LinkedWorkShiftCount { get; init; }
    public OperationalIceScheduleReviewVM? ScheduleReview { get; set; }
}

public sealed class OperationalIceScheduleReviewVM
{
    public bool IsScheduleAvailable { get; init; }
    public bool HasChanges { get; init; }
    public bool CanSync { get; init; }
    public bool HasCancelledAssignments { get; init; }
    public bool RequiresLeadReplacement { get; init; }
    public bool BlocksOpening { get; init; }
    public string SavedLabel { get; init; } = string.Empty;
    public string CurrentLabel { get; init; } = string.Empty;
    public string SavedLeadName { get; init; } = string.Empty;
    public string CurrentLeadName { get; init; } = string.Empty;
    public int StaffCount { get; init; }
    public int CancelledStaffCount { get; init; }
}

public sealed class OperationalIceScheduleOptionVM
{
    public int ScheduleShiftId { get; init; }
    public IReadOnlyList<int> StaffShiftIds { get; init; } = [];
    public string Name { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string StartLocalValue { get; init; } = string.Empty;
    public string EndLocalValue { get; init; } = string.Empty;
    public int StaffCount { get; init; }
    public int? SuggestedShiftLeadId { get; init; }
}

public sealed class OperationalIceInventoryVM
{
    public decimal PhysicalQuantity { get; init; }
    public decimal ReservedQuantity { get; init; }
    public decimal AvailableQuantity { get; init; }
    public decimal AvailableAfterSuggestedShiftQuantity { get; init; }
    public string UnitName { get; init; } = string.Empty;
}

public sealed class IcePolicyVM
{
    public int StoreId { get; init; }
    public int IngredientId { get; init; }
    public int DisplayUnitId { get; init; }
    public string IngredientName { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;
    public decimal SuggestedDailyQuantity { get; init; }
    public decimal SuggestedShiftQuantity { get; init; }
    public bool AllowSupplementalIssue { get; init; }
    public bool AllowSameDayCarryOver { get; init; }
    public bool RequireVarianceApproval { get; init; }
    public decimal VarianceApprovalQuantityThreshold { get; init; }
    public decimal VarianceApprovalPercentThreshold { get; init; }
}

public sealed class OperationalIceDetailVM
{
    public int StoreId { get; init; }
    public string StoreName { get; init; } = string.Empty;
    public int OperationalShiftId { get; init; }
    public int IceAllocationId { get; init; }
    public string ShiftName { get; init; } = string.Empty;
    public DateTime BusinessDate { get; init; }
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public string IngredientName { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;
    public decimal PhysicalQuantity { get; init; }
    public decimal AvailableQuantity { get; init; }
    public decimal ReservedStoreQuantity { get; init; }
    public decimal ReservedOutstandingQuantity { get; init; }
    public decimal OpeningCarryQuantity { get; init; }
    public decimal InitialIssuedQuantity { get; init; }
    public decimal SupplementalIssuedQuantity { get; init; }
    public decimal ReturnedQuantity { get; init; }
    public decimal ClosingCarryQuantity { get; init; }
    public decimal TheoreticalUsageQuantity { get; init; }
    public decimal? ActualUsageQuantity { get; init; }
    public decimal? VarianceQuantity { get; init; }
    public string? CloseReason { get; init; }
    public string? ReconciliationReason { get; init; }
    public string CostSnapshotStatus { get; init; } = string.Empty;
    public decimal? UnitCostSnapshot { get; init; }
    public IReadOnlyList<OperationalIceWorkShiftVM> WorkShifts { get; init; } = [];
    public IReadOnlyList<OperationalIceSupplementVM> Supplements { get; init; } = [];
    public IReadOnlyList<OperationalIceCarryVM> CarryOvers { get; init; } = [];
    public IReadOnlyList<OperationalIcePostingVM> Postings { get; init; } = [];
    public IReadOnlyList<OperationalIceOptionVM> AvailableWorkShifts { get; init; } = [];
    public IReadOnlyList<string> WorkShiftCandidateMessages { get; init; } = [];
    public IReadOnlyList<OperationalIceOptionVM> CarryTargets { get; init; } = [];
    public IReadOnlyList<OperationalIceOptionVM> StaffOptions { get; init; } = [];
    public bool CanLinkWorkShift { get; init; }
    public bool CanRequestSupplement { get; init; }
    public bool CanApproveSupplement { get; init; }
    public bool CanHandoff { get; init; }
    public bool CanSubmitClose { get; init; }
    public bool CanApproveVariance { get; init; }
    public bool CanViewReport { get; init; }
}

public sealed class OperationalIceWorkShiftVM
{
    public int WorkShiftId { get; init; }
    public string StaffName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
}

public sealed class OperationalIceSupplementVM
{
    public Guid PublicId { get; init; }
    public decimal Quantity { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAtUtc { get; init; }
}

public sealed class OperationalIceCarryVM
{
    public Guid PublicId { get; init; }
    public string Direction { get; init; } = string.Empty;
    public string OtherShiftName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Status { get; init; } = string.Empty;
    public string HandedOverBy { get; init; } = string.Empty;
    public string? ReceivedBy { get; init; }
    public DateTime? ConfirmedAtUtc { get; init; }
}

public sealed class OperationalIcePostingVM
{
    public int IceInventoryPostingId { get; init; }
    public string PostingType { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal? UnitCost { get; init; }
    public decimal? TotalCost { get; init; }
    public int? InventoryTransactionId { get; init; }
    public string ApprovedBy { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class OperationalIceOptionVM
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}
