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
                // Quantity luôn là số dương tuyệt đối; Type quyết định chiều tăng/giảm kho.
                table.HasCheckConstraint(
                    "CK_InventoryTransaction_Quantity_Positive",
                    "[Quantity] > 0"
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
            });

            entity.HasKey(x => x.InventoryTransactionId);

            // ================= PROPERTY =================

            entity.Property(x => x.Type)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.StockStatus)
                .HasConversion<int>()
                .HasDefaultValue(CafeChain.Models.Enums.Inventory.InventoryStockStatus.NORMAL)
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

            entity.HasOne(x => x.InventoryTransfer)
                .WithMany()
                .HasForeignKey(x => x.InventoryTransferId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.InventoryTransferDetail)
                .WithMany()
                .HasForeignKey(x => x.InventoryTransferDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReferenceOrder)
                .WithMany()
                .HasForeignKey(x => x.ReferenceOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.ProductionRun)
                .WithMany()
                .HasForeignKey(x => x.ProductionRunId)
                .OnDelete(DeleteBehavior.Restrict);

            // Issue #121 — exact ChildRecipe / sale-source recipe audit (not stock identity).
            entity.HasOne(x => x.SourceRecipe)
                .WithMany()
                .HasForeignKey(x => x.SourceRecipeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Issue #123 — consolidation run linkage (new movements only).
            entity.HasOne(x => x.InventoryConsolidationRun)
                .WithMany()
                .HasForeignKey(x => x.InventoryConsolidationRunId)
                .OnDelete(DeleteBehavior.Restrict);

            // Issue #128 — one BRANCH_RECEIPT_IN per receipt line.
            entity.HasOne(x => x.BranchReceiptLine)
                .WithMany()
                .HasForeignKey(x => x.BranchReceiptLineId)
                .OnDelete(DeleteBehavior.Restrict);

            // Issue #134 — cash refund sales return linkage.
            entity.HasOne(x => x.OrderRefund)
                .WithMany()
                .HasForeignKey(x => x.OrderRefundId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => x.StoreInventoryId);

            entity.HasIndex(x => x.Type);

            entity.HasIndex(x => x.StockStatus);

            entity.HasIndex(x => x.InventoryDocumentId);

            entity.HasIndex(x => x.InventoryTransferId);

            entity.HasIndex(x => x.InventoryTransferDetailId)
                .HasDatabaseName("IX_InventoryTransactions_InventoryTransferDetailId");

            entity.HasIndex(x => new { x.InventoryTransferDetailId, x.Type })
                .IsUnique()
                .HasFilter("[InventoryTransferDetailId] IS NOT NULL")
                .HasDatabaseName("UX_InventoryTransactions_TransferDetail_Type");

            entity.HasIndex(x => x.ReferenceOrderId);

            entity.HasIndex(x => x.ProductionRunId)
                .HasDatabaseName("IX_InventoryTransactions_ProductionRunId");

            entity.HasIndex(x => x.SourceRecipeId)
                .HasDatabaseName("IX_InventoryTransactions_SourceRecipeId");

            // Exactly one movement per run + inventory row + type when linked to a production run.
            entity.HasIndex(x => new { x.ProductionRunId, x.StoreInventoryId, x.Type })
                .IsUnique()
                .HasFilter("[ProductionRunId] IS NOT NULL")
                .HasDatabaseName("UX_InventoryTransactions_ProductionRun_Inventory_Type");

            entity.HasIndex(x => x.InventoryConsolidationRunId)
                .HasDatabaseName("IX_InventoryTransactions_InventoryConsolidationRunId");

            // Exactly one consolidation movement per run + inventory row + type.
            entity.HasIndex(x => new { x.InventoryConsolidationRunId, x.StoreInventoryId, x.Type })
                .IsUnique()
                .HasFilter("[InventoryConsolidationRunId] IS NOT NULL")
                .HasDatabaseName("UX_InventoryTransactions_ConsolidationRun_Inventory_Type");

            // Issue #128 — one movement per BranchReceiptLine + Type when linked.
            entity.HasIndex(x => new { x.BranchReceiptLineId, x.Type })
                .IsUnique()
                .HasFilter("[BranchReceiptLineId] IS NOT NULL")
                .HasDatabaseName("UX_InventoryTransactions_BranchReceiptLine_Type");

            entity.HasIndex(x => x.BranchReceiptLineId)
                .HasDatabaseName("IX_InventoryTransactions_BranchReceiptLineId");

            entity.HasIndex(x => x.OrderRefundId)
                .HasDatabaseName("IX_InventoryTransactions_OrderRefundId");

            // One SALES_RETURN per refund + inventory row (full-order restore slice)
            entity.HasIndex(x => new { x.OrderRefundId, x.StoreInventoryId, x.Type })
                .IsUnique()
                .HasFilter("[OrderRefundId] IS NOT NULL")
                .HasDatabaseName("UX_InventoryTransactions_OrderRefund_Inventory_Type");

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
                    Luôn là số dương tuyệt đối.
                    Type quyết định chiều giao dịch:
                    IMPORT/IN_TRANSFER/... cộng kho.
                    EXPORT/OUT_TRANSFER/... trừ kho.

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
