using CafeChain.Models;
using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ========================== ACCOUNT TYPE ==========================
    public class AccountTypeConfiguration : IEntityTypeConfiguration<AccountType>
    {
        public void Configure(EntityTypeBuilder<AccountType> entity)
        {
            entity.ToTable("AccountTypes");

            entity.HasKey(x => x.AccountTypeId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Name)
                .IsUnique();

            // seed
            entity.HasData(
                new AccountType { AccountTypeId = 1, Name = "Customer", Active = true },
                new AccountType { AccountTypeId = 2, Name = "Staff", Active = true },
                new AccountType { AccountTypeId = 3, Name = "Admin", Active = true }
            );
        }
    }

    // ========================== ACCOUNT  ==========================
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> entity)
        {
            entity.ToTable("Accounts");

            entity.HasKey(x => x.AccountId);

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.PasswordHash)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            // ✅ UNIQUE EMAIL (case insensitive)
            entity.HasIndex(x => x.Email)
                .IsUnique();

            // ================= RELATIONSHIPS =================

            entity.HasOne(x => x.AccountType)
                .WithMany(t => t.Accounts)
                .HasForeignKey(x => x.AccountTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ 1-1 Customer
            entity.HasOne(x => x.Customer)
                .WithOne(c => c.Account)
                .HasForeignKey<Account>(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.CustomerId)
                .IsUnique()
                .HasFilter("[CustomerId] IS NOT NULL");

            // ✅ 1-1 Staff
            entity.HasOne(x => x.Staff)
                .WithOne(s => s.Account)
                .HasForeignKey<Account>(x => x.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.StaffId)
                .IsUnique()
                .HasFilter("[StaffId] IS NOT NULL");
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
                new Customer { CustomerId = 1, FullName = "Nguyễn Văn A", DateOfBirth = new DateTime(2000, 12, 17), CreatedAt = new DateTime(2025, 1, 1), AvatarUrl= "/Images/Upload/avtdf.jpg",Active = true },
                new Customer { CustomerId = 2, FullName = "Nguyễn Văn B", DateOfBirth = new DateTime(2000, 01, 01), CreatedAt = new DateTime(2025, 1, 1), AvatarUrl = "/Images/Upload/avtdf.jpg", Active = true },
                new Customer { CustomerId = 3, FullName = "Nguyễn Văn C", DateOfBirth = new DateTime(2000, 05, 04), CreatedAt = new DateTime(2025, 1, 1), AvatarUrl = "/Images/Upload/avtdf.jpg", Active = true },
                new Customer { CustomerId = 4, FullName = "Nguyễn Văn D", DateOfBirth = new DateTime(2000, 04, 05), CreatedAt = new DateTime(2025, 1, 1), AvatarUrl = "/Images/Upload/avtdf.jpg", Active = true },
                new Customer { CustomerId = 5, FullName = "Nguyễn Văn E", DateOfBirth = new DateTime(2000, 02, 26), CreatedAt = new DateTime(2025, 1, 1), AvatarUrl = "/Images/Upload/avtdf.jpg", Active = true }
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

            entity.HasData(
                new CustomerAddress { CustomerAddressId = 1, CustomerId = 1, Address = "123 Đường A, Phường Long Bình, Đồng Nai" },
                new CustomerAddress { CustomerAddressId = 2, CustomerId = 2, Address = "456 Đường D, Phường Trảng Dài, Đồng Nai" },
                new CustomerAddress { CustomerAddressId = 3, CustomerId = 3, Address = "789 Đường G, Phường H, Quận I, TP. HCM" },
                new CustomerAddress { CustomerAddressId = 4, CustomerId = 4, Address = "321 Đường J, Phường K, Quận L, TP. HCM" },
                new CustomerAddress { CustomerAddressId = 5, CustomerId = 5, Address = "654 Đường M, Phường N, Quận O, Hà Nội" },
                new CustomerAddress { CustomerAddressId = 6, CustomerId = 1, Address = "987 Đường P, Phường Q, Quận R, Hà Nội" }
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

            entity.Property(x => x.Points)
                .HasDefaultValue(0);

            entity.HasIndex(x => x.CustomerId)
                .IsUnique();
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

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Ratings)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Drink)
                .WithMany(d => d.Ratings)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.CustomerId, x.DrinkId })
                .IsUnique()
                .HasFilter("[CustomerId] IS NOT NULL AND [DrinkId] IS NOT NULL");


        }
    }
}