namespace CafeChain.Tests;

public sealed class PreparedItemTargetStockMigrationTests
{
    private static string ReadMigration()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var path = Directory.GetFiles(
                Path.Combine(root, "CafeChain", "Migrations"),
                "*_AddPreparedItemTargetStockLevel.cs")
            .Single(file => !file.EndsWith(".Designer.cs", StringComparison.Ordinal));
        return File.ReadAllText(path);
    }

    [Fact]
    public void ExistingRows_TargetStock_RemainsNull()
    {
        var migration = ReadMigration();

        Assert.Contains("AddColumn<decimal>", migration, StringComparison.Ordinal);
        Assert.Contains("nullable: true", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateData", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("Sql(", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("defaultValue:", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_ContainsOnlyApprovedTargetStockSchema()
    {
        var migration = ReadMigration();
        var up = migration.Split("protected override void Down")[0];

        Assert.Equal(1, Count(up, "migrationBuilder.AddColumn"));
        Assert.Equal(2, Count(up, "migrationBuilder.AddCheckConstraint"));
        Assert.DoesNotContain("CreateTable", up, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateIndex", up, StringComparison.Ordinal);
        Assert.Contains("decimal(18,3)", up, StringComparison.Ordinal);
        Assert.Contains("TargetStockLevel] >= [MinStockLevel]", up, StringComparison.Ordinal);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
