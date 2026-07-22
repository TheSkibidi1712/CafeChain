namespace CafeChain.ViewModels.Shared;

public sealed record LogoutFormViewModel(
    string Label,
    string FormCssClass,
    string ButtonCssClass,
    string IconCssClass,
    string? FormStyle = null,
    string? ButtonStyle = null,
    string? IconStyle = null)
{
    public static LogoutFormViewModel AdminSidebar() => new(
        "Đăng xuất",
        string.Empty,
        "btn btn-link shadow-none",
        "fas fa-sign-out-alt",
        FormStyle: "padding: 4px 0;",
        ButtonStyle: "text-decoration:none; color:#dc3545; font-weight:500; font-size:13.5px; padding:10px 20px; width:100%; text-align:left;",
        IconStyle: "width:22px; text-align:center; margin-right:10px;");

    public static LogoutFormViewModel CustomerMenu() => new(
        "Đăng xuất tài khoản",
        "m-0 p-0",
        "btn btn-light w-100 rounded-3 py-2 text-danger fw-bold",
        "bi bi-box-arrow-right me-2");

    public static LogoutFormViewModel AppLauncher() => new(
        "Đăng xuất",
        string.Empty,
        "launcher-logout",
        "bi bi-box-arrow-right");
}
