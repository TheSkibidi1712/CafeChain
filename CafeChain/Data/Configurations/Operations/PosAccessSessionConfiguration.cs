using CafeChain.Models.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Operations;

public sealed class PosAccessSessionConfiguration : IEntityTypeConfiguration<PosAccessSession>
{
    public void Configure(EntityTypeBuilder<PosAccessSession> entity)
    {
        entity.ToTable("PosAccessSessions");
        entity.HasKey(x => x.PosAccessSessionId);
        entity.Property(x => x.PublicId).IsRequired();
        entity.Property(x => x.JwtId).IsRequired().HasMaxLength(64);
        entity.Property(x => x.TerminalId).IsRequired().HasMaxLength(100);
        entity.Property(x => x.Status).IsRequired().HasMaxLength(32);
        entity.Property(x => x.EndReason).HasMaxLength(500);
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => x.PublicId).IsUnique();
        entity.HasIndex(x => x.JwtId).IsUnique();
        entity.HasIndex(x => new { x.StoreId, x.Status, x.ExpiresAtUtc });
        entity.HasIndex(x => x.WorkShiftId);
        entity.HasIndex(x => x.TerminalId)
            .IsUnique()
            .HasFilter("[Status] = 'ACTIVE'")
            .HasDatabaseName("UX_PosAccessSessions_ActiveTerminal");
        entity.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Terminal).WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.WorkShift).WithMany().HasForeignKey(x => x.WorkShiftId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EndedByStaff).WithMany().HasForeignKey(x => x.EndedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
