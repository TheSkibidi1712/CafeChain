using CafeChain.Application.Interfaces.Accounts;
using CafeChain.ViewModels.Accounts;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers
{
    public class PasswordController : Controller
    {
        private readonly IPasswordResetService _service;

        public PasswordController(IPasswordResetService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var result = await _service.SendOtpAsync(vm.Email);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Message);
                return View(vm);
            }

            return RedirectToAction("VerifyOtp", new { email = vm.Email });
        }

        [HttpGet]
        public IActionResult VerifyOtp(string email)
        {
            return View(new VerifyOtpViewModel { Email = email });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var result = await _service.VerifyOtpAsync(vm.Email, vm.OtpCode);

            if (!result.IsSuccess)
            {
                if (result.Message == "LOCKED")
                {
                    TempData["Error"] = "Bạn đã nhập sai quá 5 lần. Vui lòng đăng nhập lại.";
                    return RedirectToAction("Login", "Account");
                }

                ModelState.AddModelError("", result.Message);
                return View(vm);
            }

            return RedirectToAction("ResetPassword", new { email = vm.Email, code = vm.OtpCode });
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string code)
        {
            return View(new ResetPasswordViewModel { Email = email, OtpCode = code });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordViewModel vm)
        {
            // Kiểm tra Validation
            if (!ModelState.IsValid)
            {
                var errorMsg = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
                return Json(new { success = false, message = errorMsg ?? "Dữ liệu không hợp lệ." });
            }

            var result = await _service.ResetPasswordAsync(vm.Email, vm.OtpCode, vm.NewPassword);

            if (!result.IsSuccess)
            {
                return Json(new { success = false, message = result.Message });
            }

            return Json(new { success = true, message = "Đặt lại mật khẩu thành công! Hãy đăng nhập lại bằng mật khẩu mới." });
        }
    }
}
