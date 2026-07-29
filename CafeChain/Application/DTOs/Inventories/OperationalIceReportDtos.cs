namespace CafeChain.Application.DTOs.Inventories;

public sealed class OperationalIceReportDto
{
    public int IceAllocationId { get; init; }
    public int OperationalShiftId { get; init; }
    public int StoreId { get; init; }
    public string StoreName { get; init; } = string.Empty;
    public DateTime BusinessDate { get; init; }
    public string OperationalShiftName { get; init; } = string.Empty;
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public string IngredientName { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;
    public decimal OpeningCarry { get; init; }
    public decimal InitialIssued { get; init; }
    public decimal SupplementalIssued { get; init; }
    public decimal ReturnedQuantity { get; init; }
    public decimal ClosingCarry { get; init; }
    public decimal? ActualUsage { get; init; }
    public decimal TheoreticalUsage { get; init; }
    public decimal LedgerTheoreticalUsage { get; init; }
    public decimal? Variance { get; init; }
    public decimal? TheoreticalCost { get; init; }
    public decimal? VarianceCost { get; init; }
    public decimal? ActualCost { get; init; }
    public string CostStatus { get; init; } = string.Empty;
    public string? IssuedBy { get; init; }
    public string? ShiftLead { get; init; }
    public string? ReturnedBy { get; init; }
    public string? ReturnReceivedBy { get; init; }
    public string? ClosedBy { get; init; }
    public string? ApprovedBy { get; init; }
    public string? CloseReason { get; init; }
    public string? ReconciliationReason { get; init; }
    public IReadOnlyList<int> WorkShiftIds { get; init; } = [];
    public IReadOnlyList<OperationalIceReportCarryDto> CarryOvers { get; init; } = [];
    public IReadOnlyList<OperationalIceReportPostingDto> InventoryPostings { get; init; } = [];

    public bool HasUsageSnapshotMismatch => ActualUsage.HasValue && TheoreticalUsage != LedgerTheoreticalUsage;
}

public sealed class OperationalIceReportCarryDto
{
    public string Direction { get; init; } = string.Empty;
    public string OtherShiftName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Status { get; init; } = string.Empty;
    public string HandedOverBy { get; init; } = string.Empty;
    public string? ReceivedBy { get; init; }
    public DateTime? ConfirmedAtUtc { get; init; }
}

public sealed class OperationalIceReportPostingDto
{
    public int IceInventoryPostingId { get; init; }
    public string PostingType { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public int? InventoryTransactionId { get; init; }
    public decimal Quantity { get; init; }
    public decimal? UnitCost { get; init; }
    public decimal? TotalCost { get; init; }
    public string ApprovedBy { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
