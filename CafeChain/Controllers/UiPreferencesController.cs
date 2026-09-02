using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers;

[Route("ui-preferences")]
public sealed class UiPreferencesController : Controller
{
    private static readonly HashSet<string> SupportedCultures =
        new(StringComparer.OrdinalIgnoreCase) { "vi-VN", "en-US" };

    [HttpGet("culture")]
    public IActionResult SetCulture(string culture, string? returnUrl = null)
    {
        var selectedCulture = SupportedCultures.Contains(culture) ? culture : "vi-VN";
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(selectedCulture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            });

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
