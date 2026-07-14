using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Guardrail for ShiftSupervisor / Ca trưởng demo seed (#94 / #130 follow-up):
    /// AccountId 15, StaffId 15, RoleId 8, StoreId 1, email shiftsupervisor@cafechain.vn.
    /// Uses SeedDemoIdentities — not production authorization.
    /// </summary>
    public class ShiftSupervisorSeedAccount12Tests : IntegrationTestBase
    {
        [Fact]
        public async Task Seed_AccountId15_Exists_WithShiftSupervisorEmail()
        {
            using var ctx = CreateDbContext();

            var account = await ctx.Accounts.AsNoTracking()
                .SingleOrDefaultAsync(a => a.AccountId == SeedDemoIdentities.ShiftSupervisorAccountId);

            Assert.NotNull(account);
            Assert.Equal(SeedDemoIdentities.ShiftSupervisorEmail, account!.Email);
            Assert.True(account.Active);
        }

        [Fact]
        public async Task Seed_AccountId15_LinkedToRoleId8_CaTruong()
        {
            using var ctx = CreateDbContext();

            var link = await ctx.AccountRoles.AsNoTracking()
                .SingleOrDefaultAsync(ar =>
                    ar.AccountId == SeedDemoIdentities.ShiftSupervisorAccountId &&
                    ar.RoleId == SeedDemoIdentities.ShiftSupervisorRoleId);

             Assert.NotNull(link);

            var role = await ctx.Roles.AsNoTracking()
                .SingleAsync(r => r.RoleId == SeedDemoIdentities.ShiftSupervisorRoleId);
            Assert.Equal(RoleConstants.ShiftSupervisor, role.Name);
            Assert.Equal("Ca trưởng", role.Name);
        }

        [Fact]
        public async Task Seed_Staff_References_AccountId15_StoreId1()
        {
            using var ctx = CreateDbContext();

            var staff = await ctx.Staffs.AsNoTracking()
                .SingleOrDefaultAsync(s => s.StaffId == SeedDemoIdentities.ShiftSupervisorStaffId);

            Assert.NotNull(staff);
            Assert.Equal(SeedDemoIdentities.ShiftSupervisorStaffId, staff!.StaffId);
            Assert.Equal(SeedDemoIdentities.ShiftSupervisorAccountId, staff.AccountId);
            Assert.Equal(SeedDemoIdentities.ShiftSupervisorStoreId, staff.StoreId);
            Assert.True(staff.Active);
            Assert.Contains("Ca trưởng", staff.FullName);
        }

         [Fact]
         public async Task Seed_NoDuplicate_ShiftSupervisor_Email()
         {
             using var ctx = CreateDbContext();

            var accountsWithEmail = await ctx.Accounts.AsNoTracking()
                .Where(a => a.Email == SeedDemoIdentities.ShiftSupervisorEmail)
                .ToListAsync();

            Assert.Single(accountsWithEmail);
            Assert.Equal(SeedDemoIdentities.ShiftSupervisorAccountId, accountsWithEmail[0].AccountId);

            // No other account id may carry the SS demo email.
            Assert.DoesNotContain(
                accountsWithEmail,
                a => a.AccountId != SeedDemoIdentities.ShiftSupervisorAccountId);

            var byRole = await (
                from ar in ctx.AccountRoles.AsNoTracking()
                join a in ctx.Accounts.AsNoTracking() on ar.AccountId equals a.AccountId
                join r in ctx.Roles.AsNoTracking() on ar.RoleId equals r.RoleId
                where r.Name == RoleConstants.ShiftSupervisor
                      || r.RoleId == SeedDemoIdentities.ShiftSupervisorRoleId
                select a.AccountId
            ).Distinct().ToListAsync();

            Assert.Contains(SeedDemoIdentities.ShiftSupervisorAccountId, byRole);
        }

        [Fact]
        public async Task Seed_AccountRole_Maps_15_To_8_OnlyOnce()
        {
            using var ctx = CreateDbContext();

            var rolesFor15 = await ctx.AccountRoles.AsNoTracking()
                .Where(ar => ar.AccountId == SeedDemoIdentities.ShiftSupervisorAccountId)
                .Select(ar => ar.RoleId)
                .ToListAsync();

            Assert.Single(rolesFor15);
            Assert.Equal(SeedDemoIdentities.ShiftSupervisorRoleId, rolesFor15[0]);

            // RoleId 8 must be ShiftSupervisor / Ca trưởng
            var role = await ctx.Roles.AsNoTracking()
                .SingleAsync(r => r.RoleId == SeedDemoIdentities.ShiftSupervisorRoleId);
            Assert.Equal(RoleConstants.ShiftSupervisor, role.Name);
        }
    }
}
