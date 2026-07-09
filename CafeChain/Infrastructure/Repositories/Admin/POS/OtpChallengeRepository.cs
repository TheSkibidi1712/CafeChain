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

            var approverRoles = new[]
            {
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

            return candidates
                // Ưu tiên người đang có ca làm trong ngày
                .OrderByDescending(staff => staff.StaffShifts.Any(shift =>
                    shift.WorkDate >= dayStart &&
                    shift.WorkDate < dayEnd &&
                    shift.ActualCheckIn != null &&
                    shift.ActualCheckOut == null))

                // Ưu tiên Quản lý chi nhánh trước
                .ThenBy(staff => staff.Account!.AccountRoles.Any(accountRole =>
                    accountRole.Role != null &&
                    accountRole.Role.Active &&
                    accountRole.Role.Name == RoleConstants.StoreManager)
                        ? 0
                        : 1)

                // Sau đó tới Kế toán/kho
                .ThenBy(staff => staff.Account!.AccountRoles.Any(accountRole =>
                    accountRole.Role != null &&
                    accountRole.Role.Active &&
                    accountRole.Role.Name == RoleConstants.AccountantWarehouse)
                        ? 0
                        : 1)

                .ThenBy(staff => staff.StaffId)
                .FirstOrDefault();
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
