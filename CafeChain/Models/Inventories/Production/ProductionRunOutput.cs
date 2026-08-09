using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Staffs;

namespace CafeChain.Models.Inventories.Production;

public class ProductionRunOutput
{
    public int ProductionRunOutputId { get; set; }
    public int ProductionRunId { get; set; }
    public int BaseUnitId { get; set; }
    public decimal ExpectedOutputBase { get; set; }
    public decimal ActualProducedBase { get; set; }
    public decimal AcceptedOutputBase { get; set; }
    public decimal RejectedOutputBase { get; set; }
    public decimal VariancePercent { get; set; }
    public string? Reason { get; set; }
    public int RecordedByStaffId { get; set; }
    public DateTime RecordedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ProductionRun ProductionRun { get; set; } = null!;
    public virtual Unit BaseUnit { get; set; } = null!;
    public virtual Staff RecordedByStaff { get; set; } = null!;
}
