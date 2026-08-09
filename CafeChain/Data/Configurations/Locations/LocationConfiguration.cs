using CafeChain.Models;
using CafeChain.Models.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Locations
{
    // ======================= MODULE LOCATION (2-LEVEL HIERARCHY) =======================

    public class CountryConfiguration : IEntityTypeConfiguration<Country>
    {
        public void Configure(EntityTypeBuilder<Country> entity)
        {
            entity.ToTable("Countries");
            entity.HasKey(x => x.CountryId);
            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);
            entity.HasIndex(x => x.Name).IsUnique();

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

            entity.Property(x => x.Code)
                .IsRequired()
                .IsFixedLength()
                .HasMaxLength(2);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasOne(x => x.Country)
                .WithMany(x => x.Provinces)
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.SetNull); // Nullable FK → SetNull an toàn hơn Cascade

            entity.HasIndex(x => x.CountryId);
            entity.HasIndex(x => x.Code).IsUnique();

            // 🚫 KHÔNG dùng HasData() ở đây — dữ liệu 63 tỉnh sẽ được nạp qua vietnam_locations.sql
        }
    }

    public class WardConfiguration : IEntityTypeConfiguration<Ward>
    {
        public void Configure(EntityTypeBuilder<Ward> entity)
        {
            entity.ToTable("Wards");
            entity.HasKey(x => x.WardId);

            entity.Property(x => x.Code)
                .IsRequired()
                .IsFixedLength()
                .HasMaxLength(5);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasOne(x => x.Province)
                .WithMany(x => x.Wards)
                .HasForeignKey(x => x.ProvinceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ProvinceId);
            entity.HasIndex(x => x.Code).IsUnique();

            // 🚫 KHÔNG dùng HasData() — dữ liệu sẽ được nạp qua vietnam_locations.sql
        }
    }
}
