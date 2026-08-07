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

            entity.Property(x => x.ProtectedOtpPayload)
                .HasMaxLength(2048);

            entity.Property(x => x.PayloadFingerprint)
                .IsRequired()
                .HasMaxLength(128)
                .HasDefaultValue(string.Empty);

            entity.Property(x => x.TerminalId).HasMaxLength(100);
            entity.Property(x => x.TerminalName).HasMaxLength(100);
            entity.Property(x => x.RequestKey).HasMaxLength(200);
            entity.Property(x => x.ClientIpHash).HasMaxLength(64).IsFixedLength();
            entity.Property(x => x.DeviceFingerprintHash).HasMaxLength(64).IsFixedLength();

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.FailedAttempts)
                .HasDefaultValue(0);

            entity.Property(x => x.ResendCount)
                .HasDefaultValue(0);

            entity.Property(x => x.OldValueJson);
            entity.Property(x => x.NewValueJson);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasIndex(x => x.PublicId)
                .IsUnique();

            entity.HasIndex(x => new { x.StoreId, x.Status, x.ExpiresAt });
            entity.HasIndex(x => new { x.ApproverStaffId, x.Status, x.ExpiresAt });
            entity.HasIndex(x => new { x.ActionType, x.TargetType, x.TargetId, x.StoreId });
            entity.HasIndex(x => x.RequestedByStaffId);
            entity.HasIndex(x => x.WorkShiftId);
            entity.HasIndex(x => x.TerminalId);
            entity.HasIndex(x => x.ConfirmedByStaffId);
            entity.HasIndex(x => x.RequestKey);
            entity.HasIndex(x => new { x.ClientIpHash, x.CreatedAt });
            entity.HasIndex(x => new { x.DeviceFingerprintHash, x.CreatedAt });
            entity.HasIndex(x => x.CreatedAt);

            // Helps enforce one-open-challenge lookup (application still owns status transitions).
            entity.HasIndex(x => new
            {
                x.StoreId,
                x.RequestedByStaffId,
                x.ActionType,
                x.TargetType,
                x.TargetId,
                x.Status
            });

            // SQL Server: at most one Pending/Approved challenge per actor/action/target.
            // Status transition to Used/Expired/etc. frees the key for a new request.
            entity.HasIndex(x => new
            {
                x.StoreId,
                x.RequestedByStaffId,
                x.ActionType,
                x.TargetType,
                x.TargetId
            })
                .IsUnique()
                // Provider-agnostic filter (no N' unicode prefix — breaks SQLite EnsureCreated).
                .HasFilter("[Status] IN ('Pending', 'Approved')")
                .HasDatabaseName("UX_OtpChallenges_OneActivePerActorActionTarget");

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

            entity.HasOne(x => x.ConfirmedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ConfirmedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
