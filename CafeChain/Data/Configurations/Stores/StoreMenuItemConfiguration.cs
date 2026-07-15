using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Stores
{
    public sealed class StoreMenuItemConfiguration : IEntityTypeConfiguration<StoreMenuItem>
    {
        public void Configure(EntityTypeBuilder<StoreMenuItem> entity)
        {
            entity.ToTable("StoreMenuItems", table =>
            {
                table.HasCheckConstraint("CK_StoreMenuItems_PriceOverride", "[PriceOverride] IS NULL OR [PriceOverride] >= 0");
                table.HasCheckConstraint("CK_StoreMenuItems_EffectiveWindow", "[EffectiveToUtc] IS NULL OR [EffectiveFromUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                table.HasCheckConstraint("CK_StoreMenuItems_DisplayOrder", "[DisplayOrder] >= 0");
            });

            entity.HasKey(x => x.StoreMenuItemId);
            entity.Property(x => x.IsEnabled).HasDefaultValue(false);
            entity.Property(x => x.PriceOverride).HasColumnType("decimal(18,2)");
            entity.Property(x => x.PauseReason).HasMaxLength(500);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();

            entity.HasOne(x => x.Store)
                .WithMany(x => x.StoreMenuItems)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DrinkSize)
                .WithMany(x => x.StoreMenuItems)
                .HasForeignKey(x => x.DrinkSizeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PublishedByStaff)
                .WithMany()
                .HasForeignKey(x => x.PublishedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.StoreId, x.DrinkSizeId })
                .IsUnique()
                .HasDatabaseName("UX_StoreMenuItems_Store_DrinkSize");
            entity.HasIndex(x => new { x.StoreId, x.DisplayOrder });
            entity.HasIndex(x => new { x.StoreId, x.IsEnabled, x.EffectiveFromUtc, x.EffectiveToUtc });
        }
    }
}
