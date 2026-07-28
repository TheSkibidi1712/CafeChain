using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Loyalties;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Tests;

public sealed class ModelAndSeedCoverageV17Tests
{
    [Fact]
    public void Point_transaction_uses_the_customer_collection_without_a_shadow_fk()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(PointTransaction));

        Assert.NotNull(entity);
        Assert.Null(entity!.FindProperty("CustomerId1"));

        var customerNavigation = entity.FindNavigation(nameof(PointTransaction.Customer));
        Assert.NotNull(customerNavigation);
        Assert.Equal(nameof(PointTransaction.CustomerId),
            Assert.Single(customerNavigation!.ForeignKey.Properties).Name);
        Assert.Equal(nameof(CafeChain.Models.Customers.Customer.PointTransactions),
            customerNavigation.Inverse?.Name);
    }

    [Theory]
    [InlineData(typeof(Recipe), nameof(Recipe.YieldPercentage))]
    [InlineData(typeof(Order), nameof(Order.ShippingFee))]
    [InlineData(typeof(TransactionLog), nameof(TransactionLog.Amount))]
    public void Financial_and_yield_decimals_have_explicit_schema_precision(
        Type entityType,
        string propertyName)
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(entityType)!.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(18, property!.GetPrecision());
        Assert.Equal(2, property.GetScale());
    }

    [Fact]
    public void SeedAll_classifies_empty_tables_and_contains_receipt_idempotency_contracts()
    {
        var sql = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "Scripts", "SeedAll.sql"));

        Assert.Contains("$(TargetDatabase)", sql, StringComparison.Ordinal);
        Assert.Contains("DEMO_COVERAGE_V16_RECEIPTS", sql, StringComparison.Ordinal);
        Assert.Contains("DEMO_COVERAGE_V17", sql, StringComparison.Ordinal);
        Assert.Contains("PurchaseOrderReceiptPostings", sql, StringComparison.Ordinal);
        Assert.Contains("RestockFulfillmentPostings", sql, StringComparison.Ordinal);
        Assert.Contains("SourceBranchReceiptLineId", sql, StringComparison.Ordinal);
        Assert.Contains("SeedAll has unclassified empty business tables", sql, StringComparison.Ordinal);
        Assert.Contains("OtpChallenges", sql, StringComparison.Ordinal);
        Assert.Contains("PasswordResetOtps", sql, StringComparison.Ordinal);
        Assert.Contains("SupplierDuplicateWarnings", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Shadow_fk_migration_aborts_on_conflict_before_dropping_the_column()
    {
        var migration = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "Migrations",
            "20260727150613_ModelDataIntegrityRefactor.cs"));

        var conflictGuard = migration.IndexOf("CustomerId1 <> CustomerId", StringComparison.Ordinal);
        var dropColumn = migration.IndexOf("DropColumn", StringComparison.Ordinal);

        Assert.True(conflictGuard >= 0);
        Assert.True(dropColumn > conflictGuard);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the CafeChain repository root.");
    }
}
