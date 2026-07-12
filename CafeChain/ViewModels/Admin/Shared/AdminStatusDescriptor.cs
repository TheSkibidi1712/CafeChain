namespace CafeChain.ViewModels.Admin.Shared
{
    /// <summary>
    /// Presentation-only status chip. Each domain maps its own codes to descriptors;
    /// do not merge domain state machines into one enum.
    /// </summary>
    public sealed class AdminStatusDescriptor
    {
        public string Code { get; init; } = "";
        public string Label { get; init; } = "";
        public string CssClass { get; init; } = "";
        public string? AccessibleDescription { get; init; }

        public static AdminStatusDescriptor Of(string code, string label, string cssClass, string? accessible = null)
            => new()
            {
                Code = code,
                Label = label,
                CssClass = cssClass,
                AccessibleDescription = accessible ?? label
            };
    }

    /// <summary>Domain-specific display mappers (labels preserved from #126–#128 UX).</summary>
    public static class AdminStatusDisplay
    {
        public static AdminStatusDescriptor RecipeActive(bool active) =>
            active
                ? AdminStatusDescriptor.Of("active", "Hoạt động", "rb-status-active")
                : AdminStatusDescriptor.Of("inactive", "Ngưng", "rb-status-inactive");

        public static AdminStatusDescriptor RecipeCost(bool? costComplete, string? costStatusText = null)
        {
            if (costComplete == true)
                return AdminStatusDescriptor.Of("cost_complete", costStatusText ?? "Đủ dữ liệu", "rb-status-complete");
            if (costComplete == false)
                return AdminStatusDescriptor.Of("cost_incomplete", costStatusText ?? "Thiếu dữ liệu", "rb-status-incomplete");
            return AdminStatusDescriptor.Of("cost_unknown", costStatusText ?? "—", "rb-status-inactive");
        }

        public static AdminStatusDescriptor PreparedItemConfig(string configStatusKey, string configStatusLabel)
        {
            var css = configStatusKey switch
            {
                "has_active" => "rb-status-complete",
                "inactive" => "rb-status-inactive",
                _ => "rb-status-incomplete"
            };
            return AdminStatusDescriptor.Of(configStatusKey, configStatusLabel, css);
        }

        public static AdminStatusDescriptor RestockRequest(string status) =>
            status?.ToUpperInvariant() switch
            {
                "SUBMITTED" => AdminStatusDescriptor.Of(status, "Đã gửi", "rb-status-incomplete"),
                "PROCESSING" => AdminStatusDescriptor.Of(status, "Đang xử lý", "rb-status-active"),
                "PARTIALLY_RECEIVED" => AdminStatusDescriptor.Of(status, "Nhận một phần", "rb-status-incomplete"),
                "COMPLETED" => AdminStatusDescriptor.Of(status, "Hoàn tất", "rb-status-complete"),
                "REJECTED" => AdminStatusDescriptor.Of(status, "Từ chối", "rb-status-inactive"),
                "CANCELLED" => AdminStatusDescriptor.Of(status, "Đã hủy", "rb-status-inactive"),
                _ => AdminStatusDescriptor.Of(status ?? "", status ?? "—", "rb-status-inactive")
            };

        public static AdminStatusDescriptor BranchReceipt(string status) =>
            status?.ToUpperInvariant() switch
            {
                "DRAFT" => AdminStatusDescriptor.Of(status, "Nháp", "rb-status-incomplete"),
                "CONFIRMED" => AdminStatusDescriptor.Of(status, "Đã xác nhận", "rb-status-complete"),
                _ => AdminStatusDescriptor.Of(status ?? "", status ?? "—", "rb-status-inactive")
            };

        public static AdminStatusDescriptor UnitConversionRow(string statusKey, string statusLabel)
        {
            var css = statusKey switch
            {
                "ok" => "rb-status-complete",
                "package_conflict" => "rb-status-incomplete",
                "review" => "rb-status-incomplete",
                _ => "rb-status-inactive"
            };
            return AdminStatusDescriptor.Of(statusKey, statusLabel, css);
        }
    }
}
