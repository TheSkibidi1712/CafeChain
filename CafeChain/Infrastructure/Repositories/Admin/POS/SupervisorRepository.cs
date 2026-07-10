using CafeChain.Application.Constants;
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
            // Issue #94: PIN eligible — StoreManager, Ca trưởng, Kế toán/kho
            var supervisorRoles = new[]
            {
                RoleConstants.StoreManager,
                RoleConstants.ShiftSupervisor,
                RoleConstants.AccountantWarehouse
            };

            return await _context.Staffs
                .Where(staff =>
                    staff.StoreId == storeId &&
                    staff.Active &&
                    staff.PinHash != null &&
                    staff.PinHash != "" &&
                    staff.Account != null &&
                    staff.Account.Active &&
                    staff.Account.AccountRoles.Any(accountRole =>
                        accountRole.Role != null &&
                        accountRole.Role.Active &&
                        supervisorRoles.Contains(accountRole.Role.Name)))
                .Include(staff => staff.Account)
                    .ThenInclude(account => account.AccountRoles)
                        .ThenInclude(accountRole => accountRole.Role)
                .ToListAsync();
        }

        public async Task CreateAuditLogAsync(InvoiceAuditLog log)
        {
            _context.InvoiceAuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
