using System.Globalization;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CafeChain.Application.Services.Inventories;

public sealed class PurchaseOrderBatchPdfRenderer : IPurchaseOrderBatchPdfRenderer
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public byte[] Render(PurchaseOrderBatchDocumentSnapshot snapshot, int revisionNumber, DateTime generatedAtUtc, string contentHash)
    {
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontSize(9).FontColor("#292524"));
            page.Header().Element(container => Header(container, snapshot, revisionNumber));
            page.Content().PaddingVertical(16).Column(column => Content(column, snapshot));
            page.Footer().Row(row =>
            {
                row.RelativeItem().Text($"Tạo lúc {generatedAtUtc:dd/MM/yyyy HH:mm} UTC · R{revisionNumber} · {contentHash[..12]}")
                    .FontSize(7).FontColor("#78716C");
                row.AutoItem().DefaultTextStyle(style => style.FontSize(7).FontColor("#78716C")).Text(text =>
                {
                    text.Span("Trang ");
                    text.CurrentPageNumber();
                    text.Span("/");
                    text.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Header(IContainer container, PurchaseOrderBatchDocumentSnapshot snapshot, int revisionNumber)
    {
        container.BorderBottom(1).BorderColor("#D6D3D1").PaddingBottom(12).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("CAFECHAIN").FontSize(16).Bold().FontColor("#9A3412");
                column.Item().Text("ĐƠN ĐẶT HÀNG GỘP").FontSize(18).Bold().FontColor("#1C1917");
            });
            row.AutoItem().AlignRight().Column(column =>
            {
                column.Item().Text(snapshot.BatchNumber).Bold();
                column.Item().Text($"Revision R{revisionNumber}").FontColor("#9A3412");
            });
        });
    }

    private static void Content(ColumnDescriptor column, PurchaseOrderBatchDocumentSnapshot snapshot)
    {
        column.Spacing(14);
        column.Item().Row(row =>
        {
            row.RelativeItem().Element(InfoBox).Column(info =>
            {
                info.Item().Text("NHÀ CUNG CẤP").Bold().FontColor("#9A3412");
                info.Item().Text(snapshot.Supplier.Name).Bold();
                info.Item().Text($"MST: {Value(snapshot.Supplier.TaxCode)}");
                info.Item().Text($"Địa chỉ: {Value(snapshot.Supplier.Address)}");
                info.Item().Text($"Liên hệ: {Value(snapshot.Supplier.ContactName)} · {Value(snapshot.Supplier.ContactPhone)}");
                info.Item().Text($"Email: {Value(snapshot.Supplier.ContactEmail)}");
            });
            row.ConstantItem(12);
            row.RelativeItem().Element(InfoBox).Column(info =>
            {
                info.Item().Text("THÔNG TIN ĐẶT HÀNG").Bold().FontColor("#9A3412");
                info.Item().Text($"Ngày lập: {snapshot.CreatedAtUtc:dd/MM/yyyy}");
                info.Item().Text($"Người lập: {Value(snapshot.CreatedByName)}");
                info.Item().Text($"Người duyệt: {Value(snapshot.ApprovedByName)}");
                info.Item().Text($"Ngày giao: {snapshot.ExpectedDeliveryFrom:dd/MM/yyyy} - {snapshot.ExpectedDeliveryTo:dd/MM/yyyy}");
            });
        });

        column.Item().Text("TỔNG HỢP ĐƠN HÀNG").FontSize(11).Bold();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.8f);
                columns.RelativeColumn(1.7f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1.5f);
            });
            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Nguyên liệu").Bold();
                header.Cell().Element(HeaderCell).Text("Quy cách").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Số gói").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Đơn giá").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Thành tiền").Bold();
            });
            foreach (var line in snapshot.Lines)
            {
                table.Cell().Element(BodyCell).Text(line.IngredientName);
                table.Cell().Element(BodyCell).Text($"{Quantity(line.PackageQuantity)} {line.PackageUnitName}");
                table.Cell().Element(BodyCell).AlignRight().Text(Quantity(line.PackageCount));
                table.Cell().Element(BodyCell).AlignRight().Text(Money(line.PackagePrice));
                table.Cell().Element(BodyCell).AlignRight().Text(Money(line.LineTotal));
            }
        });
        column.Item().AlignRight().Text($"TỔNG GIÁ TRỊ: {Money(snapshot.TotalAmount)} {snapshot.Currency}")
            .FontSize(12).Bold().FontColor("#9A3412");

        foreach (var store in snapshot.Stores)
        {
            column.Item().BorderTop(1).BorderColor("#D6D3D1").PaddingTop(10).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text($"GIAO TẠI {store.StoreName.ToUpperInvariant()} · {store.PurchaseOrderCode}").Bold().FontSize(10);
                section.Item().Text($"Địa chỉ: {Value(store.DeliveryAddress)}");
                section.Item().Text($"Người nhận: {Value(store.ContactName)} · {Value(store.ContactPhone)}");
                section.Item().Text($"Cần trước: {(store.NeededByDate.HasValue ? store.NeededByDate.Value.ToString("dd/MM/yyyy") : "-")}");
                if (!string.IsNullOrWhiteSpace(store.Note)) section.Item().Text($"Ghi chú: {store.Note}");
                section.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1.8f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(1.4f);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Nguyên liệu").Bold();
                        header.Cell().Element(HeaderCell).Text("Quy cách").Bold();
                        header.Cell().Element(HeaderCell).AlignRight().Text("Số gói").Bold();
                        header.Cell().Element(HeaderCell).AlignRight().Text("SL cơ sở").Bold();
                    });
                    foreach (var line in store.Lines)
                    {
                        table.Cell().Element(BodyCell).Text(line.IngredientName);
                        table.Cell().Element(BodyCell).Text($"{Quantity(line.PackageQuantity)} {line.PackageUnitName}");
                        table.Cell().Element(BodyCell).AlignRight().Text(Quantity(line.PackageCount));
                        table.Cell().Element(BodyCell).AlignRight().Text(Quantity(line.BaseQuantity));
                    }
                });
            });
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Note))
            column.Item().Element(InfoBox).Text($"Ghi chú / Điều kiện giao nhận: {snapshot.Note}");
    }

    private static IContainer InfoBox(IContainer container) =>
        container.Border(1).BorderColor("#E7E5E4").Background("#FAFAF9").Padding(10);

    private static IContainer HeaderCell(IContainer container) =>
        container.Background("#F5EDE8").BorderBottom(1).BorderColor("#D6D3D1").Padding(6);

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor("#E7E5E4").PaddingVertical(5).PaddingHorizontal(6);

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;
    private static string Quantity(decimal value) => value.ToString("0.###", VietnameseCulture);
    private static string Money(decimal value) => value.ToString("N0", VietnameseCulture);
}
