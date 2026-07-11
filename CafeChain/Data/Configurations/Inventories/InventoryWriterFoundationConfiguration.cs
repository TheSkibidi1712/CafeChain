using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Inventories.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories
{
    public sealed class StoreInventoryWriterConfigurationMap
        : IEntityTypeConfiguration<StoreInventoryWriterConfiguration>
    {
        public void Configure(EntityTypeBuilder<StoreInventoryWriterConfiguration> entity)
        {
            entity.ToTable("StoreInventoryWriterConfigurations", table =>
            {
                table.HasCheckConstraint(
                    "CK_StoreInventoryWriterConfiguration_Mode",
                    "[WriterMode] IN (0, 1, 2)");
            });

            entity.HasKey(x => x.StoreId);
            entity.Property(x => x.WriterMode)
                .HasConversion<int>()
                .HasDefaultValue(InventoryWriterMode.LegacyRecipe);
            entity.Property(x => x.HasEverActivatedPreparedItem).HasDefaultValue(false);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();

            entity.HasOne(x => x.Store)
                .WithOne(x => x.InventoryWriterConfiguration)
                .HasForeignKey<StoreInventoryWriterConfiguration>(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(
                Seed(1),
                Seed(2),
                Seed(3));
        }

        private static StoreInventoryWriterConfiguration Seed(int storeId) => new()
        {
            StoreId = storeId,
            WriterMode = InventoryWriterMode.LegacyRecipe,
            HasEverActivatedPreparedItem = false,
            CreatedAt = new DateTime(2025, 1, 1),
            UpdatedAt = new DateTime(2025, 1, 1)
        };
    }

    public sealed class InventoryWriterModeTransitionConfiguration
        : IEntityTypeConfiguration<InventoryWriterModeTransition>
    {
        public void Configure(EntityTypeBuilder<InventoryWriterModeTransition> entity)
        {
            entity.ToTable("InventoryWriterModeTransitions", table =>
            {
                table.HasCheckConstraint("CK_InventoryWriterModeTransition_FromMode", "[FromMode] IN (0, 1, 2)");
                table.HasCheckConstraint("CK_InventoryWriterModeTransition_ToMode", "[ToMode] IN (0, 1, 2)");
            });

            entity.HasKey(x => x.TransitionId);
            entity.Property(x => x.FromMode).HasConversion<int>();
            entity.Property(x => x.ToMode).HasConversion<int>();
            entity.Property(x => x.Reason).IsRequired().HasMaxLength(500);
            entity.Property(x => x.ReadinessHash).HasMaxLength(64);
            entity.Property(x => x.ReadinessSnapshotJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.FailureCode).HasMaxLength(100);
            entity.Property(x => x.RequestedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ActorAccount)
                .WithMany()
                .HasForeignKey(x => x.ActorAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.StoreId, x.RequestedAt });
            entity.HasIndex(x => x.ActorAccountId);
            entity.HasIndex(x => x.Succeeded);
        }
    }
}
