//namespace CafeChain.Tests;

//public sealed class AdminPermissionScopeUiSourceTests
//{
//    [Fact]
//    public void Scope_picker_localizes_types_and_filters_current_draft_assignments()
//    {
//        var source = Read("CafeChain", "wwwroot", "js", "Admin", "Permissions", "admin-permissions.js");

//        Assert.Contains("COUNTRY: \"Quốc gia\"", source, StringComparison.Ordinal);
//        Assert.Contains("PROVINCE: \"Tỉnh/Thành phố\"", source, StringComparison.Ordinal);
//        Assert.Contains("DISTRICT: \"Quận/Huyện\"", source, StringComparison.Ordinal);
//        Assert.Contains("WARD: \"Phường/Xã\"", source, StringComparison.Ordinal);
//        Assert.Contains("STORE: \"Cửa hàng\"", source, StringComparison.Ordinal);
//        Assert.Contains("getAvailableScopeReferences", source, StringComparison.Ordinal);
//        Assert.Contains("scope.scopeTypeId", source, StringComparison.Ordinal);
//        Assert.Contains("scope.scopeRefId", source, StringComparison.Ordinal);
//        Assert.Contains("Đã phân quyền tất cả đối tượng", source, StringComparison.Ordinal);
//        Assert.Contains("refreshCurrentScopeReferences();", source, StringComparison.Ordinal);
//    }

//    [Fact]
//    public void Permission_modal_primary_buttons_have_visible_text_in_every_state()
//    {
//        var source = Read("CafeChain", "wwwroot", "css", "Admin", "Permissions", "admin-permissions.css");

//        Assert.Contains(".perm-page,", source, StringComparison.Ordinal);
//        Assert.Contains(".perm-modal {", source, StringComparison.Ordinal);
//        Assert.Contains("var(--perm-accent, #f97316)", source, StringComparison.Ordinal);
//        Assert.Contains("var(--perm-accent-dark, #d93d20)", source, StringComparison.Ordinal);
//        Assert.Contains(".perm-primary-button.is-loading:disabled", source, StringComparison.Ordinal);
//        Assert.Contains(".perm-primary-button > i", source, StringComparison.Ordinal);
//        Assert.Contains("-webkit-text-fill-color: inherit", source, StringComparison.Ordinal);
//    }

//    private static string Read(params string[] path) =>
//        File.ReadAllText(Path.Combine([FindRepoRoot(), .. path]));

//    private static string FindRepoRoot()
//    {
//        var directory = new DirectoryInfo(AppContext.BaseDirectory);
//        while (directory != null)
//        {
//            if (Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
//                && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests")))
//                return directory.FullName;
//            directory = directory.Parent;
//        }

//        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
//    }
//}
