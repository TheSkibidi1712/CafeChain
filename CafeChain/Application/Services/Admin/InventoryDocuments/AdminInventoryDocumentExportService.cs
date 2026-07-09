using CafeChain.Application.DTOs.Admin.InventoryDocuments.Snapshot;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Export;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using X = DocumentFormat.OpenXml.Spreadsheet;
using WordDocument =
    DocumentFormat.OpenXml.Wordprocessing.Document;

namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentExportService : IAdminInventoryDocumentExportService
    {
        private const uint ExcelHeaderStyleIndex = 1;

        private const uint ExcelDateStyleIndex = 2;

        private const uint ExcelMoneyStyleIndex = 3;

        private const uint ExcelCenterStyleIndex = 4;

        private const uint ExcelBodyStyleIndex = 5;

        private const uint ExcelDateFormatId = 164;

        private const uint ExcelMoneyFormatId = 165;

        // =====================================================
        // PDF
        // =====================================================

        public Task<byte[]> ExportPdfAsync(InventoryDocumentSnapshotDTO snapshot)
        {
            var printedAt =
                DateTime.Now;

            var pdf =
                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(36);
                        page.DefaultTextStyle(x =>
                            x.FontSize(9)
                                .FontColor("#374151"));

                        page.Content().Column(col =>
                        {
                            col.Spacing(18);

                            col.Item().AlignCenter().Column(header =>
                            {
                                header.Spacing(4);

                                header.Item()
                                    .Text("CAFECHAIN")
                                    .FontSize(15)
                                    .Bold()
                                    .FontColor("#111827");

                                header.Item()
                                    .Text("Hệ thống quản lý kho chuỗi cửa hàng")
                                    .FontSize(8)
                                    .FontColor("#9CA3AF");

                                header.Item()
                                    .PaddingTop(8)
                                    .Text("PHIẾU NHẬP KHO")
                                    .FontSize(20)
                                    .Bold()
                                    .FontColor("#F97316");

                                header.Item()
                                    .PaddingTop(4)
                                    .Text($"Số phiếu: {snapshot.Code}")
                                    .FontSize(9)
                                    .FontColor("#6B7280");

                                header.Item()
                                    .PaddingTop(4)
                                    .AlignCenter()
                                    .Background("#F3F4F6")
                                    .PaddingHorizontal(10)
                                    .PaddingVertical(4)
                                    .Text("Bản xem trước")
                                    .FontSize(7)
                                    .FontColor("#6B7280");
                            });

                            col.Item().PaddingTop(6).Row(row =>
                            {
                                row.RelativeItem().Element(PdfInfoBox).Column(info =>
                                {
                                    info.Item()
                                        .Element(PdfInfoBoxHeader)
                                        .Text("Thông tin chứng từ")
                                        .Bold()
                                        .FontColor("#EA580C");

                                    info.Item()
                                        .Padding(10)
                                        .Column(body =>
                                        {
                                            body.Spacing(6);

                                            body.Item().Text(text =>
                                            {
                                                text.Span("Ngày chứng từ: ");
                                                text.Span($"{snapshot.DocumentDate:dd/MM/yyyy}").Bold();
                                            });

                                            body.Item().Text(text =>
                                            {
                                                text.Span("Cửa hàng: ");
                                                text.Span(snapshot.StoreName ?? "-").Bold();
                                            });

                                            body.Item().Text(text =>
                                            {
                                                text.Span("Người lập: ");
                                                text.Span(snapshot.StaffName ?? "-").Bold();
                                            });
                                        });
                                });

                                row.ConstantItem(16);

                                row.RelativeItem().Element(PdfInfoBox).Column(info =>
                                {
                                    info.Item()
                                        .Element(PdfInfoBoxHeader)
                                        .Text("Thông tin đối tác")
                                        .Bold()
                                        .FontColor("#EA580C");

                                    info.Item()
                                        .Padding(10)
                                        .Column(body =>
                                        {
                                            body.Spacing(6);

                                            body.Item().Text(text =>
                                            {
                                                text.Span("Nhà cung cấp: ");
                                                text.Span(snapshot.PartnerName ?? "-").Bold();
                                            });

                                            body.Item().Text(text =>
                                            {
                                                text.Span("Ngày in: ");
                                                text.Span($"{printedAt:dd/MM/yyyy HH:mm}").Bold();
                                            });
                                        });
                                });
                            });

                            col.Item()
                                .PaddingTop(2)
                                .Text("Chi tiết nguyên liệu")
                                .FontSize(10)
                                .Bold()
                                .FontColor("#111827");

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(34);
                                    columns.RelativeColumn(3.6f);
                                    columns.RelativeColumn(1.1f);
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(2.3f);
                                    columns.RelativeColumn(2.4f);
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

                                var index =
                                    1;

                                foreach (var item in snapshot.Details)
                                {
                                    table.Cell().Element(PdfBodyCell).AlignCenter().Text(index.ToString());
                                    table.Cell().Element(PdfBodyCell).Text(item.ItemName ?? string.Empty);
                                    table.Cell().Element(PdfBodyCell).AlignCenter().Text(item.UnitName ?? string.Empty);
                                    table.Cell().Element(PdfBodyCell).AlignRight().Text(FormatQuantity(item.Quantity));
                                    table.Cell().Element(PdfBodyCell).AlignRight().Text(FormatMoney(item.UnitPrice));
                                    table.Cell().Element(PdfBodyCell).AlignRight().Text(FormatMoney(item.TotalAmount));

                                    index++;
                                }

                                table.Cell().ColumnSpan(4).Element(PdfSummaryBlankCell).Text(string.Empty);
                                table.Cell().Element(PdfSummaryLabelCell).AlignRight().Text("Tổng tiền").Bold();
                                table.Cell().Element(PdfSummaryValueCell).AlignRight().Text(FormatMoney(snapshot.TotalAmount)).Bold();

                                table.Cell().ColumnSpan(4).Element(PdfSummaryBlankCell).Text(string.Empty);
                                table.Cell().Element(PdfSummaryLabelCell).AlignRight().Text("VAT").Bold();
                                table.Cell().Element(PdfSummaryValueCell).AlignRight().Text(FormatMoney(snapshot.VatAmount)).Bold();

                                table.Cell().ColumnSpan(4).Element(PdfSummaryBlankCell).Text(string.Empty);
                                table.Cell().Element(PdfSummaryLabelCell).AlignRight().Text("Thành tiền").Bold();
                                table.Cell().Element(PdfSummaryValueCell).AlignRight().Text(FormatMoney(snapshot.FinalAmount))
                                    .Bold()
                                    .FontSize(11)
                                    .FontColor("#F97316");
                            });

                            col.Item().PaddingTop(24).Row(row =>
                            {
                                row.RelativeItem().AlignCenter().Column(sign =>
                                {
                                    sign.Spacing(4);

                                    sign.Item().Text("Người lập phiếu").Bold();
                                    sign.Item().Text("(Ký, ghi rõ họ tên)").Italic().FontSize(8).FontColor("#9CA3AF");
                                    sign.Item().Height(52);
                                    sign.Item().Text(snapshot.StaffName ?? string.Empty).Bold();
                                });

                                row.RelativeItem().AlignCenter().Column(sign =>
                                {
                                    sign.Spacing(4);

                                    sign.Item().Text("Thủ kho").Bold();
                                    sign.Item().Text("(Ký, ghi rõ họ tên)").Italic().FontSize(8).FontColor("#9CA3AF");
                                    sign.Item().Height(52);
                                    sign.Item().Text("----------------").FontColor("#6B7280");
                                });

                                row.RelativeItem().AlignCenter().Column(sign =>
                                {
                                    sign.Spacing(4);

                                    sign.Item().Text("Nhà cung cấp").Bold();
                                    sign.Item().Text("(Ký, ghi rõ họ tên)").Italic().FontSize(8).FontColor("#9CA3AF");
                                    sign.Item().Height(52);
                                    sign.Item().Text("----------------").FontColor("#6B7280");
                                });
                            });
                        });
                    });
                })
                .GeneratePdf();

            return Task.FromResult(pdf);
        }

        // =====================================================
        // WORD
        // =====================================================

        public Task<byte[]> ExportWordAsync(InventoryDocumentSnapshotDTO snapshot)
        {
            var printedAt = DateTime.Now;

            using var stream = new MemoryStream();

            using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var mainPart = document.AddMainDocumentPart();

                var body = new Body();

                body.Append(
                    CreateParagraph(
                        "CAFECHAIN",
                        true,
                        JustificationValues.Center,
                        "28",
                        "111827",
                        0,
                        20));

                body.Append(
                    CreateParagraph(
                        "Hệ thống quản lý kho chuỗi cửa hàng",
                        false,
                        JustificationValues.Center,
                        "16",
                        "9CA3AF",
                        0,
                        120));

                body.Append(
                    CreateParagraph(
                        "PHIẾU NHẬP KHO",
                        true,
                        JustificationValues.Center,
                        "34",
                        "F97316",
                        0,
                        80));

                body.Append(
                    CreateParagraph(
                        $"Số phiếu: {snapshot.Code}",
                        false,
                        JustificationValues.Center,
                        "18",
                        "6B7280",
                        0,
                        80));

                body.Append(
                    CreatePreviewBadgeParagraph());

                body.Append(
                    CreateSpacerParagraph(220));

                body.Append(
                    CreateInfoTable(
                        snapshot,
                        printedAt));

                body.Append(
                    CreateSpacerParagraph(260));

                body.Append(
                    CreateParagraph(
                        "Chi tiết nguyên liệu",
                        true,
                        JustificationValues.Left,
                        "21",
                        "111827",
                        0,
                        120));

                body.Append(
                    CreateDetailsTable(snapshot));

                body.Append(
                    CreateSpacerParagraph(520));

                body.Append(
                    CreateSignatureTable(snapshot));

                body.Append(
                    CreateSectionProperties());

                mainPart.Document =
                    new WordDocument(body);

                mainPart.Document.Save();
            }

            return Task.FromResult(
                stream.ToArray());
        }

        // =====================================================
        // EXCEL
        // =====================================================

        public Task<byte[]> ExportExcelAsync(IReadOnlyList<AdminInventoryDocumentExcelRowDTO> rows)
        {
            using var stream =
                new MemoryStream();

            using (
                var document =
                SpreadsheetDocument.Create(
                    stream,
                    SpreadsheetDocumentType.Workbook))
            {
                var workbookPart =
                    document.AddWorkbookPart();

                workbookPart.Workbook =
                    new X.Workbook();

                var stylesPart =
                    workbookPart.AddNewPart<WorkbookStylesPart>();

                stylesPart.Stylesheet =
                    CreateExcelStylesheet();

                stylesPart.Stylesheet.Save();

                var worksheetPart =
                    workbookPart.AddNewPart<WorksheetPart>();

                var sheetData =
                    new X.SheetData();

                worksheetPart.Worksheet =
                    new X.Worksheet(
                        CreateExcelSheetViews(),
                        CreateExcelColumns(),
                        sheetData);

                sheetData.Append(
                    CreateExcelRow(
                        1,
                        ExcelHeaderStyleIndex,
                        "STT",
                        "Mã phiếu",
                        "Loại",
                        "Mục đích",
                        "Cửa hàng",
                        "Đối tác",
                        "Ngày chứng từ",
                        "Giá trị",
                        "Trạng thái",
                        "Ngày xác nhận"));

                var rowIndex =
                    2U;

                foreach (var row in rows)
                {
                    sheetData.Append(
                        CreateExcelDataRow(
                            rowIndex,
                            row));

                    rowIndex++;
                }

                var lastRowIndex =
                    rowIndex > 1
                        ? rowIndex - 1
                        : 1;

                worksheetPart.Worksheet.Append(
                    new X.AutoFilter
                    {
                        Reference = $"A1:J{lastRowIndex}"
                    });

                worksheetPart.Worksheet.Save();

                var sheets =
                    workbookPart.Workbook.AppendChild(
                        new X.Sheets());

                sheets.Append(
                    new X.Sheet
                    {
                        Id = workbookPart.GetIdOfPart(worksheetPart),
                        SheetId = 1,
                        Name = "PhieuKho"
                    });

                workbookPart.Workbook.Save();
            }

            return Task.FromResult(
                stream.ToArray());
        }



        // =====================================================
        // EXCEL HELPERS
        // =====================================================
        private static X.SheetViews CreateExcelSheetViews()
        {
            var sheetView =
                new X.SheetView
                {
                    WorkbookViewId = 0U
                };

            sheetView.Append(
                new X.Pane
                {
                    VerticalSplit = 1D,
                    TopLeftCell = "A2",
                    ActivePane = X.PaneValues.BottomLeft,
                    State = X.PaneStateValues.Frozen
                });

            return new X.SheetViews(sheetView);
        }

        private static X.Columns CreateExcelColumns()
        {
            return new X.Columns(
                new X.Column { Min = 1, Max = 1, Width = 8, CustomWidth = true },
                new X.Column { Min = 2, Max = 2, Width = 18, CustomWidth = true },
                new X.Column { Min = 3, Max = 3, Width = 16, CustomWidth = true },
                new X.Column { Min = 4, Max = 4, Width = 22, CustomWidth = true },
                new X.Column { Min = 5, Max = 5, Width = 24, CustomWidth = true },
                new X.Column { Min = 6, Max = 6, Width = 24, CustomWidth = true },
                new X.Column { Min = 7, Max = 7, Width = 16, CustomWidth = true },
                new X.Column { Min = 8, Max = 8, Width = 18, CustomWidth = true },
                new X.Column { Min = 9, Max = 9, Width = 16, CustomWidth = true },
                new X.Column { Min = 10, Max = 10, Width = 18, CustomWidth = true });
        }

        private static X.Row CreateExcelRow(uint rowIndex, uint styleIndex, params string[] values)
        {
            var row =
                new X.Row
                {
                    RowIndex = rowIndex
                };

            foreach (var value in values)
            {
                row.Append(
                    CreateExcelTextCell(
                        value,
                        styleIndex));
            }

            return row;
        }

        private static X.Row CreateExcelDataRow(uint rowIndex, AdminInventoryDocumentExcelRowDTO document)
        {
            var row =
                new X.Row
                {
                    RowIndex = rowIndex
                };

            row.Append(
                CreateExcelNumberCell(document.No, ExcelCenterStyleIndex),
                CreateExcelTextCell(document.Code, ExcelBodyStyleIndex),
                CreateExcelTextCell(document.Type.ToString(), ExcelBodyStyleIndex),
                CreateExcelTextCell(document.Purpose.ToString(), ExcelBodyStyleIndex),
                CreateExcelTextCell(document.StoreName, ExcelBodyStyleIndex),
                CreateExcelTextCell(document.PartnerName ?? "-", ExcelBodyStyleIndex),
                CreateExcelDateCell(document.DocumentDate),
                CreateExcelNumberCell(document.FinalAmount, ExcelMoneyStyleIndex),
                CreateExcelTextCell(document.Status.ToString(), ExcelBodyStyleIndex),
                CreateExcelDateCell(document.ConfirmedAt));

            return row;
        }

        private static X.Cell CreateExcelTextCell(string? value, uint styleIndex)
        {
            return new X.Cell
            {
                DataType = X.CellValues.InlineString,
                StyleIndex = styleIndex,
                InlineString =
                    new X.InlineString(
                        new X.Text(value ?? string.Empty)
                        {
                            Space = SpaceProcessingModeValues.Preserve
                        })
            };
        }

        private static X.Cell CreateExcelNumberCell(int value, uint styleIndex)
        {
            return CreateExcelNumberCell(
                (decimal)value,
                styleIndex);
        }

        private static X.Cell CreateExcelNumberCell(decimal value, uint styleIndex)
        {
            return new X.Cell
            {
                StyleIndex = styleIndex,
                CellValue =
                    new X.CellValue(
                        value.ToString(
                            CultureInfo.InvariantCulture))
            };
        }

        private static X.Cell CreateExcelDateCell(DateTime? value)
        {
            if (!value.HasValue)
            {
                return CreateExcelTextCell(
                    string.Empty,
                    ExcelBodyStyleIndex);
            }

            return new X.Cell
            {
                StyleIndex = ExcelDateStyleIndex,
                CellValue =
                    new X.CellValue(
                        value.Value
                            .ToOADate()
                            .ToString(
                                CultureInfo.InvariantCulture))
            };
        }

        private static X.Stylesheet CreateExcelStylesheet()
        {
            return new X.Stylesheet(
                new X.NumberingFormats(
                    new X.NumberingFormat
                    {
                        NumberFormatId = ExcelDateFormatId,
                        FormatCode = "dd/mm/yyyy"
                    },
                    new X.NumberingFormat
                    {
                        NumberFormatId = ExcelMoneyFormatId,
                        FormatCode = "#,##0"
                    })
                {
                    Count = 2
                },
                new X.Fonts(
                    new X.Font(),
                    new X.Font(
                        new X.Bold(),
                        new X.Color
                        {
                            Rgb = "FFFFFFFF"
                        }))
                {
                    Count = 2
                },
                new X.Fills(
                    new X.Fill(
                        new X.PatternFill
                        {
                            PatternType = X.PatternValues.None
                        }),
                    new X.Fill(
                        new X.PatternFill
                        {
                            PatternType = X.PatternValues.Gray125
                        }),
                    new X.Fill(
                        new X.PatternFill
                        {
                            PatternType = X.PatternValues.Solid,
                            ForegroundColor =
                                new X.ForegroundColor
                                {
                                    Rgb = "FFF97316"
                                },
                            BackgroundColor =
                                new X.BackgroundColor
                                {
                                    Indexed = 64U
                                }
                        }))
                {
                    Count = 3
                },
                new X.Borders(
                    new X.Border(),
                    CreateExcelThinBorder())
                {
                    Count = 2
                },
                new X.CellStyleFormats(
                    new X.CellFormat())
                {
                    Count = 1
                },
                new X.CellFormats(
                    new X.CellFormat(),
                    new X.CellFormat
                    {
                        FontId = 1,
                        FillId = 2,
                        BorderId = 1,
                        ApplyFont = true,
                        ApplyFill = true,
                        ApplyBorder = true,
                        ApplyAlignment = true,
                        Alignment =
                            new X.Alignment
                            {
                                Horizontal = X.HorizontalAlignmentValues.Center,
                                Vertical = X.VerticalAlignmentValues.Center
                            }
                    },
                    new X.CellFormat
                    {
                        NumberFormatId = ExcelDateFormatId,
                        BorderId = 1,
                        ApplyNumberFormat = true,
                        ApplyBorder = true
                    },
                    new X.CellFormat
                    {
                        NumberFormatId = ExcelMoneyFormatId,
                        BorderId = 1,
                        ApplyNumberFormat = true,
                        ApplyBorder = true
                    },
                    new X.CellFormat
                    {
                        BorderId = 1,
                        ApplyBorder = true,
                        ApplyAlignment = true,
                        Alignment =
                            new X.Alignment
                            {
                                Horizontal = X.HorizontalAlignmentValues.Center
                            }
                    },
                    new X.CellFormat
                    {
                        BorderId = 1,
                        ApplyBorder = true
                    })
                {
                    Count = 6
                },
                new X.CellStyles(
                    new X.CellStyle
                    {
                        Name = "Normal",
                        FormatId = 0,
                        BuiltinId = 0
                    })
                {
                    Count = 1
                },
                new X.DifferentialFormats()
                {
                    Count = 0
                },
                new X.TableStyles
                {
                    Count = 0,
                    DefaultTableStyle = "TableStyleMedium2",
                    DefaultPivotStyle = "PivotStyleLight16"
                });
        }

        private static X.Border CreateExcelThinBorder()
        {
            return new X.Border(
                new X.LeftBorder
                {
                    Style = X.BorderStyleValues.Thin
                },
                new X.RightBorder
                {
                    Style = X.BorderStyleValues.Thin
                },
                new X.TopBorder
                {
                    Style = X.BorderStyleValues.Thin
                },
                new X.BottomBorder
                {
                    Style = X.BorderStyleValues.Thin
                },
                new X.DiagonalBorder());
        }

        // =====================================================
        // PDF HELPERS
        // =====================================================

        private static IContainer PdfInfoBox(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor("#F3E4D4");
        }

        private static IContainer PdfInfoBoxHeader(IContainer container)
        {
            return container
                .Background("#FFF7ED")
                .BorderBottom(1)
                .BorderColor("#F3E4D4")
                .PaddingVertical(8)
                .PaddingHorizontal(10);
        }

        private static IContainer PdfHeaderCell(IContainer container)
        {
            return container
                .Background("#FFF7ED")
                .Border(1)
                .BorderColor("#E5E7EB")
                .PaddingVertical(8)
                .PaddingHorizontal(6);
        }

        private static IContainer PdfBodyCell(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor("#E5E7EB")
                .PaddingVertical(8)
                .PaddingHorizontal(6);
        }

        private static IContainer PdfSummaryBlankCell(IContainer container)
        {
            return container
                .BorderLeft(1)
                .BorderRight(1)
                .BorderBottom(1)
                .BorderColor("#E5E7EB")
                .PaddingVertical(8)
                .PaddingHorizontal(6);
        }

        private static IContainer PdfSummaryLabelCell(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor("#E5E7EB")
                .PaddingVertical(8)
                .PaddingHorizontal(6);
        }

        private static IContainer PdfSummaryValueCell(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor("#E5E7EB")
                .PaddingVertical(8)
                .PaddingHorizontal(6);
        }

        // =====================================================
        // WORD HELPERS
        // =====================================================

        private static Table CreateInfoTable(InventoryDocumentSnapshotDTO snapshot, DateTime printedAt)
        {
            var table = CreateBaseWordTable(true);

            table.Append(
                CreateWordTableGrid(
                    "1100",
                    "2050",
                    "1100",
                    "2050"));

            table.Append(
                CreateWordTableRow(
                    [
                        CreateWordTableCell(
                    "Mã phiếu",
                    JustificationValues.Left,
                    true,
                    "FFF7ED",
                    "1100"),

                CreateWordTableCell(
                    snapshot.Code ?? string.Empty,
                    JustificationValues.Left,
                    false,
                    null,
                    "2050"),

                CreateWordTableCell(
                    "Ngày chứng từ",
                    JustificationValues.Left,
                    true,
                    "FFF7ED",
                    "1100"),

                CreateWordTableCell(
                    snapshot.DocumentDate.ToString("dd/MM/yyyy"),
                    JustificationValues.Left,
                    false,
                    null,
                    "2050")
                    ]));

            table.Append(
                CreateWordTableRow(
                    [
                        CreateWordTableCell(
                    "Cửa hàng",
                    JustificationValues.Left,
                    true,
                    "FFF7ED",
                    "1100"),

                CreateWordTableCell(
                    snapshot.StoreName ?? "-",
                    JustificationValues.Left,
                    false,
                    null,
                    "2050"),

                CreateWordTableCell(
                    "Người lập",
                    JustificationValues.Left,
                    true,
                    "FFF7ED",
                    "1100"),

                CreateWordTableCell(
                    snapshot.StaffName ?? "-",
                    JustificationValues.Left,
                    false,
                    null,
                    "2050")
                    ]));

            table.Append(
                CreateWordTableRow(
                    [
                        CreateWordTableCell(
                    "Nhà cung cấp",
                    JustificationValues.Left,
                    true,
                    "FFF7ED",
                    "1100"),

                CreateWordTableCell(
                    snapshot.PartnerName ?? "-",
                    JustificationValues.Left,
                    false,
                    null,
                    "2050"),

                CreateWordTableCell(
                    "Ngày in",
                    JustificationValues.Left,
                    true,
                    "FFF7ED",
                    "1100"),

                CreateWordTableCell(
                    printedAt.ToString("dd/MM/yyyy HH:mm"),
                    JustificationValues.Left,
                    false,
                    null,
                    "2050")
                    ]));

            table.Append(
                CreateWordTableRow(
                    [
                        CreateWordTableCell(
                    "Ghi chú",
                    JustificationValues.Left,
                    true,
                    "FFF7ED",
                    "1100"),

                CreateWordTableCell(
                    "-",
                    JustificationValues.Left,
                    false,
                    null,
                    "5200",
                    3)
                    ]));

            return table;
        }

        private static Table CreateDetailsTable(InventoryDocumentSnapshotDTO snapshot)
        {
            var table = CreateBaseWordTable(true);

            table.Append(
                CreateWordTableGrid(
                    "420",
                    "1800",
                    "720",
                    "720",
                    "1250",
                    "1350"));

            table.Append(
                CreateWordTableRow(
                    [
                        CreateWordTableCell(
                    "STT",
                    JustificationValues.Center,
                    true,
                    "FFF7ED",
                    "420"),

                CreateWordTableCell(
                    "Nguyên liệu",
                    JustificationValues.Left,
                    true,
                    "FFF7ED",
                    "1800"),

                CreateWordTableCell(
                    "ĐVT",
                    JustificationValues.Center,
                    true,
                    "FFF7ED",
                    "720"),

                CreateWordTableCell(
                    "SL",
                    JustificationValues.Right,
                    true,
                    "FFF7ED",
                    "720"),

                CreateWordTableCell(
                    "Đơn giá",
                    JustificationValues.Right,
                    true,
                    "FFF7ED",
                    "1250"),

                CreateWordTableCell(
                    "Thành tiền",
                    JustificationValues.Right,
                    true,
                    "FFF7ED",
                    "1350")
                    ]));

            var index = 1;

            foreach (var item in snapshot.Details)
            {
                table.Append(
                    CreateWordTableRow(
                        [
                            CreateWordTableCell(
                        index.ToString(),
                        JustificationValues.Center,
                        false,
                        null,
                        "420"),

                    CreateWordTableCell(
                        item.ItemName ?? string.Empty,
                        JustificationValues.Left,
                        false,
                        null,
                        "1800"),

                    CreateWordTableCell(
                        item.UnitName ?? string.Empty,
                        JustificationValues.Center,
                        false,
                        null,
                        "720"),

                    CreateWordTableCell(
                        FormatQuantity(item.Quantity),
                        JustificationValues.Right,
                        false,
                        null,
                        "720"),

                    CreateWordTableCell(
                        FormatMoney(item.UnitPrice),
                        JustificationValues.Right,
                        false,
                        null,
                        "1250"),

                    CreateWordTableCell(
                        FormatMoney(item.TotalAmount),
                        JustificationValues.Right,
                        false,
                        null,
                        "1350")
                        ]));

                index++;
            }

            table.Append(
                CreateWordSummaryRow(
                    "Tổng tiền",
                    FormatMoney(snapshot.TotalAmount),
                    false));

            table.Append(
                CreateWordSummaryRow(
                    "VAT",
                    FormatMoney(snapshot.VatAmount),
                    false));

            table.Append(
                CreateWordSummaryRow(
                    "Thành tiền",
                    FormatMoney(snapshot.FinalAmount),
                    true));

            return table;
        }

        private static Table CreateSummaryTable(InventoryDocumentSnapshotDTO snapshot)
        {
            var table =
                CreateBaseWordTable(true);

            table.Append(
                CreateWordTableGrid(
                    "3660",
                    "1250",
                    "1350"));

            table.Append(
                CreateWordSummaryRow(
                    "Tổng tiền",
                    FormatMoney(snapshot.TotalAmount),
                    false));

            table.Append(
                CreateWordSummaryRow(
                    "VAT",
                    FormatMoney(snapshot.VatAmount),
                    false));

            table.Append(
                CreateWordSummaryRow(
                    "Thành tiền",
                    FormatMoney(snapshot.FinalAmount),
                    true));

            return table;
        }

        private static TableRow CreateWordSummaryRow(string label, string value, bool finalRow)
        {
            return CreateWordTableRow(
                [
                    CreateWordTableCell(
                        string.Empty,
                        JustificationValues.Left,
                        false,
                        null,
                        "3660",
                        4),

                    CreateWordTableCell(
                        label,
                        JustificationValues.Right,
                        true,
                        null,
                        "1250"),

                    CreateWordTableCell(
                        value,
                        JustificationValues.Right,
                        true,
                        null,
                        "1350",
                        1,
                        true,
                        false,
                        finalRow ? "F97316" : "111827",
                        finalRow ? "22" : "19")
                ]);
        }

        private static Table CreateSignatureTable(InventoryDocumentSnapshotDTO snapshot)
        {
            var table =
                CreateBaseWordTable(false);

            table.Append(
                CreateWordTableGrid(
                    "2100",
                    "2100",
                    "2100"));

            table.Append(
                CreateWordTableRow(
                    [
                        CreateWordTableCell(
                    "Người lập phiếu",
                    JustificationValues.Center,
                    true,
                    null,
                    "2100",
                    1,
                    false),

                CreateWordTableCell(
                    "Thủ kho",
                    JustificationValues.Center,
                    true,
                    null,
                    "2100",
                    1,
                    false),

                CreateWordTableCell(
                    "Nhà cung cấp",
                    JustificationValues.Center,
                    true,
                    null,
                    "2100",
                    1,
                    false)
                    ]));

            table.Append(
                CreateWordTableRow(
                    [
                        CreateWordTableCell(
                    "(Ký, ghi rõ họ tên)",
                    JustificationValues.Center,
                    false,
                    null,
                    "2100",
                    1,
                    false,
                    true,
                    "9CA3AF"),

                CreateWordTableCell(
                    "(Ký, ghi rõ họ tên)",
                    JustificationValues.Center,
                    false,
                    null,
                    "2100",
                    1,
                    false,
                    true,
                    "9CA3AF"),

                CreateWordTableCell(
                    "(Ký, ghi rõ họ tên)",
                    JustificationValues.Center,
                    false,
                    null,
                    "2100",
                    1,
                    false,
                    true,
                    "9CA3AF")
                    ]));

            table.Append(
                CreateWordTableRow(
                    [
                        CreateWordTableCell(
                    "\n\n\n",
                    JustificationValues.Center,
                    false,
                    null,
                    "2100",
                    1,
                    false),

                CreateWordTableCell(
                    "\n\n\n",
                    JustificationValues.Center,
                    false,
                    null,
                    "2100",
                    1,
                    false),

                CreateWordTableCell(
                    "\n\n\n",
                    JustificationValues.Center,
                    false,
                    null,
                    "2100",
                    1,
                    false)
                    ]));

            table.Append(
                CreateWordTableRow(
                    [
                        CreateWordTableCell(
                    snapshot.StaffName ?? string.Empty,
                    JustificationValues.Center,
                    true,
                    null,
                    "2100",
                    1,
                    false),

                CreateWordTableCell(
                    "----------------",
                    JustificationValues.Center,
                    false,
                    null,
                    "2100",
                    1,
                    false,
                    false,
                    "6B7280"),

                CreateWordTableCell(
                    "----------------",
                    JustificationValues.Center,
                    false,
                    null,
                    "2100",
                    1,
                    false,
                    false,
                    "6B7280")
                    ]));

            return table;
        }


        private static Table CreateBaseWordTable(bool bordered = true)
        {
            var table =
                new Table();

            table.AppendChild(
                new TableProperties(
                    new TableWidth
                    {
                        Width = "5000",
                        Type = TableWidthUnitValues.Pct
                    },
                    bordered ? CreateThinTableBorders() : CreateNoTableBorders(),
                    new TableCellMarginDefault(
                        new TopMargin
                        {
                            Width = "90",
                            Type = TableWidthUnitValues.Dxa
                        },
                        new BottomMargin
                        {
                            Width = "90",
                            Type = TableWidthUnitValues.Dxa
                        },
                        new LeftMargin
                        {
                            Width = "110",
                            Type = TableWidthUnitValues.Dxa
                        },
                        new RightMargin
                        {
                            Width = "110",
                            Type = TableWidthUnitValues.Dxa
                        })));

            return table;
        }

        private static TableGrid CreateWordTableGrid(params string[] widths)
        {
            var grid =
                new TableGrid();

            foreach (var width in widths)
            {
                grid.Append(
                    new GridColumn
                    {
                        Width = width
                    });
            }

            return grid;
        }


        private static TableRow CreateWordTableRow(IReadOnlyList<TableCell> cells)
        {
            var row =
                new TableRow();

            foreach (var cell in cells)
            {
                row.Append(cell);
            }

            return row;
        }

        private static TableCell CreateWordTableCell(
            string text,
            JustificationValues align,
            bool bold,
            string? shadingFill,
            string width,
            int gridSpan = 1,
            bool bordered = true,
            bool italic = false,
            string color = "374151",
            string fontSize = "19")
        {
            var properties =
                new TableCellProperties(
                    new TableCellWidth
                    {
                        Width = width,
                        Type = TableWidthUnitValues.Dxa
                    },
                    new TableCellVerticalAlignment
                    {
                        Val = TableVerticalAlignmentValues.Center
                    });

            if (gridSpan > 1)
            {
                properties.Append(
                    new GridSpan
                    {
                        Val = gridSpan
                    });
            }

            if (!bordered)
            {
                properties.Append(
                    CreateNoCellBorders());
            }

            if (!string.IsNullOrWhiteSpace(shadingFill))
            {
                properties.Append(
                    new Shading
                    {
                        Val = ShadingPatternValues.Clear,
                        Fill = shadingFill
                    });
            }

            return new TableCell(
                properties,
                CreateParagraph(
                    text,
                    bold,
                    align,
                    fontSize,
                    color,
                    0,
                    0,
                    italic));
        }

        private static Paragraph CreateParagraph(
    string text,
    bool bold = false,
    JustificationValues? alignment = null,
    string fontSize = "19",
    string color = "374151",
    int spacingBefore = 0,
    int spacingAfter = 0,
    bool italic = false)
        {
            var paragraph =
                new Paragraph();

            var paragraphProperties =
                new ParagraphProperties(
                    new SpacingBetweenLines
                    {
                        Before = spacingBefore.ToString(),
                        After = spacingAfter.ToString()
                    });

            if (alignment.HasValue)
            {
                paragraphProperties.Append(
                    new Justification
                    {
                        Val = alignment.Value
                    });
            }

            paragraph.Append(
                paragraphProperties);

            var runProperties =
                new RunProperties(
                    new RunFonts
                    {
                        Ascii = "Arial",
                        HighAnsi = "Arial"
                    },
                    new FontSize
                    {
                        Val = fontSize
                    },
                    new DocumentFormat.OpenXml.Wordprocessing.Color
                    {
                        Val = color
                    });

            if (bold)
            {
                runProperties.Append(
                    new Bold());
            }

            if (italic)
            {
                runProperties.Append(
                    new Italic());
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

        private static Paragraph CreatePreviewBadgeParagraph()
        {
            var paragraph =
                new Paragraph(
                    new ParagraphProperties(
                        new Justification
                        {
                            Val = JustificationValues.Center
                        },
                        new SpacingBetweenLines
                        {
                            Before = "0",
                            After = "0"
                        }));

            var run =
                new Run(
                    new RunProperties(
                        new RunFonts
                        {
                            Ascii = "Arial",
                            HighAnsi = "Arial"
                        },
                        new FontSize
                        {
                            Val = "14"
                        },
                        new DocumentFormat.OpenXml.Wordprocessing.Color
                        {
                            Val = "6B7280"
                        },
                        new Shading
                        {
                            Val = ShadingPatternValues.Clear,
                            Fill = "F3F4F6"
                        }),
                    new Text("  Bản xem trước  ")
                    {
                        Space = SpaceProcessingModeValues.Preserve
                    });

            paragraph.Append(run);

            return paragraph;
        }

        private static Paragraph CreateSpacerParagraph(int height = 160)
        {
            return new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines
                    {
                        Before = "0",
                        After = height.ToString()
                    }),
                new Run(
                    new Text(string.Empty)));
        }

        private static SectionProperties CreateSectionProperties()
        {
            return new SectionProperties(
                new DocumentFormat.OpenXml.Wordprocessing.PageSize
                {
                    Width = 11906,
                    Height = 16838
                },
                new DocumentFormat.OpenXml.Wordprocessing.PageMargin
                {
                    Top = 900,
                    Right = 900,
                    Bottom = 900,
                    Left = 900,
                    Header = 450,
                    Footer = 450,
                    Gutter = 0
                });
        }

        private static TableBorders CreateThinTableBorders()
        {
            return new TableBorders(
                new TopBorder
                {
                    Val = BorderValues.Single,
                    Size = 4,
                    Color = "E5E7EB"
                },
                new BottomBorder
                {
                    Val = BorderValues.Single,
                    Size = 4,
                    Color = "E5E7EB"
                },
                new LeftBorder
                {
                    Val = BorderValues.Single,
                    Size = 4,
                    Color = "E5E7EB"
                },
                new RightBorder
                {
                    Val = BorderValues.Single,
                    Size = 4,
                    Color = "E5E7EB"
                },
                new InsideHorizontalBorder
                {
                    Val = BorderValues.Single,
                    Size = 4,
                    Color = "E5E7EB"
                },
                new InsideVerticalBorder
                {
                    Val = BorderValues.Single,
                    Size = 4,
                    Color = "E5E7EB"
                });
        }

        private static TableBorders CreateNoTableBorders()
        {
            return new TableBorders(
                new TopBorder
                {
                    Val = BorderValues.None
                },
                new BottomBorder
                {
                    Val = BorderValues.None
                },
                new LeftBorder
                {
                    Val = BorderValues.None
                },
                new RightBorder
                {
                    Val = BorderValues.None
                },
                new InsideHorizontalBorder
                {
                    Val = BorderValues.None
                },
                new InsideVerticalBorder
                {
                    Val = BorderValues.None
                });
        }

        private static TableCellBorders CreateNoCellBorders()
        {
            return new TableCellBorders(
                new TopBorder
                {
                    Val = BorderValues.None
                },
                new BottomBorder
                {
                    Val = BorderValues.None
                },
                new LeftBorder
                {
                    Val = BorderValues.None
                },
                new RightBorder
                {
                    Val = BorderValues.None
                });
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
