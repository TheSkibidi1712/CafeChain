using System.Threading.Tasks;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Attendance
{
    public interface IAttendanceActionService
    {
        Task<ServiceResult> SubmitTimeActionAsync(int accountId, string actionType, string faceDescriptor, bool forceSave = false);
        Task<ServiceResult<object>> GetKioskDataAsync(int accountId);
    }
}
