using CafeChain.Models.Stores;
using CafeChain.Models.Staffs;
using System.Threading.Tasks;

namespace CafeChain.Infrastructure.Interfaces.Admin.POS
{
    /// <summary>
    /// Repository xử lý data access cho WorkShift module
    /// Tuân thủ: Repository pattern — tách query khỏi Service
    /// </summary>
    public interface IWorkShiftRepository
    {
        // === SHIFT CRUD ===
        Task<WorkShift?> GetActiveShiftAsync(int userId, int storeId);
        Task<WorkShift> CreateShiftAsync(WorkShift shift);
        Task UpdateShiftAsync(WorkShift shift);
        Task EnsurePosTerminalAsync(string terminalId, int storeId, string name);

        // === STAFF SHIFT LOOKUP ===
        Task<StaffShift?> GetTodayStaffShiftAsync(int staffId);

        // === CLOSE SHIFT DATA ===
        Task<decimal> GetTotalCashSalesAsync(int shiftId);

        // === RECONCILIATION ===
        /// <summary>
        /// Ghi bản ghi cảnh báo chênh lệch két tiền vào InvoiceAuditLog.
        /// ActionName = "SHIFT_CASH_DISCREPANCY" — sẵn sàng cho Web Admin đối soát.
        /// </summary>
        Task CreateReconciliationWarningAsync(int shiftId, int storeId, int userId, decimal discrepancy, string? reason);

        Task SaveChangesAsync();
    }
}
