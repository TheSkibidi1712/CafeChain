using CafeChain.Models;
using CafeChain.Models.Vouchers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ================================ MODULE VOUCHER ==============================
    public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
    {
        public void Configure(EntityTypeBuilder<Voucher> entity)
        {
            entity.ToTable("Vouchers", t =>
            {
                t.HasCheckConstraint("CK_Voucher_Discount",
                    "(DiscountPercent IS NOT NULL AND DiscountAmount IS NULL) OR (DiscountPercent IS NULL AND DiscountAmount IS NOT NULL)");

                t.HasCheckConstraint("CK_Voucher_Date",
                    "[StartDate] <= [EndDate]");
            });

            entity.HasKey(x => x.VoucherId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => x.Code).IsUnique();

            entity.Property(x => x.DiscountAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.MaxDiscount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.MinOrderValue)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.StartDate);
            entity.HasIndex(x => x.EndDate);
        }
    }

    public class OrderVoucherConfiguration : IEntityTypeConfiguration<OrderVoucher>
    {
        public void Configure(EntityTypeBuilder<OrderVoucher> entity)
        {
            entity.ToTable("OrderVouchers");

            entity.HasKey(x => x.OrderVoucherId);

            entity.Property(x => x.DiscountValue)
                .HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Order)
                .WithMany(x => x.OrderVouchers)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Voucher)
                .WithMany(x => x.OrderVouchers)
                .HasForeignKey(x => x.VoucherId)
                .OnDelete(DeleteBehavior.Restrict);

            // ❗ 1 order chỉ dùng 1 voucher
            entity.HasIndex(x => x.OrderId)
                .IsUnique();

            entity.HasIndex(x => x.OrderId).IsUnique();

            // optional nếu muốn 1 voucher không bị spam trong 1 order
            entity.HasIndex(x => new { x.OrderId, x.VoucherId }).IsUnique();
        }
    }

    public class VoucherUsageConfiguration : IEntityTypeConfiguration<VoucherUsage>
    {
        public void Configure(EntityTypeBuilder<VoucherUsage> entity)
        {
            entity.ToTable("VoucherUsages");

            entity.HasKey(x => x.VoucherUsageId);

            entity.Property(x => x.UsedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasOne(x => x.Voucher)
                .WithMany(x => x.VoucherUsages)
                .HasForeignKey(x => x.VoucherId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Customer)
                .WithMany(c => c.VoucherUsages)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // ❗ KHÔNG nên unique nếu có MaxUsagePerUser
            entity.HasIndex(x => new { x.VoucherId, x.CustomerId });

            entity.HasIndex(x => x.UsedAt);
        }
    }

    public class WheelConfigConfiguration : IEntityTypeConfiguration<WheelConfig>
    {
        public void Configure(EntityTypeBuilder<WheelConfig> entity)
        {
            entity.ToTable("WheelConfigs");

            entity.HasKey(x => x.WheelConfigId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.SpinCost)
                .IsRequired();

            entity.Property(x => x.SlotCount)
                .IsRequired();

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // chỉ cho 6 hoặc 8 ô
            entity.HasCheckConstraint("CK_WheelConfig_Slot",
                "[SlotCount] IN (6,8)");
        }
    }

    public class WheelPrizeConfiguration : IEntityTypeConfiguration<WheelPrize>
    {
        public void Configure(EntityTypeBuilder<WheelPrize> entity)
        {
            entity.ToTable("WheelPrizes");

            entity.HasKey(x => x.WheelPrizeId);

            entity.Property(x => x.Probability)
                .HasColumnType("decimal(5,4)");

            entity.Property(x => x.IsLose)
                .HasDefaultValue(false);

            entity.HasOne(x => x.WheelConfig)
                .WithMany(x => x.Prizes)
                .HasForeignKey(x => x.WheelConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Voucher)
                .WithMany()
                .HasForeignKey(x => x.VoucherId)
                .OnDelete(DeleteBehavior.SetNull);

            // ❗ mỗi slot chỉ 1 prize
            entity.HasIndex(x => new { x.WheelConfigId, x.SlotIndex })
                .IsUnique();

            // ❗ probability phải >= 0
            entity.HasCheckConstraint("CK_WheelPrize_Probability",
                "[Probability] >= 0");

            // ❗ lose thì không có voucher
            entity.HasCheckConstraint("CK_WheelPrize_Lose",
                "(IsLose = 1 AND VoucherId IS NULL) OR (IsLose = 0)");
        }
    }

    public class WheelSpinConfiguration : IEntityTypeConfiguration<WheelSpin>
    {
        public void Configure(EntityTypeBuilder<WheelSpin> entity)
        {
            entity.ToTable("WheelSpins");

            entity.HasKey(x => x.WheelSpinId);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasOne(x => x.WheelConfig)
                .WithMany()
                .HasForeignKey(x => x.WheelConfigId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.WheelPrize)
                .WithMany()
                .HasForeignKey(x => x.WheelPrizeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.CreatedAt);
        }
    }
}