using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WordDocument =
    DocumentFormat.OpenXml.Wordprocessing.Document;

namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentExportService : IAdminInventoryDocumentExportService
    {
        // =====================================================
        // PDF
        // =====================================================

        public Task<byte[]> ExportPdfAsync(InventoryDocumentSnapshotDTO snapshot)
        {
            var pdf = QuestPDF.Fluent.Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);

                            page.Margin(20);

                            page.DefaultTextStyle(x =>
                                x.FontSize(11));

                            // =====================================
                            // HEADER
                            // =====================================

                            page.Header()
                                .Column(col =>
                                {
                                    col.Item()
                                        .AlignCenter()
                                        .Text("CAFECHAIN")
                                        .FontSize(20)
                                        .Bold();

                                    col.Item()
                                        .AlignCenter()
                                        .Text("PHIẾU KHO")
                                        .FontSize(16)
                                        .Bold();
                                });

                            // =====================================
                            // CONTENT
                            // =====================================

                            page.Content()
                                .Column(col =>
                                {
                                    col.Spacing(5);

                                    col.Item().Text(
                                        $"Mã phiếu: {snapshot.Code}");

                                    col.Item().Text(
                                        $"Ngày: {snapshot.DocumentDate:dd/MM/yyyy}");

                                    col.Item().Text(
                                        $"Kho: {snapshot.StoreName}");

                                    col.Item().Text(
                                        $"Nhân viên: {snapshot.StaffName}");

                                    col.Item().Text(
                                        $"Đối tác: {snapshot.PartnerName}");

                                    col.Item()
                                        .PaddingVertical(10);

                                    // ==========================
                                    // TABLE
                                    // ==========================

                                    col.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(4);
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Text("Tên hàng").Bold();
                                            header.Cell().Text("ĐVT").Bold();
                                            header.Cell().Text("SL").Bold();
                                            header.Cell().Text("Đơn giá").Bold();
                                            header.Cell().Text("Thành tiền").Bold();
                                        });

                                        foreach (var item in snapshot.Details)
                                        {
                                            table.Cell()
                                                .Text(item.ItemName ?? "");

                                            table.Cell()
                                                .Text(item.UnitName ?? "");

                                            table.Cell()
                                                .Text(
                                                    item.Quantity.ToString("N2"));

                                            table.Cell()
                                                .Text(
                                                    item.UnitPrice.ToString("N0"));

                                            table.Cell()
                                                .Text(
                                                    item.TotalAmount.ToString("N0"));
                                        }
                                    });

                                    col.Item()
                                        .PaddingTop(15);

                                    // ==========================
                                    // SUMMARY
                                    // ==========================

                                    col.Item()
                                        .AlignRight()
                                        .Column(summary =>
                                        {
                                            summary.Item()
                                                .Text(
                                                    $"Tổng tiền: {snapshot.TotalAmount:N0}");

                                            summary.Item()
                                                .Text(
                                                    $"VAT: {snapshot.VatAmount:N0}");

                                            summary.Item()
                                                .Text(
                                                    $"Thành tiền: {snapshot.FinalAmount:N0}")
                                                .Bold()
                                                .FontSize(14);
                                        });
                                });

                            // =====================================
                            // FOOTER
                            // =====================================

                            page.Footer()
                                .AlignCenter()
                                .Text(text =>
                                {
                                    text.Span("CafeChain - ");

                                    text.CurrentPageNumber();

                                    text.Span(" / ");

                                    text.TotalPages();
                                });
                        });
                    })
                    .GeneratePdf();

            return Task.FromResult(pdf);
        }

        // =====================================================
        // WORD
        // =====================================================

        public Task<byte[]> ExportWordAsync(
            InventoryDocumentSnapshotDTO snapshot)
        {
            using var stream =
                new MemoryStream();

            using (
                var document =
                WordprocessingDocument.Create(
                    stream,
                    WordprocessingDocumentType.Document))
            {
                var mainPart =
                    document.AddMainDocumentPart();

                var body =
                    new Body();

                // =========================================
                // TITLE
                // =========================================

                body.Append(
                    CreateParagraph(
                        "PHIẾU KHO",
                        true));

                body.Append(
                    CreateParagraph(
                        $"Mã phiếu: {snapshot.Code}"));

                body.Append(
                    CreateParagraph(
                        $"Ngày: {snapshot.DocumentDate:dd/MM/yyyy}"));

                body.Append(
                    CreateParagraph(
                        $"Kho: {snapshot.StoreName}"));

                body.Append(
                    CreateParagraph(
                        $"Nhân viên: {snapshot.StaffName}"));

                body.Append(
                    CreateParagraph(
                        $"Đối tác: {snapshot.PartnerName}"));

                body.Append(
                    CreateParagraph(""));

                // =========================================
                // DETAILS
                // =========================================

                foreach (var item in snapshot.Details)
                {
                    body.Append(
                        CreateParagraph(
                            $"{item.ItemName} | " +
                            $"{item.Quantity:N2} {item.UnitName} | " +
                            $"{item.UnitPrice:N0} | " +
                            $"{item.TotalAmount:N0}"
                        ));
                }

                body.Append(
                    CreateParagraph(""));

                // =========================================
                // SUMMARY
                // =========================================

                body.Append(
                    CreateParagraph(
                        $"Tổng tiền: {snapshot.TotalAmount:N0}"));

                body.Append(
                    CreateParagraph(
                        $"VAT: {snapshot.VatAmount:N0}"));

                body.Append(
                    CreateParagraph(
                        $"Thành tiền: {snapshot.FinalAmount:N0}",
                        true));

                mainPart.Document =
                    new WordDocument(body);

                mainPart.Document.Save();
            }

            return Task.FromResult(
                stream.ToArray());
        }

        // =====================================================
        // HELPERS
        // =====================================================

        private static Paragraph CreateParagraph(
            string text,
            bool bold = false)
        {
            var run =
                new Run(
                    new Text(text));

            if (bold)
            {
                run.RunProperties =
                    new RunProperties(
                        new Bold());
            }

            return new Paragraph(run);
        }
    }
}