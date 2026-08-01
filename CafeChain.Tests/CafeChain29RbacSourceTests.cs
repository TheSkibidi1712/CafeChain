using System.Reflection;
using CafeChain.Application.Constants;

namespace CafeChain.Tests;

public sealed class CafeChain29RbacSourceTests
{
    [Fact]
    public void SeedAll_ReconcilesByCodeAndRoleName_WithoutMutatingOverrides()
    {
        var seed = Read("CafeChain", "Scripts", "SeedAll.sql");

        Assert.Contains("RBAC_CAFECHAIN29_V2", seed, StringComparison.Ordinal);
        Assert.Contains("SET XACT_ABORT ON", seed, StringComparison.Ordinal);
        Assert.Contains("#ExpectedRolePermissions", seed, StringComparison.Ordinal);
        Assert.Contains("JOIN dbo.Roles r ON r.Name=rm.RoleName", seed, StringComparison.Ordinal);
        Assert.Contains("#OverrideBefore", seed, StringComparison.Ordinal);
        Assert.Contains("EXCEPT", seed, StringComparison.Ordinal);
        Assert.Contains("N'ReorderSuggestion.View',1,1,1,0,1,0,0,0", seed, StringComparison.Ordinal);
        Assert.Contains("N'Restock.Create',1,0,1,0,1,0,0,0", seed, StringComparison.Ordinal);
        Assert.Contains("N'REORDER_SUGGESTION'", seed, StringComparison.Ordinal);
        Assert.Contains("N'ReorderSuggestion.View',N'REORDER_SUGGESTION'", seed, StringComparison.Ordinal);
        Assert.Contains("N'App.AdminDashboard',1,1,1,0,1,0,0,0", seed, StringComparison.Ordinal);
        Assert.Contains("SET Active=0", seed, StringComparison.Ordinal);
        Assert.Contains("UPDATE #PermissionMatrix", seed, StringComparison.Ordinal);
        Assert.Contains("SET QTHT=CASE", seed, StringComparison.Ordinal);
        Assert.Contains("SystemAdmin chưa có toàn bộ permission active", seed, StringComparison.Ordinal);
        Assert.DoesNotContain("UnitConversion.Delete',N'UNIT_CONVERSION'", seed, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemAdminFullAccess_IsCentralizedWithoutBypassingPermissionOrDeny()
    {
        var attribute = Read("CafeChain", "Application", "Authorization",
            "RequirePermissionAttribute.cs");
        var policies = Read("CafeChain", "Extensions", "Services",
            "AuthorizationServiceExtensions.cs");
        var scope = Read("CafeChain", "Application", "Services", "Security",
            "ScopeAuthorizationService.cs");
        var resolver = Read("CafeChain", "Application", "Services", "Admin", "StoreScope",
            "AdminStoreScopeResolver.cs");
        var permissionService = Read("CafeChain", "Application", "Services", "Admin",
            "Permissions", "AdminPermissionService.cs");

        Assert.Contains(".Append(RoleConstants.SystemAdmin)", attribute, StringComparison.Ordinal);
        Assert.Contains("RoleConstants.SystemAdmin", policies, StringComparison.Ordinal);
        Assert.DoesNotContain("IsActiveSystemAdminAsync", scope, StringComparison.Ordinal);
        Assert.Contains("AdminStoreScopeMode.ReorderSuggestion", resolver, StringComparison.Ordinal);
        Assert.Contains("RoleConstants.SystemAdmin", resolver, StringComparison.Ordinal);
        Assert.Contains("STORE_SCOPE_DENIED", resolver, StringComparison.Ordinal);
        Assert.Contains("Denied by account override.", permissionService, StringComparison.Ordinal);
        Assert.Contains("RBAC_CAFECHAIN29_V2", permissionService, StringComparison.Ordinal);
        Assert.Contains("RoleConstants.SystemAdmin => -10", permissionService, StringComparison.Ordinal);
    }

    [Fact]
    public void PermissionConstants_CoverEveryActiveManagedPermission()
    {
        var constants = typeof(PermissionConstants)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);
        var seed = Read("CafeChain", "Scripts", "SeedAll.sql");
        var matrixStart = seed.IndexOf("INSERT #PermissionMatrix VALUES", StringComparison.Ordinal);
        var matrixEnd = seed.IndexOf("IF EXISTS", matrixStart, StringComparison.Ordinal);
        var matrix = seed[matrixStart..matrixEnd];
        var codes = System.Text.RegularExpressions.Regex.Matches(
                matrix,
                @"\(N'([^']+)',[01],[01],[01],[01],[01],[01],[01],[01]\)")
            .Select(match => match.Groups[1].Value)
            .Where(code => code is not
                ("Drink.Delete" or "Category.Delete" or "Size.Delete" or "Topping.Delete"))
            .ToList();

        Assert.Equal(161, codes.Count);
        Assert.All(codes, code => Assert.Contains(code, constants));
    }

    [Fact]
    public void SensitiveControllers_ArePermissionFirst_AndUnitConversionDoesNotDelete()
    {
        var authorization = Read("CafeChain", "Extensions", "Services", "AuthorizationServiceExtensions.cs");
        var unit = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminUnitConversionController.cs");
        var inventory = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminInventoryDocumentController.cs");
        var production = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminProductionOrderController.cs");
        var notifications = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminNotificationsController.cs");

        Assert.Contains("RoleConstants.AccountantWarehouse", authorization, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AuthorizationPolicyConstants.AdminDashboardApp,\n                PermissionConstants.AppAdminDashboard",
            authorization,
            StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.UnitConversionView", unit, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.UnitConversionToggleStatus", unit, StringComparison.Ordinal);
        Assert.Contains("SetActiveAsync", unit, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteAsync", unit, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.InventoryDocumentConfirm", inventory, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.InventoryDocumentExport", inventory, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.ProductionOrderConfirm", production, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.NotificationView", notifications, StringComparison.Ordinal);
    }

    [Fact]
    public void RbacMutations_RequireStableRequestKey_DeduplicateAndAuditAtomically()
    {
        var dto = Read("CafeChain", "Application", "DTOs", "Admin", "Permissions",
            "AdminPermissionDtos.cs");
        var service = Read("CafeChain", "Application", "Services", "Admin", "Permissions",
            "AdminPermissionService.cs");
        var repository = Read("CafeChain", "Infrastructure", "Repositories", "Admin",
            "Permissions", "AdminPermissionRepository.cs");
        var frontend = Read("CafeChain", "wwwroot", "js", "Admin", "Permissions",
            "admin-permissions.js");

        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(
            dto, @"public string RequestKey").Count);
        Assert.Contains("ExecuteIdempotentMutationAsync", service, StringComparison.Ordinal);
        Assert.Contains("_requestDeduplication.BeginAsync", service, StringComparison.Ordinal);
        Assert.Contains("_context.AuditLogs.Add", service, StringComparison.Ordinal);
        Assert.Contains("_context.Database.BeginTransactionAsync", service, StringComparison.Ordinal);
        Assert.Contains("_context.Database.CurrentTransaction", repository, StringComparison.Ordinal);
        Assert.Contains("mutationRequestKeys: new Map()", frontend, StringComparison.Ordinal);
        Assert.Contains("window.crypto?.randomUUID?.()", frontend, StringComparison.Ordinal);
        Assert.Contains("request.complete()", frontend, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcurementAndMasterDataControllers_UsePermissionAttributes()
    {
        var controllerNames = new[]
        {
            "AdminSupplierController.cs",
            "AdminSupplierQualityController.cs",
            "AdminRestockRequestsController.cs",
            "AdminStockAlertsController.cs",
            "AdminInventoryThresholdsController.cs",
            "AdminPurchaseAdvicesController.cs",
            "AdminPurchaseOrdersController.cs",
            "AdminPurchaseOrderBatchesController.cs",
            "AdminBranchReceiptsController.cs",
            "AdminInventoryTransferController.cs",
            "AdminStoreMenuController.cs",
            "AdminDrinkProfitabilityController.cs",
            "AdminPreparedItemController.cs",
            "AdminRecipeController.cs",
            "AdminCutoverController.cs",
            "AdminInventoryWriterDiagnosticsController.cs",
            "AdminLegacyBtpConsolidationController.cs"
        };

        Assert.All(controllerNames, file =>
        {
            var source = Read("CafeChain", "Areas", "Admin", "Controllers", file);
            Assert.Contains("[RequirePermission(", source, StringComparison.Ordinal);
        });
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { FindRepoRoot() }.Concat(segments).ToArray()));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
               && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
