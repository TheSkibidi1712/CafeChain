using CafeChain.Application.Constants;
using CafeChain.ViewModels.Admin.Shared;
using Xunit;

namespace CafeChain.Tests;

public sealed class ProcurementUiTerminologyTests
{
    public static TheoryData<string, string> PurchaseAdviceStatusCases => new()
    {
        { PurchaseAdviceStatuses.Draft, "Nháp" },
        { PurchaseAdviceStatuses.Submitted, "Đã gửi" },
        { PurchaseAdviceStatuses.UnderReview, "Đang xem xét" },
        { PurchaseAdviceStatuses.Allocated, "Đã đưa vào đơn" },
        { PurchaseAdviceStatuses.Rejected, "Đã từ chối" },
        { PurchaseAdviceStatuses.Cancelled, "Đã hủy" },
        { "PARTIALLY_ALLOCATED", "Đã đưa vào đơn một phần" },
        { "FULLY_ALLOCATED", "Đã đưa vào đơn" },
        { "PARTIALLY_FULFILLED", "Đã nhận một phần" },
        { "COMPLETED", "Hoàn thành" }
    };

    public static TheoryData<string, string> PurchaseAdvicePriorityCases => new()
    {
        { "LOW", "Thấp" },
        { PurchaseAdvicePriorities.Normal, "Bình thường" },
        { PurchaseAdvicePriorities.High, "Cao" },
        { PurchaseAdvicePriorities.Urgent, "Khẩn cấp" }
    };

    public static TheoryData<string, string> BatchStatusCases => new()
    {
        { PurchaseOrderBatchStatuses.Draft, "Nháp" },
        { PurchaseOrderBatchStatuses.PendingApproval, "Chờ duyệt" },
        { PurchaseOrderBatchStatuses.Approved, "Đã duyệt" },
        { PurchaseOrderBatchStatuses.PdfGenerated, "Đã tạo PDF" },
        { PurchaseOrderBatchStatuses.SentToSupplier, "Đã gửi Nhà cung cấp" },
        { "SUPPLIER_CONFIRMED", "Nhà cung cấp đã xác nhận" },
        { PurchaseOrderBatchStatuses.PartiallyReceived, "Đã nhận một phần" },
        { PurchaseOrderBatchStatuses.Completed, "Hoàn thành" },
        { PurchaseOrderBatchStatuses.Cancelled, "Đã hủy" }
    };

    public static TheoryData<string, string> PurchaseOrderStatusCases => new()
    {
        { PurchaseOrderStatuses.Draft, "Nháp" },
        { PurchaseOrderStatuses.Approved, "Đã duyệt" },
        { PurchaseOrderStatuses.MarkedAsSent, "Đã gửi Nhà cung cấp" },
        { "SENT", "Đã gửi Nhà cung cấp" },
        { PurchaseOrderStatuses.PartiallyReceived, "Đã nhận một phần" },
        { PurchaseOrderStatuses.Completed, "Hoàn thành" },
        { PurchaseOrderStatuses.Cancelled, "Đã hủy" }
    };

    public static TheoryData<string, string> PdfRevisionStatusCases => new()
    {
        { PurchaseOrderBatchDocumentStatuses.Generated, "Sẵn sàng gửi" },
        { PurchaseOrderBatchDocumentStatuses.Sent, "Đã gửi" },
        { PurchaseOrderBatchDocumentStatuses.Superseded, "Đã thay thế" }
    };

    public static TheoryData<string, string> SendChannelCases => new()
    {
        { PurchaseOrderBatchDocumentChannels.ZaloManual, "Zalo" },
        { PurchaseOrderBatchDocumentChannels.EmailManual, "Email" }
    };

    public static TheoryData<string, string> InventoryTransactionTypeCases => new()
    {
        { "IMPORT", "Nhập kho" },
        { "EXPORT", "Xuất kho" },
        { "WASTE", "Hao hụt" },
        { "STOCK_TAKE", "Kiểm kê" },
        { "PRODUCTION_IN", "Nhập từ sản xuất" },
        { "PRODUCTION_OUT", "Xuất cho sản xuất" },
        { "SALES_DEDUCTION", "Trừ tồn bán hàng" },
        { "ADJUSTMENT_IN", "Điều chỉnh tăng" },
        { "ADJUSTMENT_OUT", "Điều chỉnh giảm" },
        { "OUT_TRANSFER", "Xuất điều chuyển" },
        { "IN_TRANSFER", "Nhập điều chuyển" },
        { "CONSOLIDATION_OUT", "Chuyển khỏi dòng nguồn" },
        { "CONSOLIDATION_IN", "Chuyển vào dòng chuẩn" },
        { "BRANCH_RECEIPT_IN", "Nhập từ phiếu nhận hàng" },
        { "SALES_RETURN", "Nhập hoàn hàng bán" }
    };

    public static TheoryData<string, string> RestockSourceCases => new()
    {
        { RestockFulfillmentDocumentTypes.BranchReceipt, "Phiếu nhận hàng" },
        { RestockFulfillmentDocumentTypes.InventoryTransfer, "Phiếu điều chuyển" },
        { RestockFulfillmentSourceTypes.Supplier, "Nhà cung cấp" },
        { RestockFulfillmentSourceTypes.Manual, "Ghi nhận thủ công" },
        { RestockFulfillmentStatuses.Planned, "Dự kiến" },
        { RestockFulfillmentStatuses.Linked, "Đã liên kết" },
        { RestockFulfillmentStatuses.Received, "Đã nhận" },
        { RestockFulfillmentStatuses.Cancelled, "Đã hủy" }
    };

    [Theory]
    [MemberData(nameof(PurchaseAdviceStatusCases))]
    public void AllPurchaseAdviceStatuses_HaveVietnameseLabels(string value, string expected) =>
        Assert.Equal(expected, AdminStatusDisplay.PurchaseAdvice(value).Label);

    [Theory]
    [MemberData(nameof(PurchaseAdvicePriorityCases))]
    public void AllPurchaseAdvicePriorities_HaveVietnameseLabels(string value, string expected) =>
        Assert.Equal(expected, AdminStatusDisplay.PurchaseAdvicePriority(value).Label);

    [Theory]
    [MemberData(nameof(BatchStatusCases))]
    public void AllBatchStatuses_HaveVietnameseLabels(string value, string expected) =>
        Assert.Equal(expected, AdminStatusDisplay.PurchaseOrderBatch(value).Label);

    [Theory]
    [MemberData(nameof(PurchaseOrderStatusCases))]
    public void AllPurchaseOrderStatuses_HaveVietnameseLabels(string value, string expected) =>
        Assert.Equal(expected, AdminStatusDisplay.PurchaseOrder(value).Label);

    [Theory]
    [MemberData(nameof(PdfRevisionStatusCases))]
    public void AllPdfRevisionStatuses_HaveVietnameseLabels(string value, string expected) =>
        Assert.Equal(expected, AdminStatusDisplay.PurchaseOrderBatchDocument(value).Label);

    [Theory]
    [MemberData(nameof(SendChannelCases))]
    public void AllSendChannels_HaveVietnameseLabels(string value, string expected) =>
        Assert.Equal(expected, AdminStatusDisplay.PurchaseOrderBatchDocumentChannel(value));

    [Theory]
    [MemberData(nameof(InventoryTransactionTypeCases))]
    public void AllInventoryTransactionTypes_HaveVietnameseLabels(string value, string expected) =>
        Assert.Equal(expected, AdminStatusDisplay.InventoryTransactionType(value));

    [Theory]
    [MemberData(nameof(RestockSourceCases))]
    public void RestockSourcesAndStatuses_HaveVietnameseLabels(string value, string expected)
    {
        var labels = new[]
        {
            AdminStatusDisplay.RestockFulfillmentDocumentType(value),
            AdminStatusDisplay.RestockFulfillmentSource(value),
            AdminStatusDisplay.RestockFulfillmentStatus(value)
        };

        Assert.Contains(expected, labels);
    }

    [Fact]
    public void UnknownEnum_DoesNotExposeRawValue()
    {
        const string raw = "NEW_INTERNAL_STATUS";

        Assert.Equal("Không xác định", AdminStatusDisplay.PurchaseAdvice(raw).Label);
        Assert.Equal("Không xác định", AdminStatusDisplay.PurchaseAdvicePriority(raw).Label);
        Assert.Equal("Không xác định", AdminStatusDisplay.PurchaseOrderBatch(raw).Label);
        Assert.Equal("Không xác định", AdminStatusDisplay.PurchaseOrder(raw).Label);
        Assert.Equal("Không xác định", AdminStatusDisplay.PurchaseOrderBatchDocument(raw).Label);
        Assert.Equal("Không xác định", AdminStatusDisplay.PurchaseOrderBatchDocumentChannel(raw));
        Assert.Equal("Không xác định", AdminStatusDisplay.RestockRequest(raw).Label);
        Assert.Equal("Không xác định", AdminStatusDisplay.RestockRequestPriority(raw).Label);
        Assert.Equal("Không xác định", AdminStatusDisplay.RestockFulfillmentDocumentType(raw));
        Assert.Equal("Không xác định", AdminStatusDisplay.RestockFulfillmentSource(raw));
        Assert.Equal("Không xác định", AdminStatusDisplay.RestockFulfillmentStatus(raw));
        Assert.Equal("Không xác định", AdminStatusDisplay.InventoryTransactionType(raw));
    }

    [Fact]
    public void VietnameseDate_UsesDdMmYyyy()
    {
        var value = new DateTime(2026, 7, 19, 3, 4, 0);

        Assert.Equal("19/07/2026", AdminStatusDisplay.Date(value));
        Assert.Equal("19/07/2026 03:04", AdminStatusDisplay.DateTime(value));
    }

    [Theory]
    [InlineData(50, "50")]
    [InlineData(50.5, "50,5")]
    [InlineData(0.125, "0,125")]
    public void Quantity_UsesVietnameseDecimalFormatting(decimal value, string expected) =>
        Assert.Equal(expected, AdminStatusDisplay.Quantity(value));

    [Fact]
    public void Currency_UsesVndFormatting() =>
        Assert.Equal("648.000 ₫", AdminStatusDisplay.Currency(648000m));

    [Fact]
    public void MixedUnitAdvice_DoesNotShowInvalidTotalQuantity()
    {
        var view = ReadRepoFile("CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml");

        Assert.DoesNotContain("TotalRequestedBaseQuantity", view);
        Assert.DoesNotContain("Tổng SL", view);
        Assert.Contains("Số dòng đề nghị", view);
    }

    [Fact]
    public void SourceReference_UsesLocalizedDocumentName()
    {
        var views = ReadProcurementViews();

        Assert.Contains("Yêu cầu nhập hàng #", views);
        Assert.DoesNotContain("Restock #", views);
    }

    [Fact]
    public void ProcurementViews_DoNotExposeKnownRawLabels()
    {
        var views = ReadProcurementViews();

        Assert.DoesNotContain(">NORMAL<", views);
        Assert.DoesNotContain(">SUBMITTED<", views);
        Assert.DoesNotContain(">UNDER_REVIEW<", views);
        Assert.DoesNotContain("PA chờ tổng hợp", views);
        Assert.DoesNotContain("Batch đơn mua", views);
        Assert.DoesNotContain("Child PO", views);
        Assert.DoesNotContain("Supplier Offer", views);
        Assert.DoesNotContain(">SUPERSEDED<", views);
        Assert.DoesNotContain("@revision.SentChannel", views);
    }

    [Fact]
    public void ProcurementNavigation_UsesStandardLabels()
    {
        var layout = ReadRepoFile("CafeChain/Areas/Admin/Views/Shared/_AdminLayout.cshtml");

        Assert.Contains("Tổng hợp đề nghị mua", layout);
        Assert.Contains("Đơn đặt hàng gộp", layout);
        Assert.Contains("> Đơn đặt hàng</a>", layout);
    }

    private static string ReadProcurementViews() => string.Join('\n', new[]
    {
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml",
        "CafeChain/Areas/Admin/Views/AdminRestockRequests/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminRestockRequests/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminStockAlerts/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminStockAlerts/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminSupplierQuality/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminSupplierQuality/Create.cshtml"
    }.Select(ReadRepoFile));

    private static string ReadRepoFile(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
