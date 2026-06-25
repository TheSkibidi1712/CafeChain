using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.DrinkInfos
{
    public class ProductTypeConfiguration : IEntityTypeConfiguration<ProductType>
    {
        public void Configure(EntityTypeBuilder<ProductType> entity)
        {
            entity.ToTable("ProductTypes");

            entity.HasKey(x => x.ProductTypeId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Code).IsUnique();

            // 🔥 SEED QUAN TRỌNG
            entity.HasData(
                new ProductType
                {
                    ProductTypeId = 1,
                    Code = "HANDCRAFTED",
                    Name = "Pha chế",
                    Active = true
                },
                new ProductType
                {
                    ProductTypeId = 2,
                    Code = "RETAIL",
                    Name = "Đóng chai",
                    Active = true
                }
            );
        }
    }
}
