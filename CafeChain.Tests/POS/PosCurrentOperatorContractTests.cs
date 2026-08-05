namespace CafeChain.Tests.POS;

public sealed class PosCurrentOperatorContractTests
{
    [Fact]
    public void Current_operator_has_separate_responsibility_pin_and_lockout_fields()
    {
        var shift = Read("CafeChain", "Models", "Stores", "WorkShift.cs");
        var staff = Read("CafeChain", "Models", "Staffs", "Staff.cs");

        Assert.Contains("CurrentOperatorStaffId", shift, StringComparison.Ordinal);
        Assert.Contains("OperatorChangedAtUtc", shift, StringComparison.Ordinal);
        Assert.Contains("PosPinHash", staff, StringComparison.Ordinal);
        Assert.Contains("PosPinFailedAttempts", staff, StringComparison.Ordinal);
        Assert.Contains("PosPinLockedUntilUtc", staff, StringComparison.Ordinal);
    }

    [Fact]
    public void Switch_operator_is_permission_scoped_idempotent_and_audited()
    {
        var service = Read("CafeChain", "Application", "Services", "POS", "WorkShiftService.cs");
        var controller = Read("CafeChain", "Controllers", "Api", "v1", "POSShiftController.cs");

        Assert.Contains("PermissionConstants.PosOperatorSwitch", service, StringComparison.Ordinal);
        Assert.Contains("POS.OPERATOR.SWITCH", service, StringComparison.Ordinal);
        Assert.Contains("POS_OPERATOR_CHANGED", service, StringComparison.Ordinal);
        Assert.Contains("PosPinLockedUntilUtc = nowUtc.AddMinutes(15)", service, StringComparison.Ordinal);
        Assert.Contains("[RequirePermission(PermissionConstants.PosOperatorSwitch)]", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Orders_and_payments_capture_operator_workshift_store_and_terminal()
    {
        var service = Read("CafeChain", "Application", "Services", "POS", "POSOrderService.cs");
        var payment = Read("CafeChain", "Models", "Payments", "Payment.cs");

        Assert.Contains("activeShift.CurrentOperatorStaffId ?? activeShift.UserId", service, StringComparison.Ordinal);
        Assert.Contains("TerminalId = activeShift.PosTerminalId", service, StringComparison.Ordinal);
        Assert.Contains("PaidByStaffId = operatorStaffId", service, StringComparison.Ordinal);
        Assert.Contains("WorkShiftId", payment, StringComparison.Ordinal);
        Assert.Contains("PaidByStaffId", payment, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_preserves_current_operator_and_financial_attribution_contract()
    {
        var migrationDirectory = Path.Combine(FindRepoRoot(), "CafeChain", "Migrations");
        var incrementalMigration = Directory.GetFiles(
                migrationDirectory,
                "*AddPosCurrentOperatorAndTransactionActors.cs")
            .SingleOrDefault(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase));
        var migration = incrementalMigration
            ?? Directory.GetFiles(migrationDirectory, "*InitialCreate.cs")
                .Where(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => path, StringComparer.Ordinal)
                .First();
        var sql = File.ReadAllText(migration);

        if (incrementalMigration != null)
        {
            Assert.Contains("CurrentOperatorStaffId = ws.UserId", sql, StringComparison.Ordinal);
            Assert.Contains("JOIN dbo.WorkShifts ws ON ws.ShiftId = o.WorkShiftId", sql, StringComparison.Ordinal);
            Assert.Contains("WorkShiftId = o.WorkShiftId", sql, StringComparison.Ordinal);
            return;
        }

        Assert.Contains("CurrentOperatorStaffId = table.Column<int>", sql, StringComparison.Ordinal);
        Assert.Contains("WorkShiftId = table.Column<int>", sql, StringComparison.Ordinal);
        Assert.Contains("PaidByStaffId = table.Column<int>", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Operator_candidate_endpoint_is_store_scoped_and_permission_filtered()
    {
        var repository = Read("CafeChain", "Infrastructure", "Repositories", "Admin", "POS", "WorkShiftRepository.cs");
        var service = Read("CafeChain", "Application", "Services", "POS", "WorkShiftService.cs");
        var controller = Read("CafeChain", "Controllers", "Api", "v1", "POSShiftController.cs");

        Assert.Contains("x.StoreId == storeId && x.Active && x.Account.Active", repository, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.PosOperatorSwitch, storeId", service, StringComparison.Ordinal);
        Assert.Contains("[HttpGet(\"operator/candidates\")]", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Staffhub_and_pos_ui_clear_pin_and_show_distinct_responsibility()
    {
        var staffHub = Read("CafeChain", "wwwroot", "js", "StaffHub", "staffhub-schedule.js");
        var pos = Read("CafeChain.Frontend", "src", "pages", "ShiftSummary.tsx");

        Assert.Contains("operatorCurrentPassword.value = \"\"", staffHub, StringComparison.Ordinal);
        Assert.Contains("operatorNewPin.value = \"\"", staffHub, StringComparison.Ordinal);
        Assert.Contains("Nhân viên chịu trách nhiệm", pos, StringComparison.Ordinal);
        Assert.Contains("Người đang thao tác", pos, StringComparison.Ordinal);
        Assert.Contains("setOperatorPin('')", pos, StringComparison.Ordinal);
        Assert.Contains("operator/candidates", pos, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { FindRepoRoot() }.Concat(path).ToArray()));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
