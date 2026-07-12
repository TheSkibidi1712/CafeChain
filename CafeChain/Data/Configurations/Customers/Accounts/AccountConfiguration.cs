using CafeChain.Application.Constants;
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
                new Account
                {
                    AccountId = 1,
                    Email = "owner@cafechain.vn",
                    PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Account
                {
                    AccountId = 2,
                    Email = "areamanager@cafechain.vn",
                    PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Account
                {
                    AccountId = 3,
                    Email = "storemanager@cafechain.vn",
                    PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Account
                {
                    AccountId = 4,
                    Email = "salesstaff@cafechain.vn",
                    PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Account
                {
                    AccountId = 5,
                    Email = "accountantwarehouse@cafechain.vn",
                    PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Account
                {
                    AccountId = 6,
                    Email = "systemadmin@cafechain.vn",
                    PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Account
                {
                    AccountId = 7,
                    Email = "khachhang@gmail.com",
                    PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                // Fixed seed — ShiftSupervisor / Ca trưởng (Issue #94 / #130)
                // AccountId is intentionally 12 via SeedDemoIdentities (do not renumber without team approval).
                new Account
                {
                    AccountId = SeedDemoIdentities.ShiftSupervisorAccountId,
                    Email = SeedDemoIdentities.ShiftSupervisorEmail,
                    PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                }
            );
        }
    }
}
