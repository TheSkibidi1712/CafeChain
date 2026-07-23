using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Systems
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
                },
                new SystemSetting
                {
                    SettingId = 2001,
                    SettingKey = "inventory_manual_external_export_negative_enabled",
                    SettingValue = "false",
                    Description = "Cho phép phiếu xuất ngoài với mục đích SALE gửi yêu cầu xuất âm."
                },
                new SystemSetting
                {
                    SettingId = 2002,
                    SettingKey = "inventory_manual_external_export_approval_required",
                    SettingValue = "true",
                    Description = "Bắt buộc maker-checker cho phiếu xuất ngoài làm âm kho."
                },
                new SystemSetting
                {
                    SettingId = 2003,
                    SettingKey = "inventory_manual_external_export_default_max_negative_quantity",
                    SettingValue = "0",
                    Description = "Hạn mức âm mặc định cho phiếu xuất ngoài."
                },
                new SystemSetting
                {
                    SettingId = 2004,
                    SettingKey = "inventory_manual_external_export_policy_version",
                    SettingValue = "manual-export-v1",
                    Description = "Phiên bản policy phiếu xuất ngoài làm âm kho."
                }
            );
        }
    }
}
