using CafeChain.Data;
using CafeChain.Models.Vouchers;
using CafeChain.Models.Customers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CafeChain.Controllers
{
    public class LuckyWheelController : Controller
    {
        private readonly AppDbContext _context;
        private readonly CafeChain.Application.Services.Admin.Vouchers.IAdminWheelService _wheelService;

        public LuckyWheelController(AppDbContext context, CafeChain.Application.Services.Admin.Vouchers.IAdminWheelService wheelService)
        {
            _context = context;
            _wheelService = wheelService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserInfo()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                var guestWheel = await _wheelService.GetActiveConfigAsync();
                return Json(new { 
                    isAuthenticated = false, 
                    spinCost = guestWheel?.SpinCost ?? 0 
                });
            }

            var accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var customer = await _context.Customers
                .Include(c => c.CustomerPoints)
                .FirstOrDefaultAsync(c => c.AccountId == accountId);

            if (customer == null) return Json(new { isAuthenticated = false });

            var activeWheel = await _wheelService.GetActiveConfigAsync();
            if (activeWheel == null) return Json(new { isAuthenticated = true, points = 0 });

            // Kiểm tra xem đã từng quay lần nào chưa (toàn hệ thống)
            var hasSpun = await _context.WheelSpins
                .AnyAsync(s => s.CustomerId == customer.CustomerId);

            return Json(new {
                isAuthenticated = true,
                points = customer.CustomerPoints.Sum(p => p.Points),
                isNewUser = !hasSpun,
                spinCost = hasSpun ? activeWheel.SpinCost : 0 // Hiển thị 0 nếu là lượt miễn phí
            });
        }

        [HttpPost]
        public async Task<IActionResult> Spin()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để quay thưởng!", notLoggedIn = true });
            }

            var accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var customer = await _context.Customers
                .Include(c => c.CustomerPoints)
                .FirstOrDefaultAsync(c => c.AccountId == accountId);

            if (customer == null) return Json(new { success = false, message = "Không tìm thấy thông tin khách hàng." });

            var activeWheel = await _wheelService.GetActiveConfigAsync();
            if (activeWheel == null || !activeWheel.Active)
            {
                return Json(new { success = false, message = "Vòng quay hiện đang đóng." });
            }

            // Kiểm tra lượt quay miễn phí (chưa từng quay lần nào trong đời)
            var hasSpun = await _context.WheelSpins
                .AnyAsync(s => s.CustomerId == customer.CustomerId);

            var totalPoints = customer.CustomerPoints.Sum(p => p.Points);
            bool isFreeSpin = !hasSpun;

            if (!isFreeSpin && totalPoints < activeWheel.SpinCost)
            {
                return Json(new { success = false, message = $"Bạn cần {activeWheel.SpinCost} điểm để quay. Hiện bạn chỉ có {totalPoints} điểm." });
            }

            // Thực hiện quay
            var prize = SelectPrize(activeWheel.Prizes.ToList());
            if (prize == null) return Json(new { success = false, message = "Lỗi cấu hình giải thưởng." });

            // Trừ điểm nếu không phải free
            if (!isFreeSpin)
            {
                var pointRecord = customer.CustomerPoints.First(); // Giả sử chỉ có 1 record điểm chính
                pointRecord.Points -= activeWheel.SpinCost;
            }

            // Lưu lịch sử quay
            var spin = new WheelSpin
            {
                CustomerId = customer.CustomerId,
                WheelConfigId = activeWheel.WheelConfigId,
                WheelPrizeId = prize.WheelPrizeId,
                CreatedAt = DateTime.Now
            };
            _context.WheelSpins.Add(spin);

            // Nếu trúng Voucher, tặng cho User
            if (!prize.IsLose && prize.VoucherId.HasValue)
            {
                // Logic tặng voucher (tùy thuộc vào cách hệ thống quản lý sở hữu voucher)
                // Ở đây ta có thể tạo VoucherUsage hoặc chỉ đơn giản là báo trúng.
                // Giả sử có bảng CustomerVouchers hoặc dùng chung VoucherUsage làm gift.
            }

            await _context.SaveChangesAsync();

            return Json(new {
                success = true,
                prizeIndex = prize.SlotIndex,
                isLose = prize.IsLose,
                voucherCode = prize.Voucher?.Code,
                newPoints = customer.CustomerPoints.Sum(p => p.Points)
            });
        }

        private WheelPrize? SelectPrize(List<WheelPrize> prizes)
        {
            if (!prizes.Any()) return null;
            
            var totalWeight = prizes.Sum(p => p.Probability);
            var randomNum = (decimal)new Random().NextDouble() * totalWeight;
            
            decimal currentWeight = 0;
            foreach (var prize in prizes)
            {
                currentWeight += prize.Probability;
                if (randomNum <= currentWeight)
                    return prize;
            }
            
            return prizes.Last();
        }
    }
}
