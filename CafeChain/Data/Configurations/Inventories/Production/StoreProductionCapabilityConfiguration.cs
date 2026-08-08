using CafeChain.Models.Inventories.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Production;

public sealed class StoreProductionCapabilityConfiguration
    : IEntityTypeConfiguration<StoreProductionCapability>
{
    public void Configure(EntityTypeBuilder<StoreProductionCapability> entity)
    {
        entity.ToTable("StoreProductionCapabilities", table =>
        {
            table.HasCheckConstraint(
                "CK_StoreProductionCapabilities_ItemXor",
                "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR " +
                "([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_StoreProductionCapabilities_EffectiveRange",
                "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
        });

        entity.HasKey(x => x.StoreProductionCapabilityId);
        entity.Property(x => x.EffectiveFromUtc).HasColumnType("datetime2");
        entity.Property(x => x.EffectiveToUtc).HasColumnType("datetime2");
        entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        entity.Property(x => x.RowVersion).IsRowVersion();

        entity.HasIndex(x => new { x.StoreId, x.IngredientId })
            .IsUnique()
            .HasFilter("[IngredientId] IS NOT NULL")
            .HasDatabaseName("UX_StoreProductionCapabilities_Store_Ingredient");
        entity.HasIndex(x => new { x.StoreId, x.PreparedItemId })
            .IsUnique()
            .HasFilter("[PreparedItemId] IS NOT NULL")
            .HasDatabaseName("UX_StoreProductionCapabilities_Store_PreparedItem");

        entity.HasOne(x => x.Store)
            .WithMany()
            .HasForeignKey(x => x.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Ingredient)
            .WithMany()
            .HasForeignKey(x => x.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.PreparedItem)
            .WithMany()
            .HasForeignKey(x => x.PreparedItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
