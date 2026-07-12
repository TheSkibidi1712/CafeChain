using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Consolidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Consolidation
{
    public sealed class InventoryConsolidationRunConfiguration
        : IEntityTypeConfiguration<InventoryConsolidationRun>
    {
        public void Configure(EntityTypeBuilder<InventoryConsolidationRun> entity)
        {
            entity.ToTable("InventoryConsolidationRuns", table =>
            {
                table.HasCheckConstraint(
                    "CK_InventoryConsolidationRuns_RunType",
                    "[RunType] IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_InventoryConsolidationRuns_Status",
                    "[Status] IN (1, 2, 3, 4, 5, 6)");
            });

            entity.HasKey(x => x.InventoryConsolidationRunId);

            entity.Property(x => x.RequestKey).IsRequired();
            entity.Property(x => x.RunType).HasConversion<int>().IsRequired();
            entity.Property(x => x.Status).HasConversion<int>().IsRequired();
            entity.Property(x => x.ManifestVersion).HasMaxLength(32).IsRequired();
            entity.Property(x => x.QueryContractVersion).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ManifestHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.DryRunHash).HasMaxLength(64);
            entity.Property(x => x.EnvironmentFingerprint).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ManifestJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ReportJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.FailureCode).HasMaxLength(100);
            entity.Property(x => x.FailureDetails).HasMaxLength(2000);

            entity.Property(x => x.BeforeAvailableTotal).HasColumnType("decimal(18,3)");
            entity.Property(x => x.BeforeReservedTotal).HasColumnType("decimal(18,3)");
            entity.Property(x => x.AfterAvailableTotal).HasColumnType("decimal(18,3)");
            entity.Property(x => x.AfterReservedTotal).HasColumnType("decimal(18,3)");

            entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired();
            entity.Property(x => x.DryRunAt).HasColumnType("datetime2");
            entity.Property(x => x.CompletedAt).HasColumnType("datetime2");
            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasIndex(x => new { x.StoreId, x.RequestKey })
                .IsUnique()
                .HasDatabaseName("UX_InventoryConsolidationRuns_Store_RequestKey");

            entity.HasIndex(x => new { x.StoreId, x.Status, x.CompletedAt })
                .HasDatabaseName("IX_InventoryConsolidationRuns_Store_Status_CompletedAt");

            entity.HasIndex(x => new { x.StoreId, x.ManifestHash })
                .HasDatabaseName("IX_InventoryConsolidationRuns_Store_ManifestHash");

            entity.HasIndex(x => x.QueryContractVersion)
                .HasDatabaseName("IX_InventoryConsolidationRuns_QueryContractVersion");

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RequestedByStaff)
                .WithMany()
                .HasForeignKey(x => x.RequestedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ApprovedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ExecutedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ExecutedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class InventoryConsolidationLineConfiguration
        : IEntityTypeConfiguration<InventoryConsolidationLine>
    {
        public void Configure(EntityTypeBuilder<InventoryConsolidationLine> entity)
        {
            entity.ToTable("InventoryConsolidationLines", table =>
            {
                table.HasCheckConstraint(
                    "CK_InventoryConsolidationLines_LineRole",
                    "[LineRole] IN (1, 2)");
            });

            entity.HasKey(x => x.InventoryConsolidationLineId);

            entity.Property(x => x.LineRole).HasConversion<int>().IsRequired();
            entity.Property(x => x.BeforeIdentityState).HasConversion<int?>();
            entity.Property(x => x.BeforeQuantitySemantics).HasConversion<int?>();
            entity.Property(x => x.BeforeAvailableQty).HasColumnType("decimal(18,3)");
            entity.Property(x => x.BeforeReservedQty).HasColumnType("decimal(18,3)");
            entity.Property(x => x.BeforeMinStockLevel).HasColumnType("decimal(18,3)");
            entity.Property(x => x.BeforeMaxNegativeQty).HasColumnType("decimal(18,3)");
            entity.Property(x => x.ApprovedConversionFactor).HasColumnType("decimal(18,6)");
            entity.Property(x => x.ConvertedAvailableQty).HasColumnType("decimal(18,3)");
            entity.Property(x => x.ConvertedReservedQty).HasColumnType("decimal(18,3)");
            entity.Property(x => x.AfterAvailableQty).HasColumnType("decimal(18,3)");
            entity.Property(x => x.AfterReservedQty).HasColumnType("decimal(18,3)");
            entity.Property(x => x.EvidenceType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EvidenceReference).HasMaxLength(500);

            entity.HasIndex(x => x.InventoryConsolidationRunId)
                .HasDatabaseName("IX_InventoryConsolidationLines_RunId");

            entity.HasIndex(x => new { x.InventoryConsolidationRunId, x.StoreInventoryId, x.LineRole })
                .IsUnique()
                .HasDatabaseName("UX_InventoryConsolidationLines_Run_Inventory_Role");

            entity.HasOne(x => x.ConsolidationRun)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.InventoryConsolidationRunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.StoreInventory)
                .WithMany()
                .HasForeignKey(x => x.StoreInventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PreparedItem)
                .WithMany()
                .HasForeignKey(x => x.PreparedItemId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
