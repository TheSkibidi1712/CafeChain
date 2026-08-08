using CafeChain.Models.Inventories.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Production;

public sealed class ProductionRunInputActualConfiguration
    : IEntityTypeConfiguration<ProductionRunInputActual>
{
    public void Configure(EntityTypeBuilder<ProductionRunInputActual> entity)
    {
        entity.ToTable("ProductionRunInputActuals", table =>
        {
            table.HasCheckConstraint(
                "CK_ProductionRunInputActuals_ItemXor",
                "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR " +
                "([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ProductionRunInputActuals_Quantities",
                "[PlannedBaseQuantity] >= 0 AND [ActualBaseQuantity] >= 0");
        });
        entity.HasKey(x => x.ProductionRunInputActualId);
        entity.Property(x => x.PlannedBaseQuantity).HasPrecision(18, 5);
        entity.Property(x => x.ActualBaseQuantity).HasPrecision(18, 5);
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => new { x.ProductionRunId, x.IngredientId })
            .IsUnique()
            .HasFilter("[IngredientId] IS NOT NULL");
        entity.HasIndex(x => new { x.ProductionRunId, x.PreparedItemId })
            .IsUnique()
            .HasFilter("[PreparedItemId] IS NOT NULL");
        entity.HasOne(x => x.ProductionRun).WithMany(x => x.ActualInputs)
            .HasForeignKey(x => x.ProductionRunId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Ingredient).WithMany()
            .HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.PreparedItem).WithMany()
            .HasForeignKey(x => x.PreparedItemId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.BaseUnit).WithMany()
            .HasForeignKey(x => x.BaseUnitId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ConfirmedByStaff).WithMany()
            .HasForeignKey(x => x.ConfirmedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProductionRunOutputConfiguration
    : IEntityTypeConfiguration<ProductionRunOutput>
{
    public void Configure(EntityTypeBuilder<ProductionRunOutput> entity)
    {
        entity.ToTable("ProductionRunOutputs", table =>
        {
            table.HasCheckConstraint(
                "CK_ProductionRunOutputs_Quantities",
                "[ExpectedOutputBase] > 0 AND [ActualProducedBase] >= 0 AND " +
                "[AcceptedOutputBase] >= 0 AND [RejectedOutputBase] >= 0 AND " +
                "[AcceptedOutputBase] + [RejectedOutputBase] <= [ActualProducedBase]");
        });
        entity.HasKey(x => x.ProductionRunOutputId);
        entity.Property(x => x.ExpectedOutputBase).HasPrecision(18, 5);
        entity.Property(x => x.ActualProducedBase).HasPrecision(18, 5);
        entity.Property(x => x.AcceptedOutputBase).HasPrecision(18, 5);
        entity.Property(x => x.RejectedOutputBase).HasPrecision(18, 5);
        entity.Property(x => x.VariancePercent).HasPrecision(9, 4);
        entity.Property(x => x.Reason).HasMaxLength(500);
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => x.ProductionRunId).IsUnique();
        entity.HasOne(x => x.ProductionRun).WithOne(x => x.ActualOutput)
            .HasForeignKey<ProductionRunOutput>(x => x.ProductionRunId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.BaseUnit).WithMany()
            .HasForeignKey(x => x.BaseUnitId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.RecordedByStaff).WithMany()
            .HasForeignKey(x => x.RecordedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProductionRunTransitionConfiguration
    : IEntityTypeConfiguration<ProductionRunTransition>
{
    public void Configure(EntityTypeBuilder<ProductionRunTransition> entity)
    {
        entity.ToTable("ProductionRunTransitions");
        entity.HasKey(x => x.ProductionRunTransitionId);
        entity.Property(x => x.FromStatus).HasMaxLength(40).IsRequired();
        entity.Property(x => x.ToStatus).HasMaxLength(40).IsRequired();
        entity.Property(x => x.Reason).HasMaxLength(500);
        entity.Property(x => x.EvidenceJson).HasColumnType("nvarchar(max)");
        entity.HasIndex(x => new { x.ProductionRunId, x.OccurredAtUtc });
        entity.HasOne(x => x.ProductionRun).WithMany(x => x.Transitions)
            .HasForeignKey(x => x.ProductionRunId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ActorStaff).WithMany()
            .HasForeignKey(x => x.ActorStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
