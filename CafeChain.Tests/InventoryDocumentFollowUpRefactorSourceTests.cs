namespace CafeChain.Tests;

public sealed class InventoryDocumentFollowUpRefactorSourceTests
{
    [Fact]
    public void Store_inventory_javascript_has_no_manual_import_price_call_and_rejects_stale_responses()
    {
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "InventoryDocument", "inventorydocumentcreate.js");

        Assert.DoesNotContain("isManualPricePurpose", script, StringComparison.Ordinal);
        Assert.Contains("setPriceEditable(false)", script, StringComparison.Ordinal);
        Assert.Contains("storeInventoryRequestVersion", script, StringComparison.Ordinal);
        Assert.Contains("currentStoreId !== requestedStoreId", script, StringComparison.Ordinal);
        Assert.Contains("loadStoreInventoryIngredients", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_negative_switch_is_default_off_and_is_sent_in_the_dto()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminInventoryDocument", "Partials", "Create", "_DocumentInfo.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "InventoryDocument", "inventorydocumentcreate.js");

        Assert.Contains("id=\"AllowNegativeStock\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"AllowNegativeStock\" checked", view, StringComparison.Ordinal);
        Assert.Contains("id=\"negativeReasonField\"", view, StringComparison.Ordinal);
        Assert.Contains("allowNegativeStock,", script, StringComparison.Ordinal);
        Assert.Contains("allowNegativeInput?.checked === true", script, StringComparison.Ordinal);
        Assert.Contains("blockedLine?.userMessage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("projected after và reason code", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inventory_filters_use_native_dates_and_type_scoped_purposes()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminInventoryDocument", "Partials", "Detail", "_FilterSection.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "InventoryDocument", "inventorydocument.js");

        Assert.Contains("type=\"date\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("vn-date-input", view, StringComparison.Ordinal);
        Assert.Contains("data-document-type", view, StringComparison.Ordinal);
        Assert.DoesNotContain("InventoryDocumentType.PRODUCTION_IN", view, StringComparison.Ordinal);
        Assert.DoesNotContain("InventoryDocumentType.PRODUCTION_OUT", view, StringComparison.Ordinal);
        Assert.DoesNotContain("InventoryDocumentType.SALES_DEDUCTION", view, StringComparison.Ordinal);
        Assert.Contains("syncPurposeFilter", script, StringComparison.Ordinal);
        Assert.Contains("không được lớn hơn", script, StringComparison.Ordinal);
    }

    [Fact]
    public void StaffHub_no_longer_forces_first_login_password_change_but_normal_change_remains()
    {
        var controller = Read("CafeChain", "Controllers", "StaffHubController.cs");
        var view = Read("CafeChain", "Views", "StaffHub", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "StaffHub", "staffhub-schedule.js");
        var accountInterface = Read("CafeChain", "Application", "Interfaces", "Accounts", "IAccountService.cs");

        Assert.DoesNotContain("ChangeRequiredPassword", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("requiredPasswordForm", view, StringComparison.Ordinal);
        Assert.DoesNotContain("requiresPasswordChange", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeRequiredPasswordAsync", accountInterface, StringComparison.Ordinal);
        Assert.Contains("ChangePasswordAsync", accountInterface, StringComparison.Ordinal);
    }

    [Fact]
    public void Negative_opt_in_has_a_backward_compatible_database_migration()
    {
        var migration = Read("CafeChain", "Migrations", "20260722230000_AddInventoryDocumentAllowNegativeStock.cs");
        var model = Read("CafeChain", "Models", "Inventories", "Documents", "InventoryDocument.cs");
        var createService = Read("CafeChain", "Application", "Services", "Admin", "InventoryDocuments", "AdminInventoryDocumentCreateService.cs");
        var processService = Read("CafeChain", "Application", "Services", "Admin", "InventoryDocuments", "AdminInventoryDocumentProcessService.cs");

        Assert.Contains("AllowNegativeStock", migration, StringComparison.Ordinal);
        Assert.Contains("defaultValue: false", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiresPasswordChange", migration, StringComparison.Ordinal);
        Assert.Contains("public bool AllowNegativeStock", model, StringComparison.Ordinal);
        Assert.Contains("existingDraft.AllowNegativeStock = dto.AllowNegativeStock", createService, StringComparison.Ordinal);
        Assert.Contains("AllowNegativeStock = dto.AllowNegativeStock", createService, StringComparison.Ordinal);
        Assert.Contains("document.AllowNegativeStock", processService, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([FindRepoRoot(), .. segments]));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
