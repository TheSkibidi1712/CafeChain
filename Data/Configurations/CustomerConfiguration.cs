using CafeChain.Models;
using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ========================== CUSTOMER ==========================
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> entity)
        {
            entity.ToTable("Customers");

            entity.HasKey(x => x.CustomerId);

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.PasswordHash)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.FullName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Email)
                .IsUnique();

            // RELATIONSHIPS
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

            entity.HasIndex(x => new { x.CustomerId, x.Phone })
                .IsUnique();
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

            entity.HasOne(x => x.Ward)
                .WithMany(w => w.CustomerAddresses)
                .HasForeignKey(x => x.WardId)
                .OnDelete(DeleteBehavior.SetNull);
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