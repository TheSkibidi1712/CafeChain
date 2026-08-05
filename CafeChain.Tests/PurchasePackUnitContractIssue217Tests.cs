using CafeChain.Application.Services.Inventories;
using Xunit;

namespace CafeChain.Tests;

public sealed class PurchasePackUnitContractIssue217Tests
{
    [Theory]
    [InlineData(100, 1000, 1, 1000, 900)]
    [InlineData(2300, 1000, 3, 3000, 700)]
    [InlineData(1400, 1000, 2, 2000, 600)]
    public void RemainingBaseQuantity_RoundsUpPerStoreToWholePackage(
        decimal remaining,
        decimal packageBase,
        int expectedPackageCount,
        decimal expectedOrdered,
        decimal expectedSurplus)
    {
        var success = PurchasePackMath.TryPlan(remaining, packageBase, out var plan);

        Assert.True(success);
        Assert.Equal(expectedPackageCount, plan.PackageCount);
        Assert.Equal(remaining, plan.DemandCoveredBaseQuantity);
        Assert.Equal(expectedOrdered, plan.OrderedBaseQuantity);
        Assert.Equal(expectedSurplus, plan.RoundingSurplusBaseQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.1)]
    [InlineData(1.5)]
    public void PackageCountMustBePositiveInteger(decimal packageCount) =>
        Assert.False(PurchasePackMath.IsWholePackageCount(packageCount));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    public void WholePackageCountIsAccepted(decimal packageCount) =>
        Assert.True(PurchasePackMath.IsWholePackageCount(packageCount));

    [Fact]
    public void PurchaseOrderCreate_UsesReadOnlyRequestReferenceAndIntegerPackageInput()
    {
        var view = ReadRepoFile("CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml");

        Assert.Contains("asp-for=\"Lines[i].RestockRequestId\" type=\"hidden\"", view);
        Assert.Contains("AdminStatusDisplay.RestockReference", view);
        Assert.DoesNotContain("Yêu cầu nhập hàng #", view);
        Assert.Contains("asp-for=\"Lines[i].PackageCount\" type=\"number\" min=\"1\" step=\"1\"", view);
        Assert.DoesNotContain("placeholder=\"Mã yêu cầu\"", view);
        Assert.DoesNotContain("step=\"0.001\" class=\"ops-input w-100\" required", view);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
