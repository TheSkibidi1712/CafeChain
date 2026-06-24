using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Customers.OTPs
{
    public class PasswordResetOtpConfiguration : IEntityTypeConfiguration<PasswordResetOtp>
    {
        public void Configure(EntityTypeBuilder<PasswordResetOtp> entity)
        {
            entity.ToTable("PasswordResetOtps");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.CodeHash)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.Property(x => x.ExpiredAt)
                .IsRequired();

            entity.Property(x => x.IsUsed)
                .HasDefaultValue(false);

            entity.Property(x => x.FailedAttempts)
                .HasDefaultValue(0); // 🔥 QUAN TRỌNG

            // ================= INDEX =================

            entity.HasIndex(x => new { x.Email, x.CodeHash, x.IsUsed });

            entity.HasIndex(x => x.Email);

            entity.HasIndex(x => x.CreatedAt);
        }
    }
}
