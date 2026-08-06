namespace CafeChain.Tests;

public sealed class AdminCrudUxContractTests
{
    [Fact]
    public void Procurement_styles_do_not_override_every_bootstrap_modal()
    {
        var css = Read("CafeChain", "wwwroot", "css", "Admin", "Procurement", "procurement-design-system.css");

        Assert.DoesNotContain("\n.modal {", css, StringComparison.Ordinal);
        Assert.Contains(".cc-warehouse-page .modal", css, StringComparison.Ordinal);
        Assert.Contains(".reorder-page .modal-content", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_toast_is_safe_and_supports_all_feedback_states()
    {
        var script = Read("CafeChain", "wwwroot", "js", "Toast", "toast.js");
        var css = Read("CafeChain", "wwwroot", "css", "Toast", "toast.css");

        Assert.Contains("text.textContent = normalizedMessage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("t.innerHTML", script, StringComparison.Ordinal);
        Assert.Contains("success", script, StringComparison.Ordinal);
        Assert.Contains("warning", script, StringComparison.Ordinal);
        Assert.Contains("error", script, StringComparison.Ordinal);
        Assert.Contains("info", script, StringComparison.Ordinal);
        Assert.Contains(".toast-item.warning", css, StringComparison.Ordinal);
        Assert.Contains(".toast-item.info", css, StringComparison.Ordinal);
        Assert.Contains("correlationId", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Mutation_guard_restores_state_and_moves_modals_outside_stacking_context()
    {
        var script = Read("CafeChain", "wwwroot", "js", "shared", "mutation-guard.js");
        var layout = Read("CafeChain", "Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml");

        Assert.Contains("originalDisabled", script, StringComparison.Ordinal);
        Assert.Contains("data-submit-busy", script, StringComparison.Ordinal);
        Assert.Contains("normalizeBootstrapModalPlacement", script, StringComparison.Ordinal);
        Assert.DoesNotContain(".modal-backdrop", script, StringComparison.Ordinal);
        Assert.Contains("id=\"cc-modal-host\"", layout, StringComparison.Ordinal);
        Assert.Contains("RenderSectionAsync(\"Modals\"", layout, StringComparison.Ordinal);
        Assert.Contains("window.toast", script, StringComparison.Ordinal);
        Assert.Contains("button instanceof HTMLButtonElement", script, StringComparison.Ordinal);
        Assert.Contains("isButton && originalLabels.has(button)", script, StringComparison.Ordinal);
        Assert.Contains("dataset.validationFeedback === \"sweetalert\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Ingredient_feedback_only_reports_strict_success_and_errors_stay_red()
    {
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "Ingredient", "ingredient.js");

        Assert.Contains("result?.success !== true", script, StringComparison.Ordinal);
        Assert.Contains("!response.ok ||", script, StringComparison.Ordinal);
        Assert.Contains("Không thể kết nối máy chủ", script, StringComparison.Ordinal);
        Assert.Contains("toast(error.message || \"Không thể lưu nguyên liệu.\", \"error\")", script, StringComparison.Ordinal);
        Assert.Contains("toast(error.message || \"Không thể cập nhật trạng thái nguyên liệu.\", \"error\")", script, StringComparison.Ordinal);
        Assert.DoesNotContain("result.success ? \"success\" : \"error\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Target_admin_modules_reuse_existing_bootstrap_modal_instances()
    {
        var scripts = new[]
        {
            Read("CafeChain", "wwwroot", "js", "Admin", "Category", "Category.js"),
            Read("CafeChain", "wwwroot", "js", "Admin", "Drink", "drink.js"),
            Read("CafeChain", "wwwroot", "js", "Admin", "DrinkSize", "drinksize.js"),
            Read("CafeChain", "wwwroot", "js", "Admin", "Topping", "topping.js"),
            Read("CafeChain", "wwwroot", "js", "Admin", "InventoryDocument", "inventorydocument.js"),
            Read("CafeChain", "wwwroot", "js", "Admin", "InventoryDocument", "inventorydocumentcreate.js")
        };

        Assert.All(scripts, script => Assert.DoesNotContain("new bootstrap.Modal", script, StringComparison.Ordinal));
    }

    [Fact]
    public void Drink_create_restores_submit_after_validation_and_hides_ai_idea_ui()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminDrink", "Create.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "Drink", "drink.js");

        Assert.Contains("id=\"drinkCreateValidationSummary\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"drinkAiIdea\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"btnDrinkAiSuggestion\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ai-image-pipeline.js", view, StringComparison.Ordinal);
        Assert.Contains("invalid-form.validate", script, StringComparison.Ordinal);
        Assert.Contains("restoreCreateSubmit", script, StringComparison.Ordinal);
        Assert.Contains("AdminMutationGuard?.unlockForm", script, StringComparison.Ordinal);
        Assert.Contains(".prop('disabled', false)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void StaffHub_uses_readable_status_fallbacks()
    {
        var script = Read("CafeChain", "wwwroot", "js", "StaffHub", "staffhub-schedule.js");

        Assert.Contains("window.showToast", script, StringComparison.Ordinal);
        Assert.Contains("401:", script, StringComparison.Ordinal);
        Assert.Contains("403:", script, StringComparison.Ordinal);
        Assert.Contains("409:", script, StringComparison.Ordinal);
        Assert.Contains("Mã tra cứu", script, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepoRoot(), .. path]));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
