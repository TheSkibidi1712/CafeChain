using System.Globalization;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CafeChain.Application.Services.Inventories;

public sealed class OperationalIceReportPdfRenderer : IOperationalIceReportPdfRenderer
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public byte[] Render(OperationalIceReportDto report, DateTime generatedAtUtc)
    {
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontSize(8.5f).FontColor("#1E293B"));
            page.Header().Element(container => Header(container, report));
            page.Content().PaddingVertical(12).Column(column => Content(column, report));
            page.Footer().Element(container => Footer(container, generatedAtUtc));
        })).GeneratePdf();
    }

    private static void Header(IContainer container, OperationalIceReportDto report)
    {
        container.BorderBottom(2).BorderColor("#6F4E37").PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("CAFECHAIN").FontSize(11).Bold().FontColor("#6F4E37");
                column.Item().Text("BÁO CÁO ĐÁ VẬN HÀNH").FontSize(17).Bold().FontColor("#2C1A11");
            });
            row.AutoItem().AlignRight().Column(column =>
            {
                column.Item().Text(report.StoreName).FontSize(9.5f).Bold().FontColor("#2C1A11");
                column.Item().Text($"{report.BusinessDate:dd/MM/yyyy} · {report.OperationalShiftName}").FontSize(8.5f).FontColor("#64748B");
                column.Item().Text($"Mã cấp đá #{report.IceAllocationId}").FontSize(8.5f).Bold().FontColor("#6F4E37");
            });
        });
    }

    private static void Content(ColumnDescriptor column, OperationalIceReportDto report)
    {
        column.Spacing(12);

        // Section 1: Meta Cards (Ca vận hành & Nhân sự)
        column.Item().Row(row =>
        {
            row.RelativeItem().Element(CardBox).Column(info =>
            {
                info.Item().Text("CA VẬN HÀNH").FontSize(8.5f).Bold().FontColor("#6F4E37");
                info.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor("#ECE3D9");
                info.Item().Text(text =>
                {
                    text.Span("Thời gian: ").FontColor("#64748B");
                    text.Span($"{report.StartAtUtc:dd/MM HH:mm} - {report.EndAtUtc:dd/MM HH:mm} UTC").Bold();
                });
                info.Item().Text(text =>
                {
                    text.Span("Nguyên liệu: ").FontColor("#64748B");
                    text.Span(report.IngredientName).Bold();
                });
                info.Item().Text(text =>
                {
                    text.Span("Trạng thái: ").FontColor("#64748B");
                    text.Span(OperationalIceDisplayText.Status(report.Status)).Bold();
                });
                info.Item().Text(text =>
                {
                    text.Span("Ca bán hàng POS: ").FontColor("#64748B");
                    text.Span(report.WorkShiftIds.Count == 0 ? "-" : string.Join(", ", report.WorkShiftIds.Select(x => $"#{x}"))).Bold();
                });
            });

            row.ConstantItem(12);

            row.RelativeItem().Element(CardBox).Column(info =>
            {
                info.Item().Text("NHÂN SỰ").FontSize(8.5f).Bold().FontColor("#6F4E37");
                info.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor("#ECE3D9");
                info.Item().Text(text =>
                {
                    text.Span("Người cấp: ").FontColor("#64748B");
                    text.Span(Value(report.IssuedBy)).Bold();
                });
                info.Item().Text(text =>
                {
                    text.Span("Ca trưởng/Người nhận: ").FontColor("#64748B");
                    text.Span(Value(report.ShiftLead)).Bold();
                });
                info.Item().Text(text =>
                {
                    text.Span("Người chốt: ").FontColor("#64748B");
                    text.Span(Value(report.ClosedBy)).Bold();
                });
                info.Item().Text(text =>
                {
                    text.Span("Người duyệt: ").FontColor("#64748B");
                    text.Span(Value(report.ApprovedBy)).Bold();
                });
            });
        });

        // Section 2: Đối chiếu số lượng
        column.Item().Text("ĐỐI CHIẾU SỐ LƯỢNG").FontSize(9.5f).Bold().FontColor("#2C1A11");
        column.Item().Border(1).BorderColor("#EAE0D6").Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(1.0f);
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(1.0f);
            });
            AddMetric(table, "Tồn chuyển đầu ca", Quantity(report.OpeningCarry, report.UnitName));
            AddMetric(table, "Cấp đầu ca", Quantity(report.InitialIssued, report.UnitName));
            AddMetric(table, "Cấp bổ sung", Quantity(report.SupplementalIssued, report.UnitName));
            AddMetric(table, "Trả kho hợp lệ", Quantity(report.ReturnedQuantity, report.UnitName));
            AddMetric(table, "Tồn chuyển cuối ca", Quantity(report.ClosingCarry, report.UnitName));
            AddMetric(table, "Tiêu hao thực tế", NullableQuantity(report.ActualUsage, report.UnitName));
            AddMetric(table, "Dùng theo POS", Quantity(report.TheoreticalUsage, report.UnitName));
            AddMetric(table, "Chênh lệch", NullableQuantity(report.Variance, report.UnitName));
        });

        // Section 3: Giá vốn và đối soát
        column.Item().Text("GIÁ VỐN VÀ ĐỐI SOÁT").FontSize(9.5f).Bold().FontColor("#2C1A11");
        column.Item().Element(CardBox).Column(cost =>
        {
            cost.Item().Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("Giá vốn theo POS: ").FontColor("#64748B");
                    text.Span(Money(report.TheoreticalCost)).Bold();
                });
                row.RelativeItem().Text(text =>
                {
                    text.Span("Giá vốn chênh lệch: ").FontColor("#64748B");
                    text.Span(Money(report.VarianceCost)).Bold();
                });
                row.RelativeItem().Text(text =>
                {
                    text.Span("Giá vốn thực tế: ").FontColor("#64748B");
                    text.Span(Money(report.ActualCost)).Bold().FontColor("#2C1A11");
                });
            });

            cost.Item().PaddingTop(6).BorderTop(0.5f).BorderColor("#ECE3D9").PaddingTop(6).Text(text =>
            {
                text.Span("Tình trạng giá vốn: ").FontColor("#64748B");
                text.Span(OperationalIceDisplayText.CostStatus(report.CostStatus)).Bold().FontColor("#99623B");
            });

            if (report.HasUsageSnapshotMismatch)
            {
                cost.Item().PaddingTop(4).Text(
                    $"⚠️ Cảnh báo đối soát: Tiêu hao đã lưu {Quantity(report.TheoreticalUsage, report.UnitName)}, dữ liệu giao dịch hiện tại {Quantity(report.LedgerTheoreticalUsage, report.UnitName)}.")
                    .FontColor("#991B1B").Bold();
            }
        });

        // Section 4: Tham chiếu bút toán kho
        column.Item().Text("THAM CHIẾU BÚT TOÁN KHO").FontSize(9.5f).Bold().FontColor("#2C1A11");
        if (report.InventoryPostings.Count == 0)
        {
            column.Item().Element(CardBox).Text("Không phát sinh bút toán điều chỉnh kho cho ca này.").FontColor("#64748B");
        }
        else
        {
            column.Item().Border(1).BorderColor("#EAE0D6").Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(2.2f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.3f);
                });
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Bút toán").Bold().FontColor("#6F4E37");
                    header.Cell().Element(HeaderCell).Text("Mã chống trùng lặp").Bold().FontColor("#6F4E37");
                    header.Cell().Element(HeaderCell).Text("Giao dịch kho").Bold().FontColor("#6F4E37");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Giá trị").Bold().FontColor("#6F4E37");
                });
                foreach (var posting in report.InventoryPostings)
                {
                    table.Cell().Element(BodyCell).Text($"#{posting.IceInventoryPostingId} · {OperationalIceDisplayText.PostingType(posting.PostingType)}");
                    table.Cell().Element(BodyCell).Text(posting.IdempotencyKey);
                    table.Cell().Element(BodyCell).Text(posting.InventoryTransactionId?.ToString() ?? "-");
                    table.Cell().Element(BodyCell).AlignRight().Text(Money(posting.TotalCost)).Bold();
                }
            });
        }

        // Section 5: Ghi chú chốt ca / Đối soát
        if (!string.IsNullOrWhiteSpace(report.CloseReason) || !string.IsNullOrWhiteSpace(report.ReconciliationReason))
        {
            column.Item().Element(CardBox).Column(note =>
            {
                note.Item().Text("GHI CHÚ CHỐT CA / ĐỐI SOÁT").FontSize(8.5f).Bold().FontColor("#6F4E37");
                note.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor("#ECE3D9");
                if (!string.IsNullOrWhiteSpace(report.CloseReason))
                {
                    note.Item().Text(text =>
                    {
                        text.Span("Lý do chênh lệch: ").Bold();
                        text.Span(report.CloseReason);
                    });
                }
                if (!string.IsNullOrWhiteSpace(report.ReconciliationReason))
                {
                    note.Item().Text(text =>
                    {
                        text.Span("Kết luận đối soát: ").Bold();
                        text.Span(report.ReconciliationReason);
                    });
                }
            });
        }
    }

    private static void Footer(IContainer container, DateTime generatedAtUtc)
    {
        container.BorderTop(1).BorderColor("#EAE0D6").PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text($"Tạo lúc {generatedAtUtc:dd/MM/yyyy HH:mm} UTC · Dữ liệu giá từ giao dịch kho")
                .FontSize(7.5f).FontColor("#64748B");
            row.AutoItem().DefaultTextStyle(style => style.FontSize(7.5f).FontColor("#64748B")).Text(text =>
            {
                text.Span("Trang ");
                text.CurrentPageNumber();
                text.Span("/");
                text.TotalPages();
            });
        });
    }

    private static void AddMetric(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(HeaderCell).Text(label).FontColor("#64748B");
        table.Cell().Element(BodyCell).Text(value).Bold();
    }

    private static IContainer CardBox(IContainer container) =>
        container.Border(1).BorderColor("#EAE0D6").Background("#FAF8F5").Padding(9);

    private static IContainer HeaderCell(IContainer container) =>
        container.Background("#FAF8F5").BorderBottom(1).BorderColor("#EAE0D6").Padding(5);

    private static IContainer BodyCell(IContainer container) =>
        container.Background("#FFFFFF").BorderBottom(1).BorderColor("#F2ECE5").Padding(5);

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;
    private static string Quantity(decimal value, string unit) => $"{value.ToString("0.###", VietnameseCulture)} {unit}";
    private static string NullableQuantity(decimal? value, string unit) => value.HasValue ? Quantity(value.Value, unit) : "Chưa chốt";
    private static string Money(decimal? value) => value.HasValue ? $"{value.Value.ToString("N0", VietnameseCulture)} đ" : "Chưa đủ dữ liệu";
}
