using CafeChain.Models.Staffs;
using CafeChain.Application.Results;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.Admin.Staffs
{
    public interface IAdminStaffShiftService
    {
        Task<Dictionary<Staff, List<StaffShift>>> GetShiftMatrixAsync(int storeId, DateTime startDate, DateTime endDate);
        Task<ServiceResult> AssignShiftAsync(int staffId, int shiftId, DateTime date, TimeSpan? customStart = null, TimeSpan? customEnd = null);
        Task<ServiceResult> UpdateStaffShiftAsync(int staffShiftId, int shiftId, TimeSpan? customStart = null, TimeSpan? customEnd = null);
        Task<List<object>> GetShiftsForStoreAsync(int storeId);
        Task<ServiceResult> UpdateShiftAsync(int shiftId, TimeSpan startTime, TimeSpan endTime, string? notes);
    }
}
