using CafeChain.Application.Constants;
using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Customers.Accounts
{
    public class AccountRoleConfiguration : IEntityTypeConfiguration<AccountRole>
    {
        public void Configure(EntityTypeBuilder<AccountRole> entity)
        {
            entity.ToTable("AccountRoles");

            entity.HasKey(x => new { x.AccountId, x.RoleId });

            entity.HasOne(x => x.Account)
                .WithMany(x => x.AccountRoles)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Role)
                .WithMany(x => x.AccountRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasData(
                new AccountRole
                {
                    AccountId = 1,
                    RoleId = 1 // Chủ doanh nghiệp
                },
                new AccountRole
                {
                    AccountId = 2,
                    RoleId = 2 // Quản lý vùng
                },
                new AccountRole
                {
                    AccountId = 3,
                    RoleId = 3 // Quản lý chi nhánh
                },
                new AccountRole
                {
                    AccountId = 4,
                    RoleId = 4 // Nhân viên bán hàng
                },
                new AccountRole
                {
                    AccountId = 5,
                    RoleId = 5 // Kế toán/kho
                },
                new AccountRole
                {
                    AccountId = 6,
                    RoleId = 6 // Quản trị hệ thống
                },
                new AccountRole
                {
                    AccountId = 7,
                    RoleId = 7 // Khách hàng
                },
                // AccountId 12 → RoleId 8 (Ca trưởng / ShiftSupervisor) — SeedDemoIdentities
                new AccountRole
                {
                    AccountId = SeedDemoIdentities.ShiftSupervisorAccountId,
                    RoleId = SeedDemoIdentities.ShiftSupervisorRoleId
                }
            );
        }
    }
}
