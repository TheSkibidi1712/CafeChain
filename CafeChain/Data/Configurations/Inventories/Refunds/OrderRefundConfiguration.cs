using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Refunds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Refunds
{
    public class OrderRefundConfiguration : IEntityTypeConfiguration<OrderRefund>
    {
        public void Configure(EntityTypeBuilder<OrderRefund> entity)
        {
            entity.ToTable("OrderRefunds", table =>
            {
                table.HasCheckConstraint(
                    "CK_OrderRefunds_Status",
                    "[Status] IN (1, 2, 3, 4)");
                table.HasCheckConstraint(
                    "CK_OrderRefunds_RefundAmount",
                    "[RefundAmount] >= 0");
            });

            entity.HasKey(x => x.OrderRefundId);

            entity.Property(x => x.RefundKey).IsRequired();
            entity.Property(x => x.Status).HasConversion<int>().IsRequired();
            entity.Property(x => x.PaymentMethodId).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.Property(x => x.RefundAmount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(x => x.CostStatus).HasConversion<int>().IsRequired();
            entity.Property(x => x.ReversedCogs).HasColumnType("decimal(18,2)");
            entity.Property(x => x.InventoryReversalStatus).HasConversion<int>().IsRequired();
            entity.Property(x => x.RequestedAtUtc).HasColumnType("datetime2").IsRequired();
            entity.Property(x => x.ProcessingAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.CompletedAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.FailureCode).HasMaxLength(64);
            entity.Property(x => x.FailureMessage).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasIndex(x => new { x.StoreId, x.RefundKey })
                .IsUnique()
                .HasDatabaseName("UX_OrderRefunds_Store_RefundKey");

            // One non-failed full refund intent per order (Failed may retry with new key)
            entity.HasIndex(x => x.OrderId)
                .IsUnique()
                .HasFilter("[Status] IN (1, 2, 3)")
                .HasDatabaseName("UX_OrderRefunds_Order_ActiveOrCompleted");

            entity.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RequestedByStaff)
                .WithMany()
                .HasForeignKey(x => x.RequestedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CompletedByStaff)
                .WithMany()
                .HasForeignKey(x => x.CompletedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
