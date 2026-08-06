using CafeChain.Application.Constants;
using Xunit;

namespace CafeChain.Tests;

public sealed class OperationalIceVietnameseLocalizationIssue303Tests
{
    [Theory]
    [InlineData(OperationalIceStatuses.Draft, "Bản nháp")]
    [InlineData(OperationalIceStatuses.Open, "Đang mở")]
    [InlineData(OperationalIceStatuses.PendingApproval, "Chờ duyệt")]
    [InlineData(OperationalIceStatuses.ReconciliationRequired, "Cần đối soát")]
    [InlineData(OperationalIceStatuses.Closed, "Đã đóng")]
    [InlineData(OperationalIceStatuses.Cancelled, "Đã hủy")]
    public void AllOperationalShiftStatuses_HaveVietnameseLabels(string status, string expected)
    {
        Assert.Equal(expected, OperationalIceDisplayText.Status(status));
    }

    [Fact]
    public void UnknownStatus_UsesSafeVietnameseFallback()
    {
        Assert.Equal("Không xác định", OperationalIceDisplayText.Status("FutureState"));
        Assert.Equal("Không xác định", OperationalIceDisplayText.WorkShiftStatus("FutureState"));
        Assert.Equal("Không xác định", OperationalIceDisplayText.CreationSource("FutureSource"));
    }

    [Fact]
    public void OperationalIceStatusAndActionLabels_AreVietnamese()
    {
        Assert.Equal("Tạo thủ công", OperationalIceDisplayText.CreationSource(OperationalIceCreationSources.Manual));
        Assert.Equal("Tạo từ lịch làm việc", OperationalIceDisplayText.CreationSource(OperationalIceCreationSources.StaffSchedule));
        Assert.Equal("Chờ duyệt", OperationalIceDisplayText.SupplementStatus(IceSupplementalIssueStatuses.Pending));
        Assert.Equal("Đã xác nhận", OperationalIceDisplayText.CarryOverStatus(IceCarryOverStatuses.Confirmed));
        Assert.Equal("Xuất kho do chênh lệch đá", OperationalIceDisplayText.PostingType(IcePostingTypes.VarianceOut));
    }

    [Fact]
    public void OperationalIceBusinessErrors_AreVietnamese()
    {
        Assert.Equal(
            "Ca bán hàng POS này đã được liên kết với một ca đá khác.",
            OperationalIceDisplayText.ErrorMessage(OperationalIceErrorCodes.WorkShiftAlreadyLinked, null));
        Assert.Equal(
            "Dữ liệu vừa được người khác cập nhật. Vui lòng kiểm tra lại trước khi tiếp tục.",
            OperationalIceDisplayText.ErrorMessage(OperationalIceErrorCodes.ConcurrencyConflict, null));
        Assert.Equal(
            "Không thể xử lý yêu cầu lúc này. Vui lòng thử lại.",
            OperationalIceDisplayText.ErrorMessage("UNKNOWN", null));
        Assert.Equal(
            "Trạng thái hiện tại không cho phép thao tác này.",
            OperationalIceDisplayText.ErrorMessage(OperationalIceErrorCodes.InvalidState, "Invalid state for WorkShift"));
    }

    [Fact]
    public void OperationalIcePages_DoNotRenderRawEnglishStatusOrTechnicalLabels()
    {
        var combined = string.Join('\n',
            Read("CafeChain/Areas/Admin/Views/AdminOperationalIce/Index.cshtml"),
            Read("CafeChain/Areas/Admin/Views/AdminOperationalIce/Details.cshtml"),
            Read("CafeChain/Areas/Admin/Views/AdminOperationalIce/Report.cshtml"));

        Assert.DoesNotContain(">WorkShift POS<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Chưa liên kết WorkShift POS", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Carry-over<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Idempotency key<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Posting<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Audit<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("_ => status", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("@shift.Status", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("@carry.Status", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("@posting.PostingType", combined, StringComparison.Ordinal);
        Assert.Contains("OperationalIceDisplayText.Status", combined, StringComparison.Ordinal);
        Assert.Contains("Tiêu hao lý thuyết POS", combined, StringComparison.Ordinal);
        Assert.Contains("Tiêu hao thực tế", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalIcePdf_UsesVietnameseDisplayMapper()
    {
        var source = Read("CafeChain/Application/Services/Inventories/OperationalIceReportPdfRenderer.cs");

        Assert.Contains("OperationalIceDisplayText.Status", source, StringComparison.Ordinal);
        Assert.Contains("OperationalIceDisplayText.PostingType", source, StringComparison.Ordinal);
        Assert.Contains("Ca bán hàng POS", source, StringComparison.Ordinal);
        Assert.Contains("Mã chống trùng lặp", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkShift POS", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FIFO/ledger", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OperationalIcePages_DoNotRenderEnumNames()
    {
        var index = Read("CafeChain/Areas/Admin/Views/AdminOperationalIce/Index.cshtml");
        var details = Read("CafeChain/Areas/Admin/Views/AdminOperationalIce/Details.cshtml");

        Assert.DoesNotContain("_ => status", index, StringComparison.Ordinal);
        Assert.DoesNotContain("_ => status", details, StringComparison.Ordinal);
        Assert.Contains("OperationalIceDisplayText.Status", index, StringComparison.Ordinal);
        Assert.Contains("OperationalIceDisplayText.Status", details, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalIcePages_DoNotRenderPropertyNames()
    {
        var combined = Read("CafeChain/Areas/Admin/Views/AdminOperationalIce/Index.cshtml")
            + Read("CafeChain/Areas/Admin/Views/AdminOperationalIce/Details.cshtml");

        Assert.DoesNotContain(">BusinessDate<", combined, StringComparison.Ordinal);
        Assert.DoesNotContain(">SourceScheduleShiftId<", combined, StringComparison.Ordinal);
        Assert.DoesNotContain(">PhysicalQuantity<", combined, StringComparison.Ordinal);
        Assert.DoesNotContain(">ReservedQuantity<", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalIcePages_DoNotRenderTechnicalExceptions()
    {
        var controller = Read("CafeChain/Areas/Admin/Controllers/AdminOperationalIceController.cs");

        Assert.Contains("OperationalIceDisplayText.ErrorMessage", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalIceAccessibilityLabels_AreVietnamese()
    {
        var combined = Read("CafeChain/Areas/Admin/Views/AdminOperationalIce/Index.cshtml")
            + Read("CafeChain/Areas/Admin/Views/AdminOperationalIce/Details.cshtml")
            + Read("CafeChain/Areas/Admin/Views/AdminOperationalIce/Report.cshtml");

        Assert.DoesNotContain("Close dialog", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Open actions", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tiến trình ca vận hành đá", combined, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
