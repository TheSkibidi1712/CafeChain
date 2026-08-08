using CafeChain.Application.Constants;

namespace CafeChain.Tests.POS;

public sealed class PosSessionAndLateApprovalPermissionSeedTests
{
    [Fact]
    public void Seed_enables_manager_permissions_and_excludes_shift_supervisor()
    {
        var root = FindRepositoryRoot();
        var seed = File.ReadAllText(Path.Combine(root, "CafeChain", "Scripts", "SeedAll.sql"));

        Assert.Contains(
            "(N'POS.WorkShift.ApproveLateOpen',N'Duyệt mở ca trễ',N'ApproveLateOpen',N'Duyệt, từ chối hoặc chuyển ngoài lịch cho yêu cầu mở ca trễ trên 30 phút',1)",
            seed,
            StringComparison.Ordinal);
        Assert.Contains(
            "(N'POS.Session.Manage',N'Quản lý phiên truy cập POS',N'ManagePosSession',N'Kết thúc hoặc thu hồi POS access session trong đúng StaffScope',1)",
            seed,
            StringComparison.Ordinal);
        Assert.Contains(
            $"(N'{PermissionConstants.PosWorkShiftApproveLateOpen}',1,1,1,0,0,0,0,0)",
            seed,
            StringComparison.Ordinal);
        Assert.Contains(
            $"(N'{PermissionConstants.PosSessionManage}',1,1,1,0,0,0,0,0)",
            seed,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Seed_prioritizes_shift_supervisor_for_outside_schedule_but_not_terminal_override()
    {
        var root = FindRepositoryRoot();
        var seed = File.ReadAllText(Path.Combine(root, "CafeChain", "Scripts", "SeedAll.sql"));

        Assert.Contains(
            $"(N'{PermissionConstants.PosWorkShiftApproveOutsideSchedule}',1,1,1,0,0,0,0,1)",
            seed,
            StringComparison.Ordinal);
        Assert.Contains(
            $"(N'{PermissionConstants.PosWorkShiftOverrideTerminal}',1,1,1,0,0,0,0,0)",
            seed,
            StringComparison.Ordinal);
        Assert.Contains(
            $"(N'{PermissionConstants.NotificationView}',1,1,1,1,1,0,0,1)",
            seed,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(current.FullName, "CafeChain.Tests")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
