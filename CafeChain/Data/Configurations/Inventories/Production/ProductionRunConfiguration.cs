using CafeChain.Models.Enums.Inventory;
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
                    "[Status] = 1");
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

            entity.Property(x => x.Notes)
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAt)
                .HasColumnType("datetime2")
                .IsRequired();

            entity.Property(x => x.ConfirmedAt)
                .HasColumnType("datetime2")
                .IsRequired();

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasIndex(x => new { x.StoreId, x.RequestKey })
                .IsUnique()
                .HasDatabaseName("UX_ProductionRuns_Store_RequestKey");

            entity.HasIndex(x => new { x.StoreId, x.CreatedAt })
                .HasDatabaseName("IX_ProductionRuns_Store_CreatedAt");

            entity.HasIndex(x => x.RecipeId)
                .HasDatabaseName("IX_ProductionRuns_RecipeId");

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
        }
    }
}
