namespace CafeChain.Tests;

public sealed class StaffAddressRefactorSourceTests
{
    [Fact]
    public void Address_endpoints_are_authenticated_master_data_without_create_permission_gate()
    {
        var source = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminStaffController.cs");
        var districts = Slice(source, "Task<IActionResult> GetDistricts", "Task<IActionResult> GetWards");
        var wards = source[source.IndexOf("Task<IActionResult> GetWards", StringComparison.Ordinal)..];

        Assert.DoesNotContain("RequirePermission", districts, StringComparison.Ordinal);
        Assert.DoesNotContain("RequirePermission", wards, StringComparison.Ordinal);
        Assert.Contains("GetDistrictsAsync(provinceId)", districts, StringComparison.Ordinal);
        Assert.Contains("GetWardsAsync(districtId)", wards, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_and_edit_use_ordered_ajax_address_loading_without_timeouts()
    {
        var create = Read("CafeChain", "Areas", "Admin", "Views", "AdminStaff", "_CreateStaffModal.cshtml");
        var edit = Read("CafeChain", "Areas", "Admin", "Views", "AdminStaff", "Edit.cshtml");

        Assert.Contains("FormData", create, StringComparison.Ordinal);
        Assert.Contains("X-Requested-With", create, StringComparison.Ordinal);
        Assert.Contains("districtRequestVersion", create, StringComparison.Ordinal);
        Assert.Contains("wardRequestVersion", create, StringComparison.Ordinal);
        Assert.Contains("data-validation-feedback=\"sweetalert\"", create, StringComparison.Ordinal);
        Assert.Contains("Thông tin chưa hợp lệ", create, StringComparison.Ordinal);
        Assert.Contains("bootstrap.Tab.getOrCreateInstance", create, StringComparison.Ordinal);
        Assert.Contains("restoreCreateSubmit", create, StringComparison.Ordinal);
        Assert.Contains("AdminMutationGuard?.unlockForm", create, StringComparison.Ordinal);
        Assert.Contains("const districts = await load", edit, StringComparison.Ordinal);
        Assert.Contains("await load(ward", edit, StringComparison.Ordinal);
        Assert.DoesNotContain("setTimeout", edit, StringComparison.Ordinal);
        Assert.DoesNotContain("GetScopeReferences", edit, StringComparison.Ordinal);
    }

    [Fact]
    public void Alert_layer_and_create_icon_are_above_and_visible()
    {
        var css = Read("CafeChain", "wwwroot", "css", "Admin", "Staff", "staff.css");
        var index = Read("CafeChain", "Areas", "Admin", "Views", "AdminStaff", "Index.cshtml");

        Assert.Contains(".swal2-container", css, StringComparison.Ordinal);
        Assert.Contains(".page-header .btn i", css, StringComparison.Ordinal);
        Assert.Contains("color: currentColor", css, StringComparison.Ordinal);
        Assert.DoesNotContain("cdn.jsdelivr.net/npm/sweetalert2", index, StringComparison.OrdinalIgnoreCase);
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return source[startIndex..endIndex];
    }

    private static string Read(params string[] parts)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", Path.Combine(parts));
        return File.ReadAllText(Path.GetFullPath(path));
    }
}
