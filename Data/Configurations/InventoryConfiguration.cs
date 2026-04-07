using CafeChain.Models;
using CafeChain.Models.Inventories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ================================ Inventory Document ============================
    public class InventoryDocumentConfiguration : IEntityTypeConfiguration<InventoryDocument>
    {
        public void Configure(EntityTypeBuilder<InventoryDocument> entity)
        {
            entity.ToTable("InventoryDocuments");

            entity.HasKey(x => x.InventoryDocumentId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Type)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            entity.Property(x => x.DocumentDate)
                .HasDefaultValueSql("GETDATE()");

            entity.HasIndex(x => x.Code).IsUnique();

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    // ================================ InventoryDocumentDetail ============================
    public class InventoryDocumentDetailConfiguration : IEntityTypeConfiguration<InventoryDocumentDetail>
    {
        public void Configure(EntityTypeBuilder<InventoryDocumentDetail> entity)
        {
            entity.ToTable("InventoryDocumentDetails");

            entity.HasKey(x => x.InventoryDocumentDetailId);

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.Unit)
                .HasMaxLength(50);

            entity.Property(x => x.UnitPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            entity.HasOne(x => x.InventoryDocument)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.InventoryDocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    // ================================ Supplier ============================
    public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
    {
        public void Configure(EntityTypeBuilder<Supplier> entity)
        {
            entity.ToTable("Suppliers");

            entity.HasKey(x => x.SupplierId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Phone)
                .HasMaxLength(20);

            entity.Property(x => x.Address)
                .HasMaxLength(300);

            entity.Property(x => x.DebtAmount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Code).IsUnique();

            entity.HasData(
                new Supplier { SupplierId = 1, Code = "SUP001", Name = "Nhà cung cấp A", Phone = "0901111111", Address = "Bình Dương", DebtAmount = 0, Active = true },
                new Supplier { SupplierId = 2, Code = "SUP002", Name = "Nhà cung cấp B", Phone = "0902222222", Address = "TP HCM", DebtAmount = 0, Active = true },
                new Supplier { SupplierId = 3, Code = "SUP003", Name = "Nhà cung cấp C", Phone = "0903333333", Address = "Đồng Nai", DebtAmount = 0, Active = true },
                new Supplier { SupplierId = 4, Code = "SUP004", Name = "Nhà cung cấp D", Phone = "0904444444", Address = "Hà Nội", DebtAmount = 0, Active = true },
                new Supplier { SupplierId = 5, Code = "SUP005", Name = "Nhà cung cấp E", Phone = "0905555555", Address = "Đà Nẵng", DebtAmount = 0, Active = true }
            );
        }
    }

    // ================================ Unit Conversion ============================
    public class UnitConversionConfiguration : IEntityTypeConfiguration<UnitConversion>
    {
        public void Configure(EntityTypeBuilder<UnitConversion> entity)
        {
            entity.ToTable("UnitConversions");

            entity.HasKey(x => x.UnitConversionId);

            entity.Property(x => x.FromUnit).HasMaxLength(50);
            entity.Property(x => x.ToUnit).HasMaxLength(50);

            entity.Property(x => x.Ratio)
                .HasColumnType("decimal(18,6)");

            entity.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.IngredientId, x.FromUnit, x.ToUnit })
                .IsUnique();
        }
    }

    // ================================ StockTake ============================
    public class StockTakeConfiguration : IEntityTypeConfiguration<StockTake>
    {
        public void Configure(EntityTypeBuilder<StockTake> entity)
        {
            entity.ToTable("StockTakes");

            entity.HasKey(x => x.StockTakeId);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.Property(x => x.IsBalanced)
                .HasDefaultValue(false);

            // ================= RELATION =================

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => x.CreatedAt);
        }
    }

    // ================================ StockTakeDetail ============================
    public class StockTakeDetailConfiguration : IEntityTypeConfiguration<StockTakeDetail>
    {
        public void Configure(EntityTypeBuilder<StockTakeDetail> entity)
        {
            entity.ToTable("StockTakeDetails", t =>
            {
                t.HasCheckConstraint("CK_StockTakeDetail_Qty", "[SystemQty] >= 0 AND [ActualQty] >= 0");
            });

            entity.HasKey(x => x.StockTakeDetailId);

            entity.Property(x => x.SystemQty)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.ActualQty)
                .HasColumnType("decimal(18,3)");

            // ================= RELATION =================

            entity.HasOne(x => x.StockTake)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.StockTakeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.StoreInventory)
                .WithMany()
                .HasForeignKey(x => x.StoreInventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            // ❌ không cho duplicate nguyên liệu trong cùng phiếu kiểm
            entity.HasIndex(x => new { x.StockTakeId, x.StoreInventoryId })
                .IsUnique();
        }
    }

    // ================================ Waste ============================
    public class WasteConfiguration : IEntityTypeConfiguration<Waste>
    {
        public void Configure(EntityTypeBuilder<Waste> entity)
        {
            entity.ToTable("Wastes");

            entity.HasKey(x => x.WasteId);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            // ================= RELATION =================

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => x.CreatedAt);
        }
    }

    // ================================ WasteDetail ============================
    public class WasteDetailConfiguration : IEntityTypeConfiguration<WasteDetail>
    {
        public void Configure(EntityTypeBuilder<WasteDetail> entity)
        {
            entity.ToTable("WasteDetails", t =>
            {
                t.HasCheckConstraint("CK_WasteDetail_Qty", "[Quantity] > 0");
            });

            entity.HasKey(x => x.WasteDetailId);

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)");

            // ================= RELATION =================

            entity.HasOne(x => x.Waste)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.WasteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.StoreInventory)
                .WithMany()
                .HasForeignKey(x => x.StoreInventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.WasteReason)
                .WithMany(x => x.WasteDetails)
                .HasForeignKey(x => x.WasteReasonId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => new { x.WasteId, x.StoreInventoryId })
                .IsUnique(); // ❌ không cho duplicate trong 1 phiếu
        }
    }

    // ================================ WasteReason ============================
    public class WasteReasonConfiguration : IEntityTypeConfiguration<WasteReason>
    {
        public void Configure(EntityTypeBuilder<WasteReason> entity)
        {
            entity.ToTable("WasteReasons");

            entity.HasKey(x => x.WasteReasonId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.HasData(
                new WasteReason { WasteReasonId = 1, Code = "EXPIRED", Name = "Hết hạn", Active = true },
                new WasteReason { WasteReasonId = 2, Code = "BROKEN", Name = "Đổ vỡ", Active = true },
                new WasteReason { WasteReasonId = 3, Code = "DAMAGED", Name = "Hư hỏng", Active = true }
            );
        }
    }


    // ================================ InventoryTransaction ============================
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
            entity.HasIndex(x => new { x.RefType, x.RefId });
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

            entity.Property(x => x.IsSystem)
                .HasDefaultValue(false);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasIndex(x => x.Code)
                .IsUnique(); // 🔥 rất quan trọng

            

            entity.HasData(
                new InventoryTransactionType { InventoryTransactionTypeId = 1, Code = "IMPORT", Name = "Nhập kho", IsSystem = true },
                new InventoryTransactionType { InventoryTransactionTypeId = 2, Code = "EXPORT", Name = "Xuất kho", IsSystem = true },
                new InventoryTransactionType { InventoryTransactionTypeId = 3, Code = "ADJUST", Name = "Điều chỉnh", IsSystem = true },
                new InventoryTransactionType { InventoryTransactionTypeId = 4, Code = "WASTE", Name = "Hao hụt", IsSystem = true }
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

            entity.HasOne(x => x.Supplier)
                .WithMany(s => s.StockImports)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany(d => d.StockImports)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => x.StaffId);
            entity.HasIndex(x => x.ImportDate);

            entity.HasData(
                new StockImport { StockImportId = 1, StoreId = 1, StaffId = 108, SupplierId = 1, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập đầu ngày" },
                new StockImport { StockImportId = 2, StoreId = 1, StaffId = 109, SupplierId = 2, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập bổ sung" },
                new StockImport { StockImportId = 3, StoreId = 2, StaffId = 108, SupplierId = 3, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập nguyên liệu" },
                new StockImport { StockImportId = 4, StoreId = 2, StaffId = 110, SupplierId = 1, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập kho" },
                new StockImport { StockImportId = 5, StoreId = 3, StaffId = 109, SupplierId = 2, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập định kỳ" },
                new StockImport { StockImportId = 6, StoreId = 1, StaffId = 108, SupplierId = 3, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập thêm" },
                new StockImport { StockImportId = 7, StoreId = 2, StaffId = 109, SupplierId = 4, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập hàng" },
                new StockImport { StockImportId = 8, StoreId = 3, StaffId = 110, SupplierId = 5, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập tuần" },
                new StockImport { StockImportId = 9, StoreId = 1, StaffId = 110, SupplierId = 1, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập khẩn" },
                new StockImport { StockImportId = 10, StoreId = 2, StaffId = 108, SupplierId = 2, ImportDate = new DateTime(2025, 5, 1), Note = "Nhập cuối ngày" }
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

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.BaseUnit)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Name).IsUnique();

            entity.HasData(
                new Ingredient { IngredientId = 1, Code = "ING00001", Name = "Cà phê", BaseUnit = "ml", Active = true },
                new Ingredient { IngredientId = 2, Code = "ING00002", Name = "Sữa đặc", BaseUnit = "ml", Active = true },
                new Ingredient { IngredientId = 3, Code = "ING00003", Name = "Trà đen", BaseUnit = "ml", Active = true },
                new Ingredient { IngredientId = 4, Code = "ING00004", Name = "Bột sữa", BaseUnit = "g", Active = true },
                new Ingredient { IngredientId = 5, Code = "ING00005", Name = "Bột socola", BaseUnit = "g", Active = true },
                new Ingredient { IngredientId = 6, Code = "ING00006", Name = "Đường", BaseUnit = "g", Active = true },
                new Ingredient { IngredientId = 7, Code = "ING00007", Name = "Đá", BaseUnit = "g", Active = true },
                new Ingredient { IngredientId = 8, Code = "ING00008", Name = "Syrup", BaseUnit = "ml", Active = true },
                new Ingredient { IngredientId = 9, Code = "ING00009", Name = "Matcha", BaseUnit = "g", Active = true },
                new Ingredient { IngredientId = 10, Code = "ING00010", Name = "Kem béo", BaseUnit = "ml", Active = true },
                new Ingredient { IngredientId = 11, Code = "ING00011", Name = "Bột năng", BaseUnit = "g", Active = true },
                new Ingredient { IngredientId = 12, Code = "ING00012", Name = "Đường nâu", BaseUnit = "g", Active = true },
                new Ingredient { IngredientId = 13, Code = "ING00013", Name = "Nước lọc", BaseUnit = "ml", Active = true },
                new Ingredient { IngredientId = 14, Code = "ING00014", Name = "Đường trắng", BaseUnit = "g", Active = true }
            );
        }
    }
}