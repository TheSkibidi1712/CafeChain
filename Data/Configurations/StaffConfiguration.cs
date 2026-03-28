using CafeChain.Models;
using CafeChain.Models.Customers;
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

            // ================= PROPERTIES =================

            entity.Property(x => x.FullName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.TaxCode)
                .HasMaxLength(50);

            entity.Property(x => x.Salary)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.DateOfBirth);

            entity.Property(x => x.AvatarUrl)
                .HasMaxLength(500);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATIONSHIPS =================

            // Staff - Store (n-1)
            entity.HasOne(x => x.Store)
                .WithMany(s => s.Staffs)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            // Staff - Account (1-1, FK nằm ở Account)
            entity.HasOne(x => x.Account)
                .WithOne(a => a.Staff)
                .HasForeignKey<Staff>(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= INDEX (optional nhưng nên có) =================

            entity.HasIndex(x => x.StoreId);

            entity.HasIndex(x => x.TaxCode)
                .HasFilter("[TaxCode] IS NOT NULL");

            entity.HasData(
                new Staff
                {
                    StaffId = 1,
                    AccountId = 6,
                    FullName = "Nguyễn Văn A",
                    TaxCode = "TAX001",
                    Salary = 8000000,
                    StoreId = 1,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    CreatedAt = new DateTime(2024, 1, 1)
                },
                new Staff
                {
                    StaffId = 2,
                    AccountId= 7,
                    FullName = "Trần Thị B",
                    TaxCode = "TAX002",
                    Salary = 10000000,
                    StoreId = 1,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    CreatedAt = new DateTime(2024, 1, 1)
                },
                new Staff
                {
                    StaffId = 3,
                    AccountId = 8,
                    FullName = "Lê Văn C",
                    TaxCode = "TAX003",
                    Salary = 12000000,
                    StoreId = 2,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    CreatedAt = new DateTime(2024, 1, 1)
                },
                new Staff
                {
                    StaffId = 4,
                    AccountId = 9,
                    FullName = "Phạm Thị D",
                    TaxCode = "TAX004",
                    Salary = 14000000,
                    StoreId = 2,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    CreatedAt = new DateTime(2024, 1, 1)
                },
                new Staff
                {
                    StaffId = 5,
                    AccountId = 10,
                    FullName = "Hoàng Văn E",
                    TaxCode = "TAX005",
                    Salary = 9000000,
                    StoreId = 3,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    CreatedAt = new DateTime(2024, 1, 1)
                },
                new Staff
                {
                    StaffId = 6,
                    AccountId= 11,
                    FullName = "Đỗ Thị F",
                    TaxCode = "TAX006",
                    Salary = 7000000,
                    StoreId = 3,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    CreatedAt = new DateTime(2024, 1, 1)
                },
                new Staff
                {
                    StaffId = 7,
                    AccountId = 12,
                    FullName = "Nguyễn Văn G",
                    TaxCode = "TAX007",
                    Salary = 8500000,
                    StoreId = 1,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    CreatedAt = new DateTime(2024, 1, 1)
                },
                new Staff
                {
                    StaffId = 8,
                    AccountId = 13,
                    FullName = "Trần Văn H",
                    TaxCode = "TAX008",
                    Salary = 9500000,
                    StoreId = 2,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    CreatedAt = new DateTime(2024, 1, 1)
                },
                new Staff
                {
                    StaffId = 9,
                    AccountId = 14,
                    FullName = "Lý Thị I",
                    TaxCode = "TAX009",
                    Salary = 6000000,
                    StoreId = 3,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    CreatedAt = new DateTime(2024, 1, 1)
                },
                new Staff
                {
                    StaffId = 10,
                    AccountId= 15,
                    FullName = "Admin Tổng",
                    TaxCode = "TAX010",
                    Salary = 16000000,
                    StoreId = 1,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    CreatedAt = new DateTime(2024, 1, 1)
                }
            );
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
                .HasFilter("[AccountNumber] IS NOT NULL AND [BankName] IS NOT NULL");

            entity.HasData(
                new StaffBank { StaffBankId = 1, StaffId = 1, BankName = "Vietcombank", AccountNumber = "123456789" },
                new StaffBank { StaffBankId = 2, StaffId = 2, BankName = "ACB", AccountNumber = "987654321" },
                new StaffBank { StaffBankId = 3, StaffId = 3, BankName = "Techcombank", AccountNumber = "456123789" }
            );
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
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ScopeType)
                .WithMany()
                .HasForeignKey(x => x.ScopeTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.StaffId);

            entity.HasIndex(x => new { x.StaffId, x.ScopeTypeId, x.ScopeRefId })
                .IsUnique();

            entity.HasData(
                // ===== STORE LEVEL =====
                new StaffScope { StaffScopeId = 1, StaffId = 1, ScopeTypeId = 4, ScopeRefId = 1 },
                new StaffScope { StaffScopeId = 2, StaffId = 2, ScopeTypeId = 4, ScopeRefId = 1 },
                new StaffScope { StaffScopeId = 3, StaffId = 7, ScopeTypeId = 4, ScopeRefId = 1 },

                new StaffScope { StaffScopeId = 4, StaffId = 3, ScopeTypeId = 4, ScopeRefId = 2 },
                new StaffScope { StaffScopeId = 5, StaffId = 4, ScopeTypeId = 4, ScopeRefId = 2 },
                new StaffScope { StaffScopeId = 6, StaffId = 8, ScopeTypeId = 4, ScopeRefId = 2 },

                new StaffScope { StaffScopeId = 7, StaffId = 5, ScopeTypeId = 4, ScopeRefId = 3 },
                new StaffScope { StaffScopeId = 8, StaffId = 6, ScopeTypeId = 4, ScopeRefId = 3 },

                // ===== PROVINCE LEVEL =====
                new StaffScope { StaffScopeId = 9, StaffId = 9, ScopeTypeId = 2, ScopeRefId = 1 },

                // ===== SYSTEM LEVEL =====
                new StaffScope { StaffScopeId = 10, StaffId = 10, ScopeTypeId = 1, ScopeRefId = 1 }
            );
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

            entity.HasData(
                new ScopeType { ScopeTypeId = 1, Code = "COUNTRY", Name = "Country" },
                new ScopeType { ScopeTypeId = 2, Code = "PROVINCE", Name = "Province" },
                new ScopeType { ScopeTypeId = 3, Code = "WARD", Name = "Ward" },
                new ScopeType { ScopeTypeId = 4, Code = "STORE", Name = "Store" }
            );
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
                .WithMany(s => s.Shifts)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.StoreId);

            entity.HasData(
                new Shift { ShiftId = 1, Name = "Ca sáng", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(12, 0, 0), StoreId = 1 },
                new Shift { ShiftId = 2, Name = "Ca chiều", StartTime = new TimeSpan(12, 0, 0), EndTime = new TimeSpan(18, 0, 0), StoreId = 1 },
                new Shift { ShiftId = 3, Name = "Ca tối", StartTime = new TimeSpan(18, 0, 0), EndTime = new TimeSpan(23, 0, 0), StoreId = 1 },

                new Shift { ShiftId = 4, Name = "Ca sáng", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(12, 0, 0), StoreId = 2 },
                new Shift { ShiftId = 5, Name = "Ca chiều", StartTime = new TimeSpan(12, 0, 0), EndTime = new TimeSpan(18, 0, 0), StoreId = 2 },

                new Shift { ShiftId = 6, Name = "Ca sáng", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(12, 0, 0), StoreId = 3 },
                new Shift { ShiftId = 7, Name = "Ca tối", StartTime = new TimeSpan(18, 0, 0), EndTime = new TimeSpan(23, 0, 0), StoreId = 3 }
            );
        }
    }

    // ========================== STAFF SHIFT ==========================
    public class StaffShiftConfiguration : IEntityTypeConfiguration<StaffShift>
    {
        public void Configure(EntityTypeBuilder<StaffShift> entity)
        {
            entity.ToTable("StaffShifts");

            entity.HasKey(x => x.StaffShiftId);

            entity.Property(x => x.WorkDate).IsRequired();

            entity.Property(x => x.ActualCheckIn).IsRequired(false);
            entity.Property(x => x.ActualCheckOut).IsRequired(false);

            entity.Property(x => x.StatusId)
                .HasDefaultValue(1); // PLANNED

            entity.HasOne(x => x.Status)
                .WithMany(x => x.StaffShifts)
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany(x => x.StaffShifts)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Shift)
                .WithMany(x => x.StaffShifts)
                .HasForeignKey(x => x.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.StaffId, x.ShiftId, x.WorkDate })
                .IsUnique();

            entity.HasIndex(x => x.WorkDate);

            entity.HasData(
                new StaffShift { StaffShiftId = 1, StaffId = 1, ShiftId = 1, WorkDate = new DateTime(2024, 1, 1), StatusId = 1 },
                new StaffShift { StaffShiftId = 2, StaffId = 2, ShiftId = 2, WorkDate = new DateTime(2024, 1, 1), StatusId = 1 },
                new StaffShift { StaffShiftId = 3, StaffId = 3, ShiftId = 4, WorkDate = new DateTime(2024, 1, 1), StatusId = 1 }
            );
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

            entity.HasIndex(x => x.Name).IsUnique();

            entity.HasData(
                new Role { RoleId = 1, Name = "Cashier", Active = true, CreatedAt = new DateTime(2024, 1, 1) },
                new Role { RoleId = 2, Name = "Store Manager", Active = true, CreatedAt = new DateTime(2024, 1, 1) },
                new Role { RoleId = 3, Name = "Ward Manager", Active = true, CreatedAt = new DateTime(2024, 1, 1) },
                new Role { RoleId = 4, Name = "Province Manager", Active = true, CreatedAt = new DateTime(2024, 1, 1) },
                new Role { RoleId = 5, Name = "Admin System", Active = true, CreatedAt = new DateTime(2024, 1, 1) },
                new Role { RoleId = 6, Name = "Customer", Active = true, CreatedAt = new DateTime(2024, 1, 1) }
            );
        }
    }

    // ========================== STAFF SHIFT STATUS ==========================
    public class StaffShiftStatusConfiguration : IEntityTypeConfiguration<StaffShiftStatus>
    {
        public void Configure(EntityTypeBuilder<StaffShiftStatus> entity)
        {
            entity.ToTable("StaffShiftStatuses");

            entity.HasKey(x => x.StaffShiftStatusId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.IsSystem)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Code).IsUnique();

            entity.HasData(
                new StaffShiftStatus { StaffShiftStatusId = 1, Code = "PLANNED", Name = "Planned", IsSystem = true },
                new StaffShiftStatus { StaffShiftStatusId = 2, Code = "CHECKED_IN", Name = "Checked In", IsSystem = true },
                new StaffShiftStatus { StaffShiftStatusId = 3, Code = "COMPLETED", Name = "Completed", IsSystem = true },
                new StaffShiftStatus { StaffShiftStatusId = 4, Code = "ABSENT", Name = "Absent", IsSystem = true }
            );
        }
    }
}