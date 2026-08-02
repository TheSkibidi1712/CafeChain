using System.Text.RegularExpressions;

namespace CafeChain.Tests;

public sealed class SupplyChainOperationalIcePermissionHardeningTests
{
    private const int Owner = 0;
    private const int AreaManager = 1;
    private const int StoreManager = 2;
    private const int SalesEmployee = 3;
    private const int WarehouseAccountant = 4;
    private const int ShiftLead = 7;

    [Fact] public void SalesEmployee_CanReportShortage() => AssertGrant("StockAlert.Create", SalesEmployee, true);
    [Fact] public void SalesEmployee_CannotVerifyAlert() => AssertGrant("StockAlert.Resolve", SalesEmployee, false);
    [Fact] public void ShiftLead_CanReceiveWithinStoreScope() => AssertGrant("Receipt.Confirm", ShiftLead, true);
    [Fact] public void ShiftLead_CannotSelectSupplySource() => AssertGrant("PurchaseAdvice.SelectSupplier", ShiftLead, false);
    [Fact] public void StoreManager_CanVerifyAlertWithinStoreScope() => AssertGrant("StockAlert.Resolve", StoreManager, true);
    [Fact] public void StoreManager_CanCreateManualRestock() => AssertGrant("Restock.Create", StoreManager, true);
    [Fact] public void StoreManager_CannotCreatePO() => AssertGrant("PurchaseOrder.Create", StoreManager, false);

    [Fact]
    public void RegionManager_CanViewOnlyRegionScope()
    {
        AssertGrant("Restock.View", AreaManager, true);
        AssertGrant("Restock.Create", AreaManager, false);
    }

    [Fact] public void WarehouseAccountant_CanCreateCentralPlannerRestock() => AssertGrant("Restock.Create", WarehouseAccountant, true);
    [Fact] public void WarehouseAccountant_CanSelectSupplySource() => AssertGrant("PurchaseAdvice.SelectSupplier", WarehouseAccountant, true);
    [Fact] public void WarehouseAccountant_CanCreatePO() => AssertGrant("PurchaseOrder.Create", WarehouseAccountant, true);

    [Fact]
    public void WarehouseAccountant_CannotApproveOwnPO()
    {
        AssertGrant("PurchaseOrder.Approve", WarehouseAccountant, false);
        var orderService = Read("CafeChain", "Application", "Services", "Inventories", "PurchaseOrderService.cs");
        var batchService = Read("CafeChain", "Application", "Services", "Inventories", "PurchaseOrderBatchService.cs");
        Assert.Contains("order.CreatedByStaffId == actorStaffId", orderService, StringComparison.Ordinal);
        Assert.Contains("batch.CreatedByStaffId == actor.StaffId", batchService, StringComparison.Ordinal);
        Assert.Contains("không thể tự duyệt", orderService, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("không thể tự duyệt", batchService, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Owner_CanApprovePO() => AssertGrant("PurchaseOrder.Approve", Owner, true);

    [Fact]
    public void OutOfScopeStore_IsRejectedByBackend()
    {
        var orderService = Read("CafeChain", "Application", "Services", "Inventories", "PurchaseOrderService.cs");
        var iceService = Read("CafeChain", "Application", "Services", "Inventories", "OperationalIceService.cs");
        Assert.Contains("CanAccessStoreAsync(actorStaffId, order.StoreId)", orderService, StringComparison.Ordinal);
        Assert.Contains("_scopeAuthorization.CanAccessStoreAsync(actor.StaffId, storeId)", iceService, StringComparison.Ordinal);
        Assert.Contains("Bạn không có quyền truy cập cửa hàng đã chọn.", iceService, StringComparison.Ordinal);
    }

    [Fact]
    public void UnauthorizedAction_IsHiddenInUI()
    {
        var orderView = Read("CafeChain", "Areas", "Admin", "Views", "AdminPurchaseOrders", "Details.cshtml");
        var batchView = Read("CafeChain", "Areas", "Admin", "Views", "AdminPurchaseOrderBatches", "Details.cshtml");
        var iceView = Read("CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Details.cshtml");
        Assert.Contains("canApprove && Model.Status", orderView, StringComparison.Ordinal);
        Assert.Contains("canSend && Model.Status", orderView, StringComparison.Ordinal);
        Assert.Contains("canCancel && Model.Status", orderView, StringComparison.Ordinal);
        Assert.Contains("batch.CreatedByStaffId != Model.Actor.StaffId", batchView, StringComparison.Ordinal);
        Assert.Contains("Model.CanLinkWorkShift", iceView, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiftLead_CanRequestSupplementForAssignedShift()
    {
        AssertGrant("OperationalIce.RequestSupplement", ShiftLead, true);
        AssertAssignedShiftGuard();
    }

    [Fact]
    public void ShiftLead_CanSubmitCloseForAssignedShift()
    {
        AssertGrant("OperationalIce.SubmitClose", ShiftLead, true);
        AssertAssignedShiftGuard();
    }

    [Fact] public void ShiftLead_CannotConfigurePolicy() => AssertGrant("OperationalIce.ConfigurePolicy", ShiftLead, false);
    [Fact] public void StoreManager_CanConfigurePolicyWithinStoreScope() => AssertGrant("OperationalIce.ConfigurePolicy", StoreManager, true);

    [Fact]
    public void StoreManager_CanCreateAndOpenShift()
    {
        AssertGrant("OperationalIce.CreateShift", StoreManager, true);
        AssertGrant("OperationalIce.OpenShift", StoreManager, true);
    }

    [Fact] public void StoreManager_CanLinkWorkShift() => AssertGrant("OperationalIce.LinkWorkShift", StoreManager, true);
    [Fact] public void WarehouseAccountant_CannotLinkWorkShiftByDefault() => AssertGrant("OperationalIce.LinkWorkShift", WarehouseAccountant, false);
    [Fact] public void RegionManager_CannotMutateWithoutExplicitPermission() => AssertGrant("OperationalIce.CreateShift", AreaManager, false);

    [Fact]
    public void OperationalIceLink_RequiresPermissionAndStoreScope()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminOperationalIceController.cs");
        var service = Read("CafeChain", "Application", "Services", "Inventories", "OperationalIceService.cs");
        Assert.Contains("HasPermissionAsync(OperationalIcePermissions.LinkWorkShift, storeId)", controller, StringComparison.Ordinal);
        Assert.Contains("AuthorizeAsync(actor, shift.StoreId, PlanningRoles", service, StringComparison.Ordinal);
        Assert.Contains("_scopeAuthorization.CanAccessStoreAsync(actor.StaffId, storeId)", service, StringComparison.Ordinal);
    }

    private static void AssertAssignedShiftGuard()
    {
        var service = Read("CafeChain", "Application", "Services", "Inventories", "OperationalIceService.cs");
        Assert.Contains("AuthorizeAssignedShift(actor,", service, StringComparison.Ordinal);
        Assert.Contains("shift.ShiftLeadId != actor.StaffId", service, StringComparison.Ordinal);
    }

    private static void AssertGrant(string permissionCode, int roleColumn, bool expected)
    {
        var seed = Read("CafeChain", "Scripts", "SeedAll.sql");
        var match = Regex.Match(
            seed,
            $@"\(N'{Regex.Escape(permissionCode)}',([01]),([01]),([01]),([01]),([01]),([01]),([01]),([01])\)");
        Assert.True(match.Success, $"Không tìm thấy permission {permissionCode} trong #PermissionMatrix.");
        Assert.Equal(expected ? "1" : "0", match.Groups[roleColumn + 1].Value);
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
