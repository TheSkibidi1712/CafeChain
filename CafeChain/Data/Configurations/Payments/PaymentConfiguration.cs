using CafeChain.Models;
using CafeChain.Models.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Reflection.Emit;

namespace CafeChain.Data.Configurations.Payments
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

            entity.Property(x => x.ReceivedAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.ChangeAmount)
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

            entity.HasData(
                new PaymentStatus { PaymentStatusId = 1, Name = "Chưa thanh toán", Code = "UNPAID", BadgeColor = "badge bg-warning text-dark" },
                new PaymentStatus { PaymentStatusId = 2, Name = "Đã thanh toán", Code = "PAID", BadgeColor = "badge bg-success" },
                new PaymentStatus { PaymentStatusId = 3, Name = "Đã hoàn tiền", Code = "REFUNDED", BadgeColor = "badge bg-info text-dark" },
                new PaymentStatus { PaymentStatusId = 4, Name = "Lỗi thanh toán", Code = "FAILED", BadgeColor = "badge bg-danger" }
            );

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

            entity.HasData(
                new PaymentMethod { PaymentMethodId = 1, Name = "Tiền mặt", Code = "CASH" },
                new PaymentMethod { PaymentMethodId = 2, Name = "Chuyển khoản", Code = "BANK" },
                new PaymentMethod { PaymentMethodId = 3, Name = "Momo", Code = "MOMO" },
                new PaymentMethod { PaymentMethodId = 4, Name = "ZaloPay", Code = "ZALOPAY" },
                new PaymentMethod { PaymentMethodId = 5, Name = "VNPay", Code = "VNPAY" }
            );
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

            entity.HasData(
                new CashSession
                {
                    CashSessionId = 1,
                    StaffId = 5,
                    StoreId = 1,
                    StartCash = 1000000,
                    EndCash = null,
                    OpenTime = new DateTime(2025, 1, 1, 8, 0, 0), // ❗ FIX
                    IsClosed = false
                },
                new CashSession
                {
                    CashSessionId = 2,
                    StaffId = 6,
                    StoreId = 1,
                    StartCash = 500000,
                    EndCash = 800000,
                    OpenTime = new DateTime(2025, 1, 1, 0, 0, 0),  // ❗ FIX (thay AddHours)
                    CloseTime = new DateTime(2025, 1, 1, 8, 0, 0), // ❗ FIX
                    IsClosed = true
                }
            );
        }
    }

    public class TransactionLogConfiguration : IEntityTypeConfiguration<TransactionLog>
    {
        public void Configure(EntityTypeBuilder<TransactionLog> entity)
        {
            entity.ToTable("TransactionLogs");
            entity.HasKey(x => x.TransactionLogId);
            entity.Property(x => x.Amount)
                .HasPrecision(18, 2);
        }
    }
}
