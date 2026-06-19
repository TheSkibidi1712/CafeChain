using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Attendance;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Infrastructure.Repositories.Attendance
{
    /// <summary>
    /// Repository implementation cho Attendance module
    /// Tách data access khỏi AttendanceSecurityService + AttendanceActionService
    /// </summary>
    public class AttendanceRepository : IAttendanceRepository
    {
        private readonly AppDbContext _context;

        public AttendanceRepository(AppDbContext context)
        {
            _context = context;
        }

        // === STAFF LOOKUP ===
        public async Task<Staff?> GetStaffByAccountIdAsync(int accountId)
        {
            return await _context.Staffs.FirstOrDefaultAsync(s => s.AccountId == accountId);
        }

        public async Task<Staff?> GetStaffWithStoreByAccountIdAsync(int accountId)
        {
            return await _context.Staffs
                .Include(s => s.Store)
                .FirstOrDefaultAsync(s => s.AccountId == accountId);
        }

        // === STORE IP VALIDATION ===
        public async Task<List<StoreIP>> GetActiveStoreIPsAsync(int storeId)
        {
            return await _context.StoreIPs
                .Where(ip => ip.StoreId == storeId && ip.IsActive)
                .ToListAsync();
        }

        // === ACCOUNT ===
        public async Task<Account?> GetAccountByIdAsync(int accountId)
        {
            return await _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
        }

        public async Task UpdateAccountAsync(Account account)
        {
            _context.Update(account);
            await _context.SaveChangesAsync();
        }

        // === STAFF UPDATE ===
        public async Task UpdateStaffAsync(Staff staff)
        {
            _context.Update(staff);
            await _context.SaveChangesAsync();
        }

        // === STAFF SHIFT (for Action — with row-level lock) ===
        public async Task<List<StaffShift>> GetStaffShiftsWithLockAsync(int staffId, DateTime today, DateTime yesterday)
        {
            var todayDateStr = today.ToString("yyyy-MM-dd");
            var yesterdayDateStr = yesterday.ToString("yyyy-MM-dd");

            return await _context.StaffShifts
                .FromSqlInterpolated($"SELECT * FROM StaffShifts WITH (UPDLOCK, ROWLOCK) WHERE StaffId = {staffId} AND (CAST(WorkDate as Date) = {todayDateStr} OR CAST(WorkDate as Date) = {yesterdayDateStr})")
                .Include(s => s.Shift)
                .ToListAsync();
        }

        public async Task CreateStaffShiftAsync(StaffShift staffShift)
        {
            _context.StaffShifts.Add(staffShift);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateStaffShiftAsync(StaffShift staffShift)
        {
            _context.Update(staffShift);
            await _context.SaveChangesAsync();
        }

        // === STAFF SHIFT (for Kiosk Data) ===
        public async Task<List<StaffShift>> GetTodayShiftsAsync(int staffId, DateTime today)
        {
            return await _context.StaffShifts
                .Where(ss => ss.StaffId == staffId && ss.WorkDate.Date == today)
                .Include(ss => ss.Shift)
                .Include(ss => ss.Status)
                .OrderBy(ss => ss.Shift.StartTime)
                .ToListAsync();
        }

        public async Task<StaffShift?> GetYesterdayActiveShiftAsync(int staffId, DateTime yesterday)
        {
            return await _context.StaffShifts
                .Where(ss => ss.StaffId == staffId && ss.WorkDate.Date == yesterday && ss.ActualCheckIn.HasValue && !ss.ActualCheckOut.HasValue)
                .Include(ss => ss.Shift)
                .Include(ss => ss.Status)
                .FirstOrDefaultAsync();
        }

        // === ATTENDANCE LOG ===
        public async Task CreateAttendanceLogAsync(AttendanceLog log)
        {
            _context.AttendanceLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
