using CafeChain.Application.Interfaces.Inventories;

namespace CafeChain.Application.Constants;

public static class PosInventoryWarningDisplayText
{
    private const string IncompleteCogsMessage =
        "Chưa đủ dữ liệu để xác định đầy đủ giá vốn của giao dịch.";

    public static string ToBusinessMessage(string? warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
            return "Có cảnh báo tồn kho cần được kiểm tra.";

        return warning.StartsWith(SalesCogsCodes.Incomplete, StringComparison.Ordinal)
            ? IncompleteCogsMessage
            : warning;
    }
}
