using CafeChain.Application.Constants;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Admin.POS
{
    public class OtpChallengeRepository : IOtpChallengeRepository
    {
        private readonly AppDbContext _context;

        public OtpChallengeRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Staff?> GetRequestingStaffAsync(int staffId, int storeId)
        {
            return await _context.Staffs
                .Include(staff => staff.Account)
                .AsNoTracking()
                .FirstOrDefaultAsync(staff =>
                    staff.StaffId == staffId &&
                    staff.StoreId == storeId &&
                    staff.Active &&
                    staff.Account != null &&
                    staff.Account.Active);
        }

        public async Task<Staff?> GetOtpApproverAsync(int storeId, DateTime utcNow)
        {
            var dayStart = utcNow.Date;
            var dayEnd = dayStart.AddDays(1);

            // OTP close-shift: email = Account.Email of staff row assigned to this store (DB), not a hard-coded address.
            // Prefer Ca trưởng (ShiftSupervisor) first; fall back StoreManager → AccountantWarehouse if no SS.
            var approverRoles = new[]
            {
                RoleConstants.ShiftSupervisor,
                RoleConstants.StoreManager,
                RoleConstants.AccountantWarehouse
            };

            var candidates = await _context.Staffs
                .Include(staff => staff.Account)
                    .ThenInclude(account => account.AccountRoles)
                        .ThenInclude(accountRole => accountRole.Role)
                .Include(staff => staff.StaffShifts)
                .Where(staff =>
                    staff.StoreId == storeId &&
                    staff.Active &&
                    staff.Account != null &&
                    staff.Account.Active &&
                    !string.IsNullOrWhiteSpace(staff.Account.Email) &&
                    staff.Account.AccountRoles.Any(accountRole =>
                        accountRole.Role != null &&
                        accountRole.Role.Active &&
                        approverRoles.Contains(accountRole.Role.Name)))
                .AsNoTracking()
                .ToListAsync();

            // 1) Prefer Ca trưởng at this store (any on-shift SS first).
            // 2) Else StoreManager / Accountant fallback.
            // Email always comes from the selected Staff.Account.Email in DB.
            return candidates
                .OrderBy(staff => GetOtpApproverRolePriority(staff))
                .ThenByDescending(staff => staff.StaffShifts.Any(shift =>
                    shift.WorkDate >= dayStart &&
                    shift.WorkDate < dayEnd &&
                    shift.ActualCheckIn != null &&
                    shift.ActualCheckOut == null))
                .ThenBy(staff => staff.StaffId)
                .FirstOrDefault();
        }

        /// <summary>
        /// Lower rank = higher priority for OTP email routing.
        /// Primary: ShiftSupervisor (Ca trưởng) assigned to the store.
        /// SalesStaff and other roles are never in the candidate set.
        /// </summary>
        private static int GetOtpApproverRolePriority(Staff staff)
        {
            var roleNames = staff.Account?.AccountRoles
                .Where(ar => ar.Role != null && ar.Role.Active)
                .Select(ar => ar.Role!.Name)
                .ToList() ?? new List<string>();

            if (roleNames.Contains(RoleConstants.ShiftSupervisor))
                return 0;
            if (roleNames.Contains(RoleConstants.StoreManager))
                return 1;
            if (roleNames.Contains(RoleConstants.AccountantWarehouse))
                return 2;
            return 99;
        }

        public async Task<Store?> GetStoreAsync(int storeId)
        {
            return await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(store => store.StoreId == storeId);
        }

        public async Task<OtpChallenge?> GetByPublicIdAsync(Guid publicId)
        {
            return await _context.OtpChallenges
                .Include(challenge => challenge.Store)
                .Include(challenge => challenge.RequestedByStaff)
                .Include(challenge => challenge.ApproverStaff)
                    .ThenInclude(staff => staff.Account)
                .FirstOrDefaultAsync(challenge => challenge.PublicId == publicId);
        }

        public async Task AddAsync(OtpChallenge challenge)
        {
            _context.OtpChallenges.Add(challenge);
            await _context.SaveChangesAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
