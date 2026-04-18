using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Models.Vouchers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
        [Area("Admin")]
    public class AdminVoucherController : Controller
    {
        private readonly IAdminVoucherService _voucherService;

        public AdminVoucherController(IAdminVoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        public async Task<IActionResult> Index()
        {
            var vouchers = await _voucherService.GetAllVouchersAsync();
            var totalUsed = await _voucherService.GetTotalUsageCountAsync();
            var rate = await _voucherService.GetConversionRateAsync();
            var levels = await _voucherService.GetAllMemberLevelsAsync();

            var viewModel = new ViewModels.Admin.Vouchers.VoucherDashboardViewModel
            {
                Vouchers = vouchers,
                TotalUsedCount = totalUsed,
                ConversionRate = rate,
                MemberLevels = levels
            };
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMemberLevel(CafeChain.Models.Loyalties.MemberLevel level)
        {
            var success = await _voucherService.UpdateMemberLevelAsync(level);
            if (!success)
            {
                TempData["Error"] = "Thứ tự điểm thưởng không hợp lệ (Bronze < Silver < Gold).";
            }
            else
            {
                TempData["Success"] = "Cập nhật hạng thành viên thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(int? VoucherId, string Code, int? DiscountPercent, decimal? DiscountAmount, decimal? MaxDiscount, decimal? MinOrderValue, DateTime? StartDate, DateTime? EndDate, int? MaxUsage, int? MaxUsagePerUser, string? DaysOfWeek, TimeSpan? StartHour, TimeSpan? EndHour)
        {
            // Kiểm tra trùng mã
            var allVouchers = await _voucherService.GetAllVouchersAsync();
            if (allVouchers.Any(v => v.Code.Equals(Code, StringComparison.OrdinalIgnoreCase) && v.VoucherId != (VoucherId ?? 0)))
            {
                TempData["Error"] = $"Mã Voucher '{Code}' đã tồn tại trong hệ thống!";
                return RedirectToAction(nameof(Index));
            }

            if (StartDate.HasValue && EndDate.HasValue && EndDate < StartDate)
            {
                TempData["Error"] = "Ngày kết thúc không thể nhỏ hơn ngày bắt đầu!";
                return RedirectToAction(nameof(Index));
            }

            if (VoucherId.HasValue && VoucherId.Value > 0)
            {
                var existing = await _voucherService.GetVoucherByIdAsync(VoucherId.Value);
                if (existing != null)
                {
                    existing.Code = Code;
                    existing.DiscountPercent = DiscountPercent;
                    existing.DiscountAmount = DiscountAmount;
                    existing.MaxDiscount = MaxDiscount;
                    existing.MinOrderValue = MinOrderValue;
                    existing.StartDate = StartDate;
                    existing.EndDate = EndDate;
                    existing.MaxUsage = MaxUsage;
                    existing.MaxUsagePerUser = MaxUsagePerUser;
                    existing.DaysOfWeek = DaysOfWeek;
                    existing.StartHour = StartHour;
                    existing.EndHour = EndHour;
                    await _voucherService.UpdateVoucherAsync(existing);
                    return RedirectToAction(nameof(Index));
                }
            }

            var voucher = new Voucher
            {
                Code = Code,
                DiscountPercent = DiscountPercent,
                DiscountAmount = DiscountAmount,
                MaxDiscount = MaxDiscount,
                MinOrderValue = MinOrderValue,
                StartDate = StartDate,
                EndDate = EndDate,
                MaxUsage = MaxUsage,
                MaxUsagePerUser = MaxUsagePerUser,
                DaysOfWeek = DaysOfWeek,
                StartHour = StartHour,
                EndHour = EndHour,
                Active = true
            };

            await _voucherService.CreateVoucherAsync(voucher);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var voucher = await _voucherService.GetVoucherByIdAsync(id);
            if (voucher == null) return NotFound();
            return View(voucher);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Voucher voucher)
        {
            if (ModelState.IsValid)
            {
                await _voucherService.UpdateVoucherAsync(voucher);
                return RedirectToAction(nameof(Index));
            }
            return View(voucher);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            await _voucherService.ToggleVoucherActiveAsync(id);
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetVoucherJson(int id)
        {
            var voucher = await _voucherService.GetVoucherByIdAsync(id);
            if (voucher == null) return NotFound();
            return Json(new {
                voucherId = voucher.VoucherId,
                code = voucher.Code,
                discountPercent = voucher.DiscountPercent,
                discountAmount = voucher.DiscountAmount,
                maxDiscount = voucher.MaxDiscount,
                minOrderValue = voucher.MinOrderValue,
                startDate = voucher.StartDate?.ToString("yyyy-MM-dd"),
                endDate = voucher.EndDate?.ToString("yyyy-MM-dd"),
                maxUsage = voucher.MaxUsage,
                maxUsagePerUser = voucher.MaxUsagePerUser,
                daysOfWeek = voucher.DaysOfWeek,
                startHour = voucher.StartHour?.ToString(@"hh\:mm"),
                endHour = voucher.EndHour?.ToString(@"hh\:mm")
            });
        }
    }
}
