using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Inventories;
using Xunit;

namespace CafeChain.Tests;

public sealed class ReplenishmentAuthorizationLocalizationIssue473Tests
{
    [Fact]
    public void UnauthorizedActor_DoesNotSeeActionableReceiveProcessingCta()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminRestockRequestsController.cs");
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminRestockRequests", "Details.cshtml");

        Assert.Contains(
            "ViewBag.CanStartProcessing = await HasEffectivePermissionAsync(PermissionConstants.RestockApprove)",
            controller,
            StringComparison.Ordinal);
        Assert.Contains("var canStartProcessing = ViewBag.CanStartProcessing", view, StringComparison.Ordinal);
        Assert.Contains("if (canStartProcessing && Model.Status == \"SUBMITTED\")", view, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "if (canWarehouse && Model.Status == \"SUBMITTED\")\n                    {\n                        <form method=\"post\" asp-action=\"StartProcessing\"",
            view.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DirectBackendAuthorization_RemainsAuthoritative()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminRestockRequestsController.cs");

        Assert.Contains("[RequirePermission(PermissionConstants.RestockApprove)]", controller, StringComparison.Ordinal);
        Assert.Contains("if (!await HasEffectivePermissionAsync(PermissionConstants.RestockApprove))", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void PosCogsIncomplete_DoesNotExposeRawReasonCode()
    {
        var controller = Read("CafeChain", "Controllers", "Api", "v1", "POSOrderController.cs");
        var mapper = Read("CafeChain", "Application", "Constants", "PosInventoryWarningDisplayText.cs");

        Assert.Contains("PosInventoryWarningDisplayText.ToBusinessMessage", controller, StringComparison.Ordinal);
        Assert.Contains("SalesCogsCodes.Incomplete", mapper, StringComparison.Ordinal);
        Assert.DoesNotContain("return inventoryResult.Errors;", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void PosCogsIncomplete_ShowsVietnameseBusinessMessage()
    {
        var mapper = Read("CafeChain", "Application", "Constants", "PosInventoryWarningDisplayText.cs");

        Assert.Contains("Chưa đủ dữ liệu để xác định đầy đủ giá vốn của giao dịch.", mapper, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"{SalesCogsCodes.Incomplete}", mapper, StringComparison.Ordinal);
        Assert.Equal(
            "Chưa đủ dữ liệu để xác định đầy đủ giá vốn của giao dịch.",
            PosInventoryWarningDisplayText.ToBusinessMessage(
                $"{SalesCogsCodes.Incomplete}: Đơn hàng đã thanh toán nhưng giá vốn chưa đầy đủ."));
    }

    [Fact]
    public void ShiftSupervisor_DefaultRole_DoesNotHaveProductionCreate()
    {
        var seed = Read("CafeChain", "Scripts", "SeedAll.sql");

        Assert.Contains("(N'ProductionOrder.Create',1,0,1,0,1,0,0,0)", seed, StringComparison.Ordinal);
        Assert.DoesNotContain("(N'ProductionOrder.Create',1,0,1,0,1,0,0,1)", seed, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyProductionCreate_RemainsPermissionGatedAndOverrideCompatible()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminProductionOrderController.cs");
        var index = Read("CafeChain", "Areas", "Admin", "Views", "AdminProductionOrder", "Index.cshtml");
        var permissionService = Read("CafeChain", "Application", "Services", "Admin", "Permissions", "AdminPermissionService.cs");

        Assert.Contains("PermissionConstants.ProductionOrderCreate", controller, StringComparison.Ordinal);
        Assert.Contains("if (canCreateLegacy)", index, StringComparison.Ordinal);
        Assert.Contains("AccountPermissionOverride", permissionService, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { FindRepoRoot() }.Concat(path).ToArray()));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
               && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
