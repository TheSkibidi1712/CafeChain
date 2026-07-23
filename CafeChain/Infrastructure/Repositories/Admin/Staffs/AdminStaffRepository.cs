using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Staffs;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using CafeChain.Models.Permissions;

namespace CafeChain.Infrastrusture.Repositories.Admin.Staffs
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
            int pageIndex, int pageSize, IReadOnlyCollection<int>? storeIds, string search, int? roleFilter)
        {
            var query = _context.Staffs
                .Include(s => s.Account)
                    .ThenInclude(a => a.AccountRoles)
                        .ThenInclude(ar => ar.Role)
                .Include(s => s.Store)
                .Include(s => s.StaffPhones)
                .AsQueryable();

            // 🔥 RULE 1: Store Manager chỉ thấy nhân viên cùng chi nhánh
            if (storeIds != null)
            {
                query = query.Where(s => storeIds.Contains(s.StoreId));
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
                    .ThenInclude(a => a.Province)
                .Include(s => s.StaffAddresses)
                    .ThenInclude(a => a.District)
                .Include(s => s.StaffAddresses)
                    .ThenInclude(a => a.Ward)
                .Include(s => s.StaffScopes)
                    .ThenInclude(ss => ss.ScopeType)
                .FirstOrDefaultAsync(s => s.StaffId == staffId);
        }

        public Task<Models.Staffs.Staff?> GetStaffByAccountIdAsync(int accountId) =>
            _context.Staffs
                .Include(x => x.Account).ThenInclude(x => x.AccountRoles).ThenInclude(x => x.Role)
                .FirstOrDefaultAsync(x => x.AccountId == accountId);

        public async Task<(int Total, int Active, int Inactive)> GetStaffCountsAsync(IReadOnlyCollection<int>? storeIds)
        {
            var query = _context.Staffs.AsQueryable();
            if (storeIds != null)
            {
                query = query.Where(s => storeIds.Contains(s.StoreId));
            }

            var total = await query.CountAsync();
            var active = await query.CountAsync(s => s.Active);
            return (total, active, total - active);
        }


        public Task<List<CafeChain.Models.Locations.Province>> GetProvincesAsync()
        {
            return _context.Provinces.OrderBy(p => p.Name).ToListAsync();
        }

        public Task<List<CafeChain.Models.Locations.District>> GetDistrictsAsync(int provinceId)
        {
            return _context.Districts.Where(d => d.ProvinceId == provinceId).OrderBy(d => d.Name).ToListAsync();
        }

        public Task<List<CafeChain.Models.Locations.Ward>> GetWardsAsync(int districtId)
        {
            return _context.Wards.Where(w => w.DistrictId == districtId).OrderBy(w => w.Name).ToListAsync();
        }

        public async Task<bool> ScopeCoversStoreAsync(int scopeTypeId, int scopeRefId, int storeId)
        {
            var store = await _context.Stores.AsNoTracking()
                .Include(x => x.Province)
                .Include(x => x.District).ThenInclude(x => x!.Province)
                .Include(x => x.Ward).ThenInclude(x => x!.District).ThenInclude(x => x!.Province)
                .FirstOrDefaultAsync(x => x.StoreId == storeId && x.Active);
            if (store == null) return false;

            var districtId = store.DistrictId ?? store.Ward?.DistrictId;
            var provinceId = store.ProvinceId ?? store.District?.ProvinceId ?? store.Ward?.District?.ProvinceId;
            var countryId = store.Province?.CountryId
                ?? store.District?.Province?.CountryId
                ?? store.Ward?.District?.Province?.CountryId;

            return scopeTypeId switch
            {
                1 => countryId == scopeRefId,
                2 => provinceId == scopeRefId,
                3 => districtId == scopeRefId,
                4 => store.WardId == scopeRefId,
                5 => store.StoreId == scopeRefId,
                _ => false
            };
        }

        public async Task<bool> IsAddressHierarchyValidAsync(int provinceId, int districtId, int wardId)
        {
            var districtValid = await _context.Districts
                .AnyAsync(d => d.DistrictId == districtId && d.ProvinceId == provinceId);
            return districtValid && await _context.Wards
                .AnyAsync(w => w.WardId == wardId && w.DistrictId == districtId);
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

                // Staff payroll/bank/dependent data is intentionally outside the current Staff contract.
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();
                var message = ParseDuplicateErrorMessage(dbEx);
                throw new InvalidOperationException(message, dbEx);
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

                // Role, permission, scope and primary store are not replaced by a profile update.
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();
                var message = ParseDuplicateErrorMessage(dbEx);
                throw new InvalidOperationException(message, dbEx);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // 🔥 RULE 3: Toggle Status → đồng bộ Staff.Active + Account.Active
        public async Task UpdateStaffProfileTransactionAsync(
            Models.Staffs.Staff staff,
            Account account,
            List<StaffPhone> staffPhones,
            List<StaffAddress> staffAddresses)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Profile edits must not replace AccountRole or StaffScope rows.
                var oldPhones = await _context.StaffPhones.Where(x => x.StaffId == staff.StaffId).ToListAsync();
                var oldAddresses = await _context.StaffAddresses.Where(x => x.StaffId == staff.StaffId).ToListAsync();

                _context.StaffPhones.RemoveRange(oldPhones);
                _context.StaffAddresses.RemoveRange(oldAddresses);

                foreach (var item in staffPhones) item.StaffId = staff.StaffId;
                foreach (var item in staffAddresses) item.StaffId = staff.StaffId;

                await _context.StaffPhones.AddRangeAsync(staffPhones);
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

        public async Task<bool> ResetPasswordAsync(int accountId, string passwordHash)
        {
            var account = await _context.Accounts.FindAsync(accountId);
            if (account == null) return false;
            account.PasswordHash = passwordHash;
            account.RequiresPasswordChange = false;
            await _context.SaveChangesAsync();
            return true;
        }

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
            if (string.IsNullOrWhiteSpace(email))
                return false;

            // Case-insensitive duplicate check (SQL LOWER) — admin may retype email with different casing.
            var normalized = email.Trim().ToLower();

            if (excludeAccountId.HasValue)
            {
                return await _context.Accounts.AnyAsync(a =>
                    a.Email != null &&
                    a.Email.ToLower() == normalized &&
                    a.AccountId != excludeAccountId.Value);
            }

            return await _context.Accounts.AnyAsync(a =>
                a.Email != null &&
                a.Email.ToLower() == normalized);
        }

        public async Task<bool> DefaultPhoneExistsAsync(string phone, int? excludeStaffId = null)
        {
            if (excludeStaffId.HasValue)
                return await _context.StaffPhones.AnyAsync(p =>
                    p.Phone == phone && p.IsDefault && p.StaffId != excludeStaffId.Value);

            return await _context.StaffPhones.AnyAsync(p => p.Phone == phone && p.IsDefault);
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
            return await _context.WorkShifts.AnyAsync(shift =>
                shift.UserId == staffId && shift.Status == "Open" && shift.EndTime == null);
        }

        // ==================== MASTER DATA (Thin Controller) ====================

        public async Task<List<Role>> GetRolesForDropdownAsync(int? storeManagerStoreId)
        {
            // Caller (AdminStaffService) applies actor-specific allow-lists.
            // Repository only excludes Customer and inactive roles.
            // storeManagerStoreId is retained for interface compatibility; filtering is service-side.
            _ = storeManagerStoreId;
            return await _context.Roles
                .Where(r => r.Active && r.Name != CafeChain.Application.Constants.RoleConstants.Customer)
                .OrderBy(r => r.RoleId)
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

        // 🔥 CCCD Uniqueness Check (Filtered — bỏ qua NULL/empty)
        public async Task<bool> CCCDExistsAsync(string cccd, int? excludeStaffId = null)
        {
            if (string.IsNullOrWhiteSpace(cccd)) return false;

            if (excludeStaffId.HasValue)
                return await _context.Staffs.AnyAsync(s =>
                    s.CCCD == cccd && s.StaffId != excludeStaffId.Value);

            return await _context.Staffs.AnyAsync(s => s.CCCD == cccd);
        }

        // ==================== DUPLICATE ERROR PARSER ====================
        /// <summary>
        /// Phân tích lỗi DbUpdateException để trả về thông báo tiếng Việt thay vì crash.
        /// Xử lý các trường hợp Unique Constraint Violation (SQL Error 2601/2627).
        /// </summary>
        private string ParseDuplicateErrorMessage(DbUpdateException dbEx)
        {
            if (dbEx.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                var msg = sqlEx.Message;
                if (msg.Contains("Email", StringComparison.OrdinalIgnoreCase))
                    return "Lỗi: Email đã tồn tại trong hệ thống. Vui lòng sử dụng email khác.";
                if (msg.Contains("CCCD", StringComparison.OrdinalIgnoreCase))
                    return "Lỗi: Số CCCD đã tồn tại trong hệ thống.";
                if (msg.Contains("Phone", StringComparison.OrdinalIgnoreCase))
                    return "Lỗi: Số điện thoại đã tồn tại trong hệ thống.";
                return $"Lỗi: Dữ liệu bị trùng lặp trong hệ thống. Chi tiết: {msg}";
            }
            return "Lỗi lưu dữ liệu nhân viên. Vui lòng kiểm tra lại thông tin và thử lại.";
        }
    }
}
