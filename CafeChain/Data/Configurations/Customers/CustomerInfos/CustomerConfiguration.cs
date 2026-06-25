using CafeChain.Models;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CafeChain.Models.Enums.Customer;

namespace CafeChain.Data.Configurations.Customers.CustomerInfos
{

    // ========================== CUSTOMER ==========================
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer> 
    { 
        public void Configure(EntityTypeBuilder<Customer> entity) 
        { 
            entity.ToTable("Customers"); 

            entity.HasKey(x => x.CustomerId); 

            // ================= BASIC FIELDS =================
            entity.Property(x => x.CustomerCode) 
                .IsRequired() 
                .HasMaxLength(50); 

            entity.HasIndex(x => x.CustomerCode) 
                .IsUnique(); 

            entity.Property(x => x.FullName) 
                .IsRequired() 
                .HasMaxLength(200); 
            
            entity.Property(x => x.DateOfBirth) 
                .IsRequired(false); 

            entity.Property(x => x.CreatedAt) 
                .HasDefaultValueSql("GETDATE()"); 

            entity.Property(x => x.UpdatedAt) 
                .IsRequired(false); 

            entity.Property(x => x.Active) 
                .HasDefaultValue(true); 

            entity.Property(x => x.LastOrderDate) 
                .IsRequired(false);

            // ================= AVATAR (CLOUDINARY) =================

            entity.Property(x => x.AvatarUrl)
                .HasMaxLength(1000)
                .IsRequired(false);

            entity.Property(x => x.AvatarPublicId)
                .HasMaxLength(300)
                .IsRequired(false);

            // ================= SOFT DELETE =================

            entity.Property(x => x.IsDeleted)
                .HasDefaultValue(false);

            entity.Property(x => x.DeletedAt)
                .IsRequired(false);


            // ================= ENUMS =================
            entity.Property(x => x.Gender) 
                .HasConversion<int>() 
                .IsRequired(false); 
            
            entity.Property(x => x.Category) 
                .HasConversion<int>() 
                .HasDefaultValue(CustomerCategory.Guest); 
            
            // ================= ANALYTICS =================
            entity.Property(x => x.TotalSpent) 
                .HasColumnType("decimal(18,2)") 
                .HasDefaultValue(0); 
            
            entity.Property(x => x.TotalOrders) 
                .HasDefaultValue(0); 
            
            entity.Property(x => x.CurrentPoints) 
                .HasDefaultValue(0);
            
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
            
            entity.HasMany(x => x.Ratings) 
                .WithOne(x => x.Customer) 
                .HasForeignKey(x => x.CustomerId) 
                .OnDelete(DeleteBehavior.SetNull); 
            
            entity.HasMany(x => x.PointTransactions) 
                .WithOne(x => x.Customer) 
                .HasForeignKey(x => x.CustomerId) 
                .OnDelete(DeleteBehavior.Cascade); 
            
            entity.HasMany(x => x.VoucherUsages) 
                .WithOne(x => x.Customer) 
                .HasForeignKey(x => x.CustomerId) 
                .OnDelete(DeleteBehavior.Cascade); 
            
            entity.HasMany(x => x.CustomerVouchers) 
                .WithOne(x => x.Customer)
                .HasForeignKey(x => x.CustomerId) 
                .OnDelete(DeleteBehavior.Cascade); // Customer -> MemberLevel
                                                  
            entity.HasOne(x => x.MemberLevel) 
                .WithMany() 
                .HasForeignKey(x => x.MemberLevelId) 
                .OnDelete(DeleteBehavior.SetNull);

            // ================= SEED DATA ================= 
            entity.HasData
                (
                    new Customer
                    {
                        CustomerId = 111,
                        AccountId = 111,
                        CustomerCode = "CUS000111",
                        Gender = Gender.Male,
                        FullName = "Khách Hàng Mới",
                        DateOfBirth = new DateTime(2000, 1, 1),
                        Category = CustomerCategory.Registered,
                        MemberLevelId = null,
                        TotalSpent = 0,
                        TotalOrders = 0,
                        CurrentPoints = 0,
                        LastOrderDate = null,
                        AvatarUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779801172/avtdf_r3cjq5.jpg",
                        AvatarPublicId = "avtdf_r3cjq5",
                        CreatedAt = new DateTime(2025, 1, 1),
                        UpdatedAt = null,
                        IsDeleted = false,
                        DeletedAt = null,
                        Active = true
                    }
                );
        } 
    }
}