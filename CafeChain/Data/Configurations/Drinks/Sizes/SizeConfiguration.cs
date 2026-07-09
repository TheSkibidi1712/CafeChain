using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Drink;

namespace CafeChain.Data.Configurations.Drinks.Sizes
{
    public class SizeConfiguration : IEntityTypeConfiguration<Size>
    {
        public void Configure(EntityTypeBuilder<Size> entity)
        {
            entity.ToTable("Sizes");

            entity.HasKey(x => x.SizeId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.SizeCode)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.Description)
                .HasMaxLength(300);

            entity.Property(x => x.SizeType)
                .HasConversion<int>()
                .IsRequired()
                .HasDefaultValue(SizeTypeEnum.Cup);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Name)
                .IsUnique();

            entity.HasIndex(x => x.SizeCode)
                .IsUnique();

            entity.HasData(
                new Size { SizeId = 1, Name = "S", SizeCode = "S", Description = "Kích thước nhỏ", SizeType = SizeTypeEnum.Cup, Active = true },
                new Size { SizeId = 2, Name = "M", SizeCode = "M", Description = "Kích thước trung bình", SizeType = SizeTypeEnum.Cup, Active = true },
                new Size { SizeId = 3, Name = "L", SizeCode = "L", Description = "Kích thước lớn", SizeType = SizeTypeEnum.Cup, Active = true },
                new Size { SizeId = 4, Name = "XL", SizeCode = "XL", Description = "Kích thước rất lớn", SizeType = SizeTypeEnum.Cup, Active = true },
                new Size { SizeId = 5, Name = "150ml", SizeCode = "150ML", Description = "Kích thước 150ml", SizeType = SizeTypeEnum.Volume, Active = true },
                new Size { SizeId = 6, Name = "200ml", SizeCode = "200ML", Description = "Kích thước 200ml", SizeType = SizeTypeEnum.Volume, Active = true },
                new Size { SizeId = 7, Name = "250ml", SizeCode = "250ML", Description = "Kích thước 250ml", SizeType = SizeTypeEnum.Volume, Active = true },
                new Size { SizeId = 8, Name = "300ml", SizeCode = "300ML", Description = "Kích thước 300ml", SizeType = SizeTypeEnum.Volume, Active = true }
            );
        }
    }
}
