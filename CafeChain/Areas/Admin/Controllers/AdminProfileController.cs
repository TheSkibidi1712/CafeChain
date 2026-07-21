using System.Security.Claims;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Interfaces.Admin.Profiles;
using CafeChain.ViewModels.Profile;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class AdminProfileController : Controller
{
    private readonly IAdminProfileService _profileService;
    private readonly IAccountService _accountService;

    public AdminProfileController(
        IAdminProfileService profileService,
        IAccountService accountService)
    {
        _profileService = profileService;
        _accountService = accountService;
    }

    [HttpGet]
    public async Task<IActionResult> MyProfile()
    {
        var profile = await _profileService.GetMyProfileAsync(GetCurrentAccountId());
        if (profile == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy hồ sơ nhân viên.";
            return RedirectToAction("Index", "Home");
        }

        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMyProfile(
        [Bind("PhoneNumber,AvatarFile")] UpdateProfileVM model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage);
            return Json(new { success = false, message = string.Join(" ", errors) });
        }

        var result = await _profileService.UpdateMyProfileAsync(GetCurrentAccountId(), model);
        if (!result.IsSuccess || result.Data == null)
            return Json(new { success = false, message = result.Message });

        if (result.Data.AvatarChanged)
            await RefreshAvatarClaimAsync(result.Data.AvatarUrl);

        return Json(new
        {
            success = true,
            message = result.Message,
            avatarUrl = result.Data.AvatarUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([FromForm] ChangePasswordVM model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage);
            return Json(new { success = false, message = string.Join(" ", errors) });
        }

        var result = await _accountService.ChangePasswordAsync(
            GetCurrentAccountId(),
            model.OldPassword,
            model.NewPassword);

        return Json(new { success = result.IsSuccess, message = result.Message });
    }

    private int GetCurrentAccountId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null || !int.TryParse(claim.Value, out var accountId))
            throw new UnauthorizedAccessException("Không thể xác thực người dùng.");
        return accountId;
    }

    private async Task RefreshAvatarClaimAsync(string avatarUrl)
    {
        var authentication = await HttpContext.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authentication.Succeeded
            || authentication.Principal?.Identity is not ClaimsIdentity identity)
            return;

        foreach (var claim in identity.FindAll("AvatarUrl").ToList())
            identity.RemoveClaim(claim);
        identity.AddClaim(new Claim("AvatarUrl", avatarUrl));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authentication.Principal,
            authentication.Properties);
    }
}
