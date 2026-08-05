using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;
using CafeChain.Models.Stores;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.POS
{
    public interface IWorkShiftService
    {
        /// <summary>
        /// Opens a new POS financial shift.
        /// Includes strict HR BYOD Interlock validation.
        /// </summary>
        Task<ServiceResult> OpenShiftAsync(int userId, int storeId, OpenShiftRequestDto request);

        Task<ServiceResult<OpenShiftAssessmentDto>> AssessOpenShiftAsync(
            int userId,
            int storeId,
            OpenShiftAssessmentRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Assesses the current staff schedule without requiring or mutating a POS terminal.
        /// Used by StaffHub as a read-only preflight before issuing a one-time POS exchange code.
        /// </summary>
        Task<ServiceResult<OpenShiftAssessmentDto>> AssessOpenContextAsync(
            int staffId,
            int storeId,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<OpenShiftAssessmentDto>> AssessOpenContextAsync(
            int staffId,
            int storeId,
            string terminalId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PosTerminalOptionDto>> GetAvailableTerminalsAsync(
            int storeId,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<PosSessionExchangeContextDto>> PrepareOpenExchangeContextAsync(
            int accountId, int staffId, int storeId, string terminalId, string requestKey,
            string? reason, Guid? otpChallengePublicId,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<PosSessionExchangeContextDto>> PrepareResumeExchangeContextAsync(
            int accountId, int staffId, int storeId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the currently open WorkShift for a user at a store.
        /// Returns null if no shift is open.
        /// </summary>
        Task<WorkShift?> GetActiveShiftAsync(int userId, int storeId);

        /// <summary>
        /// Gets a historical POS WorkShift by id for Offline Order Sync.
        /// Allows closed shifts but requires same staff and store.
        /// </summary>
        Task<WorkShift?> GetShiftByIdAsync(int shiftId, int userId, int storeId);

        /// <summary>
        /// Loads a WorkShift by its server identity before Store/Area authorization.
        /// Callers must authorize the returned StoreId before exposing any data.
        /// </summary>
        Task<WorkShift?> GetShiftByIdAsync(int shiftId);

        /// <summary>
        /// Closes an open WorkShift with cash reconciliation.
        /// Calculates expected ending cash and records discrepancy.
        /// </summary>
        Task<ServiceResult> CloseShiftAsync(int userId, int storeId, CloseShiftRequestDto request);
        Task<ServiceResult> CloseShiftAsync(int userId, int storeId, int shiftId, CloseShiftRequestDto request);

        Task<ShiftSummaryDto?> GetSummaryAsync(int userId, int storeId, int? shiftId = null);

        /// <summary>
        /// Closes an open WorkShift by supervisor/manager exception while preserving
        /// local Offline Orders for later Sync into the original WorkShift.
        /// </summary>
        Task<ServiceResult> CloseShiftByExceptionAsync(int userId, int storeId, int shiftId, CloseShiftExceptionRequestDto request);

        Task<ServiceResult> StartClosingAsync(int userId, int storeId, int shiftId, StartClosingRequestDto request);

        Task<ServiceResult> ReconcileAsync(int userId, int storeId, int shiftId, ReconcileWorkShiftRequestDto request);

        Task<ServiceResult> RegisterTerminalAsync(int userId, int storeId, PosTerminalRegisterDto request);
    }
}
