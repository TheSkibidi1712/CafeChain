using CafeChain.Models.Inventories.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Transactions
{
    public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
    {
        public void Configure(EntityTypeBuilder<InventoryTransaction> entity)
        {
            entity.ToTable("InventoryTransactions", table =>
            {
                // Quantity không được = 0
                table.HasCheckConstraint(
                    "CK_InventoryTransaction_Quantity_NotZero",
                    "[Quantity] <> 0"
                );

                // Giá vốn không âm
                table.HasCheckConstraint(
                    "CK_InventoryTransaction_UnitCost",
                    "[UnitCost] IS NULL OR [UnitCost] >= 0"
                );

                // Tổng giá vốn không âm
                table.HasCheckConstraint(
                    "CK_InventoryTransaction_TotalCost",
                    "[TotalCost] IS NULL OR [TotalCost] >= 0"
                );
                table.HasCheckConstraint(
                    "CK_InventoryTransaction_QtyBalance",
                    "[BeforeQty] + [Quantity] = [AfterQty]"
                );
            });

            entity.HasKey(x => x.InventoryTransactionId);

            // ================= PROPERTY =================

            entity.Property(x => x.Type)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.BeforeQty)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.AfterQty)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.UnitCost)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.TotalCost)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()")
                .IsRequired();

            // ================= RELATION =================

            entity.HasOne(x => x.StoreInventory)
                .WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.StoreInventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InventoryDocument)
                .WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.InventoryDocumentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.ReferenceOrder)
                .WithMany()
                .HasForeignKey(x => x.ReferenceOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            // ================= INDEX =================

            entity.HasIndex(x => x.StoreInventoryId);

            entity.HasIndex(x => x.Type);

            entity.HasIndex(x => x.InventoryDocumentId);

            entity.HasIndex(x => x.ReferenceOrderId);

            entity.HasIndex(x => x.CreatedAt);

            entity.HasIndex(x => new
            {
                x.StoreInventoryId,
                x.CreatedAt
            });

            entity.HasIndex(x => new
            {
                x.Type,
                x.CreatedAt
            });

            // ================= BUSINESS NOTE =================
            /*
                Quantity:
                    + nhập kho  -> dương
                    + xuất kho  -> âm

                BeforeQty:
                    tồn trước giao dịch

                AfterQty:
                    tồn sau giao dịch

                UnitCost:
                    giá vốn tại thời điểm phát sinh

                TotalCost:
                    = ABS(Quantity) * UnitCost

                InventoryDocumentId:
                    liên kết phiếu nhập/xuất/chuyển

                ReferenceOrderId:
                    dùng cho SALES_DEDUCTION
            */
        }
    }
}
