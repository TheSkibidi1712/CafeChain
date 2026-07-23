using CafeChain.Models.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Analytics;

public sealed class ForecastRunConfiguration : IEntityTypeConfiguration<ForecastRun>
{
    public void Configure(EntityTypeBuilder<ForecastRun> b)
    {
        b.ToTable("ForecastRuns"); b.HasKey(x => x.ForecastRunId);
        b.Property(x => x.SeriesType).HasMaxLength(40).IsRequired();
        b.Property(x => x.ModelType).HasMaxLength(40).IsRequired();
        b.Property(x => x.ModelVersion).HasMaxLength(30).IsRequired();
        b.Property(x => x.QualityStatus).HasMaxLength(40).IsRequired();
        b.Property(x => x.InputDataVersion).HasMaxLength(80).IsRequired();
        b.Property(x => x.WarningJson).HasMaxLength(4000).IsRequired();
        b.Property(x => x.Mae).HasPrecision(19, 4); b.Property(x => x.Wape).HasPrecision(9, 4);
        b.HasIndex(x => new { x.StoreId, x.SeriesType, x.EntityId, x.TrainingToExclusive, x.HorizonDays, x.ModelVersion }).IsUnique();
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ForecastPointConfiguration : IEntityTypeConfiguration<ForecastPoint>
{
    public void Configure(EntityTypeBuilder<ForecastPoint> b)
    {
        b.ToTable("ForecastPoints"); b.HasKey(x => x.ForecastPointId);
        b.Property(x => x.PointForecast).HasPrecision(19, 4);
        b.Property(x => x.LowerBound).HasPrecision(19, 4);
        b.Property(x => x.UpperBound).HasPrecision(19, 4);
        b.HasIndex(x => new { x.ForecastRunId, x.ForecastDate }).IsUnique();
        b.HasOne(x => x.ForecastRun).WithMany(x => x.Points).HasForeignKey(x => x.ForecastRunId).OnDelete(DeleteBehavior.Cascade);
    }
}
