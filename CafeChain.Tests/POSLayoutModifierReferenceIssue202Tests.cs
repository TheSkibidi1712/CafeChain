using System;
using System.IO;
using Xunit;

namespace CafeChain.Tests;

public sealed class POSLayoutModifierReferenceIssue202Tests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ModifierState_UsesDeclaredActiveModifierAcrossAddEditAndResponsiveFiltering()
    {
        var layout = File.ReadAllText(Path.Combine(RepoRoot, "CafeChain.Frontend", "src", "POSLayout.tsx"));
        var css = File.ReadAllText(Path.Combine(RepoRoot, "CafeChain.Frontend", "src", "index.css"));

        Assert.DoesNotContain("activeItemForModifiers", layout);
        Assert.Contains("const [activeModifier, setActiveModifier] = useState<ActiveModifier | null>(null)", layout);
        Assert.Contains("setActiveModifier({ item })", layout);
        Assert.Contains("applyModifierSelection(activeModifier.item, selection, activeModifier.editingCartId)", layout);
        Assert.Contains("const [cart, setCart] = useState<CartItem[]>([])", layout);
        Assert.Contains("const filteredItems = useMemo", layout);
        Assert.Contains("setSelectedCategory", layout);
        Assert.Contains("setSearchQuery", layout);
        Assert.Contains("@media (max-width: 819px)", css);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "CafeChain.Frontend"))
                && Directory.Exists(Path.Combine(current.FullName, "CafeChain.Tests")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc CafeChain.");
    }
}
