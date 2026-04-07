using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Staff;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastrusture.Repositories.Admin.Staff
{
    public class AdminStaffRepository : IAdminStaffRepository
    {
        private readonly AppDbContext _context;

        public AdminStaffRepository(AppDbContext context)
        {
            _context = context;
        }

        // ==================== READ ====================

        public async Task<(IEnumerable<Models.Staffs.Staff> Items, int TotalCount)> GetPaginatedStaffsAsync(
            int pageIndex, int pageSize, int? storeId, string search, int? roleFilter)
        {
            var query = _context.Staffs
                .Include(s => s.Account)
                    .ThenInclude(a => a.AccountRoles)
                        .ThenInclude(ar => ar.Role)
                .Include(s => s.Store)
                .Include(s => s.StaffPhones)
                .AsQueryable();

            // 🔥 RULE 1: Store Manager chỉ thấy nhân viên cùng chi nhánh
            if (storeId.HasValue)
            {
                query = query.Where(s => s.StoreId == storeId.Value);
            }

            // Search by name or email
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(s =>
                    s.FullName.ToLower().Contains(searchLower) ||
                    s.Account.Email.ToLower().Contains(searchLower));
            }

            // Filter by role
            if (roleFilter.HasValue)
            {
                query = query.Where(s =>
                    s.Account.AccountRoles.Any(ar => ar.RoleId == roleFilter.Value));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Models.Staffs.Staff> GetStaffByIdAsync(int staffId)
        {
            return await _context.Staffs
                .Include(s => s.Account)
                    .ThenInclude(a => a.AccountRoles)
                        .ThenInclude(ar => ar.Role)
                .Include(s => s.Store)
                .Include(s => s.StaffPhones)
                .Include(s => s.StaffAddresses)
                .Include(s => s.StaffScopes)
                    .ThenInclude(ss => ss.ScopeType)
                .FirstOrDefaultAsync(s => s.StaffId == staffId);
        }

        public async Task<(int Total, int Active, int Inactive)> GetStaffCountsAsync(int? storeId)
        {
            var query = _context.Staffs.AsQueryable();
            if (storeId.HasValue)
            {
                query = query.Where(s => s.StoreId == storeId.Value);
            }

            var total = await query.CountAsync();
            var active = await query.CountAsync(s => s.Active);
            return (total, active, total - active);
        }

        // ==================== WRITE (TRANSACTION) ====================

        // 🔥 RULE 3: Transaction bắt buộc cho Create
        public async Task CreateStaffTransactionAsync(
            Models.Staffs.Staff staff,
            Account account,
            List<AccountRole> accountRoles,
            List<StaffScope> staffScopes,
            List<StaffPhone> staffPhones,
            List<StaffAddress> staffAddresses)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Insert Account
                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();

                // 2. Insert Staff (link AccountId)
                staff.AccountId = account.AccountId;
                _context.Staffs.Add(staff);
                await _context.SaveChangesAsync();

                // 3. Insert AccountRoles
                foreach (var role in accountRoles)
                {
                    role.AccountId = account.AccountId;
                }
                _context.AccountRoles.AddRange(accountRoles);

                // 4. Insert StaffScopes
                foreach (var scope in staffScopes)
                {
                    scope.StaffId = staff.StaffId;
                }
                _context.StaffScopes.AddRange(staffScopes);

                // 5. Insert StaffPhones
                foreach (var phone in staffPhones)
                {
                    phone.StaffId = staff.StaffId;
                }
                if (staffPhones.Any())
                    await _context.StaffPhones.AddRangeAsync(staffPhones);

                // 6. Insert StaffAddresses
                foreach (var address in staffAddresses)
                {
                    address.StaffId = staff.StaffId;
                }
                if (staffAddresses.Any())
                    await _context.StaffAddresses.AddRangeAsync(staffAddresses);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // 🔥 RULE 3: Transaction + Clear & Replace cho Update
        public async Task UpdateStaffTransactionAsync(
            Models.Staffs.Staff staff,
            Account account,
            List<AccountRole> accountRoles,
            List<StaffScope> staffScopes,
            List<StaffPhone> staffPhones,
            List<StaffAddress> staffAddresses)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Update Account
                _context.Accounts.Update(account);

                // 2. Update Staff
                _context.Staffs.Update(staff);

                // 3. Clear & Replace AccountRoles
                var oldRoles = await _context.AccountRoles
                    .Where(r => r.AccountId == account.AccountId)
                    .ToListAsync();
                _context.AccountRoles.RemoveRange(oldRoles);

                foreach (var role in accountRoles)
                {
                    role.AccountId = account.AccountId;
                }
                _context.AccountRoles.AddRange(accountRoles);

                // 4. Clear & Replace StaffScopes
                var oldScopes = await _context.StaffScopes
                    .Where(s => s.StaffId == staff.StaffId)
                    .ToListAsync();
                _context.StaffScopes.RemoveRange(oldScopes);

                foreach (var scope in staffScopes)
                {
                    scope.StaffId = staff.StaffId;
                }
                _context.StaffScopes.AddRange(staffScopes);

                // 5. Clear & Replace StaffPhones
                var oldPhones = await _context.StaffPhones
                    .Where(p => p.StaffId == staff.StaffId)
                    .ToListAsync();
                _context.StaffPhones.RemoveRange(oldPhones);

                foreach (var phone in staffPhones)
                {
                    phone.StaffId = staff.StaffId;
                }
                if (staffPhones.Any())
                    await _context.StaffPhones.AddRangeAsync(staffPhones);

                // 6. Clear & Replace StaffAddresses
                var oldAddresses = await _context.StaffAddresses
                    .Where(a => a.StaffId == staff.StaffId)
                    .ToListAsync();
                _context.StaffAddresses.RemoveRange(oldAddresses);

                foreach (var address in staffAddresses)
                {
                    address.StaffId = staff.StaffId;
                }
                if (staffAddresses.Any())
                    await _context.StaffAddresses.AddRangeAsync(staffAddresses);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // 🔥 RULE 3: Toggle Status → đồng bộ Staff.Active + Account.Active
        public async Task<bool> ToggleStatusAsync(int staffId)
        {
            var staff = await _context.Staffs
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.StaffId == staffId);

            if (staff == null) return false;

            staff.Active = !staff.Active;
            staff.Account.Active = staff.Active; // 🔥 Đồng bộ Account

            _context.Staffs.Update(staff);
            _context.Accounts.Update(staff.Account);
            await _context.SaveChangesAsync();
            return true;
        }

        // ==================== VALIDATION QUERIES ====================

        public async Task<bool> EmailExistsAsync(string email, int? excludeAccountId = null)
        {
            if (excludeAccountId.HasValue)
                return await _context.Accounts.AnyAsync(a => a.Email == email && a.AccountId != excludeAccountId.Value);

            return await _context.Accounts.AnyAsync(a => a.Email == email);
        }

        public async Task<bool> DefaultPhoneExistsAsync(string phone, int? excludeStaffId = null)
        {
            if (excludeStaffId.HasValue)
                return await _context.StaffPhones.AnyAsync(p =>
                    p.Phone == phone && p.IsDefault && p.StaffId != excludeStaffId.Value);

            return await _context.StaffPhones.AnyAsync(p => p.Phone == phone && p.IsDefault);
        }

        public async Task<bool> TaxCodeExistsAsync(string taxCode, int? excludeStaffId = null)
        {
            if (string.IsNullOrWhiteSpace(taxCode)) return false;

            if (excludeStaffId.HasValue)
                return await _context.Staffs.AnyAsync(s =>
                    s.TaxCode == taxCode && s.StaffId != excludeStaffId.Value);

            return await _context.Staffs.AnyAsync(s => s.TaxCode == taxCode);
        }

        // 🔥 RULE 2 (Advanced): Kiểm tra ca thu ngân đang mở
        public async Task<bool> HasOpenCashSessionAsync(int staffId)
        {
            return await _context.CashSessions.AnyAsync(cs =>
                cs.StaffId == staffId && !cs.IsClosed);
        }

        // 🔥 RULE 2 (Advanced): Kiểm tra ca làm việc chưa checkout
        public async Task<bool> HasActiveShiftAsync(int staffId)
        {
            return await _context.StaffShifts.AnyAsync(ss =>
                ss.StaffId == staffId &&
                ss.Status.Code == "CHECKED_IN" &&
                ss.ActualCheckOut == null);
        }

        // ==================== MASTER DATA (Thin Controller) ====================

        public async Task<List<Role>> GetRolesForDropdownAsync(int? storeManagerStoreId)
        {
            if (storeManagerStoreId.HasValue)
            {
                // Store Manager: CHỈ được chọn Cashier (RoleId = 1)
                return await _context.Roles
                    .Where(r => r.RoleId == 1 && r.Active)
                    .ToListAsync();
            }
            // Admin System: Loại bỏ Customer (RoleId = 6)
            return await _context.Roles
                .Where(r => r.RoleId != 6 && r.Active)
                .ToListAsync();
        }

        public async Task<List<Store>> GetActiveStoresAsync()
        {
            return await _context.Stores.Where(s => s.Active).ToListAsync();
        }

        public async Task<List<ScopeType>> GetScopeTypesAsync()
        {
            return await _context.ScopeTypes.ToListAsync();
        }

        public async Task<Store> GetStoreByIdAsync(int storeId)
        {
            return await _context.Stores.FindAsync(storeId);
        }

        public async Task UpdateStaffAvatarAsync(int staffId, string avatarUrl)
        {
            var staff = await _context.Staffs.FindAsync(staffId);
            if (staff != null)
            {
                staff.AvatarUrl = avatarUrl;
                _context.Staffs.Update(staff);
                await _context.SaveChangesAsync();
            }
        }

        // 🔥 CCCD Uniqueness Check (Filtered — bỏ qua NULL/empty)
        public async Task<bool> CCCDExistsAsync(string cccd, int? excludeStaffId = null)
        {
            if (string.IsNullOrWhiteSpace(cccd)) return false;

            if (excludeStaffId.HasValue)
                return await _context.Staffs.AnyAsync(s =>
                    s.CCCD == cccd && s.StaffId != excludeStaffId.Value);

            return await _context.Staffs.AnyAsync(s => s.CCCD == cccd);
        }
    }
}
