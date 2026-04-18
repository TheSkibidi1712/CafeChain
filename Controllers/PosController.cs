using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers
{
    [Route("api/[controller]")]
    public class PosController : Controller
    {
        private readonly IAdminVoucherService _voucherService;

        public PosController(IAdminVoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        [HttpPost("validate-voucher")]
        public async Task<IActionResult> ValidateVoucher([FromBody] VoucherValidationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return Json(new { success = false, message = "Mã voucher không hợp lệ." });
            }

            var result = await _voucherService.ValidateVoucherAsync(request.Code, request.CustomerId, request.SubTotal);

            if (result.Success)
            {
                // Tính số tiền giảm thực tế dựa trên logic snapshot
                decimal discountAmount = 0;
                var v = result.Voucher;
                if (v.DiscountAmount.HasValue)
                {
                    discountAmount = v.DiscountAmount.Value;
                }
                else if (v.DiscountPercent.HasValue)
                {
                    discountAmount = (request.SubTotal * v.DiscountPercent.Value) / 100;
                    if (v.MaxDiscount.HasValue && discountAmount > v.MaxDiscount.Value)
                        discountAmount = v.MaxDiscount.Value;
                }

                return Json(new
                {
                    success = true,
                    message = result.Message,
                    discountAmount = discountAmount
                });
            }

            return Json(new { success = false, message = result.Message });
        }

        // DTO nội bộ cho request
        public class VoucherValidationRequest
        {
            public string Code { get; set; }
            public int CustomerId { get; set; }
            public decimal SubTotal { get; set; }
        }
    }
}
