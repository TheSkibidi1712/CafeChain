using CafeChain.Models;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ================================ MODULE STORE ================================
    public class StoreConfiguration : IEntityTypeConfiguration<Store>
    {
        public void Configure(EntityTypeBuilder<Store> entity)
        {
            entity.ToTable("Stores");

            entity.HasKey(x => x.StoId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Address)
                .HasMaxLength(300);

            entity.Property(x => x.Phone)
                .HasMaxLength(15);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasOne(x => x.Ward)
                .WithMany()
                .HasForeignKey(x => x.WarId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public class StoreDrinkConfiguration : IEntityTypeConfiguration<StoreDrink>
    {
        public void Configure(EntityTypeBuilder<StoreDrink> entity)
        {
            entity.ToTable("StoreDrinks");

            entity.HasKey(x => x.StoDId);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasOne(x => x.Store)
                .WithMany(x => x.StoreDrinks)
                .HasForeignKey(x => x.StoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Drink)
                .WithMany()
                .HasForeignKey(x => x.DriId)
                .OnDelete(DeleteBehavior.Restrict);

            // ❗ tránh trùng drink trong 1 store
            entity.HasIndex(x => new { x.StoId, x.DriId })
                .IsUnique();
        }
    }

    public class StoreToppingConfiguration : IEntityTypeConfiguration<StoreTopping>
    {
        public void Configure(EntityTypeBuilder<StoreTopping> entity)
        {
            entity.ToTable("StoreToppings");

            entity.HasKey(x => x.StoTId);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasOne(x => x.Store)
                .WithMany(x => x.StoreToppings)
                .HasForeignKey(x => x.StoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Topping)
                .WithMany(x => x.StoreToppings)
                .HasForeignKey(x => x.TopId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class StoreInventoryConfiguration : IEntityTypeConfiguration<StoreInventory>
    {
        public void Configure(EntityTypeBuilder<StoreInventory> entity)
        {
            entity.ToTable("StoreInventories", t =>
            {
                // ❗ chống tồn kho âm
                t.HasCheckConstraint("CK_StoreInventory_Qty",
                    "[AvailableQty] >= 0 AND [ReservedQty] >= 0");
            });

            entity.HasKey(x => x.StoIId);

            entity.Property(x => x.AvailableQty)
                .HasColumnType("decimal(18,3)")
                .HasDefaultValue(0);

            entity.Property(x => x.ReservedQty)
                .HasColumnType("decimal(18,3)")
                .HasDefaultValue(0);

            entity.Property(x => x.LastUpdated)
                .HasDefaultValueSql("GETDATE()");

            entity.HasOne(x => x.Store)
                .WithMany(x => x.StoreInventories)
                .HasForeignKey(x => x.StoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngId)
                .OnDelete(DeleteBehavior.Restrict);

            // ❗ mỗi store chỉ có 1 record cho 1 nguyên liệu
            entity.HasIndex(x => new { x.StoId, x.IngId })
                .IsUnique();
        }
    }
}