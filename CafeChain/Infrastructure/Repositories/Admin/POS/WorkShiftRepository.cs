using CafeChain.Application.Constants;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Stores;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Infrastructure.Repositories.Admin.POS
{
    /// <summary>
    /// Repository implementation cho WorkShift — tách data access khỏi Service layer
    /// </summary>
    public class WorkShiftRepository : IWorkShiftRepository
    {
        private readonly AppDbContext _context;

        public WorkShiftRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<WorkShift?> GetActiveShiftAsync(int userId, int storeId)
        {
            return await _context.WorkShifts
                .FirstOrDefaultAsync(ws => ws.UserId == userId && ws.StoreId == storeId && ws.Status == "Open");
        }

        public async Task<WorkShift?> GetShiftByIdAsync(int shiftId, int userId, int storeId)
        {
            return await _context.WorkShifts
                .FirstOrDefaultAsync(ws =>
                    ws.ShiftId == shiftId &&
                    ws.UserId == userId &&
                    ws.StoreId == storeId);
        }

        public async Task<WorkShift> CreateShiftAsync(WorkShift shift)
        {
            _context.WorkShifts.Add(shift);
            await _context.SaveChangesAsync();
            return shift;
        }

        public async Task UpdateShiftAsync(WorkShift shift)
        {
            _context.Update(shift);
            await _context.SaveChangesAsync();
        }

        public async Task EnsurePosTerminalAsync(string terminalId, int storeId, string name)
        {
            var exists = await _context.PosTerminals
                .AnyAsync(terminal => terminal.TerminalId == terminalId);

            if (exists) return;

            _context.PosTerminals.Add(new PosTerminal
            {
                TerminalId = terminalId,
                StoreId = storeId,
                Name = name,
                Active = true,
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }

        public async Task<StaffShift?> GetTodayStaffShiftAsync(int staffId)
        {
            var today = DateTime.Today;
            return await _context.StaffShifts
                .Include(ss => ss.Shift)
                .Where(ss => ss.StaffId == staffId && ss.WorkDate.Date == today && ss.ShiftId != null && !ss.IsAdHoc)
                .FirstOrDefaultAsync();
        }

        public async Task<decimal> GetTotalCashSalesAsync(int shiftId)
        {
            return await _context.Orders
                .Where(o => o.WorkShiftId == shiftId)
                .Join(_context.Payments,
                    o => o.OrderId,
                    p => p.OrderId,
                    (o, p) => new { Order = o, Payment = p })
                .Where(op => op.Payment.PaymentMethodId == 1) // 1 = Cash payment
                .SumAsync(op => (decimal?)op.Payment.Amount) ?? 0m;
        }

        public async Task<bool> HasOpenPosPaymentAsync(int shiftId, int storeId)
        {
            return await _context.Orders
                .AnyAsync(order =>
                    order.WorkShiftId == shiftId &&
                    order.StoreId == storeId &&
                    order.Source == "POS" &&
                    order.OrderStatusId == SystemConstants.OrderStatuses.AwaitingPayment &&
                    order.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
