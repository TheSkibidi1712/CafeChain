using CafeChain.Models.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Operations
{
    public class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
    {
        public void Configure(EntityTypeBuilder<OtpChallenge> entity)
        {
            entity.ToTable("OtpChallenges");

            entity.HasKey(x => x.OtpChallengeId);

            entity.Property(x => x.PublicId)
                .IsRequired();

            entity.Property(x => x.ActionType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.TargetType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Reason)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.OtpHash)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.FailedAttempts)
                .HasDefaultValue(0);

            entity.Property(x => x.ResendCount)
                .HasDefaultValue(0);

            entity.Property(x => x.OldValueJson);
            entity.Property(x => x.NewValueJson);

            entity.HasIndex(x => x.PublicId)
                .IsUnique();

            entity.HasIndex(x => new { x.StoreId, x.Status, x.ExpiresAt });
            entity.HasIndex(x => new { x.ApproverStaffId, x.Status, x.ExpiresAt });
            entity.HasIndex(x => new { x.ActionType, x.TargetType, x.TargetId, x.StoreId });
            entity.HasIndex(x => x.RequestedByStaffId);
            entity.HasIndex(x => x.WorkShiftId);
            entity.HasIndex(x => x.CreatedAt);

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.WorkShift)
                .WithMany()
                .HasForeignKey(x => x.WorkShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RequestedByStaff)
                .WithMany()
                .HasForeignKey(x => x.RequestedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApproverStaff)
                .WithMany()
                .HasForeignKey(x => x.ApproverStaffId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
