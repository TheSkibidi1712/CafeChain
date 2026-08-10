using Xunit;

namespace CafeChain.Tests;

public sealed class OperationalIceScheduleLinkUiContractTests
{
    [Fact]
    public void ScheduleCreate_PostsActualStaffShiftIdentities()
    {
        var index = Read("CafeChain/Areas/Admin/Views/AdminOperationalIce/Index.cshtml");

        Assert.Contains("name = 'SourceStaffShiftIds'", index, StringComparison.Ordinal);
        Assert.Contains("schedule.staffShiftIds", index, StringComparison.Ordinal);
        Assert.Contains("cùng giờ hiệu lực", index, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroCandidateUi_RendersVietnameseMessagesWithoutMachineReasonCodes()
    {
        var controller = Read("CafeChain/Areas/Admin/Controllers/AdminOperationalIceController.cs");
        var details = Read("CafeChain/Areas/Admin/Views/AdminOperationalIce/Details.cshtml");

        Assert.Contains(".Select(x => x.Message)", controller, StringComparison.Ordinal);
        Assert.Contains("WorkShiftCandidateMessages", details, StringComparison.Ordinal);
        Assert.DoesNotContain("ReasonCode", details, StringComparison.Ordinal);
        Assert.DoesNotContain("CANDIDATE_", details, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalIceUi_UsesConfiguredTimezoneWithoutHostLocalConversion()
    {
        var files = new[]
        {
            "CafeChain/Areas/Admin/Controllers/AdminOperationalIceController.cs",
            "CafeChain/Areas/Admin/Views/AdminOperationalIce/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminOperationalIce/Details.cshtml",
            "CafeChain/Areas/Admin/Views/AdminOperationalIce/Report.cshtml"
        };
        var combined = string.Join('\n', files.Select(Read));

        Assert.Contains("ResolveTimeZone()", combined, StringComparison.Ordinal);
        Assert.DoesNotContain(".ToLocalTime()", combined, StringComparison.Ordinal);
        Assert.DoesNotContain(".ToUniversalTime()", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", combined, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
               && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
            directory = directory.Parent;
        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
