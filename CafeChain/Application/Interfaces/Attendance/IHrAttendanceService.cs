using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.Attendance
{
    public interface IHrAttendanceService
    {
        /// <summary>
        /// Verifies if a user has checked in recently via the BYOD system using FaceID and correct IP.
        /// </summary>
        /// <param name="userId">Staff ID</param>
        /// <param name="storeId">Store ID</param>
        /// <returns>True if a valid check-in exists within the last 30 minutes</returns>
        Task<bool> VerifyRecentCheckInAsync(int userId, int storeId);
    }
}
