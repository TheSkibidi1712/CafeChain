using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Stock
{
    public class StockAlertTransitionConfiguration : IEntityTypeConfiguration<StockAlertTransition>
    {
        public void Configure(EntityTypeBuilder<StockAlertTransition> entity)
        {
            entity.ToTable("StockAlertTransitions");
            entity.HasKey(x => x.StockAlertTransitionId);
            entity.Property(x => x.PreviousStatus).HasMaxLength(32);
            entity.Property(x => x.NewStatus).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PreviousAlertType).HasMaxLength(32);
            entity.Property(x => x.NewAlertType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PreviousSeverity).HasMaxLength(16);
            entity.Property(x => x.NewSeverity).HasMaxLength(16).IsRequired();
            entity.Property(x => x.OnHandSnapshot).HasColumnType("decimal(18,3)");
            entity.Property(x => x.ReservedSnapshot).HasColumnType("decimal(18,3)");
            entity.Property(x => x.AvailableSnapshot).HasColumnType("decimal(18,3)");
            entity.Property(x => x.MinLevelSnapshot).HasColumnType("decimal(18,3)");
            entity.Property(x => x.SourceType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasOne(x => x.StockAlert)
                .WithMany(x => x.Transitions)
                .HasForeignKey(x => x.StockAlertId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ActorStaff)
                .WithMany()
                .HasForeignKey(x => x.ActorStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.StockAlertId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.SourceType, x.SourceId });
        }
    }
}
