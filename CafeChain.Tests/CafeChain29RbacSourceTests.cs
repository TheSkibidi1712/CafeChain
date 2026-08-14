using System.Reflection;
using System.Text.RegularExpressions;
using CafeChain.Application.Constants;

namespace CafeChain.Tests;

public sealed class CafeChain29RbacSourceTests
{
    private static readonly string[] ManagedRoleKeys =
        ["CDN", "QLV", "QLCN", "NVBH", "KTK", "QTHT", "KH", "CT"];

    // The matrix keeps its legacy KH bit column for script compatibility, but
    // customer identity is no longer represented by a database role.
    private static readonly string[] SeededRoleKeys =
        ["CDN", "QLV", "QLCN", "NVBH", "KTK", "QTHT", "CT"];

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
        Assert.Contains("SET QTHT=0", seed, StringComparison.Ordinal);
        Assert.Contains("PermissionCode NOT LIKE N'System.%'", seed, StringComparison.Ordinal);
        Assert.DoesNotContain("UnitConversion.Delete',N'UNIT_CONVERSION'", seed, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemAdmin_IsLeastPrivilegeWithoutPermissionOrScopeBypass()
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

        Assert.DoesNotContain(".Append(RoleConstants.SystemAdmin)", attribute, StringComparison.Ordinal);
        Assert.Contains("PermissionRequirement(PermissionConstants.AppAdminDashboard)", policies, StringComparison.Ordinal);
        Assert.DoesNotContain("IsActiveSystemAdminAsync", scope, StringComparison.Ordinal);
        Assert.Contains("AdminStoreScopeMode.ReorderSuggestion", resolver, StringComparison.Ordinal);
        Assert.Contains("STORE_SCOPE_DENIED", resolver, StringComparison.Ordinal);
        Assert.Contains("Denied by account override.", permissionService, StringComparison.Ordinal);
        Assert.Contains("RBAC_CAFECHAIN29_V2", permissionService, StringComparison.Ordinal);
        Assert.Contains("Denied by account override.", permissionService, StringComparison.Ordinal);
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
                ("Drink.Delete" or "Category.Delete" or "Size.Delete" or "Topping.Delete"
                    or "OperationalIce.Manage" or "OperationalIce.Approve" or "OperationalIce.Policy"))
            .ToList();

        Assert.True(codes.Count >= 186);
        Assert.All(codes, code => Assert.Contains(code, constants));
    }

    [Fact]
    public void SeedAll_FinalRoleCountContract_MatchesPermissionMatrices()
    {
        var seed = Read("CafeChain", "Scripts", "SeedAll.sql");
        var grants = ParseFinalManagedGrants(seed);
        var expectedBlock = Regex.Match(
            seed,
            @"INSERT #ExpectedRoleCounts VALUES(?<rows>[\s\S]*?);",
            RegexOptions.CultureInvariant);

        Assert.True(expectedBlock.Success, "Không tìm thấy #ExpectedRoleCounts trong SeedAll.sql.");

        var expectedCounts = Regex.Matches(
                expectedBlock.Groups["rows"].Value,
                @"\(N'(?<role>[^']+)',(?<count>\d+)\)",
                RegexOptions.CultureInvariant)
            .Cast<Match>()
            .ToDictionary(
                match => match.Groups["role"].Value,
                match => int.Parse(match.Groups["count"].Value),
                StringComparer.Ordinal);

        Assert.Equal(SeededRoleKeys.Length - 1, expectedCounts.Count);
        Assert.Contains(
            "INSERT #ExpectedRoleCounts(RoleKey,ExpectedCount)\n SELECT N'QTHT',COUNT(*)",
            seed.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);

        foreach (var (roleKey, expectedCount) in expectedCounts)
        {
            var roleIndex = Array.IndexOf(ManagedRoleKeys, roleKey);
            Assert.True(roleIndex >= 0, $"Role contract không xác định: {roleKey}.");
            var actualCount = grants.Values.Sum(bits => bits[roleIndex]);
            Assert.True(
                actualCount == expectedCount,
                $"Role {roleKey}: expected {expectedCount}, matrix tạo {actualCount} quyền.");
        }
    }

    [Fact]
    public void SeedAll_ProductionYieldPermissions_KeepIntendedRoleMapping()
    {
        var seed = Read("CafeChain", "Scripts", "SeedAll.sql");
        var grants = ParseFinalManagedGrants(seed);

        Assert.Equal(new[] { 0, 0, 1, 0, 0, 0, 0, 0 }, grants["ProductionOrder.Plan"]);
        Assert.Equal(new[] { 0, 0, 1, 0, 0, 0, 0, 0 }, grants["ProductionOrder.Release"]);
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 0, 1 }, grants["ProductionOrder.Start"]);
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 0, 1 }, grants["ProductionOrder.RecordActual"]);
        Assert.Equal(new[] { 0, 0, 1, 0, 0, 0, 0, 0 }, grants["ProductionOrder.AcceptOutput"]);
        Assert.Equal(new[] { 1, 0, 0, 0, 0, 0, 0, 0 }, grants["ProductionOrder.ApproveVariance"]);
        Assert.Equal(new[] { 0, 0, 1, 0, 0, 0, 0, 0 }, grants["ProductionOrder.Cancel"]);
        Assert.Equal(new[] { 0, 0, 0, 0, 1, 0, 0, 0 }, grants["Restock.SelectProductionSource"]);
    }

    [Fact]
    public void SeedAll_BomAndProductionPermissions_MatchApprovedSeparationOfDuties()
    {
        var seed = Read("CafeChain", "Scripts", "SeedAll.sql");
        var grants = ParseFinalManagedGrants(seed);

        var bomView = new[] { 1, 1, 1, 0, 1, 0, 0, 0 };
        var bomManage = new[] { 1, 0, 0, 0, 1, 0, 0, 0 };
        Assert.Equal(bomView, grants["PreparedItem.View"]);
        Assert.Equal(bomManage, grants["PreparedItem.Create"]);
        Assert.Equal(bomManage, grants["PreparedItem.Update"]);
        Assert.Equal(bomManage, grants["PreparedItem.ToggleStatus"]);
        Assert.Equal(bomView, grants["Recipe.View"]);
        Assert.Equal(bomManage, grants["Recipe.Create"]);
        Assert.Equal(bomManage, grants["Recipe.Update"]);
        Assert.Equal(bomManage, grants["Recipe.Delete"]);

        Assert.Equal(new[] { 1, 1, 1, 0, 1, 0, 0, 1 }, grants["ProductionOrder.View"]);
        Assert.Equal(new[] { 0, 0, 1, 0, 0, 0, 0, 0 }, grants["ProductionOrder.Plan"]);
        Assert.Equal(new[] { 0, 0, 1, 0, 0, 0, 0, 0 }, grants["ProductionOrder.Release"]);
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 0, 1 }, grants["ProductionOrder.Start"]);
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 0, 1 }, grants["ProductionOrder.RecordActual"]);
        Assert.Equal(new[] { 0, 0, 1, 0, 0, 0, 0, 0 }, grants["ProductionOrder.AcceptOutput"]);
        Assert.Equal(new[] { 1, 0, 0, 0, 0, 0, 0, 0 }, grants["ProductionOrder.ApproveVariance"]);
        Assert.Equal(new[] { 0, 0, 1, 0, 0, 0, 0, 0 }, grants["ProductionOrder.Cancel"]);
    }

    [Fact]
    public void ProductionPlanning_UsesProductionOrderPlanAtControllerAndServiceBoundaries()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers",
            "AdminRestockRequestsController.cs");
        var service = Read("CafeChain", "Application", "Services", "Inventories",
            "RestockRequestService.cs");

        Assert.Contains(
            "RequiredPermissionCode = PermissionConstants.ProductionOrderPlan",
            controller,
            StringComparison.Ordinal);
        Assert.Contains(
            "RequiredPermissionCode = PermissionConstants.ProductionOrderPlan",
            service,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RequiredPermissionCode = PermissionConstants.RestockSelectProductionSource",
            controller + service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BomNavigationAndRecipeDeleteGuards_RemainPermissionDriven()
    {
        var layout = Read("CafeChain", "Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml");
        var recipeController = Read("CafeChain", "Areas", "Admin", "Controllers",
            "AdminRecipeController.cs");
        var preparedItemController = Read("CafeChain", "Areas", "Admin", "Controllers",
            "AdminPreparedItemController.cs");
        var recipeService = Read("CafeChain", "Application", "Services", "Admin", "Recipes",
            "AdminRecipeService.cs");

        Assert.Contains("PermissionConstants.PreparedItemView", layout, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.RecipeView", layout, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.ProductionOrderView", layout, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.RecipeCreate", recipeController, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.RecipeUpdate", recipeController, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.RecipeDelete", recipeController, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.PreparedItemCreate", preparedItemController, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.PreparedItemUpdate", preparedItemController, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.PreparedItemToggleStatus", preparedItemController, StringComparison.Ordinal);
        Assert.Contains("rd.ChildRecipeId == recipeId", recipeService, StringComparison.Ordinal);
        Assert.Contains("recipe.Status = \"Archived\"", recipeService, StringComparison.Ordinal);
        Assert.Contains("recipe.Active = false", recipeService, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionV2Services_RequireActionPermissionWithinRunStoreScope()
    {
        var query = Read("CafeChain", "Application", "Services", "Admin", "Production",
            "ProductionRunQueryService.cs");
        var operations = Read("CafeChain", "Application", "Services", "Admin", "Production",
            "ProductionRunOperationsService.cs");
        var acceptance = Read("CafeChain", "Application", "Services", "Admin", "Production",
            "ProductionRunAcceptanceService.cs");

        Assert.Contains("PermissionConstants.ProductionOrderView, query.StoreId", query, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.ProductionOrderView, run.StoreId", query, StringComparison.Ordinal);
        Assert.Contains("HasPermissionAsync(accountId, permissionCode, run.StoreId)", operations, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.ProductionOrderAcceptOutput,\n                run.StoreId", acceptance.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void SensitiveControllers_ArePermissionFirst_AndUnitConversionDoesNotDelete()
    {
        var authorization = Read("CafeChain", "Extensions", "Services", "AuthorizationServiceExtensions.cs");
        var unit = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminUnitConversionController.cs");
        var inventory = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminInventoryDocumentController.cs");
        var production = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminProductionOrderController.cs");
        var notifications = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminNotificationsController.cs");

        Assert.Contains("AdminPanelAccessRequirement", authorization, StringComparison.Ordinal);
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

    private static Dictionary<string, int[]> ParseFinalManagedGrants(string seed)
    {
        var matrixStart = seed.IndexOf("CREATE TABLE #PermissionMatrix", StringComparison.Ordinal);
        Assert.True(matrixStart >= 0, "Không tìm thấy #PermissionMatrix trong SeedAll.sql.");

        var matrixEnd = seed.IndexOf(
            "CREATE TABLE #ExpectedRolePermissions",
            matrixStart,
            StringComparison.Ordinal);

        Assert.True(matrixEnd > matrixStart, "Không xác định được điểm kết thúc các ma trận RBAC.");

        var matrixSection = seed[matrixStart..matrixEnd];
        var rowPattern = @"\(N'(?<code>[^']+)',(?<CDN>[01]),(?<QLV>[01]),(?<QLCN>[01])," +
                         @"(?<NVBH>[01]),(?<KTK>[01]),(?<QTHT>[01]),(?<KH>[01]),(?<CT>[01])\)";
        var grants = Regex.Matches(matrixSection, rowPattern, RegexOptions.CultureInvariant)
            .Cast<Match>()
            .ToDictionary(
                match => match.Groups["code"].Value,
                match => ManagedRoleKeys
                    .Select(roleKey => int.Parse(match.Groups[roleKey].Value))
                    .ToArray(),
                StringComparer.Ordinal);

        Assert.NotEmpty(grants);
        Assert.Contains(
            "UPDATE #PermissionMatrix SET CDN=1,QLV=1,QLCN=1,KTK=1",
            matrixSection,
            StringComparison.Ordinal);
        Assert.Contains(
            "UPDATE #PermissionMatrix SET QTHT=0",
            matrixSection,
            StringComparison.Ordinal);

        var purchaseAdvice = grants["PurchaseAdvice.SelectSupplier"];
        foreach (var roleKey in new[] { "CDN", "QLV", "QLCN", "KTK" })
            purchaseAdvice[Array.IndexOf(ManagedRoleKeys, roleKey)] = 1;

        var systemAdminIndex = Array.IndexOf(ManagedRoleKeys, "QTHT");
        foreach (var (permissionCode, bits) in grants)
        {
            if (!permissionCode.StartsWith("System.", StringComparison.Ordinal))
                bits[systemAdminIndex] = 0;
        }

        return grants;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
               && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
