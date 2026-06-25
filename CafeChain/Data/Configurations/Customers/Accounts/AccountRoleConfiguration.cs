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
                // ===== NEW ORG CHART ROLES MAPPING =====
                new AccountRole { AccountId = 101, RoleId = 1 },
                new AccountRole { AccountId = 102, RoleId = 2 },
                new AccountRole { AccountId = 103, RoleId = 3 },
                new AccountRole { AccountId = 104, RoleId = 4 },
                new AccountRole { AccountId = 105, RoleId = 5 },
                new AccountRole { AccountId = 106, RoleId = 6 },
                new AccountRole { AccountId = 107, RoleId = 7 },
                new AccountRole { AccountId = 108, RoleId = 8 },
                new AccountRole { AccountId = 109, RoleId = 9 },
                new AccountRole { AccountId = 110, RoleId = 10 },
                new AccountRole { AccountId = 111, RoleId = 11 },
                new AccountRole { AccountId = 122, RoleId = 7 },
                new AccountRole { AccountId = 123, RoleId = 10 }
            );
        }
    }
}
