using System.Globalization;

namespace CafeChain.Application.Services.AI;

public sealed record OperationalAnomalyPresentationDto(
    string MetricDisplayName,
    string UnitLabel,
    string CurrentValueDisplay,
    string BaselineValueDisplay,
    string DeviationDisplay,
    string DirectionDescription,
    string SeverityDisplay,
    string StatusDisplay,
    string ConfidenceDisplay,
    IReadOnlyList<string> ReasonSummaries,
    IReadOnlyList<string> SuggestedChecks,
    string ImpactSummary,
    string WhyDetected,
    IReadOnlyList<string> ImmediateActions,
    IReadOnlyList<string> PreparationChecklist);

public static class OperationalAnomalyPresentation
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public static OperationalAnomalyPresentationDto Build(
        string metricCode,
        decimal currentValue,
        decimal baselineValue,
        decimal percentageDeviation,
        string severity,
        string status,
        string confidence,
        IReadOnlyList<string> reasonCodes)
    {
        var definition = Definition(metricCode);
        var direction = currentValue >= baselineValue ? "cao hơn" : "thấp hơn";
        var absolutePercent = Math.Abs(percentageDeviation).ToString("P1", Vietnamese);
        return new OperationalAnomalyPresentationDto(
            definition.Name,
            definition.Unit,
            FormatValue(currentValue, definition.ValueKind),
            FormatValue(baselineValue, definition.ValueKind),
            $"{direction} {absolutePercent}",
            $"Giá trị ghi nhận {direction} mức thông thường trước đây {absolutePercent}.",
            severity switch
            {
                "CRITICAL" => "Cần kiểm tra ngay",
                "HIGH" => "Cần kiểm tra",
                _ => "Cần theo dõi"
            },
            status switch
            {
                "OPEN" => "Chưa tiếp nhận",
                "ACKNOWLEDGED" => "Đã tiếp nhận",
                "RESOLVED" => "Đã xử lý",
                _ => "Chưa xác định"
            },
            confidence switch
            {
                "HIGH" => "Dữ liệu tham chiếu tốt",
                "MEDIUM" => "Dữ liệu tham chiếu vừa phải",
                "INSUFFICIENT_DATA" => "Chưa đủ dữ liệu tham chiếu",
                _ => "Chưa xác định"
            },
            reasonCodes.Select(MapReason).Distinct().ToArray(),
            definition.Checks,
            definition.Impact,
            $"Tín hiệu được phát hiện vì giá trị hiện tại {direction} mức thông thường trước đây {absolutePercent} và có các dấu hiệu: {string.Join(", ", reasonCodes.Select(MapReason).Distinct())}.",
            definition.Actions,
            definition.Preparation);
    }

    public static string BuildFallbackExplanation(
        string metricDisplayName,
        string currentValueDisplay,
        string baselineValueDisplay,
        string directionDescription,
        IReadOnlyList<string> suggestedChecks)
    {
        var checks = suggestedChecks.Count == 0
            ? "Hãy đối chiếu dữ liệu nguồn và quy trình vận hành liên quan."
            : $"Nên kiểm tra: {string.Join("; ", suggestedChecks)}.";
        return $"Phát hiện {metricDisplayName.ToLower(Vietnamese)} có giá trị {currentValueDisplay}, trong khi mức thông thường trước đây là {baselineValueDisplay}. {directionDescription} Ảnh hưởng có thể xảy ra: cần đối soát lại dữ liệu và chuẩn bị chứng từ liên quan trước khi quyết định vận hành. Vì vậy hệ thống phát tín hiệu để người quản lý kiểm tra sớm. {checks} Đây chỉ là tín hiệu cần xác minh, chưa đủ cơ sở kết luận nguyên nhân, gian lận hoặc trách nhiệm cá nhân.";
    }

    private static string MapReason(string code) => code switch
    {
        "BELOW_SEASONAL_BASELINE" => "Thấp hơn mức thường thấy trong lịch sử.",
        "ABOVE_SEASONAL_BASELINE" => "Cao hơn mức thường thấy trong lịch sử.",
        "MATERIAL_DEVIATION" => "Mức chênh lệch đủ lớn để cần đối chiếu.",
        "ROBUST_SCORE_EXCEEDED" => "Khác biệt rõ so với các ngày có dữ liệu trước đó.",
        _ => "Có dấu hiệu khác với dữ liệu vận hành thông thường."
    };

    private static string FormatValue(decimal value, AnomalyValueKind kind) => kind switch
    {
        AnomalyValueKind.Currency => $"{value.ToString("N0", Vietnamese)} ₫",
        AnomalyValueKind.Count => $"{value.ToString("N0", Vietnamese)} lần",
        AnomalyValueKind.OrderCount => $"{value.ToString("N0", Vietnamese)} đơn",
        _ => value.ToString("N2", Vietnamese)
    };

    private static AnomalyDefinition Definition(string metricCode)
    {
        if (metricCode.StartsWith("PRODUCT_VOLUME:", StringComparison.Ordinal))
            return new("Sản lượng bán của sản phẩm", "sản phẩm", AnomalyValueKind.Quantity,
            [
                "đối chiếu tình trạng còn bán của sản phẩm",
                "kiểm tra giờ hoạt động và lượng đơn trong ngày",
                "kiểm tra thay đổi menu hoặc tồn kho nguyên liệu"
            ], "Có thể làm lệch kế hoạch chuẩn bị nguyên liệu, nhân sự và lượng sản phẩm cần sản xuất.",
            ["đối chiếu số đơn và trạng thái bán của sản phẩm", "kiểm tra tồn kho BTP/nguyên liệu liên quan"],
            ["báo cáo đơn theo khung giờ", "lịch menu và ca nhân sự", "sổ xuất kho hoặc lệnh sản xuất"]);

        return metricCode switch
        {
            "REVENUE" => new("Doanh thu thuần", "đồng", AnomalyValueKind.Currency,
            [
                "đơn hoàn tất, đơn hủy và đơn hoàn tiền",
                "khuyến mãi hoặc thay đổi giờ hoạt động",
                "đối chiếu doanh thu theo ca và phương thức thanh toán"
            ], "Có thể ảnh hưởng dự báo doanh thu, kế hoạch nhập hàng và phân bổ nhân sự.",
            ["đối chiếu doanh thu theo ca", "kiểm tra đơn hủy, hoàn tiền và khuyến mãi"],
            ["báo cáo doanh thu theo ca", "danh sách thanh toán/hoàn tiền", "chương trình khuyến mãi"]),
            "ORDER_COUNT" => new("Số đơn hoàn tất", "đơn", AnomalyValueKind.OrderCount,
            [
                "số đơn theo từng khung giờ",
                "đơn hủy hoặc gián đoạn bán hàng",
                "tình trạng terminal và ca làm việc"
            ], "Có thể ảnh hưởng năng lực phục vụ, định biên ca và kế hoạch chuẩn bị hàng.",
            ["kiểm tra phân bố đơn theo giờ", "đối chiếu trạng thái terminal và ca POS"],
            ["báo cáo đơn hoàn tất/hủy", "log terminal", "lịch mở ca và phân ca"]),
            "WASTE_ADJUSTMENT" => new("Hao hụt và điều chỉnh tồn kho", "đơn vị quy đổi", AnomalyValueKind.Quantity,
            [
                "phiếu điều chỉnh và lý do hao hụt",
                "kiểm kê theo ca và chứng từ kho liên quan",
                "đơn vị quy đổi của nguyên liệu bị điều chỉnh"
            ], "Có thể làm tăng giá vốn, làm sai số dư tồn kho và gây thiếu nguyên liệu đột xuất.",
            ["đối chiếu phiếu điều chỉnh với kiểm kê thực tế", "xác nhận đơn vị chuẩn trước khi ghi nhận"],
            ["phiếu điều chỉnh/kiểm kê", "BOM và quy đổi đơn vị", "lịch sử nhập-xuất kho"]),
            "CASH_DISCREPANCY" => new("Chênh lệch tiền mặt cuối ca", "đồng", AnomalyValueKind.Currency,
            [
                "tiền đầu ca, tiền kiểm đếm cuối ca và tiền bán bằng tiền mặt",
                "giao dịch hoàn tiền, hủy đơn hoặc thanh toán ghi nhận muộn",
                "biên bản đối soát của các ca trong ngày"
            ], "Có thể làm chậm đối soát và ảnh hưởng quyết định khóa ca hoặc bàn giao tiền.",
            ["tạm giữ kết luận và đối chiếu tiền đầu/cuối ca", "kiểm tra hoàn tiền, hủy đơn và thanh toán muộn"],
            ["biên bản kiểm đếm", "báo cáo thanh toán tiền mặt", "lịch sử hoàn/hủy đơn"]),
            "SUPPLIER_ISSUE" => new("Sự cố nhà cung cấp", "sự cố", AnomalyValueKind.Count,
            [
                "phiếu nhận hàng và sự cố chất lượng mới phát sinh",
                "ngày giao dự kiến so với ngày nhận thực tế",
                "nhà cung cấp hoặc nguyên liệu xuất hiện nhiều lần"
            ], "Có thể ảnh hưởng chất lượng nguyên liệu, tiến độ nhập và khả năng phục vụ.",
            ["kiểm tra phiếu nhận và số lượng bị ảnh hưởng", "đánh giá phương án thay thế hoặc giao bù"],
            ["phiếu nhận hàng", "biên bản sự cố", "lịch giao và lịch sử nhà cung cấp"]),
            _ => new("Chỉ số vận hành", "giá trị", AnomalyValueKind.Quantity,
            ["dữ liệu nguồn của ngày phát hiện", "chứng từ và thao tác vận hành liên quan"],
            "Có thể ảnh hưởng quyết định vận hành nếu không được xác minh kịp thời.",
            ["đối chiếu dữ liệu nguồn với chứng từ gốc"],
            ["báo cáo nguồn", "chứng từ vận hành liên quan"])
        };
    }

    private enum AnomalyValueKind { Currency, Count, OrderCount, Quantity }
    private sealed record AnomalyDefinition(
        string Name,
        string Unit,
        AnomalyValueKind ValueKind,
        IReadOnlyList<string> Checks,
        string Impact,
        IReadOnlyList<string> Actions,
        IReadOnlyList<string> Preparation);
}
