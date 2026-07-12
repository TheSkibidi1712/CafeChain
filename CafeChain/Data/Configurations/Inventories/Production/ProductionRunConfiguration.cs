using CafeChain.Models.Inventories.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Production
{
    public class ProductionRunConfiguration : IEntityTypeConfiguration<ProductionRun>
    {
        public void Configure(EntityTypeBuilder<ProductionRun> entity)
        {
            entity.ToTable("ProductionRuns", table =>
            {
                table.HasCheckConstraint(
                    "CK_ProductionRuns_RequestedRunCount",
                    "[RequestedRunCount] > 0 AND [RequestedRunCount] <= 9999");

                table.HasCheckConstraint(
                    "CK_ProductionRuns_Status",
                    "[Status] IN (1, 2)");

                table.HasCheckConstraint(
                    "CK_ProductionRuns_ValuationStatus",
                    "[ValuationStatus] IN (0, 1)");
            });

            entity.HasKey(x => x.ProductionRunId);

            entity.Property(x => x.RequestedRunCount)
                .HasColumnType("decimal(18,5)")
                .IsRequired();

            entity.Property(x => x.RequestKey)
                .IsRequired();

            entity.Property(x => x.RequestFingerprint)
                .HasMaxLength(64)
                .IsFixedLength()
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.ValuationStatus)
                .HasConversion<int>()
                .IsRequired()
                .HasDefaultValue(Models.Enums.Inventory.ProductionValuationStatus.Pending);

            entity.Property(x => x.TotalInputCost)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.OutputUnitCost)
                .HasColumnType("decimal(18,8)");

            entity.Property(x => x.ValuedAtUtc)
                .HasColumnType("datetime2");

            entity.Property(x => x.Notes)
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAt)
                .HasColumnType("datetime2")
                .IsRequired();

            entity.Property(x => x.ConfirmedAt)
                .HasColumnType("datetime2")
                .IsRequired();

            entity.Property(x => x.CompletedAt)
                .HasColumnType("datetime2");

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasIndex(x => new { x.StoreId, x.RequestKey })
                .IsUnique()
                .HasDatabaseName("UX_ProductionRuns_Store_RequestKey");

            entity.HasIndex(x => new { x.StoreId, x.CreatedAt })
                .HasDatabaseName("IX_ProductionRuns_Store_CreatedAt");

            entity.HasIndex(x => x.RecipeId)
                .HasDatabaseName("IX_ProductionRuns_RecipeId");

            entity.HasIndex(x => x.CompletedByStaffId);

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Recipe)
                .WithMany()
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByStaff)
                .WithMany()
                .HasForeignKey(x => x.CreatedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CompletedByStaff)
                .WithMany()
                .HasForeignKey(x => x.CompletedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
