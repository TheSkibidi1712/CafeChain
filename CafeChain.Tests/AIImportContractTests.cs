using System.Reflection;
using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Areas.Admin.Filters;
using CafeChain.Data;
using CafeChain.Models.AIImport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CafeChain.Tests;

public sealed class AIImportContractTests
{
    [Fact]
    public void Api_mutations_have_antiforgery_and_expected_routes()
    {
        var mutations = new Dictionary<string, string>
        {
            [nameof(AIImportController.Analyze)] = "/api/ai-import/analyze",
            [nameof(AIImportController.Reanalyze)] = "/api/ai-import/{id:int}/reanalyze",
            [nameof(AIImportController.PatchGroup)] = "/api/ai-import/{id:int}/groups/{groupId:int}",
            [nameof(AIImportController.PatchItem)] = "/api/ai-import/{id:int}/items/{itemId:int}",
            [nameof(AIImportController.Confirm)] = "/api/ai-import/{id:int}/confirm",
            [nameof(AIImportController.Cancel)] = "/api/ai-import/{id:int}/cancel",
            [nameof(AIImportController.RemoveSource)] = "/api/ai-import/{id:int}/sources/{sourceDocumentId:int}"
        };

        foreach (var (methodName, route) in mutations)
        {
            var method = typeof(AIImportController).GetMethod(methodName)!;
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            var routeAttribute = method.GetCustomAttributes().OfType<IRouteTemplateProvider>().Single(x => x.Template != null);
            Assert.Equal(route, routeAttribute.Template);
        }
    }

    [Fact]
    public void Controller_enforces_six_ai_import_permissions()
    {
        var expected = new Dictionary<string, string>
        {
            [nameof(AIImportController.Index)] = PermissionConstants.AIImportView,
            [nameof(AIImportController.Analyze)] = PermissionConstants.AIImportUpload,
            [nameof(AIImportController.Reanalyze)] = PermissionConstants.AIImportAnalyze,
            [nameof(AIImportController.Confirm)] = PermissionConstants.AIImportConfirm,
            [nameof(AIImportController.Cancel)] = PermissionConstants.AIImportCancel,
            [nameof(AIImportController.History)] = PermissionConstants.AIImportHistory
        };
        foreach (var (methodName, permission) in expected)
        {
            var policy = typeof(AIImportController).GetMethod(methodName)!.GetCustomAttribute<RequirePermissionAttribute>()?.Policy;
            Assert.Equal(RequirePermissionAttribute.PolicyPrefix + permission, policy);
        }
    }

    [Fact]
    public void Ef_model_contains_import_tables_indexes_constraints_and_rowversion()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var context = new AppDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var session = model.FindEntityType(typeof(ImportSession))!;
        var group = model.FindEntityType(typeof(ImportGroup))!;
        var source = model.FindEntityType(typeof(ImportSourceDocument))!;
        var item = model.FindEntityType(typeof(ImportItem))!;
        var audit = model.FindEntityType(typeof(ImportAudit))!;

        Assert.Equal("ImportSessions", session.GetTableName());
        Assert.Equal("ImportGroups", group.GetTableName());
        Assert.Equal("ImportSourceDocuments", source.GetTableName());
        Assert.Equal("ImportItems", item.GetTableName());
        Assert.Equal("ImportAudits", audit.GetTableName());
        Assert.True(session.FindProperty(nameof(ImportSession.RowVersion))!.IsConcurrencyToken);
        Assert.Equal(10, session.FindProperty(nameof(ImportSession.SourceFormat))!.GetMaxLength());
        Assert.NotNull(group.FindProperty(nameof(ImportGroup.ExtractionMode)));
        Assert.NotNull(group.FindProperty(nameof(ImportGroup.ImportSourceDocumentId)));
        Assert.Contains(source.GetCheckConstraints(), x => x.Name == "CK_ImportSourceDocuments_Status");
        Assert.NotNull(item.FindProperty(nameof(ImportItem.SourceLocatorJson)));
        Assert.NotNull(item.FindProperty(nameof(ImportItem.AiConfidence)));
        Assert.NotNull(item.FindProperty(nameof(ImportItem.ManualReviewConfirmed)));
        Assert.NotNull(group.FindProperty(nameof(ImportGroup.SourceColumnsJson)));
        Assert.NotNull(audit.FindProperty(nameof(ImportAudit.AiChunkCount)));
        Assert.Contains(session.GetIndexes(), x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "UploadedByAccountId", "CreatedAtUtc" }));
        Assert.Contains(item.GetCheckConstraints(), x => x.Name == "CK_ImportItems_Action");
        Assert.Contains(item.GetCheckConstraints(), x => x.Name == "CK_ImportItems_Status");
    }

    [Fact]
    public void Import_entities_are_compatible_with_runtime_lazy_loading_proxies()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseLazyLoadingProxies()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new AppDbContext(options);
        Assert.NotNull(context.Model.FindEntityType(typeof(ImportSession)));
        Assert.NotNull(context.Model.FindEntityType(typeof(ImportSourceDocument)));
        Assert.NotNull(context.Model.FindEntityType(typeof(ImportAudit)));
    }

    [Fact]
    public void Document_source_migration_is_backward_compatible_and_keeps_initial_migration()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var context = new AppDbContext(options);
        Assert.Contains("20260815105817_InitialCreate", context.Database.GetMigrations());
        Assert.Contains("20260815141744_AddAIImportOcrRuntimeAndMultiFile", context.Database.GetMigrations());
        Assert.Equal(2, context.Database.GetMigrations().Count());
        var migration = Read("CafeChain", "Migrations", "20260815105817_InitialCreate.cs");
        Assert.Contains("name: \"ImportSessions\"", migration, StringComparison.Ordinal);
        Assert.Contains("CK_ImportSessions_Status", migration, StringComparison.Ordinal);
        Assert.Contains("name: \"ImportSessions\"", migration[migration.IndexOf("protected override void Down", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.Contains("ManualReviewConfirmed = table.Column<bool>", migration, StringComparison.Ordinal);
        Assert.Contains("SourceColumnsJson = table.Column<string>", migration, StringComparison.Ordinal);
        Assert.Contains("ExtractionVersion = table.Column<string>", migration, StringComparison.Ordinal);
        Assert.Contains("FieldEvidenceJson = table.Column<string>", migration, StringComparison.Ordinal);
        var forward = Read("CafeChain", "Migrations", "20260815141744_AddAIImportOcrRuntimeAndMultiFile.cs");
        Assert.Contains("name: \"ImportSourceDocuments\"", forward, StringComparison.Ordinal);
        Assert.Contains("name: \"RequestedOcr\"", forward, StringComparison.Ordinal);
        Assert.Contains("name: \"EffectiveOcr\"", forward, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirm_contract_uses_existing_deduplication_global_scope_and_serializable_transaction()
    {
        var service = Read("CafeChain", "Application", "Services", "AIImport", "AIImportService.cs");
        Assert.Contains("BeginTransactionAsync(IsolationLevel.Serializable", service, StringComparison.Ordinal);
        Assert.Contains("_deduplication.BeginScopedAsync", service, StringComparison.Ordinal);
        Assert.Contains("\"AIImport.Confirm\"", service, StringComparison.Ordinal);
        Assert.Contains("sessionId,\n                0,\n                actor.AccountId", service, StringComparison.Ordinal);
        Assert.Contains("ExecuteUpdateAsync", service, StringComparison.Ordinal);
        Assert.Contains("MarkSuccessAsync", service, StringComparison.Ordinal);
        Assert.Contains("PHIÊN_ĐÃ_XỬ_LÝ", service, StringComparison.Ordinal);
        Assert.Contains("PREVIEW_ĐÃ_THAY_ĐỔI", service, StringComparison.Ordinal);
        Assert.Contains("DỮ_LIỆU_ĐÃ_TỒN_TẠI", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Supplier_create_joins_ambient_import_transaction()
    {
        var supplier = Read("CafeChain", "Application", "Services", "Admin", "Suppliers", "AdminSupplierService.cs");
        Assert.Contains("_context.Database.CurrentTransaction == null", supplier, StringComparison.Ordinal);
        Assert.Contains("PrepareDuplicateWarningAsync", supplier, StringComparison.Ordinal);
    }

    [Fact]
    public void SeedAll_is_idempotent_and_grants_ai_import_only_to_owner_and_accountant_warehouse()
    {
        var seed = Read("CafeChain", "Scripts", "SeedAll.sql");
        Assert.Contains("IF NOT EXISTS(SELECT 1 FROM dbo.PermissionGroups WHERE Code=N'AI_IMPORT')", seed, StringComparison.Ordinal);
        foreach (var code in new[] { "View", "Upload", "Analyze", "Confirm", "Cancel", "History" })
        {
            Assert.Contains($"(N'AIImport.{code}',1,0,0,0,1,0,0,0)", seed, StringComparison.Ordinal);
        }
        Assert.Contains("(N'CDN',187)", seed, StringComparison.Ordinal);
        Assert.Contains("(N'KTK',124)", seed, StringComparison.Ordinal);
        Assert.DoesNotContain("(N'AIImport.View',1,1", seed, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui_contract_has_permission_sidebar_antiforgery_stale_refresh_and_idempotency_retry()
    {
        var layout = Read("CafeChain", "Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml");
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AIImport", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "AIImport", "ai-import.js");
        Assert.Contains("effectivePermissions.Contains(PermissionConstants.AIImportView)", layout, StringComparison.Ordinal);
        Assert.Contains("@Html.AntiForgeryToken()", view, StringComparison.Ordinal);
        Assert.Contains("headers.set('RequestVerificationToken', token)", script, StringComparison.Ordinal);
        Assert.Contains("state.confirmKey ||=", script, StringComparison.Ordinal);
        Assert.Contains("'Idempotency-Key': state.confirmKey", script, StringComparison.Ordinal);
        Assert.Contains("error.code === 'PREVIEW_ĐÃ_THAY_ĐỔI'", script, StringComparison.Ordinal);
        Assert.Contains("overrideReason", script, StringComparison.Ordinal);
        Assert.Contains(".xlsx,.docx,.pdf", view, StringComparison.Ordinal);
        Assert.Contains("session.sourceFormat", script, StringComparison.Ordinal);
        Assert.Contains("group.extractionMode", script, StringComparison.Ordinal);
        Assert.Contains("item.evidenceSnippet", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_uses_configuration_driven_request_size_filter()
    {
        var method = typeof(AIImportController).GetMethod(nameof(AIImportController.Analyze))!;
        Assert.NotNull(method.GetCustomAttribute<AIImportRequestSizeLimitAttribute>());
        Assert.Null(method.GetCustomAttribute<RequestFormLimitsAttribute>());
    }

    [Fact]
    public void Editor_options_and_modal_match_the_five_create_forms_in_vietnamese()
    {
        var controller = typeof(AIImportController).GetMethod(nameof(AIImportController.EditorOptions))!;
        var route = controller.GetCustomAttributes().OfType<IRouteTemplateProvider>().Single(x => x.Template != null);
        var permission = controller.GetCustomAttribute<RequirePermissionAttribute>()?.Policy;
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AIImport", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "AIImport", "ai-import.js");

        Assert.Equal("/api/ai-import/{id:int}/editor-options", route.Template);
        Assert.Equal(RequirePermissionAttribute.PolicyPrefix + PermissionConstants.AIImportView, permission);
        Assert.Contains("/editor-options", script, StringComparison.Ordinal);
        foreach (var label in new[] { "Danh mục", "Đồ uống", "Size", "Nguyên liệu", "Nhà cung cấp", "Lưu và kiểm tra lại" })
            Assert.Contains(label, view + script, StringComparison.Ordinal);
        Assert.Contains("type: 'icon'", script, StringComparison.Ordinal);
        Assert.Contains("type: 'select', optionSource: 'categories'", script, StringComparison.Ordinal);
        Assert.Contains("type: 'select', optionSource: 'productTypes'", script, StringComparison.Ordinal);
        Assert.Contains("type: 'select', optionSource: 'units'", script, StringComparison.Ordinal);
        Assert.Contains("type: 'email'", script, StringComparison.Ordinal);
        Assert.Contains("type: 'tel'", script, StringComparison.Ordinal);
        Assert.Contains("type: 'textarea'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui_uses_sweetalert_for_operation_feedback_and_confirmations()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AIImport", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "AIImport", "ai-import.js");
        var styles = Read("CafeChain", "wwwroot", "css", "Admin", "AIImport", "ai-import.css");

        Assert.DoesNotContain("id=\"messageBox\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("function message(", script, StringComparison.Ordinal);
        Assert.DoesNotContain("confirm(", script, StringComparison.Ordinal);
        Assert.Contains("window.Swal.fire", script, StringComparison.Ordinal);
        Assert.Contains("Xác nhận nhập", script, StringComparison.Ordinal);
        Assert.Contains("Hủy phiên", script, StringComparison.Ordinal);
        Assert.Contains("Phân tích thành công", script, StringComparison.Ordinal);
        Assert.Contains("target: activeDialog || document.body", script, StringComparison.Ordinal);
        Assert.Contains(".edit-dialog>.swal2-container", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui_icon_editor_keeps_the_last_valid_single_unicode_symbol()
    {
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "AIImport", "ai-import.js");

        Assert.Contains("Chỉ nhập 1 biểu tượng Unicode.", script, StringComparison.Ordinal);
        Assert.Contains("lastValidIcon", script, StringComparison.Ordinal);
        Assert.Contains("input.value = lastValidIcon", script, StringComparison.Ordinal);
        Assert.Contains("new Intl.Segmenter", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui_editor_keeps_server_errors_visible_and_all_actions_reachable()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AIImport", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "AIImport", "ai-import.js");
        var styles = Read("CafeChain", "wwwroot", "css", "Admin", "AIImport", "ai-import.css");

        Assert.Contains("id=\"editDialogBody\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"editWarningMessages\"", view, StringComparison.Ordinal);
        Assert.Contains("input.dataset.serverError", script, StringComparison.Ordinal);
        Assert.Contains("delete input.dataset.serverError", script, StringComparison.Ordinal);
        Assert.Contains("editDialogBody.scrollTop = 0", script, StringComparison.Ordinal);
        Assert.Contains(".edit-dialog-body", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-y:auto", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Saving_a_document_item_resolves_low_confidence_manual_review()
    {
        var service = Read("CafeChain", "Application", "Services", "AIImport", "AIImportService.cs");
        var validator = Read("CafeChain", "Application", "Services", "AIImport", "AIImportCandidateValidator.cs");

        Assert.Contains("request.ManualReviewConfirmed", service, StringComparison.Ordinal);
        Assert.Contains("ManualReviewPayloadHash", service, StringComparison.Ordinal);
        Assert.Contains("AIImportIssueResolutions.ManualReview", validator, StringComparison.Ordinal);
        Assert.Contains("AIImportValidationContract.ResolveStatus", validator, StringComparison.Ordinal);
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AIImport", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "AIImport", "ai-import.js");
        Assert.Contains("id=\"manualReviewConfirmed\"", view, StringComparison.Ordinal);
        Assert.Contains("byId('manualReviewConfirmed').checked", script, StringComparison.Ordinal);
        Assert.Contains("issue.metadata?.resolution === 'MANUAL_REVIEW'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Reanalyze_and_ui_send_expected_preview_version_and_canonical_issues()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AIImportController.cs");
        var service = Read("CafeChain", "Application", "Services", "AIImport", "AIImportService.cs");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "AIImport", "ai-import.js");
        Assert.Contains("AIImportReanalyzeRequest request", controller, StringComparison.Ordinal);
        Assert.Contains("expectedPreviewVersion: state.session.previewVersion", script, StringComparison.Ordinal);
        Assert.Contains("item.issues", script, StringComparison.Ordinal);
        Assert.Contains("manualReviewConfirmed", script, StringComparison.Ordinal);
        Assert.Contains("sourceColumns", script, StringComparison.Ordinal);
        Assert.Contains("REANALYZE_CLAIMED", service, StringComparison.Ordinal);
        Assert.Contains("catch (DbUpdateConcurrencyException)", service, StringComparison.Ordinal);
        Assert.Contains("PHÂN_TÍCH_LẠI_THẤT_BẠI", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase9_registers_deep_modules_and_scoped_validation_without_changing_public_contract()
    {
        var registrations = Read("CafeChain", "Extensions", "Services", "ApplicationServiceExtensions.cs");
        var service = Read("CafeChain", "Application", "Services", "AIImport", "AIImportService.cs");
        foreach (var module in new[]
                 {
                     "AIImportEntityRegistry", "AIImportPreviewValidator", "AIImportResolutionEngine",
                     "AIImportAnalysisCoordinator", "AIImportPreviewMutationCoordinator", "AIImportConfirmCoordinator",
                     "AIImportSessionQuery"
                 })
            Assert.Contains(module, registrations + service, StringComparison.Ordinal);
        Assert.Contains("public static AIImportValidationScope ForItem", Read("CafeChain", "Application", "Services", "AIImport", "AIImportEntityRegistry.cs"), StringComparison.Ordinal);
        Assert.Contains("_mutationCoordinator.ItemScope", service, StringComparison.Ordinal);
        Assert.Contains("_confirmCoordinator.BuildExecutionPlan", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui_cancel_session_closes_the_explanation_editor_and_clears_its_state()
    {
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "AIImport", "ai-import.js");

        Assert.Contains("function closeEditDialog()", script, StringComparison.Ordinal);
        Assert.Contains("if (dialog?.open) dialog.close();", script, StringComparison.Ordinal);
        Assert.Contains("state.editingItem = null;", script, StringComparison.Ordinal);
        Assert.Contains("closeAllDialogs();", script, StringComparison.Ordinal);
        Assert.Contains("state.sessionGeneration++;", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Browser_contract_exposes_group_remap_cancel_invalidation_ocr_and_multi_file_controls()
    {
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "AIImport", "ai-import.js");
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AIImport", "Index.cshtml");
        Assert.Contains("remapFromEditor", script, StringComparison.Ordinal);
        Assert.Contains("candidateSourceKeys", script, StringComparison.Ordinal);
        Assert.Contains("sessionGeneration", script, StringComparison.Ordinal);
        Assert.Contains("closeAllDialogs", script, StringComparison.Ordinal);
        Assert.Contains("form.append('Files'", script, StringComparison.Ordinal);
        Assert.Contains("ocr-capability", script, StringComparison.Ordinal);
        Assert.Contains("multiple accept=", view, StringComparison.Ordinal);
        Assert.Contains("id=\"useOcr\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Ocr_settings_and_import_toggle_expose_the_locked_runtime_contract()
    {
        var settingsView = Read("CafeChain", "Areas", "Admin", "Views", "AdminSetting", "Partials", "_OcrSettings.cshtml");
        var settingsController = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminSettingController.cs");
        var importView = Read("CafeChain", "Areas", "Admin", "Views", "AIImport", "Index.cshtml");
        var importScript = Read("CafeChain", "wwwroot", "js", "Admin", "AIImport", "ai-import.js");

        Assert.Contains("value=\"@Model.Provider\" disabled", settingsView, StringComparison.Ordinal);
        Assert.Contains("Model.ProviderVersion", settingsView, StringComparison.Ordinal);
        Assert.Contains("Model.ExecutableAvailable", settingsView, StringComparison.Ordinal);
        Assert.Contains("Model.ModelDataReady", settingsView, StringComparison.Ordinal);
        Assert.Contains("<select class=\"form-select\" asp-for=\"Languages\">", settingsView, StringComparison.Ordinal);
        Assert.Contains("<option value=\"vie+eng\">", settingsView, StringComparison.Ordinal);
        Assert.Contains("<option value=\"vie\">", settingsView, StringComparison.Ordinal);
        Assert.Contains("<option value=\"eng\">", settingsView, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"ocrCapabilityText\"", importView, StringComparison.Ordinal);
        Assert.DoesNotContain("ocrCapabilityText", importScript, StringComparison.Ordinal);
        Assert.Contains("async function refreshOcrCapability()", importScript, StringComparison.Ordinal);
        Assert.Contains("window.addEventListener('focus', refreshOcrCapability)", importScript, StringComparison.Ordinal);
        Assert.Contains("Đã lưu cấu hình nhưng OCR chưa sẵn sàng", settingsController, StringComparison.Ordinal);
    }

    [Fact]
    public void Ocr_preview_contract_exposes_modes_field_provenance_and_independent_manual_review()
    {
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "AIImport", "ai-import.js");
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AIImport", "Index.cshtml");

        Assert.Contains("PDF_OCR_DETERMINISTIC", script, StringComparison.Ordinal);
        Assert.Contains("PDF_OCR_AI_EXTRACTION", script, StringComparison.Ordinal);
        Assert.Contains("PDF_MIXED_TEXT_OCR", script, StringComparison.Ordinal);
        Assert.Contains("item.fieldEvidence", script, StringComparison.Ordinal);
        Assert.Contains("evidence.ocrConfidence", script, StringComparison.Ordinal);
        Assert.Contains("issue.metadata?.resolution === 'MANUAL_REVIEW'", script, StringComparison.Ordinal);
        Assert.Contains("bằng chứng nguồn/OCR", view, StringComparison.Ordinal);
        Assert.Contains("overrideReasonGroup", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Successful_confirm_closes_the_import_workspace_and_any_host_modal()
    {
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "AIImport", "ai-import.js");

        Assert.Contains("function closeImportWorkspace(result)", script, StringComparison.Ordinal);
        Assert.Contains("byId('workspace').hidden = true;", script, StringComparison.Ordinal);
        Assert.Contains("state.session = null;", script, StringComparison.Ordinal);
        Assert.Contains("bootstrapModal.getOrCreateInstance(modal).hide();", script, StringComparison.Ordinal);
        Assert.Contains("new CustomEvent('ai-import:completed'", script, StringComparison.Ordinal);
        Assert.Contains("clearMutationState();\n            closeImportWorkspace(result);", script, StringComparison.Ordinal);
        Assert.DoesNotContain("await loadSession(state.session.sessionId);\n            clearMutationState();", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Documentation_contains_demo_accounts_credentials_and_production_warning()
    {
        var guide = Read("CafeChain", "Doc", "AI_SMART_IMPORT_IMPLEMENTATION_AND_USER_GUIDE.md");
        var rules = Read("CafeChain", "Doc", "AI_SMART_IMPORT_BUSINESS_RULES.md");
        Assert.Contains("owner@cafechain.vn", guide, StringComparison.Ordinal);
        Assert.Contains("accountantwarehouse@cafechain.vn", guide, StringComparison.Ordinal);
        Assert.Contains("The@1712", guide, StringComparison.Ordinal);
        Assert.Contains("không dùng production", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IRequestDeduplicationService.BeginScopedAsync", rules, StringComparison.Ordinal);
        Assert.Contains("StoreId = 0", rules, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(FindRoot(), Path.Combine(parts))).ReplaceLineEndings("\n");
    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !Directory.Exists(Path.Combine(current.FullName, "CafeChain"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
