using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Suppliers
{
    public class SupplierPhoneConfiguration : IEntityTypeConfiguration<SupplierPhone>
    {
        public void Configure(EntityTypeBuilder<SupplierPhone> entity)
        {
            entity.ToTable("SupplierPhones");

            entity.HasKey(x => x.SupplierPhoneId);

            // ================= PROPERTY =================

            entity.Property(x => x.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.Description)
                .HasMaxLength(200);

            entity.Property(x => x.IsPrimary)
                .HasDefaultValue(false);

            // ================= INDEX =================

            entity.HasIndex(x => x.SupplierId);

            entity.HasIndex(x => new
            {
                x.SupplierId,
                x.PhoneNumber
            }).IsUnique();

            // ================= RELATION =================

            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.Phones)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= SEED =================

            entity.HasData(
                new SupplierPhone
                {
                    SupplierPhoneId = 1,
                    SupplierId = 1,
                    PhoneNumber = "0901111111",
                    IsPrimary = true,
                    Description = "Hotline"
                },
                new SupplierPhone
                {
                    SupplierPhoneId = 2,
                    SupplierId = 1,
                    PhoneNumber = "0901111112",
                    IsPrimary = false,
                    Description = "Kho hàng"
                },
                new SupplierPhone
                {
                    SupplierPhoneId = 3,
                    SupplierId = 2,
                    PhoneNumber = "0902222222",
                    IsPrimary = true,
                    Description = "Hotline"
                },
                new SupplierPhone
                {
                    SupplierPhoneId = 4,
                    SupplierId = 3,
                    PhoneNumber = "0903333333",
                    IsPrimary = true,
                    Description = "Hotline"
                },
                new SupplierPhone
                {
                    SupplierPhoneId = 5,
                    SupplierId = 4,
                    PhoneNumber = "0904444444",
                    IsPrimary = true,
                    Description = "Hotline"
                },
                new SupplierPhone
                {
                    SupplierPhoneId = 6,
                    SupplierId = 5,
                    PhoneNumber = "0905555555",
                    IsPrimary = true,
                    Description = "Hotline"
                }
            );
        }
    }
}
