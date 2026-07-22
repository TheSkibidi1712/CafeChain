using CafeChain.Models;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Stores
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
            .WithMany(x => x.Stores)
            .HasForeignKey(x => x.WardId)
            .OnDelete(DeleteBehavior.NoAction); // 🔥 FIX

            entity.HasOne(x => x.District)
                .WithMany()
                .HasForeignKey(x => x.DistrictId)
                .OnDelete(DeleteBehavior.NoAction); // 🔥 FIX

            entity.HasOne(x => x.Province)
                .WithMany()
                .HasForeignKey(x => x.ProvinceId)
                .OnDelete(DeleteBehavior.NoAction); // 🔥 FIX

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
                t.HasCheckConstraint(
                    "CK_StoreInventories_XOR_Item",
                    @"([IngredientId] IS NOT NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NULL)
                    OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL AND [PreparedItemId] IS NULL)
                    OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL AND [PreparedItemId] IS NOT NULL)
                    OR ([IngredientId] IS NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NOT NULL)"
                );

                // ADR-0001: ĐÃ XÓA CK_StoreInventory_AvailableQty >= 0
                // Lý do: Blind Selling cho phép kho âm khi bán trước nhập kho sau.
                // Kiểm soát kho âm nằm ở tầng Service (InventoryDeductionService)
                // + InventoryTransaction log (BeforeQty/AfterQty) để đối soát.

                t.HasCheckConstraint(
                    "CK_StoreInventory_ReservedQty",
                    "[ReservedQty] >= 0"
                );

                t.HasCheckConstraint(
                    "CK_StoreInventories_BtpLifecycle",
                    @"([IngredientId] IS NOT NULL
                        AND [BtpIdentityState] IS NULL
                        AND [QuantitySemanticsStatus] IS NULL
                        AND [SupersededByStoreInventoryId] IS NULL
                        AND [QuantitySemanticsEvidenceType] IS NULL
                        AND [QuantitySemanticsEvidenceReference] IS NULL
                        AND [QuantitySemanticsReviewedAt] IS NULL
                        AND [QuantitySemanticsReviewedByAccountId] IS NULL)
                    OR ([IngredientId] IS NULL AND (
                        ([BtpIdentityState] = 0 AND [QuantitySemanticsStatus] IS NOT NULL AND [SupersededByStoreInventoryId] IS NULL)
                        OR ([BtpIdentityState] = 1 AND [PreparedItemId] IS NOT NULL AND [QuantitySemanticsStatus] = 1
                            AND [SupersededByStoreInventoryId] IS NULL
                            AND [QuantitySemanticsEvidenceType] IS NOT NULL
                            AND [QuantitySemanticsEvidenceReference] IS NOT NULL
                            AND [QuantitySemanticsReviewedAt] IS NOT NULL
                            AND [QuantitySemanticsReviewedByAccountId] IS NOT NULL)
                        OR ([BtpIdentityState] = 2 AND [QuantitySemanticsStatus] IS NOT NULL AND [SupersededByStoreInventoryId] IS NOT NULL)))"
                );

                t.HasCheckConstraint(
                    "CK_StoreInventories_QuantityEvidence",
                    @"[QuantitySemanticsStatus] IS NULL
                    OR [QuantitySemanticsStatus] = 0
                    OR ([QuantitySemanticsEvidenceType] IS NOT NULL
                        AND [QuantitySemanticsEvidenceReference] IS NOT NULL
                        AND [QuantitySemanticsReviewedAt] IS NOT NULL
                        AND [QuantitySemanticsReviewedByAccountId] IS NOT NULL)"
                );

                t.HasCheckConstraint(
                    "CK_StoreInventories_NotSelfSuperseded",
                    "[SupersededByStoreInventoryId] IS NULL OR [SupersededByStoreInventoryId] <> [StoreInventoryId]"
                );

                // ADR-0001: ĐÃ XÓA CK_StoreInventory_ReservedQty_Valid (ReservedQty <= AvailableQty)
                // Lý do: Constraint này tự động vi phạm khi AvailableQty xuống âm.
            });

            entity.HasKey(x => x.StoreInventoryId);

            // ================= COLUMN =================
            entity.Property(x => x.AvailableQty)
                .HasColumnType("decimal(18,3)")
                .HasDefaultValue(0);

            entity.Property(x => x.ReservedQty)
                .HasColumnType("decimal(18,3)")
                .HasDefaultValue(0);

            entity.Property(x => x.MaxNegativeQty)
                .HasColumnType("decimal(18,3)");

            // Issue #97 — nullable min threshold for stock alerts
            entity.Property(x => x.MinStockLevel)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.LastUpdated)
                .HasDefaultValueSql("GETDATE()");

            entity.Property(x => x.BtpIdentityState).HasConversion<int?>();
            entity.Property(x => x.QuantitySemanticsStatus).HasConversion<int?>();
            entity.Property(x => x.QuantitySemanticsEvidenceType).HasConversion<int?>();
            entity.Property(x => x.QuantitySemanticsEvidenceReference).HasMaxLength(500);

            // 🔥 QUAN TRỌNG: concurrency token
            entity.Property(x => x.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            // ================= RELATION =================
            entity.HasOne(x => x.Store)
                .WithMany(x => x.StoreInventories)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany(x => x.StoreInventories)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Recipe)
                .WithMany()
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PreparedItem)
                .WithMany()
                .HasForeignKey(x => x.PreparedItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SupersededByStoreInventory)
                .WithMany()
                .HasForeignKey(x => x.SupersededByStoreInventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<CafeChain.Models.Customers.Account>()
                .WithMany()
                .HasForeignKey(x => x.QuantitySemanticsReviewedByAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= UNIQUE =================
            entity.HasIndex(x => new { x.StoreId, x.IngredientId })
                .IsUnique()
                .HasFilter("[IngredientId] IS NOT NULL")
                .HasDatabaseName("UX_Store_Ingredient");

            entity.HasIndex(x => new { x.StoreId, x.RecipeId })
                .IsUnique()
                .HasFilter("[RecipeId] IS NOT NULL")
                .HasDatabaseName("UX_Store_Recipe");

            // Intentionally non-unique during the dual-read period. A legacy RecipeId row
            // and a future PreparedItem-only row may coexist until a reviewed cutover.
            entity.HasIndex(x => new { x.StoreId, x.PreparedItemId })
                .HasFilter("[PreparedItemId] IS NOT NULL")
                .HasDatabaseName("IX_Store_PreparedItem_Compatibility");

            // Reverse property order so EF keeps this as a separate index from the
            // #115 StoreId + PreparedItemId compatibility index. Uniqueness is still
            // enforced on the same pair for canonical rows.
            entity.HasIndex(x => new { x.PreparedItemId, x.StoreId })
                .IsUnique()
                .HasFilter("[PreparedItemId] IS NOT NULL AND [BtpIdentityState] = 1")
                .HasDatabaseName("UX_Store_PreparedItem_Canonical");

            // ================= INDEX =================
            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => x.IngredientId);
            entity.HasIndex(x => x.RecipeId);
            entity.HasIndex(x => x.PreparedItemId);
            entity.HasIndex(x => x.SupersededByStoreInventoryId);

            // ================= SEED =================
            entity.HasData(
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
                new StoreInventory
                {
                    StoreInventoryId = 3,
                    StoreId = 2,
                    IngredientId = 1,
                    AvailableQty = 80,
                    ReservedQty = 0,
                    LastUpdated = new DateTime(2025, 1, 1)
                },
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

    // ================================ MODULE POS TERMINAL ================================
    public class PosTerminalConfiguration : IEntityTypeConfiguration<PosTerminal>
    {
        public void Configure(EntityTypeBuilder<PosTerminal> entity)
        {
            entity.ToTable("PosTerminals");

            entity.HasKey(x => x.TerminalId);

            entity.Property(x => x.TerminalId)
                .HasMaxLength(100);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    // ================================ MODULE WORK SHIFT (Quản lý Ca) ================================
    public class WorkShiftConfiguration : IEntityTypeConfiguration<WorkShift>
    {
        public void Configure(EntityTypeBuilder<WorkShift> entity)
        {
            entity.ToTable("WorkShifts");

            entity.HasKey(x => x.ShiftId);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Open");

            entity.Property(x => x.StartingCash)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            entity.Property(x => x.ExpectedEndingCash)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            entity.Property(x => x.ActualEndingCash)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.CashDiscrepancy)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.IsExceptionClosed)
                .HasDefaultValue(false);

            entity.Property(x => x.ExceptionCloseReason)
                .HasMaxLength(500);

            entity.Property(x => x.OfflineEstimatedTotalAtClose)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.OfflineCashTotalAtClose)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.RequiresReconciliation)
                .HasDefaultValue(false);

            entity.Property(x => x.HasLateOfflineSync)
                .HasDefaultValue(false);

            entity.Property(x => x.LateOfflineSyncCount)
                .HasDefaultValue(0);

            entity.HasIndex(x => x.ExceptionClosedByStaffId);

            entity.HasIndex(x => x.StoreId);

            entity.HasIndex(x => new { x.StoreId, x.RequiresReconciliation });

            // ─── Relationships ────────────────────────────
            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ExceptionClosedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ExceptionClosedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            // WorkShift → Orders (1:N)
            entity.HasMany(x => x.Orders)
                .WithOne(o => o.WorkShift)
                .HasForeignKey(o => o.WorkShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            // WorkShift → PosTerminal (N:1)
            entity.HasOne(x => x.PosTerminal)
                .WithMany(p => p.WorkShifts)
                .HasForeignKey(x => x.PosTerminalId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
