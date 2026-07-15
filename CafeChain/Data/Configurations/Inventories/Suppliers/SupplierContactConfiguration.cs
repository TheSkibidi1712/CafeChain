using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Suppliers
{
    public class SupplierContactConfiguration : IEntityTypeConfiguration<SupplierContact>
    {
        public void Configure(EntityTypeBuilder<SupplierContact> entity)
        {
            entity.ToTable("SupplierContacts");

            entity.HasKey(x => x.SupplierContactId);

            // ================= PROPERTY =================

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Email)
                .HasMaxLength(150);

            entity.Property(x => x.PhoneNumber)
                .HasMaxLength(20);

            entity.Property(x => x.Position)
                .HasMaxLength(100);

            entity.Property(x => x.Note)
                .HasMaxLength(1000);

            entity.Property(x => x.IsPrimary)
                .HasDefaultValue(false);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            // ================= INDEX =================

            entity.HasIndex(x => x.SupplierId);

            entity.HasIndex(x => x.Email);

            entity.HasIndex(x => new { x.SupplierId, x.PhoneNumber });

            // ================= RELATION =================

            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.Contacts)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= SEED =================

            entity.HasData(
                new SupplierContact
                {
                    SupplierContactId = 1,
                    SupplierId = 1,
                    Name = "Nguyễn Văn A",
                    PhoneNumber = "0901111111",
                    Email = "a@supplier.com",
                    Position = "Manager",
                    IsPrimary = true,
                    Active = true,
                    Note = "Liên hệ chính"
                },
                new SupplierContact
                {
                    SupplierContactId = 2,
                    SupplierId = 2,
                    Name = "Trần Văn B",
                    PhoneNumber = "0902222222",
                    Email = "b@supplier.com",
                    Position = "Sales",
                    IsPrimary = true,
                    Active = true,
                    Note = "Phụ trách bán hàng"
                },
                new SupplierContact
                {
                    SupplierContactId = 3,
                    SupplierId = 3,
                    Name = "Lê Văn C",
                    PhoneNumber = "0903333333",
                    Email = "c@supplier.com",
                    Position = "Owner",
                    IsPrimary = true,
                    Active = true,
                    Note = "Chủ doanh nghiệp"
                },
                new SupplierContact
                {
                    SupplierContactId = 4,
                    SupplierId = 4,
                    Name = "Phạm Văn D",
                    PhoneNumber = "0904444444",
                    Email = "d@supplier.com",
                    Position = "Director",
                    IsPrimary = true,
                    Active = true,
                    Note = "Giám đốc"
                },
                new SupplierContact
                {
                    SupplierContactId = 5,
                    SupplierId = 5,
                    Name = "Hoàng Văn E",
                    PhoneNumber = "0905555555",
                    Email = "e@supplier.com",
                    Position = "Manager",
                    IsPrimary = true,
                    Active = true,
                    Note = "Quản lý kinh doanh"
                }
            );
        }
    }
}
