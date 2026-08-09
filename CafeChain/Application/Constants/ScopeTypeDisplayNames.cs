using CafeChain.Application.Interfaces.Security;

namespace CafeChain.Application.Constants;

/// <summary>
/// Tên hiển thị tiếng Việt của phạm vi dữ liệu. Code và ID trong database vẫn
/// được giữ ổn định; không dùng tên hiển thị để quyết định nghiệp vụ.
/// </summary>
public static class ScopeTypeDisplayNames
{
    public const string Country = "Toàn chuỗi";
    public const string Province = "Tỉnh/Thành phố";
    public const string Ward = "Xã/Phường/Đặc khu";
    public const string Store = "Cửa hàng";

    public static string FromCode(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "COUNTRY" => Country,
        "PROVINCE" => Province,
        "WARD" => Ward,
        "STORE" => Store,
        _ => string.IsNullOrWhiteSpace(code) ? "Phạm vi" : code.Trim()
    };

    public static string FromLevel(ScopeLevel level) => level switch
    {
        ScopeLevel.Country => Country,
        ScopeLevel.Province => Province,
        ScopeLevel.Ward => Ward,
        ScopeLevel.Store => Store,
        _ => "Phạm vi"
    };
}
