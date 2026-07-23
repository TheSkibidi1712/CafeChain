using CafeChain.Models.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Analytics;

public sealed class OperationalAnomalyConfiguration : IEntityTypeConfiguration<OperationalAnomaly>
{
    public void Configure(EntityTypeBuilder<OperationalAnomaly> e)
    {
        e.ToTable("OperationalAnomalies"); e.HasKey(x => x.OperationalAnomalyId);
        e.Property(x => x.MetricCode).HasMaxLength(64).IsRequired();
        e.Property(x => x.PeriodKey).HasMaxLength(40).IsRequired();
        e.Property(x => x.Severity).HasMaxLength(16).IsRequired();
        e.Property(x => x.Confidence).HasMaxLength(16).IsRequired();
        e.Property(x => x.Status).HasMaxLength(16).IsRequired();
        e.Property(x => x.ReasonCodesJson).HasMaxLength(1000).IsRequired();
        e.Property(x => x.Feedback).HasMaxLength(500);
        foreach (var property in new[] { nameof(OperationalAnomaly.CurrentValue), nameof(OperationalAnomaly.BaselineValue), nameof(OperationalAnomaly.AbsoluteDeviation), nameof(OperationalAnomaly.PercentageDeviation), nameof(OperationalAnomaly.RobustScore) })
            e.Property<decimal>(property).HasColumnType("decimal(18,4)");
        e.Property(x => x.RowVersion).IsRowVersion();
        e.HasIndex(x => new { x.StoreId, x.MetricCode, x.PeriodKey }).IsUnique();
        e.HasIndex(x => new { x.StoreId, x.Status, x.Severity });
        e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.AcknowledgedByStaff).WithMany().HasForeignKey(x => x.AcknowledgedByStaffId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.FeedbackByStaff).WithMany().HasForeignKey(x => x.FeedbackByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
