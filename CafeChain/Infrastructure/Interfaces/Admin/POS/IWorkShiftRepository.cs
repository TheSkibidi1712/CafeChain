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
        Task<WorkShift?> GetActiveShiftByTerminalAsync(string terminalId, int storeId);
        Task<IReadOnlyList<PosTerminal>> GetActiveTerminalsAsync(int storeId);
        Task<WorkShift?> GetShiftByIdAsync(int shiftId, int userId, int storeId);
        Task<WorkShift?> GetShiftByIdAsync(int shiftId);
        Task<WorkShift> CreateShiftAsync(WorkShift shift);
        Task UpdateShiftAsync(WorkShift shift);
        Task<WorkShift> BindTerminalForResumeAsync(
            int shiftId, int userId, int storeId, string terminalId,
            CancellationToken cancellationToken = default);
        Task EnsurePosTerminalAsync(string terminalId, int storeId, string name);
        Task<PosTerminal> RegisterPosTerminalAsync(string terminalId, int storeId, string name);
        Task<Staff?> GetStaffForOperatorAsync(int staffId);
        Task<IReadOnlyList<Staff>> GetActiveOperatorCandidatesAsync(int storeId);

        // === STAFF SHIFT LOOKUP ===
        Task<StaffShift?> GetEffectiveStaffShiftAsync(int staffId, int storeId, DateTime now);

        // === CLOSE SHIFT DATA ===
        Task<decimal> GetTotalCashSalesAsync(int shiftId);
        Task<bool> HasOpenPosPaymentAsync(int shiftId, int storeId);

        Task SaveChangesAsync();
    }
}
