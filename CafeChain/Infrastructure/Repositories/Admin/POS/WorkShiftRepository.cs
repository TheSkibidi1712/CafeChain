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

        public async Task<StaffShift?> GetEffectiveStaffShiftAsync(int staffId, int storeId, DateTime now)
        {
            var today = now.Date;
            var candidates = await _context.StaffShifts
                .Include(ss => ss.Shift)
                .Include(ss => ss.Status)
                .Where(ss => ss.StaffId == staffId
                    && ss.Staff.StoreId == storeId
                    && ss.Shift.StoreId == storeId
                    && ss.Status.Code == "SCHEDULED"
                    && ss.WorkDate >= today.AddDays(-1)
                    && ss.WorkDate <= today)
                .ToListAsync();

            return candidates
                .Select(schedule => new
                {
                    Schedule = schedule,
                    Start = schedule.WorkDate.Date.Add(schedule.CustomStartTime ?? schedule.Shift.StartTime),
                    End = ResolveEnd(schedule)
                })
                .Where(x => x.Start <= now && now < x.End)
                .OrderByDescending(x => x.Start)
                .Select(x => x.Schedule)
                .FirstOrDefault();
        }

        private static DateTime ResolveEnd(StaffShift schedule)
        {
            var start = schedule.CustomStartTime ?? schedule.Shift.StartTime;
            var end = schedule.CustomEndTime ?? schedule.Shift.EndTime;
            var result = schedule.WorkDate.Date.Add(end);
            return schedule.Shift.IsOvernight || end <= start ? result.AddDays(1) : result;
        }

        public async Task<decimal> GetTotalCashSalesAsync(int shiftId)
        {
            var settledCashAmounts = await _context.Orders
                .Where(o => o.WorkShiftId == shiftId)
                .Join(_context.Payments,
                    o => o.OrderId,
                    p => p.OrderId,
                    (o, p) => new { Order = o, Payment = p })
                .Where(op =>
                    op.Payment.PaymentMethodId == 1 &&
                    op.Payment.PaymentStatusId == SystemConstants.PaymentStatuses.Paid &&
                    op.Order.PaymentStatusId == SystemConstants.PaymentStatuses.Paid &&
                    op.Order.OrderStatusId != SystemConstants.OrderStatuses.Cancelled)
                .Select(op => op.Payment.Amount)
                .ToListAsync();

            return settledCashAmounts.Sum();
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
