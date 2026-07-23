using CafeChain.Helpers;
using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Tests;

public sealed class InventoryDocumentPurposeRefactorSourceTests
{
    [Fact]
    public void Create_ui_exposes_only_export_stock_take_and_waste()
    {
        var root = FindRepoRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "CafeChain",
            "wwwroot",
            "js",
            "Admin",
            "InventoryDocument",
            "inventorydocumentcreate.js"));

        Assert.DoesNotContain("inventory-type-import", script, StringComparison.Ordinal);
        Assert.DoesNotContain("data-inventory-type=\"1\"", script, StringComparison.Ordinal);
        Assert.Contains("data-inventory-type=\"2\"", script, StringComparison.Ordinal);
        Assert.Contains("data-inventory-type=\"3\"", script, StringComparison.Ordinal);
        Assert.Contains("data-inventory-type=\"4\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_view_and_javascript_do_not_offer_retired_purposes()
    {
        var root = FindRepoRoot();
        var view = File.ReadAllText(Path.Combine(
            root,
            "CafeChain",
            "Areas",
            "Admin",
            "Views",
            "AdminInventoryDocument",
            "Partials",
            "Create",
            "_DocumentInfo.cshtml"));
        var script = File.ReadAllText(Path.Combine(
            root,
            "CafeChain",
            "wwwroot",
            "js",
            "Admin",
            "InventoryDocument",
            "inventorydocumentcreate.js"));

        foreach (var retired in new[] { "GIFT", "DEBT", "SAMPLE", "ADJUSTMENT_OUT", "IMPORT_ADJUSTMENT" })
        {
            Assert.DoesNotContain(retired, view, StringComparison.Ordinal);
            Assert.DoesNotContain(retired, script, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Legacy_purposes_remain_readable_for_history()
    {
        Assert.Equal("Quà tặng", InventoryDocumentPurpose.GIFT.ToVietnamese());
        Assert.Equal("Ghi nợ", InventoryDocumentPurpose.DEBT.ToVietnamese());
        Assert.Equal("Hàng mẫu", InventoryDocumentPurpose.SAMPLE.ToVietnamese());
        Assert.Equal("Điều chỉnh tăng", InventoryDocumentPurpose.IMPORT_ADJUSTMENT.ToVietnamese());
        Assert.Equal("Điều chỉnh giảm", InventoryDocumentPurpose.ADJUSTMENT_OUT.ToVietnamese());
    }

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
