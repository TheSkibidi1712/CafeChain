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
            page.Margin(30);
            page.DefaultTextStyle(style => style.FontSize(9).FontColor("#1F2937"));
            page.Header().Element(container => Header(container, report));
            page.Content().PaddingVertical(14).Column(column => Content(column, report));
            page.Footer().Row(row =>
            {
                row.RelativeItem().Text($"Tạo lúc {generatedAtUtc:dd/MM/yyyy HH:mm} UTC · Dữ liệu giá từ giao dịch kho")
                    .FontSize(7).FontColor("#64748B");
                row.AutoItem().DefaultTextStyle(style => style.FontSize(7).FontColor("#64748B")).Text(text =>
                {
                    text.Span("Trang ");
                    text.CurrentPageNumber();
                    text.Span("/");
                    text.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Header(IContainer container, OperationalIceReportDto report)
    {
        container.BorderBottom(1).BorderColor("#D6D3D1").PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("CAFECHAIN").FontSize(15).Bold().FontColor("#6F4E37");
                column.Item().Text("BÁO CÁO ĐÁ VẬN HÀNH").FontSize(18).Bold();
            });
            row.AutoItem().AlignRight().Column(column =>
            {
                column.Item().Text(report.StoreName).Bold();
                column.Item().Text($"{report.BusinessDate:dd/MM/yyyy} · {report.OperationalShiftName}");
                column.Item().Text($"Mã cấp đá #{report.IceAllocationId}").FontColor("#64748B");
            });
        });
    }

    private static void Content(ColumnDescriptor column, OperationalIceReportDto report)
    {
        column.Spacing(12);
        column.Item().Row(row =>
        {
            row.RelativeItem().Element(InfoBox).Column(info =>
            {
                info.Item().Text("CA VẬN HÀNH").Bold().FontColor("#6F4E37");
                info.Item().Text($"Thời gian: {report.StartAtUtc:dd/MM HH:mm} - {report.EndAtUtc:dd/MM HH:mm} UTC");
                info.Item().Text($"Nguyên liệu: {report.IngredientName}");
                info.Item().Text($"Trạng thái: {OperationalIceDisplayText.Status(report.Status)}");
                info.Item().Text($"Ca bán hàng POS: {(report.WorkShiftIds.Count == 0 ? "-" : string.Join(", ", report.WorkShiftIds.Select(x => $"#{x}")))}");
            });
            row.ConstantItem(10);
            row.RelativeItem().Element(InfoBox).Column(info =>
            {
                info.Item().Text("NHÂN SỰ").Bold().FontColor("#6F4E37");
                info.Item().Text($"Người cấp: {Value(report.IssuedBy)}");
                info.Item().Text($"Ca trưởng/Người nhận: {Value(report.ShiftLead)}");
                info.Item().Text($"Người chốt: {Value(report.ClosedBy)}");
                info.Item().Text($"Người duyệt: {Value(report.ApprovedBy)}");
            });
        });

        column.Item().Text("ĐỐI CHIẾU SỐ LƯỢNG").FontSize(11).Bold();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
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

        column.Item().Text("GIÁ VỐN VÀ ĐỐI SOÁT").FontSize(11).Bold();
        column.Item().Element(InfoBox).Column(cost =>
        {
            cost.Item().Row(row =>
            {
                row.RelativeItem().Text($"Giá vốn theo POS: {Money(report.TheoreticalCost)}");
                row.RelativeItem().Text($"Giá vốn chênh lệch: {Money(report.VarianceCost)}");
                row.RelativeItem().Text($"Giá vốn thực tế: {Money(report.ActualCost)}").Bold();
            });
            cost.Item().PaddingTop(5).Text(OperationalIceDisplayText.CostStatus(report.CostStatus)).FontColor("#99623B");
            if (report.HasUsageSnapshotMismatch)
            {
                cost.Item().PaddingTop(5).Text(
                    $"Cảnh báo đối soát: tiêu hao đã lưu {Quantity(report.TheoreticalUsage, report.UnitName)}, dữ liệu giao dịch hiện tại {Quantity(report.LedgerTheoreticalUsage, report.UnitName)}.")
                    .FontColor("#991B1B");
            }
        });

        column.Item().Text("THAM CHIẾU BÚT TOÁN KHO").FontSize(11).Bold();
        if (report.InventoryPostings.Count == 0)
        {
            column.Item().Text("Không phát sinh bút toán điều chỉnh kho cho ca này.").FontColor("#64748B");
        }
        else
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(2.5f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.2f);
                });
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Loại").Bold();
                    header.Cell().Element(HeaderCell).Text("Mã chống trùng lặp").Bold();
                    header.Cell().Element(HeaderCell).Text("Giao dịch kho").Bold();
                    header.Cell().Element(HeaderCell).AlignRight().Text("Giá trị").Bold();
                });
                foreach (var posting in report.InventoryPostings)
                {
                    table.Cell().Element(BodyCell).Text(OperationalIceDisplayText.PostingType(posting.PostingType));
                    table.Cell().Element(BodyCell).Text(posting.IdempotencyKey);
                    table.Cell().Element(BodyCell).Text(posting.InventoryTransactionId?.ToString() ?? "-");
                    table.Cell().Element(BodyCell).AlignRight().Text(Money(posting.TotalCost));
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(report.CloseReason) || !string.IsNullOrWhiteSpace(report.ReconciliationReason))
        {
            column.Item().Element(InfoBox).Column(note =>
            {
                note.Item().Text("GHI CHÚ CHỐT CA / ĐỐI SOÁT").Bold().FontColor("#6F4E37");
                if (!string.IsNullOrWhiteSpace(report.CloseReason)) note.Item().Text(report.CloseReason);
                if (!string.IsNullOrWhiteSpace(report.ReconciliationReason)) note.Item().Text(report.ReconciliationReason);
            });
        }
    }

    private static void AddMetric(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(HeaderCell).Text(label).FontColor("#64748B");
        table.Cell().Element(BodyCell).Text(value).Bold();
    }

    private static IContainer InfoBox(IContainer container) =>
        container.Border(1).BorderColor("#E7E5E4").Background("#FAFAF9").Padding(9);

    private static IContainer HeaderCell(IContainer container) =>
        container.Background("#F7F3EE").BorderBottom(1).BorderColor("#D6D3D1").Padding(6);

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor("#E7E5E4").Padding(6);

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;
    private static string Quantity(decimal value, string unit) => $"{value.ToString("0.###", VietnameseCulture)} {unit}";
    private static string NullableQuantity(decimal? value, string unit) => value.HasValue ? Quantity(value.Value, unit) : "Chưa chốt";
    private static string Money(decimal? value) => value.HasValue ? $"{value.Value.ToString("N0", VietnameseCulture)} đ" : "Chưa đủ dữ liệu";
}
