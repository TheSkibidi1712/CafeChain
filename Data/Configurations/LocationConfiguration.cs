using CafeChain.Models;
using CafeChain.Models.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ================================ MODULE LOCATION ==============================
    public class CountryConfiguration : IEntityTypeConfiguration<Country>
    {
        public void Configure(EntityTypeBuilder<Country> entity)
        {
            entity.ToTable("Countries");

            entity.HasKey(x => x.CountryId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasIndex(x => x.Name)
                .IsUnique();

            entity.HasData(
                new Country { CountryId = 1, Name = "Vietnam" }
            );
        }
    }

    public class ProvinceConfiguration : IEntityTypeConfiguration<Province>
    {
        public void Configure(EntityTypeBuilder<Province> entity)
        {
            entity.ToTable("Provinces");

            entity.HasKey(x => x.ProvinceId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasOne(x => x.Country)
                .WithMany(x => x.Provinces)
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🔥 tránh trùng tỉnh trong cùng quốc gia
            entity.HasIndex(x => new { x.CountryId, x.Name })
                .IsUnique();

            entity.HasData(
                new Province { ProvinceId = 1, CountryId = 1, Name = "Bình Dương" },
                new Province { ProvinceId = 2, CountryId = 1, Name = "Hồ Chí Minh" }
            );
        }
    }

    public class WardConfiguration : IEntityTypeConfiguration<Ward>
    {
        public void Configure(EntityTypeBuilder<Ward> entity)
        {
            entity.ToTable("Wards");

            entity.HasKey(x => x.WardId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasOne(x => x.Province)
                .WithMany(x => x.Wards)
                .HasForeignKey(x => x.ProvinceId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🔥 tránh trùng phường trong cùng tỉnh
            entity.HasIndex(x => new { x.ProvinceId, x.Name })
                .IsUnique();

            entity.HasData(
                // Bình Dương
                new Ward { WardId = 1, ProvinceId = 1, Name = "Phú Lợi" },      // Thủ Dầu Một
                new Ward { WardId = 2, ProvinceId = 1, Name = "Lái Thiêu" },    // Thuận An
                new Ward { WardId = 3, ProvinceId = 1, Name = "Dĩ An" },        // Dĩ An

                // TP.HCM
                new Ward { WardId = 4, ProvinceId = 2, Name = "Phường 1" },
                new Ward { WardId = 5, ProvinceId = 2, Name = "Phường 2" }
            );
        }
    }
}