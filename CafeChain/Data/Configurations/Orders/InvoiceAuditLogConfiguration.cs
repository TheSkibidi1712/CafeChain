using CafeChain.Models.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Orders
{
    /// <summary>
    /// EF Core configuration cho InvoiceAuditLog — Nhật ký Trưởng ca duyệt bypass
    /// Two FKs to Staff (Cashier, Supervisor) — dùng NoAction để tránh cascade cycle
    /// </summary>
    public class InvoiceAuditLogConfiguration : IEntityTypeConfiguration<InvoiceAuditLog>
    {
        public void Configure(EntityTypeBuilder<InvoiceAuditLog> entity)
        {
            entity.ToTable("InvoiceAuditLogs");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.ActionName)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Reason)
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // OrderId nullable — audit log có thể được tạo trước khi order hoàn tất
            entity.Property(x => x.OrderId)
                .IsRequired(false);

            // DiscountValue — giá trị chiết khấu voucher được duyệt bypass
            entity.Property(x => x.DiscountValue)
                .HasColumnType("decimal(18,2)")
                .IsRequired(false);

            // Two FK to Staff — NoAction để tránh multiple cascade paths
            entity.HasOne(x => x.Cashier)
                .WithMany()
                .HasForeignKey(x => x.CashierId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Supervisor)
                .WithMany()
                .HasForeignKey(x => x.SupervisorId)
                .OnDelete(DeleteBehavior.NoAction);

            // Index
            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.CashierId);
            entity.HasIndex(x => x.SupervisorId);
            entity.HasIndex(x => x.CreatedAt);
        }
    }
}
