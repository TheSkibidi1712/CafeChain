using CafeChain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
    {
        public void Configure(EntityTypeBuilder<SystemSetting> builder)
        {
            builder.ToTable("SystemSettings");

            builder.HasKey(s => s.SettingId);

            builder.Property(s => s.SettingKey)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(s => s.SettingKey).IsUnique();

            builder.Property(s => s.SettingValue)
                .IsRequired();

            builder.Property(s => s.Description)
                .HasMaxLength(500);

            // Seed Data
            builder.HasData(
                new SystemSetting
                {
                    SettingId = 1,
                    SettingKey = "Map_Default_Center",
                    SettingValue = "10.8231, 106.6297",
                    Description = "Toạ độ trung tâm mặc định (VD: TPHCM - 10.8231, 106.6297)"
                }
            );
        }
    }
}
