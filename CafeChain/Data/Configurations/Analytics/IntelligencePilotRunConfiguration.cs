using CafeChain.Models.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Analytics;

public sealed class IntelligencePilotRunConfiguration : IEntityTypeConfiguration<IntelligencePilotRun>
{
    public void Configure(EntityTypeBuilder<IntelligencePilotRun> entity)
    {
        entity.ToTable("IntelligencePilotRuns");
        entity.HasKey(x => x.IntelligencePilotRunId);
        entity.Property(x => x.FeatureCode).HasMaxLength(64).IsRequired();
        entity.Property(x => x.RunMode).HasMaxLength(16).IsRequired();
        entity.Property(x => x.MetricsJson).HasColumnType("nvarchar(max)").IsRequired();
        entity.Property(x => x.ErrorCategory).HasMaxLength(64);
        entity.HasIndex(x => new { x.FeatureCode, x.StoreId, x.CompletedAtUtc });
    }
}

