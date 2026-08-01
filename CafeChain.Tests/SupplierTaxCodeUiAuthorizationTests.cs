using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Areas.Admin.Controllers;
using System.Reflection;
using Xunit;

namespace CafeChain.Tests;

public sealed class SupplierTaxCodeUiAuthorizationTests
{
    [Fact]
    public void SupplierTaxCode_MutationEndpointsUseBusinessPermissions()
    {
        AssertMutationPermission(nameof(AdminSupplierController.Create), PermissionConstants.SupplierCreate);
        AssertMutationPermission(nameof(AdminSupplierController.Update), PermissionConstants.SupplierUpdate);
        AssertMutationPermission(nameof(AdminSupplierController.ToggleStatus), PermissionConstants.SupplierToggleStatus);
    }

    [Fact]
    public void SupplierTaxCode_MutationEndpointsDoNotEmbedRoleAllowLists()
    {
        foreach (var action in new[]
                 {
                     nameof(AdminSupplierController.Create),
                     nameof(AdminSupplierController.Update),
                     nameof(AdminSupplierController.ToggleStatus)
                 })
        {
            Assert.Null(PermissionAttribute(action).Roles);
        }
    }

    [Fact]
    public void SupplierSoftOverride_UsesProtectedCreateEndpoint_AndHasNoForceFlag()
    {
        AssertMutationPermission(nameof(AdminSupplierController.Create), PermissionConstants.SupplierCreate);
        Assert.Null(typeof(AdminSupplierCreateDTO).GetProperty("ForceCreate", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(typeof(AdminSupplierCreateDTO).GetProperty(nameof(AdminSupplierCreateDTO.DuplicateWarningId)));
        Assert.NotNull(typeof(AdminSupplierCreateDTO).GetProperty(nameof(AdminSupplierCreateDTO.DuplicateOverrideReason)));
    }

    [Fact]
    public void SupplierUi_ContainsTaxCodeInlineValidationAndDuplicateActions()
    {
        var view = File.ReadAllText(RepoFile("CafeChain", "Areas", "Admin", "Views", "AdminSupplier", "Index.cshtml"));
        var script = File.ReadAllText(RepoFile("CafeChain", "wwwroot", "js", "Admin", "Supplier", "supplier.js"));

        Assert.Contains("Mã số thuế", view);
        Assert.Contains("createTaxCodeError", view);
        Assert.Contains("overviewTaxCodeError", view);
        Assert.Contains("Mở nhà cung cấp hiện có", view);
        Assert.Contains("Kích hoạt lại", view);
        Assert.Contains("Xác nhận vẫn tạo mới", view);
        Assert.Contains("isSoft ? softData.warningId : null", script);
        Assert.Contains("toggle('is-hidden', !isSoft)", script);
        Assert.Contains("duplicateWarningId", script);
        Assert.DoesNotContain("forceCreate", script, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertMutationPermission(string action, string permissionCode)
    {
        var authorize = PermissionAttribute(action);
        Assert.Equal(RequirePermissionAttribute.PolicyPrefix + permissionCode, authorize.Policy);
        Assert.Null(authorize.Roles);
    }

    private static RequirePermissionAttribute PermissionAttribute(string action) =>
        typeof(AdminSupplierController)
            .GetMethod(action)!
            .GetCustomAttribute<RequirePermissionAttribute>()!;

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
