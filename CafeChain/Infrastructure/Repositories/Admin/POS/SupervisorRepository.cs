using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Orders;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Infrastructure.Repositories.Admin.POS
{
    public class SupervisorRepository : ISupervisorRepository
    {
        private readonly AppDbContext _context;

        public SupervisorRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Staff>> GetSupervisorsWithPinAsync(int storeId)
        {
            return await _context.Staffs
                .Where(s => s.StoreId == storeId
                    && s.Active
                    && s.PinHash != null
                    && s.PinHash != "")
                .Include(s => s.Account)
                .ToListAsync();
        }

        public async Task CreateAuditLogAsync(InvoiceAuditLog log)
        {
            _context.InvoiceAuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
