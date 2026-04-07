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

            entity.HasKey(x => x.StoreId);

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

            // ─── GPS coordinates precision ─────────────────────────────────────────
            entity.Property(x => x.Latitude)
                .HasColumnType("decimal(9,6)");

            entity.Property(x => x.Longitude)
                .HasColumnType("decimal(9,6)");

            // ─── Location FK relationships ─────────────────────────────────────────
            entity.HasOne(x => x.Ward)
                .WithMany(w => w.Stores)
                .HasForeignKey(x => x.WardId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.District)
                .WithMany()
                .HasForeignKey(x => x.DistrictId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Province)
                .WithMany()
                .HasForeignKey(x => x.ProvinceId)
                .OnDelete(DeleteBehavior.SetNull);

            // ─── Seed data ─────────────────────────────────────────────────────────
            // ⚠️ WardId = NULL vì bảng Wards sẽ bị xóa sạch và nạp lại qua vietnam_locations.sql
            entity.HasData(
                new Store
                {
                    StoreId = 1,
                    Name = "CafeChain Thủ Dầu Một",
                    Address = "123 Đại lộ Bình Dương",
                    Phone = "0900000001",
                    WardId = null,
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Store
                {
                    StoreId = 2,
                    Name = "CafeChain Thuận An",
                    Address = "456 Nguyễn Trãi",
                    Phone = "0900000002",
                    WardId = null,
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Store
                {
                    StoreId = 3,
                    Name = "CafeChain Dĩ An",
                    Address = "789 Lê Hồng Phong",
                    Phone = "0900000003",
                    WardId = null,
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                }
            );
        }
    }


    public class StoreDrinkConfiguration : IEntityTypeConfiguration<StoreDrink>
    {
        public void Configure(EntityTypeBuilder<StoreDrink> entity)
        {
            entity.ToTable("StoreDrinks");

            entity.HasKey(x => x.StoreDrinkId);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasOne(x => x.Store)
                .WithMany(x => x.StoreDrinks)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Drink)
                .WithMany(s => s.StoreDrinks)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Restrict);

            // ❗ tránh trùng drink trong 1 store
            entity.HasIndex(x => new { x.StoreId, x.DrinkId })
                .IsUnique();

            entity.HasData(
                // Store 1
                new StoreDrink { StoreDrinkId = 1, StoreId = 1, DrinkId = 1, Active = true },
                new StoreDrink { StoreDrinkId = 2, StoreId = 1, DrinkId = 2, Active = true },

                // Store 2
                new StoreDrink { StoreDrinkId = 3, StoreId = 2, DrinkId = 1, Active = true },
                new StoreDrink { StoreDrinkId = 4, StoreId = 2, DrinkId = 3, Active = true },

                // Store 3
                new StoreDrink { StoreDrinkId = 5, StoreId = 3, DrinkId = 2, Active = true },
                new StoreDrink { StoreDrinkId = 6, StoreId = 3, DrinkId = 4, Active = true }
            );
        }
    }

    public class StoreToppingConfiguration : IEntityTypeConfiguration<StoreTopping>
    {
        public void Configure(EntityTypeBuilder<StoreTopping> entity)
        {
            entity.ToTable("StoreToppings");

            entity.HasKey(x => x.StoreToppingId);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasOne(x => x.Store)
                .WithMany(x => x.StoreToppings)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Topping)
                .WithMany(x => x.StoreToppings)
                .HasForeignKey(x => x.ToppingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasData(
                new StoreTopping { StoreToppingId = 1, StoreId = 1, ToppingId = 1, Active = true },
                new StoreTopping { StoreToppingId = 2, StoreId = 1, ToppingId = 2, Active = true },

                new StoreTopping { StoreToppingId = 3, StoreId = 2, ToppingId = 1, Active = true },

                new StoreTopping { StoreToppingId = 4, StoreId = 3, ToppingId = 2, Active = true }
            );
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

            entity.HasKey(x => x.StoreInventoryId);

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
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany(s => s.StoreInventories)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            // ❗ mỗi store chỉ có 1 record cho 1 nguyên liệu
            entity.HasIndex(x => new { x.StoreId, x.IngredientId })
                .IsUnique();

            entity.HasData(
                // Store 1
                new StoreInventory
                {
                    StoreInventoryId = 1,
                    StoreId = 1,
                    IngredientId = 1,
                    AvailableQty = 100,
                    ReservedQty = 0,
                    LastUpdated = new DateTime(2025, 1, 1)
                },
                new StoreInventory
                {
                    StoreInventoryId = 2,
                    StoreId = 1,
                    IngredientId = 2,
                    AvailableQty = 50,
                    ReservedQty = 0,
                    LastUpdated = new DateTime(2025, 1, 1)
                },

                // Store 2
                new StoreInventory
                {
                    StoreInventoryId = 3,
                    StoreId = 2,
                    IngredientId = 1,
                    AvailableQty = 80,
                    ReservedQty = 0,
                    LastUpdated = new DateTime(2025, 1, 1)
                },

                // Store 3
                new StoreInventory
                {
                    StoreInventoryId = 4,
                    StoreId = 3,
                    IngredientId = 2,
                    AvailableQty = 60,
                    ReservedQty = 0,
                    LastUpdated = new DateTime(2025, 1, 1)
                }
            );
        }
    }
}