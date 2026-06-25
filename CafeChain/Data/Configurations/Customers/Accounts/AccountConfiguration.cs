using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Customers.Accounts
{
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> entity)
        {
            entity.ToTable("Accounts");
            entity.HasKey(x => x.AccountId);

            entity.Property(x => x.Email).IsRequired().HasMaxLength(255);
            entity.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(x => x.Active).HasDefaultValue(true);
            entity.Property(x => x.FailedLoginAttempts).HasDefaultValue(0);

            entity.Property(x => x.LockoutEnd).IsRequired(false);

            entity.HasIndex(x => x.Email).IsUnique();

            // 1-1: Account -> Customer (AccountId là FK trong bảng Customers)
            entity.HasOne(x => x.Customer)
                .WithOne(x => x.Account)
                .HasForeignKey<Customer>(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // 1-1: Account -> Staff (AccountId là FK trong bảng Staffs)
            entity.HasOne(x => x.Staff)
                .WithOne(x => x.Account)
                .HasForeignKey<Staff>(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== CLEAN SLATE: Chỉ 11 Accounts (101-111) =====
            entity.HasData(
                new Account { AccountId = 101, Email = "superadmin@cafechain.vn", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 102, Email = "ceo@cafechain.vn", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 103, Email = "cfo@cafechain.vn", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 104, Email = "marketing@cafechain.vn", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 105, Email = "operations@cafechain.vn", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 106, Email = "hr@cafechain.vn", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 107, Email = "areamanager@cafechain.vn", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 108, Email = "storemanager@cafechain.vn", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 109, Email = "shiftsupervisor@cafechain.vn", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 110, Email = "nvcasang@cafechain.vn", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 111, Email = "khachhang@gmail.com", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 122, Email = "districtmanager@gmail.com", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 123, Email = "nvcachieu@gmail.com", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) }
            );
        }
    }
}
