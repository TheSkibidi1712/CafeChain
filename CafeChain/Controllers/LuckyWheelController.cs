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
                .FirstOrDefaultAsync(c => c.AccountId == accountId);

            if (customer == null) return Json(new { isAuthenticated = false });

            var activeWheel = await _wheelService.GetActiveConfigAsync();
            if (activeWheel == null) return Json(new { isAuthenticated = true, points = 0 });

            var totalSpins = await _context.WheelSpins
                .CountAsync(s => s.CustomerId == customer.CustomerId);
            var spinsToday = await _context.WheelSpins
                .CountAsync(s => s.CustomerId == customer.CustomerId && s.CreatedAt.Date == DateTime.Today.Date);

            bool isFreeSpin = (totalSpins == 0);
            bool canSpinToday = true;

            if (totalSpins > 1 && spinsToday >= 1) canSpinToday = false;
            if (totalSpins == 1 && spinsToday > 1) canSpinToday = false;

            DateTime? nextSpinTime = null;
            if (!canSpinToday)
            {
                nextSpinTime = DateTime.Today.AddDays(1);
            }

            return Json(new {
                isAuthenticated = true,
                points = customer.CurrentPoints,
                isNewUser = isFreeSpin,
                spinCost = isFreeSpin ? 0 : activeWheel.SpinCost,
                canSpinToday = canSpinToday,
                nextSpinTime = nextSpinTime
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
                .FirstOrDefaultAsync(c => c.AccountId == accountId);

            if (customer == null) return Json(new { success = false, message = "Không tìm thấy thông tin khách hàng." });

            var activeWheel = await _wheelService.GetActiveConfigAsync();
            if (activeWheel == null || !activeWheel.Active)
            {
                return Json(new { success = false, message = "Vòng quay hiện đang đóng." });
            }

            var totalSpins = await _context.WheelSpins
                .CountAsync(s => s.CustomerId == customer.CustomerId);
            var spinsToday = await _context.WheelSpins
                .CountAsync(s => s.CustomerId == customer.CustomerId && s.CreatedAt.Date == DateTime.Today.Date);

            // Logic giới hạn quay: 
            // - Acc mới: 1 lần free, và được quay thêm 1 lần xài điểm trong ngày đầu tiên.
            // - Các acc khác: Chỉ được quay 1 lần 1 ngày.
            if (totalSpins > 1 && spinsToday >= 1)
            {
                return Json(new { success = false, message = "Bạn đã hết lượt quay ngày hôm nay. Hãy quay lại vào ngày mai nhé!" });
            }
            if (totalSpins == 1 && spinsToday > 1)
            {
                return Json(new { success = false, message = "Bạn đã hết lượt quay ngày hôm nay. Hãy quay lại vào ngày mai nhé!" });
            }

            var totalPoints = customer.CurrentPoints;
            bool isFreeSpin = (totalSpins == 0);

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
                customer.CurrentPoints -= activeWheel.SpinCost;
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
                var customerVoucher = new CustomerVoucher
                {
                    CustomerId = customer.CustomerId,
                    VoucherId = prize.VoucherId.Value,
                    IsUsed = false,
                    CollectedDate = DateTime.Now
                };
                _context.CustomerVouchers.Add(customerVoucher);
            }

            await _context.SaveChangesAsync();

            return Json(new {
                success = true,
                prizeIndex = prize.SlotIndex,
                isLose = prize.IsLose,
                voucherCode = prize.Voucher?.Code,
                newPoints = customer.CurrentPoints
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
