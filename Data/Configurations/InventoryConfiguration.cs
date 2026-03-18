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
                // ================= CONSTRAINT =================
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

            entity.HasOne(x => x.StoreInventory)
                .WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.StockImportId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.TransactionType)
                .WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.InventoryTransactionTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => x.StockImportId);
            entity.HasIndex(x => x.InventoryTransactionTypeId);
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
        }
    }
}