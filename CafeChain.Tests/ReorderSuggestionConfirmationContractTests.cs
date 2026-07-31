using CafeChain.Application.DTOs.Admin.Procurement;

namespace CafeChain.Tests;

public sealed class ReorderSuggestionConfirmationContractTests
{
    [Fact]
    public void Confirm_request_does_not_accept_calculated_business_values_from_client()
    {
        var properties = typeof(ConfirmReorderSuggestionRequest)
            .GetProperties()
            .Select(x => x.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["IngredientId", "RequestKey", "StoreId", "SuggestionToken"],
            properties);
    }

    [Fact]
    public void Confirm_boundary_keeps_permission_scope_revalidation_lock_and_idempotency()
    {
        var root = FindRepoRoot();
        var controller = File.ReadAllText(Path.Combine(
            root,
            "CafeChain",
            "Areas",
            "Admin",
            "Controllers",
            "AdminReorderSuggestionsController.cs"));
        var service = File.ReadAllText(Path.Combine(
            root,
            "CafeChain",
            "Application",
            "Services",
            "Inventories",
            "ReorderSuggestionConfirmationService.cs"));

        Assert.Contains("[ValidateAntiForgeryToken]", controller, StringComparison.Ordinal);
        Assert.Contains(
            "[RequirePermission(PermissionConstants.RestockCreate)]",
            controller,
            StringComparison.Ordinal);
        Assert.Contains("_storeScopeResolver.ResolveAsync", controller, StringComparison.Ordinal);
        Assert.Contains("REORDER_SUGGESTION_CONFIRM", service, StringComparison.Ordinal);
        Assert.Contains("_deduplication.BeginAsync", service, StringComparison.Ordinal);
        Assert.Contains("_authorization.CanConfirmAsync", service, StringComparison.Ordinal);
        Assert.Contains("_repository.AcquireIngredientLockAsync", service, StringComparison.Ordinal);
        Assert.Contains("_suggestions.CalculateForStoreAsync", service, StringComparison.Ordinal);
        Assert.Contains("_tokens.ComputeDecisionFingerprint", service, StringComparison.Ordinal);
        Assert.Contains("_deduplication.MarkSuccessAsync", service, StringComparison.Ordinal);
        Assert.Contains("_repository.CommitTransactionAsync", service, StringComparison.Ordinal);
        Assert.Contains("_repository.RollbackTransactionAsync", service, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
               && !File.Exists(Path.Combine(
                   directory.FullName,
                   "CafeChain",
                   "CafeChain.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Không tìm thấy repo root.");
    }
}
