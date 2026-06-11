using CafeChain.Application.Results;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Infrastructure.Interfaces.Attendance
{
    /// <summary>
    /// Repository xử lý data access cho Attendance module (Security + Action)
    /// Tuân thủ: Repository pattern — tách query khỏi Service
    /// </summary>
    public interface IAttendanceRepository
    {
        // === STAFF LOOKUP ===
        Task<Staff?> GetStaffByAccountIdAsync(int accountId);
        Task<Staff?> GetStaffWithStoreByAccountIdAsync(int accountId);

        // === STORE IP VALIDATION ===
        Task<List<StoreIP>> GetActiveStoreIPsAsync(int storeId);

        // === ACCOUNT ===
        Task<Account?> GetAccountByIdAsync(int accountId);
        Task UpdateAccountAsync(Account account);

        // === STAFF UPDATE ===
        Task UpdateStaffAsync(Staff staff);

        // === STAFF SHIFT (for Action) ===
        Task<List<StaffShift>> GetStaffShiftsWithLockAsync(int staffId, DateTime today, DateTime yesterday);
        Task CreateStaffShiftAsync(StaffShift staffShift);
        Task UpdateStaffShiftAsync(StaffShift staffShift);

        // === STAFF SHIFT (for Kiosk Data) ===
        Task<List<StaffShift>> GetTodayShiftsAsync(int staffId, DateTime today);
        Task<StaffShift?> GetYesterdayActiveShiftAsync(int staffId, DateTime yesterday);

        // === ATTENDANCE LOG ===
        Task CreateAttendanceLogAsync(AttendanceLog log);

        Task SaveChangesAsync();
    }
}
