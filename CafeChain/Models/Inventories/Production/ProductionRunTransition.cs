using CafeChain.Models.Staffs;

namespace CafeChain.Models.Inventories.Production;

public class ProductionRunTransition
{
    public int ProductionRunTransitionId { get; set; }
    public int ProductionRunId { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public int ActorStaffId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Reason { get; set; }
    public string? EvidenceJson { get; set; }

    public virtual ProductionRun ProductionRun { get; set; } = null!;
    public virtual Staff ActorStaff { get; set; } = null!;
}
