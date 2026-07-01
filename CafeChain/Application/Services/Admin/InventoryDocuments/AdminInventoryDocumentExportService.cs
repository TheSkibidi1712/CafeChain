using CafeChain.Application.DTOs.Admin.InventoryDocuments.Snapshot;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
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
            var pdf =
                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(32);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header().Column(header =>
                        {
                            header.Item()
                                .AlignCenter()
                                .Text("CAFECHAIN")
                                .FontSize(15)
                                .Bold()
                                .FontColor("#111827");

                            header.Item()
                                .PaddingTop(4)
                                .AlignCenter()
                                .Text("PHIẾU NHẬP KHO")
                                .FontSize(20)
                                .Bold()
                                .FontColor("#F97316");

                            header.Item()
                                .PaddingTop(2)
                                .AlignCenter()
                                .Text($"Số phiếu: {snapshot.Code}")
                                .FontSize(10)
                                .FontColor("#6B7280");
                        });

                        page.Content()
                            .PaddingTop(18)
                            .Column(col =>
                            {
                                col.Spacing(14);

                                col.Item().Row(row =>
                                {
                                    row.RelativeItem().Element(PdfInfoBox).Column(info =>
                                    {
                                        info.Spacing(5);
                                        info.Item().Text("Thông tin chứng từ").Bold().FontColor("#F97316");
                                        info.Item().Text($"Ngày chứng từ: {snapshot.DocumentDate:dd/MM/yyyy}");
                                        info.Item().Text($"Cửa hàng: {snapshot.StoreName}");
                                        info.Item().Text($"Người lập: {snapshot.StaffName}");
                                    });

                                    row.ConstantItem(16);

                                    row.RelativeItem().Element(PdfInfoBox).Column(info =>
                                    {
                                        info.Spacing(5);
                                        info.Item().Text("Thông tin đối tác").Bold().FontColor("#F97316");
                                        info.Item().Text($"Đối tác: {snapshot.PartnerName ?? "-"}");
                                        info.Item().Text($"Ngày in: {DateTime.Now:dd/MM/yyyy HH:mm}");
                                    });
                                });

                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(32);
                                        columns.RelativeColumn(4);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1.3f);
                                        columns.RelativeColumn(1.8f);
                                        columns.RelativeColumn(2);
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(PdfHeaderCell).AlignCenter().Text("STT").Bold();
                                        header.Cell().Element(PdfHeaderCell).Text("Nguyên liệu").Bold();
                                        header.Cell().Element(PdfHeaderCell).AlignCenter().Text("ĐVT").Bold();
                                        header.Cell().Element(PdfHeaderCell).AlignRight().Text("SL").Bold();
                                        header.Cell().Element(PdfHeaderCell).AlignRight().Text("Đơn giá").Bold();
                                        header.Cell().Element(PdfHeaderCell).AlignRight().Text("Thành tiền").Bold();
                                    });

                                    var index = 1;

                                    foreach (var item in snapshot.Details)
                                    {
                                        table.Cell().Element(PdfBodyCell).AlignCenter().Text(index.ToString());
                                        table.Cell().Element(PdfBodyCell).Text(item.ItemName ?? "");
                                        table.Cell().Element(PdfBodyCell).AlignCenter().Text(item.UnitName ?? "");
                                        table.Cell().Element(PdfBodyCell).AlignRight().Text(FormatQuantity(item.Quantity));
                                        table.Cell().Element(PdfBodyCell).AlignRight().Text(FormatMoney(item.UnitPrice));
                                        table.Cell().Element(PdfBodyCell).AlignRight().Text(FormatMoney(item.TotalAmount));

                                        index++;
                                    }
                                });

                                col.Item().AlignRight().Width(230).Column(summary =>
                                {
                                    summary.Spacing(6);
                                    summary.Item().Row(row =>
                                    {
                                        row.RelativeItem().Text("Tổng tiền");
                                        row.RelativeItem().AlignRight().Text(FormatMoney(snapshot.TotalAmount)).Bold();
                                    });
                                    summary.Item().Row(row =>
                                    {
                                        row.RelativeItem().Text("VAT");
                                        row.RelativeItem().AlignRight().Text(FormatMoney(snapshot.VatAmount)).Bold();
                                    });
                                    summary.Item()
                                        .BorderTop(1)
                                        .BorderColor("#E5E7EB")
                                        .PaddingTop(6)
                                        .Row(row =>
                                        {
                                            row.RelativeItem().Text("Thành tiền").Bold();
                                            row.RelativeItem().AlignRight().Text(FormatMoney(snapshot.FinalAmount)).Bold().FontSize(13).FontColor("#F97316");
                                        });
                                });

                                col.Item().PaddingTop(22).Row(row =>
                                {
                                    row.RelativeItem().AlignCenter().Column(sign =>
                                    {
                                        sign.Item().Text("Người lập phiếu").Bold();
                                        sign.Item().Text("(Ký, họ tên)").FontSize(9).FontColor("#6B7280");
                                        sign.Item().Height(48);
                                        sign.Item().Text(snapshot.StaffName ?? "");
                                    });

                                    row.RelativeItem().AlignCenter().Column(sign =>
                                    {
                                        sign.Item().Text("Thủ kho").Bold();
                                        sign.Item().Text("(Ký, họ tên)").FontSize(9).FontColor("#6B7280");
                                        sign.Item().Height(48);
                                        sign.Item().Text("");
                                    });

                                    row.RelativeItem().AlignCenter().Column(sign =>
                                    {
                                        sign.Item().Text("Đối tác").Bold();
                                        sign.Item().Text("(Ký, họ tên)").FontSize(9).FontColor("#6B7280");
                                        sign.Item().Height(48);
                                        sign.Item().Text(snapshot.PartnerName ?? "");
                                    });
                                });
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text.Span("CafeChain - Trang ");
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

                body.Append(
                    CreateParagraph(
                        "CAFECHAIN",
                        true,
                        JustificationValues.Center,
                        "26"));

                body.Append(
                    CreateParagraph(
                        "PHIẾU NHẬP KHO",
                        true,
                        JustificationValues.Center,
                        "34"));

                body.Append(
                    CreateParagraph(
                        $"Số phiếu: {snapshot.Code}",
                        false,
                        JustificationValues.Center));

                body.Append(CreateSpacerParagraph());

                body.Append(
                    CreateInfoTable(snapshot));

                body.Append(CreateSpacerParagraph());

                body.Append(
                    CreateParagraph(
                        "Chi tiết nguyên liệu",
                        true,
                        null,
                        "24"));

                body.Append(
                    CreateDetailsTable(snapshot));

                body.Append(CreateSpacerParagraph());

                body.Append(
                    CreateSummaryTable(snapshot));

                body.Append(CreateSpacerParagraph());

                body.Append(
                    CreateSignatureTable(snapshot));

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

        private static IContainer PdfInfoBox(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor("#E5E7EB")
                .Background("#FFFBF7")
                .Padding(10);
        }

        private static IContainer PdfHeaderCell(IContainer container)
        {
            return container
                .Background("#FFF7ED")
                .Border(1)
                .BorderColor("#FDBA74")
                .PaddingVertical(6)
                .PaddingHorizontal(5);
        }

        private static IContainer PdfBodyCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor("#E5E7EB")
                .PaddingVertical(6)
                .PaddingHorizontal(5);
        }

        private static Table CreateInfoTable(
            InventoryDocumentSnapshotDTO snapshot)
        {
            var table =
                CreateBaseWordTable();

            table.Append(
                CreateWordTableRow(
                    [
                        ("Mã phiếu", JustificationValues.Left),
                        (snapshot.Code, JustificationValues.Left),
                        ("Ngày chứng từ", JustificationValues.Left),
                        (snapshot.DocumentDate.ToString("dd/MM/yyyy"), JustificationValues.Left)
                    ],
                    true));

            table.Append(
                CreateWordTableRow(
                    [
                        ("Cửa hàng", JustificationValues.Left),
                        (snapshot.StoreName, JustificationValues.Left),
                        ("Người lập", JustificationValues.Left),
                        (snapshot.StaffName, JustificationValues.Left)
                    ]));

            table.Append(
                CreateWordTableRow(
                    [
                        ("Đối tác", JustificationValues.Left),
                        (snapshot.PartnerName ?? "-", JustificationValues.Left),
                        ("Ngày in", JustificationValues.Left),
                        (DateTime.Now.ToString("dd/MM/yyyy HH:mm"), JustificationValues.Left)
                    ]));

            return table;
        }

        private static Table CreateDetailsTable(
            InventoryDocumentSnapshotDTO snapshot)
        {
            var table =
                CreateBaseWordTable();

            table.Append(
                CreateWordTableRow(
                    [
                        ("STT", JustificationValues.Center),
                        ("Nguyên liệu", JustificationValues.Left),
                        ("ĐVT", JustificationValues.Center),
                        ("SL", JustificationValues.Right),
                        ("Đơn giá", JustificationValues.Right),
                        ("Thành tiền", JustificationValues.Right)
                    ],
                    true));

            var index = 1;

            foreach (var item in snapshot.Details)
            {
                table.Append(
                    CreateWordTableRow(
                        [
                            (index.ToString(), JustificationValues.Center),
                            (item.ItemName ?? "", JustificationValues.Left),
                            (item.UnitName ?? "", JustificationValues.Center),
                            (FormatQuantity(item.Quantity), JustificationValues.Right),
                            (FormatMoney(item.UnitPrice), JustificationValues.Right),
                            (FormatMoney(item.TotalAmount), JustificationValues.Right)
                        ]));

                index++;
            }

            return table;
        }

        private static Table CreateSummaryTable(
            InventoryDocumentSnapshotDTO snapshot)
        {
            var table =
                CreateBaseWordTable();

            table.Append(
                CreateWordTableRow(
                    [
                        ("", JustificationValues.Left),
                        ("Tổng tiền", JustificationValues.Right),
                        (FormatMoney(snapshot.TotalAmount), JustificationValues.Right)
                    ]));

            table.Append(
                CreateWordTableRow(
                    [
                        ("", JustificationValues.Left),
                        ("VAT", JustificationValues.Right),
                        (FormatMoney(snapshot.VatAmount), JustificationValues.Right)
                    ]));

            table.Append(
                CreateWordTableRow(
                    [
                        ("", JustificationValues.Left),
                        ("Thành tiền", JustificationValues.Right),
                        (FormatMoney(snapshot.FinalAmount), JustificationValues.Right)
                    ],
                    true));

            return table;
        }

        private static Table CreateSignatureTable(
            InventoryDocumentSnapshotDTO snapshot)
        {
            var table =
                CreateBaseWordTable(false);

            table.Append(
                CreateWordTableRow(
                    [
                        ("Người lập phiếu\n(Ký, họ tên)", JustificationValues.Center),
                        ("Thủ kho\n(Ký, họ tên)", JustificationValues.Center),
                        ("Đối tác\n(Ký, họ tên)", JustificationValues.Center)
                    ],
                    true,
                    false));

            table.Append(
                CreateWordTableRow(
                    [
                        ("\n\n" + (snapshot.StaffName ?? ""), JustificationValues.Center),
                        ("\n\n", JustificationValues.Center),
                        ("\n\n" + (snapshot.PartnerName ?? ""), JustificationValues.Center)
                    ],
                    false,
                    false));

            return table;
        }

        private static Table CreateBaseWordTable(
            bool bordered = true)
        {
            var table =
                new Table();

            var borders =
                bordered
                    ? new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 6, Color = "D1D5DB" },
                        new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "D1D5DB" },
                        new LeftBorder { Val = BorderValues.Single, Size = 6, Color = "D1D5DB" },
                        new RightBorder { Val = BorderValues.Single, Size = 6, Color = "D1D5DB" },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6, Color = "D1D5DB" },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 6, Color = "D1D5DB" })
                    : new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None });

            table.AppendChild(
                new TableProperties(
                    new TableWidth
                    {
                        Width = "5000",
                        Type = TableWidthUnitValues.Pct
                    },
                    borders));

            return table;
        }

        private static TableRow CreateWordTableRow(
            IReadOnlyList<(string Text, JustificationValues Align)> cells,
            bool header = false,
            bool bordered = true)
        {
            var row =
                new TableRow();

            foreach (var cell in cells)
            {
                row.Append(
                    CreateWordTableCell(
                        cell.Text,
                        cell.Align,
                        header,
                        bordered));
            }

            return row;
        }

        private static TableCell CreateWordTableCell(
            string text,
            JustificationValues align,
            bool header,
            bool bordered)
        {
            var properties =
                new TableCellProperties(
                    new TableCellWidth
                    {
                        Type = TableWidthUnitValues.Auto
                    });

            if (header && bordered)
            {
                properties.Append(
                    new Shading
                    {
                        Fill = "FFF7ED"
                    });
            }

            return new TableCell(
                properties,
                CreateParagraph(
                    text,
                    header,
                    align));
        }

        private static Paragraph CreateParagraph(
            string text,
            bool bold = false,
            JustificationValues? alignment = null,
            string fontSize = "21")
        {
            var runProperties =
                new RunProperties(
                    new FontSize
                    {
                        Val = fontSize
                    });

            if (bold)
            {
                runProperties.Append(
                    new Bold());
            }

            var paragraph =
                new Paragraph();

            if (alignment.HasValue)
            {
                paragraph.Append(
                    new ParagraphProperties(
                        new Justification
                        {
                            Val = alignment.Value
                        }));
            }

            var lines =
                text.Split('\n');

            var run =
                new Run(runProperties);

            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    run.Append(
                        new Break());
                }

                run.Append(
                    new Text(lines[i])
                    {
                        Space = SpaceProcessingModeValues.Preserve
                    });
            }

            paragraph.Append(run);

            return paragraph;
        }

        private static Paragraph CreateSpacerParagraph()
        {
            return CreateParagraph("");
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN"));
        }

        private static string FormatQuantity(decimal value)
        {
            return value.ToString(
                "0.##",
                CultureInfo.GetCultureInfo("vi-VN"));
        }
    }
}
