using CafeChain.Models;
using CafeChain.Models.Inventories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ================================ MODULE INVENTORY ============================
    public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
    {
        public void Configure(EntityTypeBuilder<InventoryTransaction> entity)
        {
            entity.ToTable("InventoryTransactions", t =>
            {
                t.HasCheckConstraint("CK_InventoryTransaction_Qty", "[Quantity] > 0");
            });

            entity.HasKey(x => x.InventoryTransactionId);

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.RefType)
                .HasMaxLength(50);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATION =================

            // ✅ ĐÚNG: StoreInventory
            entity.HasOne(x => x.StoreInventory)
                .WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.StoreInventoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ ĐÚNG: TransactionType
            entity.HasOne(x => x.TransactionType)
                .WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.InventoryTransactionTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ ĐÚNG: StockImport
            entity.HasOne(x => x.StockImport)
                .WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.StockImportId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => x.StoreInventoryId);
            entity.HasIndex(x => x.StockImportId);
            entity.HasIndex(x => x.InventoryTransactionTypeId);

            // ================= SEED =================

            entity.HasData(
                new InventoryTransaction
                {
                    InventoryTransactionId = 1,
                    StoreInventoryId = 1, // ✅ QUAN TRỌNG
                    StockImportId = 1,
                    InventoryTransactionTypeId = 1,
                    Quantity = 1000m,
                    RefType = "IMPORT",
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new InventoryTransaction
                {
                    InventoryTransactionId = 2,
                    StoreInventoryId = 2,
                    StockImportId = 2,
                    InventoryTransactionTypeId = 1,
                    Quantity = 800m,
                    RefType = "IMPORT",
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new InventoryTransaction
                {
                    InventoryTransactionId = 3,
                    StoreInventoryId = 3,
                    StockImportId = 3,
                    InventoryTransactionTypeId = 2,
                    Quantity = 200m,
                    RefType = "EXPORT",
                    CreatedAt = new DateTime(2025, 1, 1)
                }
            );
        }
    }

    public class InventoryTransactionTypeConfiguration : IEntityTypeConfiguration<InventoryTransactionType>
    {
        public void Configure(EntityTypeBuilder<InventoryTransactionType> entity)
        {
            entity.ToTable("InventoryTransactionTypes");

            entity.HasKey(x => x.InventoryTransactionTypeId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasIndex(x => x.Code)
                .IsUnique(); // 🔥 rất quan trọng

            entity.HasData(
                new InventoryTransactionType { InventoryTransactionTypeId = 1, Code = "IMPORT", Name = "Nhập kho" },
                new InventoryTransactionType { InventoryTransactionTypeId = 2, Code = "EXPORT", Name = "Xuất kho" },
                new InventoryTransactionType { InventoryTransactionTypeId = 3, Code = "ADJUST", Name = "Điều chỉnh" },
                new InventoryTransactionType { InventoryTransactionTypeId = 4, Code = "WASTE", Name = "Hao hụt" },
                new InventoryTransactionType { InventoryTransactionTypeId = 5, Code = "RETURN", Name = "Trả hàng" },
                new InventoryTransactionType { InventoryTransactionTypeId = 6, Code = "TRANSFER", Name = "Chuyển kho" },
                new InventoryTransactionType { InventoryTransactionTypeId = 7, Code = "SALE", Name = "Bán hàng" },
                new InventoryTransactionType { InventoryTransactionTypeId = 8, Code = "CANCEL", Name = "Hủy giao dịch" },
                new InventoryTransactionType { InventoryTransactionTypeId = 9, Code = "CHECK", Name = "Kiểm kho" },
                new InventoryTransactionType { InventoryTransactionTypeId = 10, Code = "LOSS", Name = "Thất thoát" }
            );
        }
    }

    public class StockImportConfiguration : IEntityTypeConfiguration<StockImport>
    {
        public void Configure(EntityTypeBuilder<StockImport> entity)
        {
            entity.ToTable("StockImports");

            entity.HasKey(x => x.StockImportId);

            entity.Property(x => x.ImportDate)
                .HasDefaultValueSql("GETDATE()");

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            entity.HasOne(x => x.Store)
                .WithMany(s => s.StockImports)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany(d => d.StockImports)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => x.StaffId);
            entity.HasIndex(x => x.ImportDate);

            entity.HasData(
                new StockImport { StockImportId = 1, StoreId = 1, StaffId = 1, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập đầu ngày" },
                new StockImport { StockImportId = 2, StoreId = 1, StaffId = 2, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập bổ sung" },
                new StockImport { StockImportId = 3, StoreId = 2, StaffId = 1, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập nguyên liệu" },
                new StockImport { StockImportId = 4, StoreId = 2, StaffId = 3, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập kho" },
                new StockImport { StockImportId = 5, StoreId = 3, StaffId = 2, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập định kỳ" },
                new StockImport { StockImportId = 6, StoreId = 1, StaffId = 1, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập thêm" },
                new StockImport { StockImportId = 7, StoreId = 2, StaffId = 2, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập hàng" },
                new StockImport { StockImportId = 8, StoreId = 3, StaffId = 3, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập tuần" },
                new StockImport { StockImportId = 9, StoreId = 1, StaffId = 3, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập khẩn" },
                new StockImport { StockImportId = 10, StoreId = 2, StaffId = 1, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập cuối ngày" }
            );
        }
    }

    public class StockImportDetailConfiguration : IEntityTypeConfiguration<StockImportDetail>
    {
        public void Configure(EntityTypeBuilder<StockImportDetail> entity)
        {
            entity.ToTable("StockImportDetails", t =>
            {
                t.HasCheckConstraint("CK_StockImportDetail_Qty", "[Quantity] > 0");
                t.HasCheckConstraint("CK_StockImportDetail_Price", "[UnitPrice] >= 0");
            });

            entity.HasKey(x => x.StockImportDetailId);

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.UnitPrice)
                .HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.StockImport)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.StockImportId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany(x => x.StockImportDetails)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔥 tránh duplicate ingredient trong 1 phiếu
            entity.HasIndex(x => new { x.StockImportId, x.IngredientId })
                .IsUnique();

            entity.HasData(
                new StockImportDetail { StockImportDetailId = 1, StockImportId = 1, IngredientId = 1, Quantity = 1000m, UnitPrice = 200000m },
                new StockImportDetail { StockImportDetailId = 2, StockImportId = 1, IngredientId = 2, Quantity = 500m, UnitPrice = 15000m },
                new StockImportDetail { StockImportDetailId = 3, StockImportId = 2, IngredientId = 3, Quantity = 800m, UnitPrice = 12000m },
                new StockImportDetail { StockImportDetailId = 4, StockImportId = 3, IngredientId = 4, Quantity = 600m, UnitPrice = 18000m },
                new StockImportDetail { StockImportDetailId = 5, StockImportId = 4, IngredientId = 5, Quantity = 700m, UnitPrice = 25000m },
                new StockImportDetail { StockImportDetailId = 6, StockImportId = 5, IngredientId = 6, Quantity = 1000m, UnitPrice = 8000m },
                new StockImportDetail { StockImportDetailId = 7, StockImportId = 6, IngredientId = 7, Quantity = 2000m, UnitPrice = 2000m },
                new StockImportDetail { StockImportDetailId = 8, StockImportId = 7, IngredientId = 8, Quantity = 300m, UnitPrice = 30000m },
                new StockImportDetail { StockImportDetailId = 9, StockImportId = 8, IngredientId = 9, Quantity = 400m, UnitPrice = 35000m },
                new StockImportDetail { StockImportDetailId = 10, StockImportId = 9, IngredientId = 10, Quantity = 500m, UnitPrice = 22000m }
            );
        }
    }

    public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
    {
        public void Configure(EntityTypeBuilder<Ingredient> entity)
        {
            entity.ToTable("Ingredients");

            entity.HasKey(x => x.IngredientId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Unit)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Name)
                .IsUnique();

            entity.HasData(
                new Ingredient { IngredientId = 1, Name = "Cà phê", Unit = "ml", Active = true },
                new Ingredient { IngredientId = 2, Name = "Sữa đặc", Unit = "ml", Active = true },
                new Ingredient { IngredientId = 3, Name = "Trà đen", Unit = "ml", Active = true },
                new Ingredient { IngredientId = 4, Name = "Bột sữa", Unit = "g", Active = true },
                new Ingredient { IngredientId = 5, Name = "Bột socola", Unit = "g", Active = true },
                new Ingredient { IngredientId = 6, Name = "Đường", Unit = "g", Active = true },
                new Ingredient { IngredientId = 7, Name = "Đá", Unit = "g", Active = true },
                new Ingredient { IngredientId = 8, Name = "Nước lọc", Unit = "ml", Active = true },

                // topping raw (nếu quản lý kho topping)
                new Ingredient { IngredientId = 9, Name = "Trân châu đen", Unit = "g", Active = true },
                new Ingredient { IngredientId = 10, Name = "Thạch", Unit = "g", Active = true }
            );
        }
    }
}