using CafeChain.Models;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Staffs
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
                .HasMaxLength(14);

            entity.Property(x => x.CCCD)
                .HasMaxLength(12)
                .IsFixedLength(true);

            entity.Property(x => x.BaseSalary)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.DateOfBirth);

            entity.Property(x => x.PinHash)
                .HasMaxLength(100)
                .IsRequired(false);

            // ================= AVATAR =================

            entity.Property(x => x.AvatarUrl)
                .HasMaxLength(1000)
                .IsRequired(false);

            entity.Property(x => x.AvatarPublicId)
                .HasMaxLength(300)
                .IsRequired(false);

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
                .IsUnique()
                .HasFilter("[TaxCode] IS NOT NULL AND [TaxCode] <> ''");

            entity.HasIndex(x => x.CCCD)
                .IsUnique()
                .HasFilter("[CCCD] IS NOT NULL AND [CCCD] <> ''");

            entity.HasData(
                new Staff
                {
                    StaffId = 1,
                    AccountId = 1,
                    FullName = "Chủ doanh nghiệp",
                    TaxCode = "TAX101",
                    BaseSalary = 100000000,
                    StoreId = 1,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    AvatarPublicId = "staffs/default-avatar",
                    CreatedAt = new DateTime(2026, 1, 1)
                },
                new Staff
                {
                    StaffId = 2,
                    AccountId = 2,
                    FullName = "Quản lý vùng TP.HCM",
                    TaxCode = "TAX102",
                    BaseSalary = 45000000,
                    StoreId = 1,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    AvatarPublicId = "staffs/default-avatar",
                    CreatedAt = new DateTime(2026, 1, 1)
                },
                new Staff
                {
                    StaffId = 3,
                    AccountId = 3,
                    FullName = "Quản lý chi nhánh Quận 1",
                    TaxCode = "TAX103",
                    BaseSalary = 25000000,
                    StoreId = 1,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    AvatarPublicId = "staffs/default-avatar",
                    CreatedAt = new DateTime(2026, 1, 1)
                },
                new Staff
                {
                    StaffId = 4,
                    AccountId = 4,
                    FullName = "Nhân viên bán hàng",
                    TaxCode = "TAX104",
                    BaseSalary = 9000000,
                    StoreId = 1,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    AvatarPublicId = "staffs/default-avatar",
                    CreatedAt = new DateTime(2026, 1, 1)
                },
                new Staff
                {
                    StaffId = 5,
                    AccountId = 5,
                    FullName = "Nhân viên kế toán kho",
                    TaxCode = "TAX105",
                    BaseSalary = 15000000,
                    StoreId = 1,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    AvatarPublicId = "staffs/default-avatar",
                    CreatedAt = new DateTime(2026, 1, 1)
                },
                new Staff
                {
                    StaffId = 6,
                    AccountId = 6,
                    FullName = "Quản trị hệ thống",
                    TaxCode = "TAX106",
                    BaseSalary = 35000000,
                    StoreId = 1,
                    Active = true,
                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    AvatarPublicId = "staffs/default-avatar",
                    CreatedAt = new DateTime(2026, 1, 1)
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
                // ===== COUNTRY / SYSTEM LEVEL =====
                // Chủ doanh nghiệp: xem toàn bộ hệ thống
                new StaffScope
                {
                    StaffScopeId = 1,
                    StaffId = 1,
                    ScopeTypeId = 1, // SCOPE_COUNTRY
                    ScopeRefId = 1
                },

                // Quản trị hệ thống: cấu hình toàn hệ thống
                new StaffScope
                {
                    StaffScopeId = 2,
                    StaffId = 6,
                    ScopeTypeId = 1, // SCOPE_COUNTRY
                    ScopeRefId = 1
                },

                // ===== PROVINCE / AREA LEVEL =====
                // Quản lý vùng TP.HCM
                // Nếu ProvinceId TP.HCM trong seed của bạn không phải 79 thì đổi lại ScopeRefId cho đúng
                new StaffScope
                {
                    StaffScopeId = 3,
                    StaffId = 2,
                    ScopeTypeId = 2, // SCOPE_PROVINCE
                    ScopeRefId = 79
                },

                // ===== STORE LEVEL =====
                // Quản lý chi nhánh Quận 1
                new StaffScope
                {
                    StaffScopeId = 4,
                    StaffId = 3,
                    ScopeTypeId = 5, // SCOPE_STORE
                    ScopeRefId = 1
                },

                // Nhân viên bán hàng
                new StaffScope
                {
                    StaffScopeId = 5,
                    StaffId = 4,
                    ScopeTypeId = 5, // SCOPE_STORE
                    ScopeRefId = 1
                },

                // Nhân viên kế toán kho
                new StaffScope
                {
                    StaffScopeId = 6,
                    StaffId = 5,
                    ScopeTypeId = 5, // SCOPE_STORE
                    ScopeRefId = 1
                }
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
                new ScopeType { ScopeTypeId = 3, Code = "DISTRICT", Name = "District" },
                new ScopeType { ScopeTypeId = 4, Code = "WARD", Name = "Ward" },
                new ScopeType { ScopeTypeId = 5, Code = "STORE", Name = "Store" }
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
                new StaffShift { StaffShiftId = 1, StaffId = 4, ShiftId = 1, WorkDate = new DateTime(2026, 1, 1), StatusId = 1 },
                new StaffShift { StaffShiftId = 2, StaffId = 5, ShiftId = 2, WorkDate = new DateTime(2026, 1, 1), StatusId = 1 },
                new StaffShift { StaffShiftId = 3, StaffId = 6, ShiftId = 4, WorkDate = new DateTime(2026, 1, 1), StatusId = 1 }
            );
        }
    }



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

    // ========================== STAFF PHONE ==========================
    public class StaffPhoneConfiguration : IEntityTypeConfiguration<StaffPhone>
    {
        public void Configure(EntityTypeBuilder<StaffPhone> entity)
        {
            entity.ToTable("StaffPhones");

            entity.HasKey(x => x.StaffPhoneId);

            entity.Property(x => x.Phone)
                .IsRequired()
                .HasMaxLength(15);

            entity.Property(x => x.IsDefault)
                .HasDefaultValue(false);

            entity.HasOne(x => x.Staff)
                .WithMany(x => x.StaffPhones)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.StaffId);

            entity.HasData(
                new StaffPhone { StaffPhoneId = 1, StaffId = 1, Phone = "0901000101", IsDefault = true },
                new StaffPhone { StaffPhoneId = 2, StaffId = 2, Phone = "0901000102", IsDefault = true },
                new StaffPhone { StaffPhoneId = 3, StaffId = 3, Phone = "0901000103", IsDefault = true },
                new StaffPhone { StaffPhoneId = 4, StaffId = 4, Phone = "0901000104", IsDefault = true },
                new StaffPhone { StaffPhoneId = 5, StaffId = 5, Phone = "0901000105", IsDefault = true },
                new StaffPhone { StaffPhoneId = 6, StaffId = 6, Phone = "0901000106", IsDefault = true }
            );
        }
    }

    // ========================== STAFF ADDRESS ==========================
    public class StaffAddressConfiguration : IEntityTypeConfiguration<StaffAddress>
    {
        public void Configure(EntityTypeBuilder<StaffAddress> entity)
        {
            entity.ToTable("StaffAddresses");

            entity.HasKey(x => x.StaffAddressId);

            entity.Property(x => x.Address)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(x => x.IsDefault)
                .HasDefaultValue(false);

            entity.HasOne(x => x.Staff)
                .WithMany(x => x.StaffAddresses)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.StaffId);

            entity.HasData(
                new StaffAddress { StaffAddressId = 1, StaffId = 1, Address = "123 Đường Nguyễn Huệ, Q1, TP.HCM", IsDefault = true },
                new StaffAddress { StaffAddressId = 2, StaffId = 2, Address = "456 Đường Lê Lợi, Q3, TP.HCM", IsDefault = true },
                new StaffAddress { StaffAddressId = 3, StaffId = 3, Address = "789 Đường Trần Hưng Đạo, Q5, TP.HCM", IsDefault = true }
            );
        }
    }
}