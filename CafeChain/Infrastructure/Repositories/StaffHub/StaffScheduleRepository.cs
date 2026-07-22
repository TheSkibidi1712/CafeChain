using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.StaffHub;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.StaffHub;

public sealed class StaffScheduleRepository : IStaffScheduleRepository
{
    private readonly AppDbContext _context;
    public StaffScheduleRepository(AppDbContext context) => _context = context;

    public Task<Staff?> GetStaffScheduleAsync(int staffId, DateTime fromDate, DateTime toDate, CancellationToken ct) =>
        _context.Staffs.AsNoTracking()
            .Include(x => x.Account)
            .Include(x => x.Store)
            .Include(x => x.StaffShifts.Where(s => s.WorkDate >= fromDate.Date && s.WorkDate <= toDate.Date))
                .ThenInclude(x => x.Shift)
            .Include(x => x.StaffShifts.Where(s => s.WorkDate >= fromDate.Date && s.WorkDate <= toDate.Date))
                .ThenInclude(x => x.Status)
            .SingleOrDefaultAsync(x => x.StaffId == staffId && x.Active, ct);
}
