using CafeChain.Models;
using CafeChain.Models.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ================================ MODULE PAYMENT ==============================
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> entity)
        {
            entity.ToTable("Payments");

            entity.HasKey(x => x.PaymentId);

            entity.Property(x => x.Amount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.TransactionCode)
                .HasMaxLength(100);

            entity.Property(x => x.PaidAt);

            // RELATION

            entity.HasOne(x => x.Order)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.PaymentMethod)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PaymentStatus)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.PaymentStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CashSession)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.CashSessionId)
                .OnDelete(DeleteBehavior.SetNull);

            // INDEX

            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.PaymentStatusId);
        }
    }

    public class PaymentStatusConfiguration : IEntityTypeConfiguration<PaymentStatus>
    {
        public void Configure(EntityTypeBuilder<PaymentStatus> entity)
        {
            entity.ToTable("PaymentStatuses");

            entity.HasKey(x => x.PaymentStatusId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => x.Code).IsUnique();
        }
    }

    public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
    {
        public void Configure(EntityTypeBuilder<PaymentMethod> entity)
        {
            entity.ToTable("PaymentMethods");

            entity.HasKey(x => x.PaymentMethodId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => x.Code).IsUnique();
        }
    }

    public class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
    {
        public void Configure(EntityTypeBuilder<CashSession> entity)
        {
            entity.ToTable("CashSessions");

            entity.HasKey(x => x.CashSessionId);

            entity.Property(x => x.StartCash)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.EndCash)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.OpenTime)
                .HasDefaultValueSql("GETDATE()");

            entity.Property(x => x.IsClosed)
                .HasDefaultValue(false);

            entity.HasOne(x => x.Staff)
                .WithMany(c => c.CashSessions)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Store)
                .WithMany(x => x.CashSessions)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => x.StaffId);
        }
    }
}