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
                    SettingKey = "inventory_allow_negative_stock",
                    SettingValue = "false",
                    Description = "Cho phép tồn kho âm có kiểm soát."
                },
                new SystemSetting
                {
                    SettingId = 2002,
                    SettingKey = "inventory_require_manager_approval_for_negative_stock",
                    SettingValue = "false",
                    Description = "Yêu cầu quản lý/admin xác nhận khi giao dịch làm âm kho."
                },
                new SystemSetting
                {
                    SettingId = 2003,
                    SettingKey = "inventory_default_max_negative_quantity",
                    SettingValue = "0",
                    Description = "Ngưỡng âm kho mặc định nếu tồn kho nguyên liệu không cấu hình riêng."
                }
            );
        }
    }
}
