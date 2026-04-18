using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Data;
using CafeChain.Models.Loyalties;
using CafeChain.Models.Vouchers;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Vouchers
{
    public class AdminVoucherService : IAdminVoucherService
    {
        private readonly AppDbContext _context;

        public AdminVoucherService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, string Message, Voucher Voucher)> ValidateVoucherAsync(string code, int customerId, decimal subTotal)
        {
            // 🚫 TRUY VẤN TRỰC TIẾP DB - KHÔNG DÙNG CACHE (Theo AC5)
            var voucher = await _context.Vouchers
                .AsNoTracking() // Để đảm bảo data tươi mới
                .FirstOrDefaultAsync(v => v.Code == code);

            if (voucher == null)
                return (false, "Mã voucher không tồn tại.", null);

            // 1. Mã có tồn tại và Active = True không?
            if (!voucher.Active)
                return (false, "Voucher này hiện đã bị khóa.", null);

            // 2. Thời gian hiện tại có nằm trong hiệu lực [StartDate - EndDate] không?
            var now = DateTime.Now;
            if ((voucher.StartDate.HasValue && now < voucher.StartDate.Value) || 
                (voucher.EndDate.HasValue && now > voucher.EndDate.Value))
                return (false, "Voucher này đã hết hạn hoặc chưa đến ngày bắt đầu.", null);

            // 3. Tổng tiền gốc (SubTotal) có đạt MinOrderValue không?
            if (voucher.MinOrderValue.HasValue && subTotal < voucher.MinOrderValue.Value)
                return (false, $"Đơn hàng tối thiểu {voucher.MinOrderValue.Value:N0}đ mới được dùng mã này.", null);

            // 4. SĐT khách hàng này đã dùng mã này quá UsageLimitPerCustomer chưa?
            if (customerId != 0)
            {
                var usageCount = await _context.VoucherUsages
                    .CountAsync(u => u.VoucherId == voucher.VoucherId && u.CustomerId == customerId);

                if (voucher.MaxUsagePerUser.HasValue && usageCount >= voucher.MaxUsagePerUser.Value)
                    return (false, "Bạn đã sử dụng hết lượt dùng của mã này.", null);
            }
            
            // Check tong luot dung cua voucher
            var totalUsed = await _context.VoucherUsages.CountAsync(u => u.VoucherId == voucher.VoucherId);
            if (voucher.MaxUsage.HasValue && totalUsed >= voucher.MaxUsage.Value)
                return (false, "Mã voucher này đã hết lượt sử dụng.", null);

            // 5. Check ngày trong tuần
            if (!string.IsNullOrEmpty(voucher.DaysOfWeek))
            {
                var currentDay = (int)now.DayOfWeek;
                int vnDay = currentDay == 0 ? 8 : currentDay + 1;
                if (!voucher.DaysOfWeek.Split(',').Contains(vnDay.ToString()))
                    return (false, "Voucher không áp dụng cho ngày hôm nay.", null);
            }

            // 6. Check khung giờ
            if (voucher.StartHour.HasValue && voucher.EndHour.HasValue)
            {
                var currentTime = now.TimeOfDay;
                if (currentTime < voucher.StartHour.Value || currentTime > voucher.EndHour.Value)
                    return (false, $"Voucher chỉ áp dụng từ {voucher.StartHour?.ToString(@"hh\:mm")} đến {voucher.EndHour?.ToString(@"hh\:mm")}.", null);
            }

            return (true, "Áp dụng thành công!", voucher);
        }

        public async Task<decimal> CalculateMemberDiscountAsync(int customerId, decimal subTotal)
        {
            if (customerId == 0) return 0;

            var points = await _context.CustomerPoints
                .Where(p => p.CustomerId == customerId)
                .Select(p => p.Points)
                .FirstOrDefaultAsync();

            var level = await _context.MemberLevels
                .Where(l => points >= l.MinPoints && (l.MaxPoints == null || points <= l.MaxPoints))
                .OrderByDescending(l => l.MinPoints)
                .FirstOrDefaultAsync();

            if (level != null && level.DiscountPercent > 0)
            {
                return subTotal * level.DiscountPercent / 100m;
            }

            return 0;
        }

        public async Task<int> GetCustomerPointsAsync(int customerId)
        {
            if (customerId == 0) return 0;
            return await _context.CustomerPoints
                .Where(p => p.CustomerId == customerId)
                .Select(p => p.Points)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Voucher>> GetAllVouchersAsync()
        {
            return await _context.Vouchers.OrderByDescending(v => v.VoucherId).ToListAsync();
        }

        public async Task<Voucher> GetVoucherByIdAsync(int id)
        {
            return await _context.Vouchers.FindAsync(id);
        }

        public async Task<bool> CreateVoucherAsync(Voucher voucher)
        {
            _context.Vouchers.Add(voucher);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateVoucherAsync(Voucher voucher)
        {
            _context.Entry(voucher).State = EntityState.Modified;
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> ToggleVoucherActiveAsync(int id)
        {
            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null) return false;
            voucher.Active = !voucher.Active; // 🔄 ĐẢO TRẠNG THÁI (Bật/Tắt)
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<int> GetTotalUsageCountAsync()
        {
            return await _context.VoucherUsages.CountAsync();
        }

        public async Task<double> GetConversionRateAsync()
        {
            var totalOrders = await _context.Orders.CountAsync();
            if (totalOrders == 0) return 0;
            
            var ordersWithVoucher = await _context.OrderVouchers.CountAsync();
            return (double)ordersWithVoucher / totalOrders * 100;
        }

        public async Task<IEnumerable<MemberLevel>> GetAllMemberLevelsAsync()
        {
            return await _context.MemberLevels.OrderBy(l => l.MinPoints).ToListAsync();
        }

        public async Task<bool> UpdateMemberLevelAsync(MemberLevel level)
        {
            var existing = await _context.MemberLevels.FindAsync(level.MemberId);
            if (existing == null) return false;

            // 🛑 VALIDATION: Bronze < Silver < Gold
            var allLevels = await _context.MemberLevels.AsNoTracking().ToListAsync();
            
            // Tìm các hạng dựa trên tên (Cố định Bronze, Silver, Gold)
            var bronze = allLevels.FirstOrDefault(l => "Bronze".Equals(l.Name, StringComparison.OrdinalIgnoreCase));
            var silver = allLevels.FirstOrDefault(l => "Silver".Equals(l.Name, StringComparison.OrdinalIgnoreCase));
            var gold = allLevels.FirstOrDefault(l => "Gold".Equals(l.Name, StringComparison.OrdinalIgnoreCase));

            var isBronze = "Bronze".Equals(existing.Name, StringComparison.OrdinalIgnoreCase);
            var isSilver = "Silver".Equals(existing.Name, StringComparison.OrdinalIgnoreCase);
            var isGold = "Gold".Equals(existing.Name, StringComparison.OrdinalIgnoreCase);

            if (isBronze)
            {
                // Nếu đang sửa Bronze, điểm phải nhỏ hơn Silver
                if (silver != null && level.MinPoints >= silver.MinPoints)
                    return false;
            }
            else if (isSilver)
            {
                // Nếu đang sửa Silver, điểm phải lớn hơn Bronze và nhỏ hơn Gold
                if (bronze != null && level.MinPoints <= bronze.MinPoints)
                    return false;
                if (gold != null && level.MinPoints >= gold.MinPoints)
                    return false;
            }
            else if (isGold)
            {
                // Nếu đang sửa Gold, điểm phải lớn hơn Silver
                if (silver != null && level.MinPoints <= silver.MinPoints)
                    return false;
            }
            
            existing.MinPoints = level.MinPoints;
            existing.MaxPoints = level.MaxPoints;
            existing.DiscountPercent = level.DiscountPercent;
            
            _context.MemberLevels.Update(existing);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
