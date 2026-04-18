using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
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
                new Role { RoleId = 1, Name = CafeChain.Application.Constants.RoleConstants.SuperAdmin, Active = true, IsStoreLevel = false, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 2, Name = CafeChain.Application.Constants.RoleConstants.CEO, Active = true, IsStoreLevel = false, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 3, Name = CafeChain.Application.Constants.RoleConstants.CFO, Active = true, IsStoreLevel = false, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 4, Name = CafeChain.Application.Constants.RoleConstants.MarketingManager, Active = true, IsStoreLevel = false, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 5, Name = CafeChain.Application.Constants.RoleConstants.OperationsManager, Active = true, IsStoreLevel = false, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 6, Name = CafeChain.Application.Constants.RoleConstants.HRManager, Active = true, IsStoreLevel = false, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 7, Name = CafeChain.Application.Constants.RoleConstants.AreaManager, Active = true, IsStoreLevel = false, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 8, Name = CafeChain.Application.Constants.RoleConstants.StoreManager, Active = true, IsStoreLevel = true, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 9, Name = CafeChain.Application.Constants.RoleConstants.ShiftSupervisor, Active = true, IsStoreLevel = true, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 10, Name = CafeChain.Application.Constants.RoleConstants.Cashier, Active = true, IsStoreLevel = true, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 11, Name = CafeChain.Application.Constants.RoleConstants.Customer, Active = true, IsStoreLevel = false, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 12, Name = CafeChain.Application.Constants.RoleConstants.WarehouseKeeper, Active = true, IsStoreLevel = true, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 13, Name = CafeChain.Application.Constants.RoleConstants.GeneralStaff, Active = true, IsStoreLevel = true, CreatedAt = new DateTime(2026, 1, 1) }
            );
        }
    }
}
