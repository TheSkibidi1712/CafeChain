using System.Text.RegularExpressions;

namespace CafeChain.Tests;

public sealed class SeedAllPackagingUnitContractTests
{
    private static readonly int[] PackagingIngredientIds = [32, 33, 34, 35, 36, 37];

    [Fact]
    public void Package_units_remain_in_catalog_but_are_not_physical_conversions()
    {
        var sql = ReadSeed();
        var conversionSeed = Slice(
            sql,
            "DECLARE @UnitConversionSeed TABLE",
            "SET IDENTITY_INSERT dbo.UnitConversions ON;");

        Assert.Contains("(14, N'CARTON'", sql, StringComparison.Ordinal);
        foreach (var ingredientId in PackagingIngredientIds)
        {
            Assert.Matches(
                new Regex($@"\({ingredientId},\s*N'ING_[^']+',\s*N'[^']+',\s*9,\s*1\)"),
                sql);
        }

        Assert.DoesNotMatch(
            new Regex(@"\(10[2-7],\s*\d+,\s*14,", RegexOptions.CultureInvariant),
            conversionSeed);
        Assert.DoesNotMatch(
            new Regex(@"\(\d+,\s*\d+,\s*(10|11|12|14),\s*[^,]+,\s*(1|3|9),", RegexOptions.CultureInvariant),
            conversionSeed);
        Assert.Contains("DECLARE @LegacyPackageConversion TABLE", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Packaging_supplier_offers_use_piece_content_quantities()
    {
        var sql = ReadSeed();
        var expectedOffers = new Dictionary<int, (int IngredientId, int Quantity)>
        {
            [35] = (32, 1000), [36] = (33, 1000), [37] = (34, 1000),
            [38] = (35, 1000), [39] = (36, 2000), [40] = (37, 500),
            [69] = (32, 1000), [70] = (33, 1000), [71] = (34, 1000),
            [72] = (35, 1000), [73] = (36, 2000), [74] = (37, 500)
        };

        foreach (var (offerId, expected) in expectedOffers)
        {
            Assert.Matches(
                new Regex($@"\({offerId},\s*{expected.IngredientId},\s*\d+,\s*9,\s*{expected.Quantity},"),
                sql);
        }

        Assert.Contains(
            "SET PackageQuantity=o.PackageQuantity,PackageUnitId=o.UnitId",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Representative_supplier_content_is_normalized_to_ingredient_base_units()
    {
        var sql = ReadSeed();

        Assert.Matches(new Regex(@"\(10,\s*14,\s*6,\s*1,\s*1000,"), sql); // coffee: 1 kg -> 1000 g
        Assert.Matches(new Regex(@"\(4,\s*8,\s*4,\s*3,\s*750,"), sql); // vanilla syrup: 750 ml
        Assert.Matches(new Regex(@"\(13,\s*16,\s*7,\s*3,\s*12000,"), sql); // milk carton: 12 L -> 12000 ml

        var conversionSeed = Slice(
            sql,
            "DECLARE @UnitConversionSeed TABLE",
            "SET IDENTITY_INSERT dbo.UnitConversions ON;");
        Assert.Matches(new Regex(@"\(73,\s*14,\s*2,\s*1,\s*1,\s*1000,\s*1\)"), conversionSeed);
        Assert.Matches(new Regex(@"\(75,\s*16,\s*4,\s*1,\s*3,\s*1000,\s*1\)"), conversionSeed);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0, $"Start marker not found: {startMarker}");
        Assert.True(end > start, $"End marker not found: {endMarker}");
        return source[start..end];
    }

    private static string ReadSeed() => File.ReadAllText(Path.Combine(
        FindRepoRoot(), "CafeChain", "Scripts", "SeedAll.sql"));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
