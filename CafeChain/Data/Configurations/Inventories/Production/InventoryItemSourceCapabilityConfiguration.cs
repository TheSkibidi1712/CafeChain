using CafeChain.Models.Inventories.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Production;

public sealed class InventoryItemSourceCapabilityConfiguration
    : IEntityTypeConfiguration<InventoryItemSourceCapability>
{
    public void Configure(EntityTypeBuilder<InventoryItemSourceCapability> entity)
    {
        entity.ToTable("InventoryItemSourceCapabilities", table =>
        {
            table.HasCheckConstraint(
                "CK_InventoryItemSourceCapabilities_ItemXor",
                "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR " +
                "([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_InventoryItemSourceCapabilities_EffectiveRange",
                "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
        });

        entity.HasKey(x => x.InventoryItemSourceCapabilityId);
        entity.Property(x => x.EffectiveFromUtc).HasColumnType("datetime2");
        entity.Property(x => x.EffectiveToUtc).HasColumnType("datetime2");
        entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        entity.Property(x => x.RowVersion).IsRowVersion();

        entity.HasIndex(x => x.IngredientId)
            .IsUnique()
            .HasFilter("[IngredientId] IS NOT NULL")
            .HasDatabaseName("UX_InventoryItemSourceCapabilities_Ingredient");
        entity.HasIndex(x => x.PreparedItemId)
            .IsUnique()
            .HasFilter("[PreparedItemId] IS NOT NULL")
            .HasDatabaseName("UX_InventoryItemSourceCapabilities_PreparedItem");

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
