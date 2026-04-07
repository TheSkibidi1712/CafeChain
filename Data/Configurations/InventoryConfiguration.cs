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
                .WithMany(x => x.InventoryDocuments)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany(x => x.InventoryDocuments)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Supplier)
                .WithMany(s => s.InventoryDocuments) // 🔥 thêm nav
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
                .WithMany(i => i.InventoryDocumentDetails) // 🔥 FIX
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

            entity.HasMany(x => x.InventoryDocuments)
                .WithOne(d => d.Supplier)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);


            entity.HasIndex(x => x.Code).IsUnique();

            entity.HasData(
                new Supplier { SupplierId = 1, Code = "SUP001", Name = "Nhà cung cấp A", Phone = "0901111111", Address = "Bình Dương", DebtAmount = 0, Active = true },
                new Supplier { SupplierId = 2, Code = "SUP002", Name = "Nhà cung cấp B", Phone = "0902222222", Address = "TP HCM", DebtAmount = 0, Active = true },
                new Supplier { SupplierId = 3, Code = "SUP003", Name = "Nhà cung cấp C", Phone = "0903333333", Address = "Đồng Nai", DebtAmount = 0, Active = true },
                new Supplier { SupplierId = 4, Code = "SUP004", Name = "Nhà cung cấp D", Phone = "0904444444", Address = "Hà Nội", DebtAmount = 0, Active = true },
                new Supplier { SupplierId = 5, Code = "SUP005", Name = "Nhà cung cấp E", Phone = "0905555555", Address = "Đà Nẵng", DebtAmount = 100000, Active = true }
            );
        }
    }
    // ================================ Unit ============================
    public class UnitConfiguration : IEntityTypeConfiguration<Unit>
    {
        public void Configure(EntityTypeBuilder<Unit> entity)
        {
            entity.ToTable("Units");

            entity.HasKey(x => x.UnitId);

            entity.Property(x => x.UnitCode)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(x => x.UnitCode).IsUnique();

            entity.HasData(
                new Unit { UnitId = 1, UnitCode = "g", Name = "Gram", Active = true },
                new Unit { UnitId = 2, UnitCode = "kg", Name = "Kilogram", Active = true },
                new Unit { UnitId = 3, UnitCode = "ml", Name = "Milliliter", Active = true },
                new Unit { UnitId = 4, UnitCode = "l", Name = "Liter", Active = true }
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

            entity.Property(x => x.FromQuantity)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.ToQuantity)
                .HasColumnType("decimal(18,3)");

            // Ingredient
            entity.HasOne(x => x.Ingredients)
                .WithMany(i => i.UnitConversions)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);

            // From Unit
            entity.HasOne(x => x.FromUnit)
                .WithMany(u => u.FromConversions)
                .HasForeignKey(x => x.FromUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // To Unit
            entity.HasOne(x => x.ToUnit)
                .WithMany(u => u.ToConversions)
                .HasForeignKey(x => x.ToUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔥 tránh duplicate rule
            entity.HasIndex(x => new { x.IngredientId, x.FromUnitId, x.ToUnitId })
                .IsUnique();

            entity.HasData(
                // Gram ↔ Kg
                new UnitConversion { UnitConversionId = 1, IngredientId = 1, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },

                // ml ↔ l
                new UnitConversion { UnitConversionId = 2, IngredientId = 2, FromUnitId = 4, FromQuantity = 1, ToUnitId = 3, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 3, IngredientId = 8, FromUnitId = 4, FromQuantity = 1, ToUnitId = 3, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 4, IngredientId = 10, FromUnitId = 4, FromQuantity = 1, ToUnitId = 3, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 5, IngredientId = 13, FromUnitId = 4, FromQuantity = 1, ToUnitId = 3, ToQuantity = 1000 }
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
                t.HasCheckConstraint("CK_InventoryTransaction_Qty", "[Quantity] <> 0");
            });

            entity.HasKey(x => x.InventoryTransactionId);

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.RefType)
                .HasMaxLength(50);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATION =================

            entity.HasOne(x => x.StoreInventory)
            .WithMany(x => x.InventoryTransactions)
            .HasForeignKey(x => x.StoreInventoryId)
            .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.TransactionType)
                .WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.InventoryTransactionTypeId)
                .OnDelete(DeleteBehavior.Restrict);



            // ================= INDEX =================
            entity.HasIndex(x => new { x.RefType, x.RefId });
            entity.HasIndex(x => x.StoreInventoryId);
            entity.HasIndex(x => x.InventoryTransactionTypeId);

            // ================= SEED =================
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


    public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
    {
        public void Configure(EntityTypeBuilder<Ingredient> entity)
        {
            entity.ToTable("Ingredients");

            entity.HasKey(x => x.IngredientId);

            // ================= PROPERTY =================

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.BaseUnitId)
                .IsRequired();

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            // ================= INDEX =================

            entity.HasIndex(x => x.Code).IsUnique();

            // ❗ optional: bỏ nếu không cần strict
            // entity.HasIndex(x => x.Name).IsUnique();

            // ================= RELATION =================

            entity.HasOne(x => x.BaseUnit)
                .WithMany(u => u.Ingredients)
                .HasForeignKey(x => x.BaseUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.InventoryDocumentDetails)
                .WithOne(d => d.Ingredient)
                .HasForeignKey(d => d.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= SEED =================
            // ⚠️ phải dùng BaseUnitId (FK), KHÔNG dùng string nữa

            entity.HasData(
                new Ingredient { IngredientId = 1, Code = "ING00001", Name = "Cà phê hạt Robusta 1kg", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 2, Code = "ING00002", Name = "Sữa đặc Ông Thọ Vinamilk 380g", BaseUnitId = 3, Active = true },
                new Ingredient { IngredientId = 3, Code = "ING00003", Name = "Trà đen Lipton hộp 100 túi", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 4, Code = "ING00004", Name = "Bột sữa B-One 1kg", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 5, Code = "ING00005", Name = "Bột cacao Van Houten 1kg", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 6, Code = "ING00006", Name = "Đường trắng Biên Hòa 1kg", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 7, Code = "ING00007", Name = "Đá viên 1kg", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 8, Code = "ING00008", Name = "Syrup Torani Vanilla 750ml", BaseUnitId = 3, Active = true },
                new Ingredient { IngredientId = 9, Code = "ING00009", Name = "Matcha Nhật Bản 500g", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 10, Code = "ING00010", Name = "Kem béo Rich's 1L", BaseUnitId = 3, Active = true },
                new Ingredient { IngredientId = 11, Code = "ING00011", Name = "Bột năng Vĩnh Thuận 400g", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 12, Code = "ING00012", Name = "Đường nâu Hàn Quốc 1kg", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 13, Code = "ING00013", Name = "Nước lọc Lavie 500ml", BaseUnitId = 3, Active = true }
            );
        }
    }

    // ================================ IngredientSupplier ============================
    public class IngredientSupplierConfiguration : IEntityTypeConfiguration<IngredientSupplier>
    {
        public void Configure(EntityTypeBuilder<IngredientSupplier> entity)
        {
            entity.ToTable("IngredientSuppliers");

            entity.HasKey(x => x.IngredientSupplierId);

            entity.Property(x => x.Price)
                .HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Ingredient)
                .WithMany(x => x.IngredientSuppliers)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.IngredientSuppliers)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Unit)
                .WithMany()
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔥 tránh trùng
            entity.HasIndex(x => new { x.IngredientId, x.SupplierId })
                .IsUnique();

            entity.HasData(
                // ===== ĐƯỜNG BIÊN HÒA =====
                new IngredientSupplier
                {
                    IngredientSupplierId = 1,
                    IngredientId = 6,
                    SupplierId = 1,
                    Price = 22000, // 22k / 1kg
                    UnitId = 2, // kg
                    IsPrimary = true
                },

                // ===== SỮA ĐẶC ÔNG THỌ =====
                new IngredientSupplier
                {
                    IngredientSupplierId = 2,
                    IngredientId = 2,
                    SupplierId = 2,
                    Price = 27000, // ~27k / lon 380g (~300ml)
                    UnitId = 3, // ml (quy đổi base)
                    IsPrimary = true
                },

                // ===== CÀ PHÊ HẠT =====
                new IngredientSupplier
                {
                    IngredientSupplierId = 3,
                    IngredientId = 1,
                    SupplierId = 3,
                    Price = 140000, // 140k / kg
                    UnitId = 2,
                    IsPrimary = true
                },

                // ===== SYRUP TORANI =====
                new IngredientSupplier
                {
                    IngredientSupplierId = 4,
                    IngredientId = 8,
                    SupplierId = 4,
                    Price = 250000, // 750ml
                    UnitId = 3,
                    IsPrimary = true
                },

                // ===== KEM BÉO RICH =====
                new IngredientSupplier
                {
                    IngredientSupplierId = 5,
                    IngredientId = 10,
                    SupplierId = 2,
                    Price = 95000, // 1L
                    UnitId = 4, // l
                    IsPrimary = true
                },

                // ===== MATCHA =====
                new IngredientSupplier
                {
                    IngredientSupplierId = 6,
                    IngredientId = 9,
                    SupplierId = 5,
                    Price = 450000, // 500g
                    UnitId = 1,
                    IsPrimary = true
                },

                // ===== CACAO =====
                new IngredientSupplier
                {
                    IngredientSupplierId = 7,
                    IngredientId = 5,
                    SupplierId = 3,
                    Price = 180000, // 1kg
                    UnitId = 2,
                    IsPrimary = false
                },

                // ===== BỘT SỮA =====
                new IngredientSupplier
                {
                    IngredientSupplierId = 8,
                    IngredientId = 4,
                    SupplierId = 1,
                    Price = 85000, // 1kg
                    UnitId = 2,
                    IsPrimary = false
                },

                // ===== TRÀ LIPTON =====
                new IngredientSupplier
                {
                    IngredientSupplierId = 9,
                    IngredientId = 3,
                    SupplierId = 4,
                    Price = 120000, // 100 túi
                    UnitId = 1,
                    IsPrimary = true
                }
            );
        }
    }
}