using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Analytics;

public class OperationalAnomaly
{
    public int OperationalAnomalyId { get; set; }
    public int StoreId { get; set; }
    public string MetricCode { get; set; } = string.Empty;
    public string PeriodKey { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal BaselineValue { get; set; }
    public decimal AbsoluteDeviation { get; set; }
    public decimal PercentageDeviation { get; set; }
    public decimal RobustScore { get; set; }
    public DateTime WindowFromUtc { get; set; }
    public DateTime WindowToExclusiveUtc { get; set; }
    public int SampleCount { get; set; }
    public string Severity { get; set; } = "INFO";
    public string Confidence { get; set; } = "LOW";
    public string Status { get; set; } = "OPEN";
    public string ReasonCodesJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public int? AcknowledgedByStaffId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? Feedback { get; set; }
    public int? FeedbackByStaffId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual Store Store { get; set; } = null!;
    public virtual Staff? AcknowledgedByStaff { get; set; }
    public virtual Staff? FeedbackByStaff { get; set; }
}
