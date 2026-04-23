using CafeChain.Application.Results;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.POS
{
    public interface IWorkShiftService
    {
        /// <summary>
        /// Opens a new POS financial shift.
        /// Includes strict HR BYOD Interlock validation.
        /// </summary>
        Task<ServiceResult> OpenShiftAsync(int userId, int storeId, decimal startingCash);
    }
}
