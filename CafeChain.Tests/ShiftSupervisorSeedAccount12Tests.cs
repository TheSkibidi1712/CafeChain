using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Fixed seed identity for ShiftSupervisor / Ca trưởng:
    /// AccountId = 12, RoleId = 8, Staff.AccountId = 12.
    /// </summary>
    public class ShiftSupervisorSeedAccount12Tests : IntegrationTestBase
    {
        private const int FixedAccountId = 12;
        private const int FixedStaffId = 12;
        private const int ShiftSupervisorRoleId = 8;

        [Fact]
        public async Task Seed_AccountId12_Exists_WithShiftSupervisorEmail()
        {
            using var ctx = CreateDbContext();

            var account = await ctx.Accounts.AsNoTracking()
                .SingleOrDefaultAsync(a => a.AccountId == FixedAccountId);

            Assert.NotNull(account);
            Assert.Equal("shiftsupervisor@cafechain.vn", account!.Email);
            Assert.True(account.Active);
        }

        [Fact]
        public async Task Seed_AccountId12_LinkedToRoleId8_CaTruong()
        {
            using var ctx = CreateDbContext();

            var link = await ctx.AccountRoles.AsNoTracking()
                .SingleOrDefaultAsync(ar =>
                    ar.AccountId == FixedAccountId &&
                    ar.RoleId == ShiftSupervisorRoleId);

            Assert.NotNull(link);

            var role = await ctx.Roles.AsNoTracking()
                .SingleAsync(r => r.RoleId == ShiftSupervisorRoleId);
            Assert.Equal(RoleConstants.ShiftSupervisor, role.Name);
            Assert.Equal("Ca trưởng", role.Name);
        }

        [Fact]
        public async Task Seed_Staff_References_AccountId12_StoreId1()
        {
            using var ctx = CreateDbContext();

            var staff = await ctx.Staffs.AsNoTracking()
                .SingleOrDefaultAsync(s => s.AccountId == FixedAccountId);

            Assert.NotNull(staff);
            Assert.Equal(FixedStaffId, staff!.StaffId);
            Assert.Equal(FixedAccountId, staff.AccountId);
            Assert.Equal(1, staff.StoreId);
            Assert.True(staff.Active);
            Assert.Contains("Ca trưởng", staff.FullName);
        }

        [Fact]
        public async Task Seed_NoDuplicate_ShiftSupervisor_Email()
        {
            using var ctx = CreateDbContext();

            var count = await ctx.Accounts.AsNoTracking()
                .CountAsync(a => a.Email == "shiftsupervisor@cafechain.vn");

            Assert.Equal(1, count);

            var byRole = await (
                from ar in ctx.AccountRoles.AsNoTracking()
                join a in ctx.Accounts.AsNoTracking() on ar.AccountId equals a.AccountId
                join r in ctx.Roles.AsNoTracking() on ar.RoleId equals r.RoleId
                where r.Name == RoleConstants.ShiftSupervisor
                select a.AccountId
            ).Distinct().ToListAsync();

            Assert.Contains(FixedAccountId, byRole);
            // Official seed should only introduce AccountId 12 for this role (tests may add more).
            Assert.Contains(FixedAccountId, byRole);
        }

        [Fact]
        public async Task Seed_AccountRole_Maps_12_To_8_OnlyOnce()
        {
            using var ctx = CreateDbContext();

            var rolesFor12 = await ctx.AccountRoles.AsNoTracking()
                .Where(ar => ar.AccountId == FixedAccountId)
                .Select(ar => ar.RoleId)
                .ToListAsync();

            Assert.Single(rolesFor12);
            Assert.Equal(ShiftSupervisorRoleId, rolesFor12[0]);
        }
    }
}
