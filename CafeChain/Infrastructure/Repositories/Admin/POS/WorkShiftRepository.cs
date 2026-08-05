using CafeChain.Application.Constants;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Stores;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using CafeChain.Application.Results;
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
            var activeStatuses = WorkShiftStatuses.ActiveResponsibility;
            return await _context.WorkShifts
                .Include(ws => ws.User)
                .Include(ws => ws.PosTerminal)
                .FirstOrDefaultAsync(ws => ws.UserId == userId && ws.StoreId == storeId
                    && activeStatuses.Contains(ws.Status));
        }

        public async Task<WorkShift?> GetActiveShiftByTerminalAsync(string terminalId, int storeId)
        {
            var activeStatuses = WorkShiftStatuses.ActiveResponsibility;
            return await _context.WorkShifts
                .Include(x => x.User)
                .Include(x => x.PosTerminal)
                .FirstOrDefaultAsync(x => x.StoreId == storeId
                    && x.PosTerminalId == terminalId
                    && activeStatuses.Contains(x.Status));
        }

        public async Task<IReadOnlyList<PosTerminal>> GetActiveTerminalsAsync(int storeId)
        {
            return await _context.PosTerminals.AsNoTracking()
                .Where(x => x.StoreId == storeId && x.Active && x.Store.Active)
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<WorkShift?> GetShiftByIdAsync(int shiftId, int userId, int storeId)
        {
            return await _context.WorkShifts
                .Include(ws => ws.User)
                .FirstOrDefaultAsync(ws =>
                    ws.ShiftId == shiftId &&
                    ws.UserId == userId &&
                    ws.StoreId == storeId);
        }

        public async Task<WorkShift?> GetShiftByIdAsync(int shiftId)
        {
            return await _context.WorkShifts
                .Include(ws => ws.User)
                .FirstOrDefaultAsync(ws => ws.ShiftId == shiftId);
        }

        public async Task<WorkShift> CreateShiftAsync(WorkShift shift)
        {
            IDbContextTransaction? transaction = null;
            var ownsTransaction = _context.Database.CurrentTransaction == null && _context.Database.IsRelational();
            if (ownsTransaction)
                transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                var activeStatuses = WorkShiftStatuses.ActiveResponsibility;
                var staffConflict = await _context.WorkShifts.FirstOrDefaultAsync(x =>
                    x.UserId == shift.UserId && activeStatuses.Contains(x.Status));
                if (staffConflict != null)
                    throw BuildStaffConflict(staffConflict);

                if (!string.IsNullOrWhiteSpace(shift.PosTerminalId))
                {
                    var terminalConflict = await _context.WorkShifts.FirstOrDefaultAsync(x =>
                        x.PosTerminalId == shift.PosTerminalId
                        && activeStatuses.Contains(x.Status));
                    if (terminalConflict != null)
                        throw new WorkShiftBusinessException(
                            WorkShiftErrorCodes.TerminalAlreadyHasOpenShift,
                            "Terminal đang có phiên POS chưa kết thúc.");
                }

                _context.WorkShifts.Add(shift);
                await _context.SaveChangesAsync();
                if (transaction != null)
                    await transaction.CommitAsync();
                return shift;
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();
            }
        }

        private static WorkShiftBusinessException BuildStaffConflict(WorkShift shift) => shift.Status switch
        {
            WorkShiftStatuses.Open => new(
                WorkShiftErrorCodes.StaffAlreadyHasOpenShift,
                "Bạn đang có một phiên POS hoạt động. Hãy tiếp tục sử dụng hoặc đóng phiên hiện tại trước khi mở phiên mới."),
            WorkShiftStatuses.Closing => new(
                WorkShiftErrorCodes.WorkShiftPendingClose,
                "Phiên POS trước đang trong quá trình chốt két. Hãy hoàn tất đóng phiên trước khi mở phiên mới."),
            WorkShiftStatuses.ExpiredPendingClose => new(
                WorkShiftErrorCodes.WorkShiftPendingClose,
                "Phiên POS trước đã hết thời lượng nhưng chưa được kiểm đếm và đóng. Hãy xử lý phiên cũ trước khi mở phiên mới."),
            _ => new(WorkShiftErrorCodes.ConcurrencyConflict, "Trạng thái phiên POS đã thay đổi.")
        };

        public async Task UpdateShiftAsync(WorkShift shift)
        {
            _context.Update(shift);
            await _context.SaveChangesAsync();
        }

        public async Task EnsurePosTerminalAsync(string terminalId, int storeId, string name)
        {
            var terminal = await _context.PosTerminals
                .Include(x => x.Store)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TerminalId == terminalId);
            if (terminal == null)
                throw new WorkShiftBusinessException(
                    WorkShiftErrorCodes.TerminalNotFound,
                    "Terminal chưa được đăng ký và phê duyệt.");
            if (terminal.StoreId != storeId)
                throw new WorkShiftBusinessException(
                    WorkShiftErrorCodes.TerminalStoreMismatch,
                    "Terminal không thuộc cửa hàng hiện tại.");
            if (!terminal.Active)
                throw new WorkShiftBusinessException(
                    WorkShiftErrorCodes.TerminalInactive,
                    "Terminal đã bị vô hiệu hóa.");
            if (terminal.Store == null || !terminal.Store.Active)
                throw new WorkShiftBusinessException(
                    WorkShiftErrorCodes.TerminalInactive,
                    "Cửa hàng của terminal đã bị vô hiệu hóa.");
        }

        public async Task<PosTerminal> RegisterPosTerminalAsync(string terminalId, int storeId, string name)
        {
            var existing = await _context.PosTerminals.FirstOrDefaultAsync(x => x.TerminalId == terminalId);
            if (existing != null)
            {
                if (existing.StoreId != storeId)
                    throw new WorkShiftBusinessException(WorkShiftErrorCodes.TerminalStoreMismatch, "Terminal đã thuộc cửa hàng khác.");
                existing.Name = name;
                existing.Active = true;
                await _context.SaveChangesAsync();
                return existing;
            }

            var terminal = new PosTerminal
            {
                TerminalId = terminalId,
                StoreId = storeId,
                Name = name,
                Active = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.PosTerminals.Add(terminal);
            await _context.SaveChangesAsync();
            return terminal;
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
                    && ss.WorkDate <= today.AddDays(1))
                .ToListAsync();

            return candidates
                .Select(schedule => new
                {
                    Schedule = schedule,
                    Start = schedule.WorkDate.Date.Add(schedule.CustomStartTime ?? schedule.Shift.StartTime),
                    End = ResolveEnd(schedule)
                })
                .Where(x => x.Start.AddMinutes(-30) <= now && now <= x.End.AddMinutes(30))
                .OrderBy(x => Math.Abs((x.Start - now).TotalMinutes))
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
