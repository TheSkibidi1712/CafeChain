using System.Text.RegularExpressions;

namespace CafeChain.Tests;

public sealed class SeedAllPackagingUnitContractTests
{
    private static readonly int[] PackagingIngredientIds = [32, 33, 34, 35, 36, 37];

    [Fact]
    public void Packaging_inventory_uses_pieces_and_procurement_uses_cartons()
    {
        var sql = ReadSeed();

        Assert.Contains("(14, N'DEMO_CARTON'", sql, StringComparison.Ordinal);
        foreach (var ingredientId in PackagingIngredientIds)
        {
            Assert.Matches(
                new Regex($@"\({ingredientId},\s*N'DEMO_ING_[^']+',\s*N'[^']+',\s*9,\s*1\)"),
                sql);
            Assert.Matches(
                new Regex($@"\(10[2-7],\s*{ingredientId},\s*14,\s*1,\s*9,\s*(500|1000|2000),\s*1\)"),
                sql);
        }
    }

    [Fact]
    public void Packaging_supplier_offers_and_price_history_use_carton_snapshots()
    {
        var sql = ReadSeed();

        foreach (var offerId in Enumerable.Range(35, 6).Concat(Enumerable.Range(69, 6)))
        {
            Assert.Matches(
                new Regex($@"\({offerId},\s*(32|33|34|35|36|37),\s*\d+,\s*14,\s*1,"),
                sql);
            Assert.Matches(
                new Regex($@"\(\d+,\s*{offerId},\s*\d+(?:\.\d+)?,\s*1,\s*14,"),
                sql);
        }
    }

    private static string ReadSeed() => File.ReadAllText(Path.Combine(
        FindRepoRoot(), "CafeChain", "Scripts", "SeedAll.sql"));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
