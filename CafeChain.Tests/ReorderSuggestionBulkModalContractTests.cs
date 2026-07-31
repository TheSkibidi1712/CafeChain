using System.IO;
using Xunit;

namespace CafeChain.Tests;

public sealed class ReorderSuggestionBulkModalContractTests
{
    private const string ViewPath =
        "CafeChain/Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml";
    private const string CssPath =
        "CafeChain/wwwroot/css/Admin/Procurement/reorder-suggestions.css";

    [Fact]
    public void Modal_UsesOnlyServerCalculatedActionableSuggestions()
    {
        var view = Read(ViewPath);

        Assert.Contains("item.CanConfirm", view, StringComparison.Ordinal);
        Assert.Contains(
            "item.FinalSuggestedQuantity.GetValueOrDefault() > 0m",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReorderRecommendationLevels.Urgent",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReorderRecommendationLevels.NearReorder",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReorderRecommendationLevels.ProcurementInProgress",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "item.SuggestionStatus == ReorderRecommendationLevels.DataIncomplete",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-auto-open=\"@(actionableItems.Any() ? \"true\" : \"false\")\"",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "class=\"form-check-input js-reorder-bulk-select\"",
            view,
            StringComparison.Ordinal);
        Assert.Contains("checked />", view, StringComparison.Ordinal);
    }

    [Fact]
    public void BulkConfirmation_ReusesConfirmContractSequentiallyAndKeepsRequestKey()
    {
        var view = Read(ViewPath);

        Assert.Contains(
            "title: 'Xác nhận tạo yêu cầu nhập'",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "for (const row of selectedRows)",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "row.dataset.requestKey ||= crypto.randomUUID()",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "await postForm(confirmEndpoint",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "requestKey: row.dataset.requestKey",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "result.replaceChildren()",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "messageNode.textContent = message",
            view,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "batchConfirmEndpoint",
            view,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "finalSuggestedQuantity: row.dataset",
            view,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Modal_HasPerItemOutcomeAndResponsiveSemanticStyles()
    {
        var view = Read(ViewPath);
        var css = Read(CssPath);

        Assert.Contains("js-reorder-bulk-result", view, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", view, StringComparison.Ordinal);
        Assert.Contains(
            "reorder-bulk-result--success",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            "reorder-bulk-result--retry",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            "reorder-status--near_reorder",
            css,
            StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 640px)", css, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null
               && !Directory.Exists(Path.Combine(dir.FullName, "CafeChain")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new DirectoryNotFoundException("Không tìm thấy repo root.");
    }
}
