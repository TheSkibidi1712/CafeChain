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

            entity.HasKey(x => x.CouId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasIndex(x => x.Name)
                .IsUnique();
        }
    }

    public class ProvinceConfiguration : IEntityTypeConfiguration<Province>
    {
        public void Configure(EntityTypeBuilder<Province> entity)
        {
            entity.ToTable("Provinces");

            entity.HasKey(x => x.ProId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasOne(x => x.Country)
                .WithMany(x => x.Provinces)
                .HasForeignKey(x => x.CouId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🔥 tránh trùng tỉnh trong cùng quốc gia
            entity.HasIndex(x => new { x.CouId, x.Name })
                .IsUnique();
        }
    }

    public class WardConfiguration : IEntityTypeConfiguration<Ward>
    {
        public void Configure(EntityTypeBuilder<Ward> entity)
        {
            entity.ToTable("Wards");

            entity.HasKey(x => x.WarId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasOne(x => x.Province)
                .WithMany(x => x.Wards)
                .HasForeignKey(x => x.ProId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🔥 tránh trùng phường trong cùng tỉnh
            entity.HasIndex(x => new { x.ProId, x.Name })
                .IsUnique();
        }
    }
}