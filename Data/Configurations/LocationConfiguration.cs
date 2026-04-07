using CafeChain.Models;
using CafeChain.Models.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ======================= MODULE LOCATION (3-LEVEL HIERARCHY) =======================

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

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasOne(x => x.Country)
                .WithMany(x => x.Provinces)
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.SetNull); // Nullable FK → SetNull an toàn hơn Cascade

            // Tránh trùng tên tỉnh trong cùng quốc gia
            entity.HasIndex(x => new { x.CountryId, x.Name }).IsUnique();

            // 🚫 KHÔNG dùng HasData() ở đây — dữ liệu 63 tỉnh sẽ được nạp qua vietnam_locations.sql
        }
    }

    public class DistrictConfiguration : IEntityTypeConfiguration<District>
    {
        public void Configure(EntityTypeBuilder<District> entity)
        {
            entity.ToTable("Districts");
            entity.HasKey(x => x.DistrictId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasOne(x => x.Province)
                .WithMany(x => x.Districts)
                .HasForeignKey(x => x.ProvinceId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa Tỉnh → tự xóa các Quận

            // Tránh trùng tên quận trong cùng tỉnh
            entity.HasIndex(x => new { x.ProvinceId, x.Name }).IsUnique();

            // 🚫 KHÔNG dùng HasData() — dữ liệu sẽ được nạp qua vietnam_locations.sql
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

            // 🔥 THAY ĐỔI QUAN TRỌNG: Ward → District (thay vì Ward → Province)
            entity.HasOne(x => x.District)
                .WithMany(x => x.Wards)
                .HasForeignKey(x => x.DistrictId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa Quận → tự xóa các Phường

            // Tránh trùng tên phường trong cùng quận
            entity.HasIndex(x => new { x.DistrictId, x.Name }).IsUnique();

            // 🚫 KHÔNG dùng HasData() — dữ liệu sẽ được nạp qua vietnam_locations.sql
        }
    }
}