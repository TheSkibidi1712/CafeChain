using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;
using Xunit;

namespace CafeChain.Tests;

public sealed class SupplierTaxCodeUiAuthorizationTests
{
    [Fact]
    public void SupplierTaxCode_MutationEndpointsKeepExistingAuthorizedRoles()
    {
        AssertMutationRoles(nameof(AdminSupplierController.Create));
        AssertMutationRoles(nameof(AdminSupplierController.Update));
        AssertMutationRoles(nameof(AdminSupplierController.ToggleStatus));
    }

    [Fact]
    public void SupplierTaxCode_UnauthorizedRolesAreNotGrantedMutation()
    {
        var roles = MutationRoles(nameof(AdminSupplierController.Create));
        Assert.DoesNotContain(RoleConstants.StoreManager, roles);
        Assert.DoesNotContain(RoleConstants.AreaManager, roles);
        Assert.DoesNotContain(RoleConstants.SalesStaff, roles);
        Assert.DoesNotContain(RoleConstants.ShiftSupervisor, roles);
        Assert.DoesNotContain(RoleConstants.SystemAdmin, roles);
    }

    [Fact]
    public void SupplierSoftOverride_UsesProtectedCreateEndpoint_AndHasNoForceFlag()
    {
        AssertMutationRoles(nameof(AdminSupplierController.Create));
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

    private static void AssertMutationRoles(string action)
    {
        var roles = MutationRoles(action);
        Assert.Contains(RoleConstants.BusinessOwner, roles);
        Assert.Contains(RoleConstants.AccountantWarehouse, roles);
        Assert.Equal(2, roles.Count);
    }

    private static List<string> MutationRoles(string action) =>
        typeof(AdminSupplierController)
            .GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Roles!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

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
