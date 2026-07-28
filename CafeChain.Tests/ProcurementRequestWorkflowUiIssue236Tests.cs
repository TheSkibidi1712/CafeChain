using Xunit;

namespace CafeChain.Tests;

public sealed class ProcurementRequestWorkflowUiIssue236Tests
{
    [Fact]
    public void SubmittedRequest_ShowsWorkflowProgression_AndSourcingActions()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRestockRequests/Details.cshtml");
        var workflow = Read("CafeChain/Application/Services/Inventories/RestockRequestWorkflowService.cs");

        Assert.Contains("Tiến trình xử lý", view);
        Assert.Contains("Yêu cầu bổ sung", workflow);
        Assert.Contains("Xét nguồn cung", view);
        Assert.Contains("Đề nghị mua", view);
        Assert.Contains("Gửi nhà cung cấp", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Điều chuyển nội bộ", view);
        Assert.Contains("Mua ngoài", view);
        Assert.Contains("Sản xuất nội bộ", view);
        Assert.Contains("Bạn không có quyền xét nguồn cung cho yêu cầu này.", view);
    }

    [Fact]
    public void PurchaseDecision_CanCreateNewPa_AndAddToExistingDraftPa()
    {
        var requestView = Read("CafeChain/Areas/Admin/Views/AdminRestockRequests/Details.cshtml");
        var controller = Read("CafeChain/Areas/Admin/Controllers/AdminPurchaseAdvicesController.cs");

        Assert.Contains("Tạo PA mới", requestView);
        Assert.Contains("Thêm vào PA nháp", requestView);
        Assert.Contains("AddRestockRequestToDraft", requestView);
        Assert.Contains("AddRestockRequestToDraftAsync", controller);
        Assert.Contains("Chưa có PA liên kết", requestView);
        Assert.Contains("Chưa có đơn đặt hàng liên kết", requestView);
        Assert.DoesNotContain("Tạo phiếu nhận", requestView);
        Assert.DoesNotContain("Tạo đơn đặt hàng cho phần còn lại", requestView);
    }

    [Fact]
    public void PaForm_ShowsSupplierSelection_AndPurchaseModesAtConsolidationStep()
    {
        var details = Read("CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml");
        var consolidation = Read("CafeChain/Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml");

        Assert.Contains("Nhà cung cấp và hình thức mua", details);
        Assert.Contains("Chọn nhà cung cấp và tổng hợp", details);
        Assert.Contains("PurchaseMode", consolidation);
        Assert.Contains("Packaged", consolidation);
        Assert.Contains("Loose", consolidation);
    }

    private static string Read(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
