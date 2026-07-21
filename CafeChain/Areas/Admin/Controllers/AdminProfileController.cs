using CafeChain.Data;
using CafeChain.ViewModels.Profile;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class AdminProfileController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AdminProfileController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ====================================================================
        // HELPER: Trích xuất AccountId an toàn từ Claims (Zero-Trust Core)
        // ====================================================================
        private int GetCurrentAccountId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null || !int.TryParse(claim.Value, out int accountId))
                throw new UnauthorizedAccessException("Không thể xác thực người dùng.");
            return accountId;
        }

        // ====================================================================
        // GET /Admin/AdminProfile/MyProfile
        // ====================================================================
        [HttpGet]
        public async Task<IActionResult> MyProfile()
        {
            var accountId = GetCurrentAccountId();

            var staff = await _context.Staffs
                .Include(s => s.Account)
                    .ThenInclude(a => a.AccountRoles)
                        .ThenInclude(ar => ar.Role)
                .Include(s => s.Store)
                .Include(s => s.StaffPhones)
                .FirstOrDefaultAsync(s => s.AccountId == accountId);

            if (staff == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ nhân viên.";
                return RedirectToAction("Index", "Home");
            }

            var roleName = staff.Account.AccountRoles
                .Select(ar => ar.Role.Name)
                .FirstOrDefault() ?? "Chưa phân quyền";

            var vm = new MyProfileVM
            {
                FullName = staff.FullName,
                Email = staff.Account.Email,
                CCCD = staff.CCCD,
                DateOfBirth = staff.DateOfBirth,
                Gender = staff.Gender,
                RoleName = roleName,
                StoreName = staff.Store?.Name ?? "Chưa phân chi nhánh",
                EmployeeStatus = staff.EmployeeStatus,
                Active = staff.Active,
                StartDate = staff.StartDate,
                AvatarUrl = staff.AvatarUrl,
                PhoneNumber = staff.StaffPhones?.FirstOrDefault(p => p.IsDefault)?.Phone
                              ?? staff.StaffPhones?.FirstOrDefault()?.Phone
            };

            return View(vm);
        }

        // ====================================================================
        // POST /Admin/AdminProfile/UpdateMyProfile
        // Anti-Overposting: CHỈ bind PhoneNumber + AvatarFile
        // ====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMyProfile([Bind("PhoneNumber,AvatarFile")] UpdateProfileVM model)
        {
            var accountId = GetCurrentAccountId();

            var staff = await _context.Staffs
                .Include(s => s.StaffPhones)
                .FirstOrDefaultAsync(s => s.AccountId == accountId);

            if (staff == null)
                return Json(new { success = false, message = "Không tìm thấy hồ sơ nhân viên." });

            string? updatedAvatarUrl = null;

            // === Cập nhật Avatar ===
            if (model.AvatarFile != null && model.AvatarFile.Length > 0)
            {
                updatedAvatarUrl = await SaveAvatarAsync(model.AvatarFile);
                staff.AvatarUrl = updatedAvatarUrl;
            }

            // === Cập nhật Số điện thoại ===
            if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                // Kiểm tra trùng SĐT trong toàn hệ thống (trừ chính mình)
                bool phoneExists = await _context.Staffs
                    .Where(s => s.StaffId != staff.StaffId)
                    .SelectMany(s => s.StaffPhones)
                    .AnyAsync(p => p.Phone == model.PhoneNumber);

                if (phoneExists)
                    return Json(new { success = false, message = "Số điện thoại này đã được sử dụng bởi nhân viên khác." });

                var defaultPhone = staff.StaffPhones?.FirstOrDefault(p => p.IsDefault);
                if (defaultPhone != null)
                {
                    defaultPhone.Phone = model.PhoneNumber;
                }
                else
                {
                    staff.StaffPhones ??= new List<Models.Staffs.StaffPhone>();
                    staff.StaffPhones.Add(new Models.Staffs.StaffPhone
                    {
                        StaffId = staff.StaffId,
                        Phone = model.PhoneNumber,
                        IsDefault = true
                    });
                }
            }

            _context.Update(staff);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(updatedAvatarUrl))
            {
                await RefreshAvatarClaimAsync(updatedAvatarUrl);
            }

            return Json(new
            {
                success = true,
                message = "Cập nhật hồ sơ thành công!",
                avatarUrl = updatedAvatarUrl
            });
        }

        // ====================================================================
        // POST /Admin/AdminProfile/ChangePassword
        // ====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([FromForm] ChangePasswordVM model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(" ", errors) });
            }

            var accountId = GetCurrentAccountId();

            var account = await _context.Accounts.FindAsync(accountId);
            if (account == null)
                return Json(new { success = false, message = "Tài khoản không tồn tại." });

            // Xác minh mật khẩu cũ bằng BCrypt
            if (string.IsNullOrEmpty(account.PasswordHash) ||
                !BCrypt.Net.BCrypt.Verify(model.OldPassword, account.PasswordHash))
            {
                return Json(new { success = false, message = "Mật khẩu hiện tại không đúng." });
            }

            // Mã hóa và lưu mật khẩu mới
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            account.RequiresPasswordChange = false; // Đánh dấu đã đổi pass (Kiosk Security)

            _context.Update(account);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đổi mật khẩu thành công!" });
        }

        // ====================================================================
        // HELPER: Lưu avatar (sao chép pattern từ AdminStaffService)
        // ====================================================================
        private async Task<string> SaveAvatarAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return "/Images/Upload/avtdf.jpg";

            var uploadsDir = Path.Combine(_env.WebRootPath, "Images", "avatars");
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/Images/avatars/{fileName}";
        }

        private async Task RefreshAvatarClaimAsync(string avatarUrl)
        {
            var authentication = await HttpContext.AuthenticateAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
            if (!authentication.Succeeded
                || authentication.Principal?.Identity is not ClaimsIdentity identity)
            {
                return;
            }

            foreach (var claim in identity.FindAll("AvatarUrl").ToList())
            {
                identity.RemoveClaim(claim);
            }
            identity.AddClaim(new Claim("AvatarUrl", avatarUrl));

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                authentication.Principal,
                authentication.Properties);
        }
    }
}
