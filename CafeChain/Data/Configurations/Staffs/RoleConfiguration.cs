using CafeChain.Models.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Staffs
{
    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> entity)
        {
            entity.ToTable("Roles");

            entity.HasKey(x => x.RoleId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasIndex(x => x.Name).IsUnique();

            entity.HasData(
                new Role
                {
                    RoleId = 1,
                    Name = CafeChain.Application.Constants.RoleConstants.BusinessOwner,
                    Active = true,
                    IsStoreLevel = false,
                    CreatedAt = new DateTime(2026, 1, 1)
                },
                new Role
                {
                    RoleId = 2,
                    Name = CafeChain.Application.Constants.RoleConstants.AreaManager,
                    Active = true,
                    IsStoreLevel = false,
                    CreatedAt = new DateTime(2026, 1, 1)
                },
                new Role
                {
                    RoleId = 3,
                    Name = CafeChain.Application.Constants.RoleConstants.StoreManager,
                    Active = true,
                    IsStoreLevel = true,
                    CreatedAt = new DateTime(2026, 1, 1)
                },
                new Role
                {
                    RoleId = 4,
                    Name = CafeChain.Application.Constants.RoleConstants.SalesStaff,
                    Active = true,
                    IsStoreLevel = true,
                    CreatedAt = new DateTime(2026, 1, 1)
                },
                new Role
                {
                    RoleId = 5,
                    Name = CafeChain.Application.Constants.RoleConstants.AccountantWarehouse,
                    Active = true,
                    IsStoreLevel = true,
                    CreatedAt = new DateTime(2026, 1, 1)
                },
                new Role
                {
                    RoleId = 6,
                    Name = CafeChain.Application.Constants.RoleConstants.SystemAdmin,
                    Active = true,
                    IsStoreLevel = false,
                    CreatedAt = new DateTime(2026, 1, 1)
                },
                new Role
                {
                    RoleId = 7,
                    Name = CafeChain.Application.Constants.RoleConstants.Customer,
                    Active = true,
                    IsStoreLevel = false,
                    CreatedAt = new DateTime(2026, 1, 1)
                },
                // RoleId 8 — store-level operational approver (PIN / OTP / future stock-alert routing).
                // Seed only in configuration; apply via approved EF migration before production use.
                new Role
                {
                    RoleId = 8,
                    Name = CafeChain.Application.Constants.RoleConstants.ShiftSupervisor,
                    Active = true,
                    IsStoreLevel = true,
                    CreatedAt = new DateTime(2026, 1, 1)
                }
            );
        }
    }
}
