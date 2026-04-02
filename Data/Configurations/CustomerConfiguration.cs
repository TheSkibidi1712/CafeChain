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

            entity.HasData(
                new Account { AccountId = 1, Email = "a@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 2, Email = "b@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 3, Email = "c@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 4, Email = "d@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 5, Email = "e@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 6, Email = "f@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 7, Email = "g@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 8, Email = "o@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 9, Email = "p@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 10, Email = "i@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 11, Email = "u@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 12, Email = "y@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 13, Email = "t@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 14, Email = "r@gmail.com", PasswordHash = "123" },
                new Account { AccountId = 15, Email = "w@gmail.com", PasswordHash = "123" }
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

            // ================= SEED =================
            entity.HasData(
                new Customer { CustomerId = 1, AccountId = 1, FullName = "Nguyễn Văn A", DateOfBirth = new DateTime(2000, 12, 17), CreatedAt = new DateTime(2025, 1, 1), AvatarUrl= "/Images/Upload/avtdf.jpg",Active = true },
                new Customer { CustomerId = 2, AccountId = 2, FullName = "Nguyễn Văn B", DateOfBirth = new DateTime(2000, 01, 01), CreatedAt = new DateTime(2025, 1, 1), AvatarUrl = "/Images/Upload/avtdf.jpg", Active = true },
                new Customer { CustomerId = 3, AccountId = 3, FullName = "Nguyễn Văn C", DateOfBirth = new DateTime(2000, 05, 04), CreatedAt = new DateTime(2025, 1, 1), AvatarUrl = "/Images/Upload/avtdf.jpg", Active = true },
                new Customer { CustomerId = 4, AccountId = 4, FullName = "Nguyễn Văn D", DateOfBirth = new DateTime(2000, 04, 05), CreatedAt = new DateTime(2025, 1, 1), AvatarUrl = "/Images/Upload/avtdf.jpg", Active = true },
                new Customer { CustomerId = 5, AccountId = 5, FullName = "Nguyễn Văn E", DateOfBirth = new DateTime(2000, 02, 26), CreatedAt = new DateTime(2025, 1, 1), AvatarUrl = "/Images/Upload/avtdf.jpg", Active = true }
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

            entity.HasData(
                new CustomerPhone { CustomerPhoneId = 1, CustomerId = 1, Phone = "0123456789" },
                new CustomerPhone { CustomerPhoneId = 2, CustomerId = 2, Phone = "0987654321" },
                new CustomerPhone { CustomerPhoneId = 3, CustomerId = 3, Phone = "0112233445" },
                new CustomerPhone { CustomerPhoneId = 4, CustomerId = 4, Phone = "0223344556" },
                new CustomerPhone { CustomerPhoneId = 5, CustomerId = 5, Phone = "0334455667" },
                new CustomerPhone { CustomerPhoneId = 6, CustomerId = 1, Phone = "0445566778" }
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

            entity.HasData(
                new CustomerAddress { CustomerAddressId = 1, CustomerId = 1, Address = "123 Đường A", WardId = 6, IsDefault = true},
                new CustomerAddress { CustomerAddressId = 2, CustomerId = 2, Address = "456 Đường D", WardId = 7, IsDefault = true},
                new CustomerAddress { CustomerAddressId = 3, CustomerId = 3, Address = "789 Đường G", WardId = 4, IsDefault = true},
                new CustomerAddress { CustomerAddressId = 4, CustomerId = 4, Address = "321 Đường J", WardId = 5, IsDefault = true},
                new CustomerAddress { CustomerAddressId = 5, CustomerId = 5, Address = "654 Đường M", WardId = 1, IsDefault = true},
                new CustomerAddress { CustomerAddressId = 6, CustomerId = 1, Address = "987 Đường P", WardId = 2, IsDefault = false}
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

            entity.HasData(
                new CustomerBank { CustomerBankId = 1, CustomerId = 1, BankName = "Vietcombank", AccountNumber = "123456789" },
                new CustomerBank { CustomerBankId = 2, CustomerId = 2, BankName = "Techcombank", AccountNumber = "987654321" },
                new CustomerBank { CustomerBankId = 3, CustomerId = 3, BankName = "BIDV", AccountNumber = "111222333" },
                new CustomerBank { CustomerBankId = 4, CustomerId = 4, BankName = "Vietinbank", AccountNumber = "444555666" },
                new CustomerBank { CustomerBankId = 5, CustomerId = 5, BankName = "Agribank", AccountNumber = "777888999" },
                new CustomerBank { CustomerBankId = 6, CustomerId = 1, BankName = "Sacombank", AccountNumber = "222333444" }
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

            entity.HasData(
                new CustomerPoint { CustomerPointId = 1, CustomerId = 1, Points = 100 },
                new CustomerPoint { CustomerPointId = 2, CustomerId = 2, Points = 200 },
                new CustomerPoint { CustomerPointId = 3, CustomerId = 3, Points = 50 },
                new CustomerPoint { CustomerPointId = 4, CustomerId = 4, Points = 0 },
                new CustomerPoint { CustomerPointId = 5, CustomerId = 5, Points = 300 }
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