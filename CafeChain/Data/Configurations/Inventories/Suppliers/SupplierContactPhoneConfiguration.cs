using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Suppliers
{
    public class SupplierContactPhoneConfiguration : IEntityTypeConfiguration<SupplierContactPhone>
    {
        public void Configure(EntityTypeBuilder<SupplierContactPhone> entity)
        {
            entity.ToTable("SupplierContactPhones");

            entity.HasKey(x => x.SupplierContactPhoneId);

            // ================= PROPERTY =================

            entity.Property(x => x.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.Description)
                .HasMaxLength(200);

            entity.Property(x => x.IsPrimary)
                .HasDefaultValue(false);

            // ================= INDEX =================

            entity.HasIndex(x => x.SupplierContactId);

            entity.HasIndex(x => new
            {
                x.SupplierContactId,
                x.PhoneNumber
            }).IsUnique();

            // ================= RELATION =================

            entity.HasOne(x => x.SupplierContact)
                .WithMany(x => x.Phones)
                .HasForeignKey(x => x.SupplierContactId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= SEED =================

            entity.HasData(
                new SupplierContactPhone
                {
                    SupplierContactPhoneId = 1,
                    SupplierContactId = 1,
                    PhoneNumber = "0901111111",
                    IsPrimary = true,
                    Description = "Số chính"
                },
                new SupplierContactPhone
                {
                    SupplierContactPhoneId = 2,
                    SupplierContactId = 2,
                    PhoneNumber = "0902222222",
                    IsPrimary = true,
                    Description = "Sales"
                },
                new SupplierContactPhone
                {
                    SupplierContactPhoneId = 3,
                    SupplierContactId = 3,
                    PhoneNumber = "0903333333",
                    IsPrimary = true,
                    Description = "Owner"
                },
                new SupplierContactPhone
                {
                    SupplierContactPhoneId = 4,
                    SupplierContactId = 4,
                    PhoneNumber = "0904444444",
                    IsPrimary = true,
                    Description = "Director"
                },
                new SupplierContactPhone
                {
                    SupplierContactPhoneId = 5,
                    SupplierContactId = 5,
                    PhoneNumber = "0905555555",
                    IsPrimary = true,
                    Description = "Manager"
                }
            );
        }
    }
}
