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

        /// <summary>
        /// Closes an open WorkShift by supervisor/manager exception while preserving
        /// local Offline Orders for later Sync into the original WorkShift.
        /// </summary>
        Task<ServiceResult> CloseShiftByExceptionAsync(int userId, int storeId, int shiftId, CloseShiftExceptionRequestDto request);
    }
}
