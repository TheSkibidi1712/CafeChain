using CafeChain.Models;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ================================ Inventory Debt ============================
    public class InventoryDebtConfiguration : IEntityTypeConfiguration<InventoryDebt>
    {
        public void Configure(EntityTypeBuilder<InventoryDebt> builder)
        {
            builder.ToTable("InventoryDebts");

            // ================= PRIMARY KEY =================
            builder.HasKey(x => x.InventoryDebtId);

            // ================= PROPERTIES =================
            builder.Property(x => x.PartnerName)
                .HasMaxLength(255);

            builder.Property(x => x.Amount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(x => x.PaidAmount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.PaidAt)
                .IsRequired(false);

            // ================= INDEX =================
            builder.HasIndex(x => x.InventoryDocumentId);

            builder.HasIndex(x => new { x.PartnerType, x.PartnerId });
            builder.HasIndex(x => x.PaidAmount);

            // ================= RELATION =================
            builder.HasOne<InventoryDocument>()
                .WithMany(x => x.Debts)
                .HasForeignKey(x => x.InventoryDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= CONSTRAINT =================

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_InventoryDebt_Amount", "[Amount] >= 0");
                t.HasCheckConstraint("CK_InventoryDebt_PaidAmount", "[PaidAmount] >= 0");
                t.HasCheckConstraint("CK_InventoryDebt_Paid_Limit", "[PaidAmount] <= [Amount]");
            });
        }
    }

    // ================================ Inventory Document ============================
    public class InventoryDocumentConfiguration : IEntityTypeConfiguration<InventoryDocument>
    {
        public void Configure(EntityTypeBuilder<InventoryDocument> entity)
        {
            entity.ToTable("InventoryDocuments");

            entity.HasKey(x => x.InventoryDocumentId);

            // ================= BASIC =================

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.Property(x => x.Type)
                .IsRequired();

            entity.Property(x => x.Status)
                .IsRequired();

            entity.Property(x => x.Purpose)
                .IsRequired();

            entity.Property(x => x.DocumentDate)
                .HasDefaultValueSql("GETDATE()");

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            // ================= PARTNER =================

            entity.Property(x => x.PartnerType)
                .HasDefaultValue(InventoryPartnerType.NONE); // NONE

            entity.Property(x => x.PartnerName)
                .HasMaxLength(200);


            // ================= RELATION =================

            entity.HasOne(x => x.Store)
                .WithMany(x => x.InventoryDocuments)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany(x => x.InventoryDocuments)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.InventoryDocuments)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.SetNull); // 🔥 đúng

            // ================= TRANSFER =================

            // export document (1-1)
            entity.HasOne(x => x.ExportTransfer)
                .WithOne(x => x.ExportDocument)
                .HasForeignKey<InventoryTransfer>(x => x.ExportDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            // import document (1-1)
            entity.HasOne(x => x.ImportTransfer)
                .WithOne(x => x.ImportDocument)
                .HasForeignKey<InventoryTransfer>(x => x.ImportDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => new { x.StoreId, x.DocumentDate });

            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.SupplierId);
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
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.BaseQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.UnitPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.TotalAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            // ================= RELATION =================

            entity.HasOne(x => x.InventoryDocument)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.InventoryDocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany(i => i.InventoryDocumentDetails)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Unit)
                .WithMany(x => x.InventoryDocumentDetails)
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);


            // ================= INDEX =================
            entity.HasIndex(x => x.InventoryDocumentId);
        }
    }

    // ================================ INVENTORY TRANSFER ============================
    public class InventoryTransferConfiguration : IEntityTypeConfiguration<InventoryTransfer>
    {
        public void Configure(EntityTypeBuilder<InventoryTransfer> entity)
        {
            entity.ToTable("InventoryTransfers");

            entity.HasKey(x => x.InventoryTransferId);

            // ================= BASIC =================

            entity.Property(x => x.Status)
                .IsRequired()
                .HasConversion<int>(); // 🔥 enum -> int

            entity.Property(x => x.TotalExportQty)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.TotalReceivedQty)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATION: DOCUMENT =================

            entity.HasOne(x => x.ExportDocument)
                .WithOne(d => d.ExportTransfer)
                .HasForeignKey<InventoryTransfer>(x => x.ExportDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ImportDocument)
                .WithOne(d => d.ImportTransfer)
                .HasForeignKey<InventoryTransfer>(x => x.ImportDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= RELATION: STORE =================

            entity.HasOne(x => x.FromStore)
                .WithMany(s => s.ExportTransfers)
                .HasForeignKey(x => x.FromStoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ToStore)
                .WithMany(s => s.ImportTransfers)
                .HasForeignKey(x => x.ToStoreId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= RELATION: DETAILS =================
            entity.HasMany(x => x.Details)
                .WithOne(d => d.InventoryTransfer)
                .HasForeignKey(d => d.InventoryTransferId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= INDEX =================

            entity.HasIndex(x => x.ExportDocumentId)
                .IsUnique(); // mỗi export chỉ có 1 transfer

            entity.HasIndex(x => x.ImportDocumentId)
                .IsUnique()
                .HasFilter("[ImportDocumentId] IS NOT NULL");

            entity.HasIndex(x => x.Status);

            entity.HasIndex(x => new { x.FromStoreId, x.ToStoreId });
        }
    }

    // ================================ InventoryTransferDetail ============================
    public class InventoryTransferDetailConfig : IEntityTypeConfiguration<InventoryTransferDetail>
    {
        public void Configure(EntityTypeBuilder<InventoryTransferDetail> builder)
        {
            builder.HasKey(x => x.InventoryTransferDetailId);

            builder.Property(x => x.ExportQuantity)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(x => x.ReceivedQuantity)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            builder.Property(x => x.UnitPrice)
                .HasColumnType("decimal(18,2)");

            builder.Property(x => x.Note)
                .HasMaxLength(500);

            // ===== RELATION =====
            builder.HasOne(x => x.Ingredient)
                .WithMany(i => i.InventoryTransferDetails)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔥 UNIQUE (1 ingredient / transfer)
            builder.HasIndex(x => new { x.InventoryTransferId, x.IngredientId })
                .IsUnique();
        }
    }

    // ================================ Supplier ============================
    public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
    {
        public void Configure(EntityTypeBuilder<Supplier> entity)
        {
            entity.ToTable("Suppliers");

            entity.HasKey(x => x.SupplierId);

            // ================= BASIC =================

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.TaxCode)
                .HasMaxLength(50);

            entity.Property(x => x.Website)
                .HasMaxLength(200);

            entity.Property(x => x.Address)
                .HasMaxLength(300);

            entity.Property(x => x.DebtAmount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            // ================= INDEX =================

            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.TaxCode);

            // ================= RELATION =================

            entity.HasMany(x => x.Phones)
                .WithOne(x => x.Supplier)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.BankAccounts)
                .WithOne(x => x.Supplier)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Contacts)
                .WithOne(x => x.Supplier)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= SEED =================

            entity.HasData(
                new Supplier { SupplierId = 1, Code = "SUP001", Name = "Nhà cung cấp A", TaxCode = "0101234567", Website = "https://supA.com", Address = "Bình Dương", DebtAmount = 0, Active = true },
                new Supplier { SupplierId = 2, Code = "SUP002", Name = "Nhà cung cấp B", TaxCode = "0201234567", Website = "https://supB.com", Address = "TP HCM", DebtAmount = 0, Active = true },
                new Supplier { SupplierId = 3, Code = "SUP003", Name = "Nhà cung cấp C", TaxCode = "0301234567", Website = "https://supC.com", Address = "Đồng Nai", DebtAmount = 0, Active = true },
                new Supplier { SupplierId = 4, Code = "SUP004", Name = "Nhà cung cấp D", TaxCode = "0401234567", Website = "https://supD.com", Address = "Hà Nội", DebtAmount = 0, Active = true },
                new Supplier { SupplierId = 5, Code = "SUP005", Name = "Nhà cung cấp E", TaxCode = "0501234567", Website = "https://supE.com", Address = "Đà Nẵng", DebtAmount = 100000, Active = true }
            );
        }
    }

    // ================================ SupplierPhone ============================
    public class SupplierPhoneConfiguration : IEntityTypeConfiguration<SupplierPhone>
    {
        public void Configure(EntityTypeBuilder<SupplierPhone> entity)
        {
            entity.ToTable("SupplierPhones");

            entity.HasKey(x => x.SupplierPhoneId);

            entity.Property(x => x.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.IsPrimary)
                .HasDefaultValue(false);

            entity.HasIndex(x => x.SupplierId);

            // ================= SEED =================

            entity.HasData(
                new SupplierPhone { SupplierPhoneId = 1, SupplierId = 1, PhoneNumber = "0901111111", IsPrimary = true },
                new SupplierPhone { SupplierPhoneId = 2, SupplierId = 1, PhoneNumber = "0901111112", IsPrimary = false },

                new SupplierPhone { SupplierPhoneId = 3, SupplierId = 2, PhoneNumber = "0902222222", IsPrimary = true },

                new SupplierPhone { SupplierPhoneId = 4, SupplierId = 3, PhoneNumber = "0903333333", IsPrimary = true },

                new SupplierPhone { SupplierPhoneId = 5, SupplierId = 4, PhoneNumber = "0904444444", IsPrimary = true },

                new SupplierPhone { SupplierPhoneId = 6, SupplierId = 5, PhoneNumber = "0905555555", IsPrimary = true }
            );
        }
    }

    // ================================ SupplierBankAccount ============================
    public class SupplierBankAccountConfiguration : IEntityTypeConfiguration<SupplierBankAccount>
    {
        public void Configure(EntityTypeBuilder<SupplierBankAccount> entity)
        {
            entity.ToTable("SupplierBankAccounts");

            entity.HasKey(x => x.SupplierBankAccountId);

            entity.Property(x => x.BankName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.AccountNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.AccountHolder)
                .HasMaxLength(150);

            entity.Property(x => x.IsPrimary)
                .HasDefaultValue(false);

            entity.HasIndex(x => x.SupplierId);

            entity.HasIndex(x => new { x.SupplierId, x.AccountNumber })
                .IsUnique();

            // ================= SEED =================

            entity.HasData(
                new SupplierBankAccount { SupplierBankAccountId = 1, SupplierId = 1, BankName = "Vietcombank", AccountNumber = "111111111", AccountHolder = "NCC A", IsPrimary = true },
                new SupplierBankAccount { SupplierBankAccountId = 2, SupplierId = 2, BankName = "ACB", AccountNumber = "222222222", AccountHolder = "NCC B", IsPrimary = true },
                new SupplierBankAccount { SupplierBankAccountId = 3, SupplierId = 3, BankName = "Techcombank", AccountNumber = "333333333", AccountHolder = "NCC C", IsPrimary = true },
                new SupplierBankAccount { SupplierBankAccountId = 4, SupplierId = 4, BankName = "BIDV", AccountNumber = "444444444", AccountHolder = "NCC D", IsPrimary = true },
                new SupplierBankAccount { SupplierBankAccountId = 5, SupplierId = 5, BankName = "MB Bank", AccountNumber = "555555555", AccountHolder = "NCC E", IsPrimary = true }
            );
        }
    }

    // ================================ SupplierContact ============================
    public class SupplierContactConfiguration : IEntityTypeConfiguration<SupplierContact>
    {
        public void Configure(EntityTypeBuilder<SupplierContact> entity)
        {
            entity.ToTable("SupplierContacts");

            entity.HasKey(x => x.SupplierContactId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Phone)
                .HasMaxLength(20);

            entity.Property(x => x.Email)
                .HasMaxLength(150);

            entity.Property(x => x.Position)
                .HasMaxLength(100);

            entity.Property(x => x.IsPrimary)
                .HasDefaultValue(false);

            entity.HasIndex(x => x.SupplierId);

            // ================= SEED =================

            entity.HasData(
                new SupplierContact { SupplierContactId = 1, SupplierId = 1, Name = "Nguyễn Văn A", Phone = "0901111111", Email = "a@supplier.com", Position = "Manager", IsPrimary = true },
                new SupplierContact { SupplierContactId = 2, SupplierId = 2, Name = "Trần Văn B", Phone = "0902222222", Email = "b@supplier.com", Position = "Sales", IsPrimary = true },
                new SupplierContact { SupplierContactId = 3, SupplierId = 3, Name = "Lê Văn C", Phone = "0903333333", Email = "c@supplier.com", Position = "Owner", IsPrimary = true },
                new SupplierContact { SupplierContactId = 4, SupplierId = 4, Name = "Phạm Văn D", Phone = "0904444444", Email = "d@supplier.com", Position = "Director", IsPrimary = true },
                new SupplierContact { SupplierContactId = 5, SupplierId = 5, Name = "Hoàng Văn E", Phone = "0905555555", Email = "e@supplier.com", Position = "Manager", IsPrimary = true }
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
                // ===== KHỐI LƯỢNG =====
                new Unit { UnitId = 1, UnitCode = "g", Name = "Gram", Type = UnitType.KhoiLuong, Active = true },
                new Unit { UnitId = 2, UnitCode = "kg", Name = "Kilogram", Type = UnitType.KhoiLuong, Active = true },

                // ===== THỂ TÍCH =====
                new Unit { UnitId = 3, UnitCode = "ml", Name = "Milliliter", Type = UnitType.TheTich, Active = true },
                new Unit { UnitId = 4, UnitCode = "l", Name = "Liter", Type = UnitType.TheTich, Active = true },
                new Unit { UnitId = 5, UnitCode = "oz", Name = "Ounce", Type = UnitType.TheTich, Active = true },
                new Unit { UnitId = 6, UnitCode = "cup", Name = "Cup", Type = UnitType.TheTich, Active = true },
                new Unit { UnitId = 7, UnitCode = "tbsp", Name = "Tablespoon", Type = UnitType.TheTich, Active = true },
                new Unit { UnitId = 8, UnitCode = "tsp", Name = "Teaspoon", Type = UnitType.TheTich, Active = true },

                // ===== ĐẾM =====
                new Unit { UnitId = 9, UnitCode = "pcs", Name = "Piece", Type = UnitType.Dem, Active = true },
                new Unit { UnitId = 10, UnitCode = "bottle", Name = "Bottle", Type = UnitType.Dem, Active = true },
                new Unit { UnitId = 11, UnitCode = "can", Name = "Can", Type = UnitType.Dem, Active = true },
                new Unit { UnitId = 12, UnitCode = "pack", Name = "Pack", Type = UnitType.Dem, Active = true }
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

                // ================= MASS (kg -> g) =================
                new UnitConversion { UnitConversionId = 1, IngredientId = 1, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 2, IngredientId = 3, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 3, IngredientId = 4, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 4, IngredientId = 5, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 5, IngredientId = 6, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 6, IngredientId = 7, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 7, IngredientId = 9, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 8, IngredientId = 11, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 9, IngredientId = 12, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },

                // ================= VOLUME (l -> ml) =================
                new UnitConversion { UnitConversionId = 20, IngredientId = 2, FromUnitId = 4, FromQuantity = 1, ToUnitId = 3, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 21, IngredientId = 8, FromUnitId = 4, FromQuantity = 1, ToUnitId = 3, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 22, IngredientId = 10, FromUnitId = 4, FromQuantity = 1, ToUnitId = 3, ToQuantity = 1000 },
                new UnitConversion { UnitConversionId = 23, IngredientId = 13, FromUnitId = 4, FromQuantity = 1, ToUnitId = 3, ToQuantity = 1000 },

                // ================= oz =================
                new UnitConversion { UnitConversionId = 30, IngredientId = 2, FromUnitId = 5, FromQuantity = 1, ToUnitId = 3, ToQuantity = 29.5735m },
                new UnitConversion { UnitConversionId = 31, IngredientId = 8, FromUnitId = 5, FromQuantity = 1, ToUnitId = 3, ToQuantity = 29.5735m },
                new UnitConversion { UnitConversionId = 32, IngredientId = 10, FromUnitId = 5, FromQuantity = 1, ToUnitId = 3, ToQuantity = 29.5735m },

                // ================= cup =================
                new UnitConversion { UnitConversionId = 40, IngredientId = 2, FromUnitId = 6, FromQuantity = 1, ToUnitId = 3, ToQuantity = 240 },
                new UnitConversion { UnitConversionId = 41, IngredientId = 8, FromUnitId = 6, FromQuantity = 1, ToUnitId = 3, ToQuantity = 240 },
                new UnitConversion { UnitConversionId = 42, IngredientId = 10, FromUnitId = 6, FromQuantity = 1, ToUnitId = 3, ToQuantity = 240 },

                // ================= tbsp =================
                new UnitConversion { UnitConversionId = 50, IngredientId = 2, FromUnitId = 7, FromQuantity = 1, ToUnitId = 3, ToQuantity = 15 },

                // ================= tsp =================
                new UnitConversion { UnitConversionId = 60, IngredientId = 2, FromUnitId = 8, FromQuantity = 1, ToUnitId = 3, ToQuantity = 5 },

                // ================= COUNT (quan trọng nhất) =================

                // Syrup Torani (750ml)
                new UnitConversion { UnitConversionId = 70, IngredientId = 8, FromUnitId = 10, FromQuantity = 1, ToUnitId = 3, ToQuantity = 750 },

                // Sữa đặc (1 lon ~ 300ml)
                new UnitConversion { UnitConversionId = 71, IngredientId = 2, FromUnitId = 11, FromQuantity = 1, ToUnitId = 3, ToQuantity = 300 },

                // Nước Lavie (1 chai = 500ml)
                new UnitConversion { UnitConversionId = 72, IngredientId = 13, FromUnitId = 11, FromQuantity = 1, ToUnitId = 3, ToQuantity = 500 }
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
                t.HasCheckConstraint("CK_InventoryTransaction_Qty_NotZero", "[Quantity] <> 0");
            });

            entity.HasKey(x => x.InventoryTransactionId);

            // ================= COLUMNS =================

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.BeforeQty)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.AfterQty)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()")
                .IsRequired();

            // ================= RELATIONS =================

            entity.HasOne(x => x.StoreInventory)
                .WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.StoreInventoryId)
                .OnDelete(DeleteBehavior.Restrict); // 🔥 FIX: tránh mất lịch sử

            entity.HasOne(x => x.InventoryDocument)
                .WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.InventoryDocumentId)
                .OnDelete(DeleteBehavior.SetNull);

            // ================= INDEX =================

            entity.HasIndex(x => x.StoreInventoryId);

            entity.HasIndex(x => x.Type);

            entity.HasIndex(x => x.InventoryDocumentId);

            entity.HasIndex(x => x.CreatedAt); // 🔥 query timeline

            entity.HasIndex(x => new { x.StoreInventoryId, x.CreatedAt }); // 🔥 history theo kho
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