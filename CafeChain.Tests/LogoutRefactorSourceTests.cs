namespace CafeChain.Tests;

public sealed class LogoutRefactorSourceTests
{
    [Fact]
    public void Logout_remains_post_only_and_does_not_depend_on_antiforgery_state()
    {
        var controller = Read("CafeChain", "Controllers", "AccountController.cs");
        var logoutStart = controller.IndexOf("// ========================= LOGOUT", StringComparison.Ordinal);
        var logoutEnd = controller.IndexOf("// ========================= CHECK LOCK STATUS", logoutStart, StringComparison.Ordinal);
        var logoutAction = controller[logoutStart..logoutEnd];

        Assert.Contains("[HttpPost]", logoutAction, StringComparison.Ordinal);
        Assert.Contains("[IgnoreAntiforgeryToken]", logoutAction, StringComparison.Ordinal);
        Assert.DoesNotContain("[HttpGet]", logoutAction, StringComparison.Ordinal);
        Assert.DoesNotContain("[ValidateAntiForgeryToken]", logoutAction, StringComparison.Ordinal);
        Assert.Contains("SignOutAsync", logoutAction, StringComparison.Ordinal);
        Assert.Contains("Session.Clear", logoutAction, StringComparison.Ordinal);
        Assert.Contains("Response.Cookies.Delete(\"Cart\")", logoutAction, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_logout_entry_point_uses_the_shared_tokenless_post_form()
    {
        var partial = Read("CafeChain", "Views", "Shared", "_LogoutForm.cshtml");
        var model = Read("CafeChain", "ViewModels", "Shared", "LogoutFormViewModel.cs");
        var hosts = new[]
        {
            Read("CafeChain", "Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml"),
            Read("CafeChain", "Views", "AppLauncher", "Index.cshtml"),
            Read("CafeChain", "Views", "Customer", "Profile.cshtml"),
            Read("CafeChain", "Views", "Customer", "MyVouchers.cshtml"),
            Read("CafeChain", "Views", "Customer", "ChangePassword.cshtml")
        };

        Assert.Contains("asp-action=\"Logout\"", partial, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", partial, StringComparison.Ordinal);
        Assert.Contains("asp-antiforgery=\"false\"", partial, StringComparison.Ordinal);
        Assert.DoesNotContain("AntiForgeryToken", partial, StringComparison.Ordinal);
        Assert.Contains("AdminSidebar", model, StringComparison.Ordinal);
        Assert.Contains("CustomerMenu", model, StringComparison.Ordinal);
        Assert.Contains("AppLauncher", model, StringComparison.Ordinal);

        foreach (var host in hosts)
        {
            Assert.Contains("~/Views/Shared/_LogoutForm.cshtml", host, StringComparison.Ordinal);
            Assert.DoesNotContain("asp-action=\"Logout\"", host, StringComparison.Ordinal);
        }
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([FindRoot(), .. parts]));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
