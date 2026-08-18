using System;
using System.IO;
using Xunit;

namespace CafeChain.Tests;

public sealed class PreparedItemInventoryFinalClosureTests
{
    [Fact]
    public void CanonicalPreparedItemInventory_DoesNotShowLegacyPrimaryWarning()
    {
        var view = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "CafeChain",
            "Areas",
            "Admin",
            "Views",
            "AdminStoreInventory",
            "Partials",
            "_InventoryTablePartial.cshtml"));

        Assert.DoesNotContain("bán thành phẩm vẫn là định danh tồn kho chính", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Chưa xác nhận số lượng tồn theo đơn vị chuẩn.", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Chưa xác nhận số lượng theo đơn vị tồn kho.", view, StringComparison.Ordinal);
    }

    [Fact]
    public void SeededPreparedItemInventory_IsCanonical_AndUsesBaseUom()
    {
        var seed = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Scripts", "SeedAll.sql"));

        Assert.Contains("SELECT x.StoreInventoryId,1,NULL,NULL,x.PreparedItemId,1,1,NULL", seed, StringComparison.Ordinal);
        Assert.Contains("si.StoreId=1 AND si.PreparedItemId=x.PreparedItemId", seed, StringComparison.Ordinal);
        Assert.Contains("OR si.RecipeId IS NOT NULL OR si.PreparedItemId<>x.PreparedItemId", seed, StringComparison.Ordinal);
        Assert.DoesNotContain("si.StoreId=1 AND si.RecipeId=x.RecipeId", seed, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyRecipeEvidence_RemainsTraceable_InSeedTransactions()
    {
        var seed = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Scripts", "SeedAll.sql"));

        Assert.Contains("SourceRecipeId", seed, StringComparison.Ordinal);
        Assert.Contains("x.PreparedItemId,x.RecipeId,x.OpeningQty", seed, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CafeChain.slnx"))
                || File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Không tìm thấy thư mục gốc repository.");
    }
}
