using CafeChain.Models;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ========================== ACCOUNT  ==========================
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
                new Account { AccountId = 110, Email = "cashier@cafechain.vn", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) },
                new Account { AccountId = 111, Email = "khachhang@gmail.com", PasswordHash = "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", Active = true, CreatedAt = new DateTime(2025, 1, 1) }
            );
        }
    }

    // ========================== ACCOUNT ROLE ==========================
    public class AccountRoleConfiguration : IEntityTypeConfiguration<AccountRole>
    {
        public void Configure(EntityTypeBuilder<AccountRole> entity)
        {
            entity.ToTable("AccountRoles");

            entity.HasKey(x => new { x.AccountId, x.RoleId });

            entity.HasOne(x => x.Account)
                .WithMany(x => x.AccountRoles)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Role)
                .WithMany(x => x.AccountRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasData(
                // ===== NEW ORG CHART ROLES MAPPING =====
                new AccountRole { AccountId = 101, RoleId = 1 },
                new AccountRole { AccountId = 102, RoleId = 2 },
                new AccountRole { AccountId = 103, RoleId = 3 },
                new AccountRole { AccountId = 104, RoleId = 4 },
                new AccountRole { AccountId = 105, RoleId = 5 },
                new AccountRole { AccountId = 106, RoleId = 6 },
                new AccountRole { AccountId = 107, RoleId = 7 },
                new AccountRole { AccountId = 108, RoleId = 8 },
                new AccountRole { AccountId = 109, RoleId = 9 },
                new AccountRole { AccountId = 110, RoleId = 10 },
                new AccountRole { AccountId = 111, RoleId = 11 }
            );
        }
    }

    // ========================== CUSTOMER ==========================
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> entity)
        {
            entity.ToTable("Customers");

            entity.HasKey(x => x.CustomerId);

            entity.Property(x => x.FullName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.AvatarUrl)
                .HasMaxLength(500);

            entity.Property(x => x.DateOfBirth);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            // ================= RELATIONSHIPS =================

            entity.HasMany(x => x.CustomerPhones)
                .WithOne(x => x.Customer)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.CustomerAddresses)
                .WithOne(x => x.Customer)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.CustomerBanks)
                .WithOne(x => x.Customer)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.CustomerPoints)
                .WithOne(x => x.Customer)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Ratings)
                .WithOne(x => x.Customer)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            // ===== CLEAN SLATE: Chỉ 1 Customer (111) → AccountId 111 =====
            entity.HasData(
                new Customer { CustomerId = 111, AccountId = 111, FullName = "Khách Hàng Mới", DateOfBirth = new DateTime(2000, 1, 1), CreatedAt = new DateTime(2025, 1, 1), AvatarUrl = "/Images/Upload/avtdf.jpg", Active = true }
            );
        }
    }

    // ========================== CUSTOMER PHONE ==========================
    public class CustomerPhoneConfiguration : IEntityTypeConfiguration<CustomerPhone>
    {
        public void Configure(EntityTypeBuilder<CustomerPhone> entity)
        {
            entity.ToTable("CustomerPhones");

            entity.HasKey(x => x.CustomerPhoneId);

            entity.Property(x => x.Phone)
                .IsRequired()
                .HasMaxLength(15);

            entity.HasIndex(x => new {x.Phone })
                .IsUnique();

            // ===== CLEAN SLATE: Chỉ 1 Phone cho Customer 111 =====
            entity.HasData(
                new CustomerPhone { CustomerPhoneId = 1, CustomerId = 111, Phone = "0900111222" }
            );
        }
    }

    // ========================== CUSTOMER ADDRESS ==========================
    public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
    {
        public void Configure(EntityTypeBuilder<CustomerAddress> entity)
        {
            entity.ToTable("CustomerAddresses");

            entity.HasKey(x => x.CustomerAddressId);

            entity.Property(x => x.Address)
                .IsRequired()
                .HasMaxLength(300);

            // 🔥 Khai báo quan hệ với bảng Ward (Location system)
            entity.HasOne(x => x.Ward)
                .WithMany(w => w.CustomerAddresses)
                .HasForeignKey(x => x.WardId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== CLEAN SLATE: Chỉ 1 Address cho Customer 111 =====
            entity.HasData(
                new CustomerAddress { CustomerAddressId = 1, CustomerId = 111, Address = "123 Đường Nguyễn Huệ, Q1, TP.HCM", WardId = 1, IsDefault = true }
            );
        }
    }

    // ========================== CUSTOMER BANK ==========================
    public class CustomerBankConfiguration : IEntityTypeConfiguration<CustomerBank>
    {
        public void Configure(EntityTypeBuilder<CustomerBank> entity)
        {
            entity.ToTable("CustomerBanks");

            entity.HasKey(x => x.CustomerBankId);

            entity.Property(x => x.BankName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.AccountNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => new { x.BankName, x.AccountNumber })
                .IsUnique();

            // ===== CLEAN SLATE: Chỉ 1 Bank cho Customer 111 =====
            entity.HasData(
                new CustomerBank { CustomerBankId = 1, CustomerId = 111, BankName = "Vietcombank", AccountNumber = "111222333444" }
            );
        }
    }

    // ========================== CUSTOMER POINT ==========================
    public class CustomerPointConfiguration : IEntityTypeConfiguration<CustomerPoint>
    {
        public void Configure(EntityTypeBuilder<CustomerPoint> entity)
        {
            entity.ToTable("CustomerPoints");

            entity.HasKey(x => x.CustomerPointId);

            entity.Property(x => x.Points).HasDefaultValue(0);

            entity.HasIndex(x => x.CustomerId).IsUnique();

            // ===== CLEAN SLATE: Chỉ 1 Point cho Customer 111 =====
            entity.HasData(
                new CustomerPoint { CustomerPointId = 1, CustomerId = 111, Points = 0 }
            );
        }
    }

    // ========================== RATING ==========================
    public class RatingConfiguration : IEntityTypeConfiguration<Rating>
    {
        public void Configure(EntityTypeBuilder<Rating> entity)
        {
            entity.ToTable("Ratings", t =>
            {
                t.HasCheckConstraint("CK_Rating_Stars", "[Stars] BETWEEN 1 AND 5");
            });

            entity.HasKey(x => x.RatingId);

            entity.Property(x => x.Stars)
                .IsRequired();

            entity.Property(x => x.Comment)
                .HasMaxLength(1000);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATION =================

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Ratings)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Drink)
                .WithMany(d => d.Ratings)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🔥 SELF RELATION (reply comment)
            entity.HasOne(x => x.ParentRating)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ParentRatingId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            // 🔥 chỉ cho phép 1 user rating 1 drink (chỉ áp dụng cho comment CHA)
            entity.HasIndex(x => new { x.CustomerId, x.DrinkId })
                .IsUnique()
                .HasFilter("[ParentRatingId] IS NULL AND [CustomerId] IS NOT NULL AND [DrinkId] IS NOT NULL");

            // 🔥 index để load comment nhanh
            entity.HasIndex(x => x.DrinkId);
            entity.HasIndex(x => x.ParentRatingId);
        }
    }

    public class RatingImageConfiguration : IEntityTypeConfiguration<RatingImage>
    {
        public void Configure(EntityTypeBuilder<RatingImage> entity)
        {
            entity.ToTable("RatingImages");

            entity.HasKey(x => x.RatingImageId);

            entity.Property(x => x.ImageUrl)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasOne(x => x.Rating)
                .WithMany(r => r.Images)
                .HasForeignKey(x => x.RatingId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🔥 index để load ảnh nhanh
            entity.HasIndex(x => x.RatingId);
        }
    }

    // =========================== RATING REACTION ==========================
    public class RatingReactionConfiguration : IEntityTypeConfiguration<RatingReaction>
    {
        public void Configure(EntityTypeBuilder<RatingReaction> entity)
        {
            entity.ToTable("RatingReactions");

            entity.HasKey(x => x.RatingReactionId);

            entity.Property(x => x.Type)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATION =================

            entity.HasOne(x => x.Rating)
                .WithMany(x => x.Reactions)
                .HasForeignKey(x => x.RatingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= UNIQUE =================

            // 🔥 mỗi user chỉ 1 reaction / 1 comment
            entity.HasIndex(x => new { x.RatingId, x.CustomerId })
                .IsUnique();

            // ================= INDEX =================

            entity.HasIndex(x => x.RatingId);
        }
    }

    // ========================== PASSWORD RESET OTP ==========================
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
                .HasMaxLength(10);

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