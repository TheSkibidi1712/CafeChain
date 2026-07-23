using CafeChain.Models.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Analytics;

public sealed class PosRecommendationCatalogConfiguration : IEntityTypeConfiguration<PosRecommendationCatalog>
{
    public void Configure(EntityTypeBuilder<PosRecommendationCatalog> e)
    {
        e.ToTable("PosRecommendationCatalog"); e.HasKey(x => x.PosRecommendationCatalogId);
        e.Property(x => x.Support).HasColumnType("decimal(9,6)");
        e.Property(x => x.Confidence).HasColumnType("decimal(9,6)");
        e.Property(x => x.Lift).HasColumnType("decimal(9,6)");
        e.Property(x => x.Margin).HasColumnType("decimal(18,2)");
        e.Property(x => x.ModelVersion).HasMaxLength(40).IsRequired();
        e.HasIndex(x => new { x.StoreId, x.TriggerDrinkId, x.RecommendedDrinkId, x.ModelVersion }).IsUnique();
        e.HasIndex(x => new { x.StoreId, x.TriggerDrinkId, x.Rank, x.ExpiresAtUtc });
        e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.TriggerDrink).WithMany().HasForeignKey(x => x.TriggerDrinkId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.RecommendedDrink).WithMany().HasForeignKey(x => x.RecommendedDrinkId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PosRecommendationExposureConfiguration : IEntityTypeConfiguration<PosRecommendationExposure>
{
    public void Configure(EntityTypeBuilder<PosRecommendationExposure> e)
    {
        e.ToTable("PosRecommendationExposures"); e.HasKey(x => x.PosRecommendationExposureId);
        e.Property(x => x.Variant).HasMaxLength(16).IsRequired();
        e.Property(x => x.ModelVersion).HasMaxLength(40).IsRequired();
        e.HasIndex(x => x.RecommendationSessionId).IsUnique();
        e.HasIndex(x => new { x.StoreId, x.CreatedAtUtc });
        e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class PosRecommendationExposureItemConfiguration : IEntityTypeConfiguration<PosRecommendationExposureItem>
{
    public void Configure(EntityTypeBuilder<PosRecommendationExposureItem> e)
    {
        e.ToTable("PosRecommendationExposureItems"); e.HasKey(x => x.PosRecommendationExposureItemId);
        e.HasIndex(x => new { x.PosRecommendationExposureId, x.TriggerDrinkId, x.RecommendedDrinkId }).IsUnique();
        e.HasOne(x => x.Exposure).WithMany(x => x.Items).HasForeignKey(x => x.PosRecommendationExposureId).OnDelete(DeleteBehavior.Cascade);
    }
}

