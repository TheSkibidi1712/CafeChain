using CafeChain.Models;
using CafeChain.Models.Loyalties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ================================ MODULE LOYALTY ===============================
    public class PointTransactionConfiguration : IEntityTypeConfiguration<PointTransaction>
    {
        public void Configure(EntityTypeBuilder<PointTransaction> entity)
        {
            entity.ToTable("PointTransactions");

            entity.HasKey(x => x.PointTransactionId);

            entity.Property(x => x.Points)
                .IsRequired();

            entity.Property(x => x.BalanceAfter)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Type)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.PointTransactionTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.CustomerId);
        }
    }

    public class MemberLevelConfiguration : IEntityTypeConfiguration<MemberLevel>
    {
        public void Configure(EntityTypeBuilder<MemberLevel> entity)
        {
            entity.ToTable("MemberLevels");

            entity.HasKey(x => x.MemberId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(x => x.Name)
                .IsUnique();
        }
    }

    public class PointTransactionTypeConfiguration : IEntityTypeConfiguration<PointTransactionType>
    {
        public void Configure(EntityTypeBuilder<PointTransactionType> entity)
        {
            entity.ToTable("PointTransactionTypes");

            entity.HasKey(x => x.PointTransactionTypeId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(x => x.Code)
                .IsUnique();
        }
    }
}