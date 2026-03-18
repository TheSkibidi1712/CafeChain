using CafeChain.Models;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ========================== STAFF ==========================
    public class StaffConfiguration : IEntityTypeConfiguration<Staff>
    {
        public void Configure(EntityTypeBuilder<Staff> entity)
        {
            entity.ToTable("Staffs");

            entity.HasKey(x => x.StaffId);

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.PasswordHash)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.FullName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.TaxCode)
                .HasMaxLength(50);

            entity.Property(x => x.Salary)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasIndex(x => x.Email)
                .IsUnique();

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    // ========================== STAFF BANK ==========================
    public class StaffBankConfiguration : IEntityTypeConfiguration<StaffBank>
    {
        public void Configure(EntityTypeBuilder<StaffBank> entity)
        {
            entity.ToTable("StaffBanks");

            entity.HasKey(x => x.StaffBankId);

            entity.Property(x => x.BankName)
                .HasMaxLength(100);

            entity.Property(x => x.AccountNumber)
                .HasMaxLength(50);

            entity.HasOne(x => x.Staff)
                .WithMany(x => x.StaffBanks)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.BankName, x.AccountNumber })
                .IsUnique()
                .HasFilter("[AccountNumber] IS NOT NULL");
        }
    }

    // ========================== STAFF ROLE ==========================
    public class StaffRoleConfiguration : IEntityTypeConfiguration<StaffRole>
    {
        public void Configure(EntityTypeBuilder<StaffRole> entity)
        {
            entity.ToTable("StaffRoles");

            entity.HasKey(x => x.StaffRoleId);

            entity.Property(x => x.AssignedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasOne(x => x.Staff)
                .WithMany(x => x.StaffRoles)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Role)
                .WithMany(x => x.StaffRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.StaffId, x.RoleId })
                .IsUnique();
        }
    }

    // ========================== STAFF SCOPE ==========================
    public class StaffScopeConfiguration : IEntityTypeConfiguration<StaffScope>
    {
        public void Configure(EntityTypeBuilder<StaffScope> entity)
        {
            entity.ToTable("StaffScopes");

            entity.HasKey(x => x.StaffScopeId);

            entity.Property(x => x.ScopeRefId)
                .IsRequired();

            entity.HasOne(x => x.Staff)
                .WithMany(x => x.StaffScopes)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ScopeType)
                .WithMany()
                .HasForeignKey(x => x.ScopeTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.StaffId);

            entity.HasIndex(x => new { x.StaffId, x.ScopeTypeId, x.ScopeRefId })
                .IsUnique();
        }
    }

    // ========================== SCOPE TYPE ==========================
    public class ScopeTypeConfiguration : IEntityTypeConfiguration<ScopeType>
    {
        public void Configure(EntityTypeBuilder<ScopeType> entity)
        {
            entity.ToTable("ScopeTypes");

            entity.HasKey(x => x.ScopeTypeId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.HasIndex(x => x.Name)
                .IsUnique();
        }
    }

    // ========================== SHIFT ==========================
    public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
    {
        public void Configure(EntityTypeBuilder<Shift> entity)
        {
            entity.ToTable("Shifts");

            entity.HasKey(x => x.ShiftId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.StartTime)
                .IsRequired();

            entity.Property(x => x.EndTime)
                .IsRequired();

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.IsOvernight)
                .HasDefaultValue(false);

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.StoId);
        }
    }

    // ========================== STAFF SHIFT ==========================
    public class StaffShiftConfiguration : IEntityTypeConfiguration<StaffShift>
    {
        public void Configure(EntityTypeBuilder<StaffShift> entity)
        {
            entity.ToTable("StaffShifts");

            entity.HasKey(x => x.StaffShiftId);

            entity.Property(x => x.WorkDate)
                .IsRequired();

            entity.HasOne(x => x.Staff)
                .WithMany(x => x.StaffShifts)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Shift)
                .WithMany(x => x.StaffShifts)
                .HasForeignKey(x => x.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.StaffId, x.ShiftId, x.WorkDate })
                .IsUnique();

            entity.HasIndex(x => x.WorkDate);
        }
    }

    // ========================== ROLE ==========================
    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> entity)
        {
            entity.ToTable("Roles");

            entity.HasKey(x => x.RoleId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasIndex(x => x.Name)
                .IsUnique();
        }
    }
}