using CafeChain.Models.Orders;
using CafeChain.Models.Staffs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Infrastructure.Interfaces.Admin.POS
{
    /// <summary>
    /// Repository xử lý data access cho Supervisor PIN Auth module
    /// </summary>
    public interface ISupervisorRepository
    {
        Task<List<Staff>> GetSupervisorsWithPinAsync(int storeId);
        Task CreateAuditLogAsync(InvoiceAuditLog log);
    }
}
