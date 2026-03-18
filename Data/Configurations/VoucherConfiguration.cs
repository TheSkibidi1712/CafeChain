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
                // constraint: chỉ cho 1 loại discount
                t.HasCheckConstraint("CK_Voucher_Discount",
                    "(DiscountPercent IS NOT NULL AND DiscountAmount IS NULL) OR (DiscountPercent IS NULL AND DiscountAmount IS NOT NULL)");
            });

            entity.HasKey(x => x.VouId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.Property(x => x.DiscountAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.MaxDiscount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.MinOrderValue)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.StartDate);
            entity.Property(x => x.EndDate);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);
        }
    }

    public class OrderVoucherConfiguration : IEntityTypeConfiguration<OrderVoucher>
    {
        public void Configure(EntityTypeBuilder<OrderVoucher> entity)
        {
            entity.ToTable("OrderVouchers");

            entity.HasKey(x => x.OrVId);

            entity.Property(x => x.DiscountValue)
                .HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Order)
                .WithMany(x => x.OrderVouchers)
                .HasForeignKey(x => x.OrdId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Voucher)
                .WithMany(x => x.OrderVouchers)
                .HasForeignKey(x => x.VouId)
                .OnDelete(DeleteBehavior.Restrict);

            // ❗ 1 order chỉ dùng 1 voucher
            entity.HasIndex(x => x.OrdId)
                .IsUnique();
        }
    }

    public class VoucherUsageConfiguration : IEntityTypeConfiguration<VoucherUsage>
    {
        public void Configure(EntityTypeBuilder<VoucherUsage> entity)
        {
            entity.ToTable("VoucherUsages");

            entity.HasKey(x => x.VouUId);

            entity.Property(x => x.UsedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasOne(x => x.Voucher)
                .WithMany(x => x.VoucherUsages)
                .HasForeignKey(x => x.VouId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CusId)
                .OnDelete(DeleteBehavior.Cascade);

            // ❗ mỗi user dùng 1 lần (tuỳ business)
            entity.HasIndex(x => new { x.VouId, x.CusId })
                .IsUnique();
        }
    }
}