namespace CafeChain.Tests;

public sealed class ShiftSchedulingUiSourceTests
{
    [Fact]
    public void Admin_schedule_view_uses_old_week_grid_dropdown_and_template_drag_drop()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminStaffShift", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "StaffShift", "admin-staff-shift.js");
        var css = Read("CafeChain", "wwwroot", "css", "Admin", "StaffShift", "admin-staff-shift.css");

        Assert.Contains("roster-table", view, StringComparison.Ordinal);
        Assert.Contains("scheduleShift", view, StringComparison.Ordinal);
        Assert.Contains("draggable-template", view, StringComparison.Ordinal);
        Assert.Contains("schedule-drop-zone", view, StringComparison.Ordinal);
        Assert.Contains("data-assign-url", view, StringComparison.Ordinal);
        Assert.Contains("@section Scripts", view, StringComparison.Ordinal);
        Assert.Contains("~/js/Admin/StaffShift/admin-staff-shift.js", view, StringComparison.Ordinal);
        Assert.DoesNotContain("new bootstrap.Modal", view, StringComparison.Ordinal);

        Assert.Contains("DOMContentLoaded", script, StringComparison.Ordinal);
        Assert.Contains("dragstart", script, StringComparison.Ordinal);
        Assert.Contains("application/x-cafechain-shift", script, StringComparison.Ordinal);
        Assert.Contains("addEventListener(\"drop\"", script, StringComparison.Ordinal);
        Assert.Contains("openSchedule", script, StringComparison.Ordinal);
        Assert.Contains("AdminMutationGuard", script, StringComparison.Ordinal);
        Assert.Contains("drag-over", css, StringComparison.Ordinal);
        Assert.Contains("position: sticky", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Drag_drop_only_prefills_confirmation_modal_and_does_not_move_existing_schedule()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminStaffShift", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "StaffShift", "admin-staff-shift.js");

        Assert.Contains("draggable=\"@(Model.CanCreate ? \"true\" : \"false\")\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("draggable=\"true\"", ScheduleCardsOnly(view), StringComparison.Ordinal);
        var dropStart = script.IndexOf("zone.addEventListener(\"drop\"", StringComparison.Ordinal);
        var dropEnd = script.IndexOf("});", dropStart, StringComparison.Ordinal);
        var dropHandler = script[dropStart..dropEnd];
        Assert.Contains("openSchedule", dropHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("post(", dropHandler, StringComparison.Ordinal);
    }

    [Fact]
    public void StaffHub_script_is_deferred_until_layout_dependencies_are_available()
    {
        var view = Read("CafeChain", "Views", "StaffHub", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "StaffHub", "staffhub-schedule.js");

        Assert.Contains("@section Scripts", view, StringComparison.Ordinal);
        Assert.Contains("~/js/StaffHub/staffhub-schedule.js", view, StringComparison.Ordinal);
        Assert.DoesNotContain("bootstrap.Modal.getOrCreateInstance", view, StringComparison.Ordinal);
        Assert.Contains("DOMContentLoaded", script, StringComparison.Ordinal);
        Assert.Contains("AdminMutationGuard.run(\"staffhub-change-password\"", script, StringComparison.Ordinal);
        Assert.Contains("AdminMutationGuard.run(\"staffhub-open-pos\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Controller_keeps_permission_and_store_scope_authority_for_all_mutations()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminStaffShiftController.cs");

        Assert.Contains("RequirePermission(PermissionConstants.ShiftView)", controller, StringComparison.Ordinal);
        Assert.Contains("RequirePermission(PermissionConstants.ShiftCreate)", controller, StringComparison.Ordinal);
        Assert.Contains("RequirePermission(PermissionConstants.ShiftUpdate)", controller, StringComparison.Ordinal);
        Assert.Contains("RequirePermission(PermissionConstants.ShiftCancel)", controller, StringComparison.Ordinal);
        Assert.Contains("_scopeResolver.ResolveAsync(actor, targetStoreId", controller, StringComparison.Ordinal);
    }

    private static string ScheduleCardsOnly(string view)
    {
        var start = view.IndexOf("<article class=\"schedule-card", StringComparison.Ordinal);
        var end = view.IndexOf("</article>", start, StringComparison.Ordinal);
        return start >= 0 && end > start ? view[start..end] : string.Empty;
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
