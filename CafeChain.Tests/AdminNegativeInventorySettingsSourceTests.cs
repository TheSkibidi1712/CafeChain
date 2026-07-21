using System.Text.RegularExpressions;

namespace CafeChain.Tests;

public sealed class AdminNegativeInventorySettingsSourceTests
{
    [Fact]
    public void Controller_uses_conventional_post_antiforgery_and_dedicated_roles()
    {
        var source = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminSettingController.cs");

        Assert.DoesNotContain("[Route(", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\[HttpPost\s*\(\s*[^)]", RegexOptions.CultureInvariant), source);
        Assert.Contains("[Authorize(Roles = RoleConstants.BusinessOwner", source, StringComparison.Ordinal);
        Assert.Matches(new Regex(
            @"\[HttpPost\]\s*\[ValidateAntiForgeryToken\]\s*public\s+async\s+Task<IActionResult>\s+UpdateNegativeInventory",
            RegexOptions.CultureInvariant | RegexOptions.Singleline), source);
        Assert.DoesNotContain("Task<IActionResult> Update(Dictionary", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui_has_runtime_controls_without_hardcoded_admin_routes()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminSetting", "Index.cshtml")
                   + Read("CafeChain", "Areas", "Admin", "Views", "AdminSetting", "Partials", "_NegativeInventorySettings.cshtml");
        var client = Read("CafeChain", "wwwroot", "js", "Admin", "Settings", "adminsetting.js");

        Assert.Contains("Cấu hình âm kho", view, StringComparison.Ordinal);
        Assert.Contains("UpdateNegativeInventory", view, StringComparison.Ordinal);
        Assert.Contains("Cho phép gửi yêu cầu xuất âm kho", view, StringComparison.Ordinal);
        Assert.Contains("NegativeInventoryLimitModes.Custom", view, StringComparison.Ordinal);
        Assert.Contains("negative-display-unit", view, StringComparison.Ordinal);
        Assert.Contains("negativeInventoryStoreTabs", view, StringComparison.Ordinal);
        Assert.Contains("negativeInventoryPagination", view, StringComparison.Ordinal);
        Assert.Contains("negativeInventoryPageSize", view, StringComparison.Ordinal);
        Assert.Contains("<option value=\"10\" selected>", view, StringComparison.Ordinal);
        Assert.Contains("<option value=\"20\">", view, StringComparison.Ordinal);
        Assert.Contains("<option value=\"50\">", view, StringComparison.Ordinal);
        Assert.Contains("renderPagination", client, StringComparison.Ordinal);
        Assert.Contains("revealRow", client, StringComparison.Ordinal);
        Assert.DoesNotContain("negativeInventoryStoreFilter", view + client, StringComparison.Ordinal);
        Assert.DoesNotContain("Tất cả cửa hàng", view, StringComparison.Ordinal);
        Assert.DoesNotContain("SQL Server gate", view + client, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NegativeInventoryAcceptance.md", view + client, StringComparison.Ordinal);
        Assert.DoesNotContain("/Admin/AdminSetting/", view + client, StringComparison.Ordinal);
        Assert.DoesNotContain("/Admin/Setting/", view + client, StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_layout_uses_avatar_and_staff_name_as_profile_link()
    {
        var layout = Read("CafeChain", "Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml");

        Assert.Contains("User.FindFirstValue(ClaimTypes.Name)", layout, StringComparison.Ordinal);
        Assert.Contains("User.FindFirstValue(\"AvatarUrl\")", layout, StringComparison.Ordinal);
        Assert.Contains("topbar-user-name", layout, StringComparison.Ordinal);
        Assert.Contains("@currentStaffName", layout, StringComparison.Ordinal);
        Assert.Contains("id=\"adminTopbarAvatar\"", layout, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"AdminProfile\"", layout, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"MyProfile\"", layout, StringComparison.Ordinal);
        Assert.Contains("/Images/Upload/avtdf.jpg", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("> Hồ sơ của tôi", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void Profile_avatar_update_refreshes_cookie_claim_and_uses_area_routes()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminProfileController.cs");
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminProfile", "MyProfile.cshtml");

        Assert.Contains("[Authorize]", controller, StringComparison.Ordinal);
        Assert.Contains("RefreshAvatarClaimAsync", controller, StringComparison.Ordinal);
        Assert.Contains("AuthenticateAsync", controller, StringComparison.Ordinal);
        Assert.Contains("new Claim(\"AvatarUrl\", avatarUrl)", controller, StringComparison.Ordinal);
        Assert.Contains("authentication.Properties", controller, StringComparison.Ordinal);
        Assert.Contains("avatarUrl = updatedAvatarUrl", controller, StringComparison.Ordinal);
        Assert.Contains("Url.Action(\"UpdateMyProfile\", \"AdminProfile\"", view, StringComparison.Ordinal);
        Assert.Contains("Url.Action(\"ChangePassword\", \"AdminProfile\"", view, StringComparison.Ordinal);
        Assert.Contains("#profileAvatar, #adminTopbarAvatar", view, StringComparison.Ordinal);
        Assert.DoesNotContain("/Profile/UpdateMyProfile", view, StringComparison.Ordinal);
        Assert.DoesNotContain("/Profile/ChangePassword", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui_removes_trailing_zeroes_without_reducing_decimal_precision()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminSetting", "Partials", "_NegativeInventorySettings.cshtml");
        var service = Read("CafeChain", "Application", "Services", "Admin", "Settings", "AdminSettingService.cs");

        Assert.Contains("#,##0.###", view, StringComparison.Ordinal);
        Assert.Contains("0.###", view, StringComparison.Ordinal);
        Assert.Contains("step=\"0.001\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ToString(\"N3\")", view, StringComparison.Ordinal);
        Assert.Contains("FormatQuantity(effectiveLimit)", service, StringComparison.Ordinal);
        Assert.DoesNotContain("effectiveLimit:N3", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Store_inventory_seed_remains_unchanged()
    {
        var storeConfiguration = Read("CafeChain", "Data", "Configurations", "Stores", "StoreConfiguration.cs");
        var seedFour = Regex.Match(
            storeConfiguration,
            @"StoreInventoryId\s*=\s*4,[\s\S]*?LastUpdated\s*=\s*new DateTime\(2025,\s*1,\s*1\)",
            RegexOptions.CultureInvariant).Value;
        Assert.NotEmpty(seedFour);
        Assert.Contains("StoreId = 3", seedFour, StringComparison.Ordinal);
        Assert.Contains("IngredientId = 2", seedFour, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxNegativeQty", seedFour, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([FindRepoRoot(), .. path]));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests")))
                return directory.FullName;
            directory = directory.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
