using CafeChain.Application.Options;
using CafeChain.Application.Services.AI;
using CafeChain.Infrastrusture.Interfaces.Systems;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests;

public sealed class AdminAiSupplierRefactorTests
{
    [Fact]
    public void Operational_anomaly_presentation_uses_plain_vietnamese_and_actionable_checks()
    {
        var result = OperationalAnomalyPresentation.Build(
            "CASH_DISCREPANCY",
            75_000m,
            5_000m,
            14m,
            "HIGH",
            "ACKNOWLEDGED",
            "MEDIUM",
            ["ABOVE_SEASONAL_BASELINE", "ROBUST_SCORE_EXCEEDED"]);

        Assert.Equal("Chênh lệch tiền mặt cuối ca", result.MetricDisplayName);
        Assert.Equal("Cần kiểm tra", result.SeverityDisplay);
        Assert.Equal("Đã tiếp nhận", result.StatusDisplay);
        Assert.Contains("75.000", result.CurrentValueDisplay, StringComparison.Ordinal);
        Assert.Contains(result.SuggestedChecks, x => x.Contains("tiền đầu ca", StringComparison.OrdinalIgnoreCase));

        var fallback = OperationalAnomalyPresentation.BuildFallbackExplanation(
            result.MetricDisplayName,
            result.CurrentValueDisplay,
            result.BaselineValueDisplay,
            result.DirectionDescription,
            result.SuggestedChecks);
        Assert.DoesNotContain("robust", fallback, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("baseline", fallback, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chưa đủ cơ sở kết luận", fallback, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Supplier_feature_gate_prefers_system_settings_and_limits_store_allowlist()
    {
        var repository = new Mock<ISystemSettingRepository>();
        repository.Setup(x => x.GetValuesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, string>
            {
                [SupplierIntelligenceFeatureGate.EnabledKey] = "true",
                [SupplierIntelligenceFeatureGate.ShadowModeKey] = "true",
                [SupplierIntelligenceFeatureGate.FullRolloutKey] = "false",
                [SupplierIntelligenceFeatureGate.StoreAllowlistKey] = "1; 3,invalid"
            });
        var service = new SupplierIntelligenceFeatureGate(
            repository.Object,
            Options.Create(new SupplierIntelligenceOptions
            {
                Enabled = false,
                FullRollout = true
            }));

        var state = await service.GetStateAsync();

        Assert.Equal("SYSTEM_SETTINGS", state.Source);
        Assert.Equal("SHADOW", state.Mode);
        Assert.True(state.IsEnabledForStore(1));
        Assert.True(state.IsEnabledForStore(3));
        Assert.False(state.IsEnabledForStore(2));
    }

    [Fact]
    public async Task Supplier_feature_gate_uses_configuration_only_when_no_runtime_keys_exist()
    {
        var repository = new Mock<ISystemSettingRepository>();
        repository.Setup(x => x.GetValuesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, string>());
        var service = new SupplierIntelligenceFeatureGate(
            repository.Object,
            Options.Create(new SupplierIntelligenceOptions
            {
                Enabled = true,
                ShadowMode = false,
                StoreAllowlist = [9]
            }));

        var state = await service.GetStateAsync();

        Assert.Equal("CONFIGURATION", state.Source);
        Assert.Equal("ACTIVE", state.Mode);
        Assert.True(state.IsEnabledForStore(9));
        Assert.False(state.IsEnabledForStore(1));
    }

    [Fact]
    public void Admin_layout_notifications_supplier_and_anomaly_ui_have_required_contracts()
    {
        var layout = Read("CafeChain", "Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml");
        Assert.Contains("AdminBranchReceipts", layout, StringComparison.Ordinal);
        Assert.Contains("OperationalAnomalyView", layout, StringComparison.Ordinal);
        Assert.Contains("PosWorkShiftApproveLateOpen", layout, StringComparison.Ordinal);
        Assert.Contains("Tín hiệu vận hành", layout, StringComparison.Ordinal);
        Assert.Contains("Duyệt mở ca trễ", layout, StringComparison.Ordinal);

        var notifications = Read("CafeChain", "Application", "Services", "Operations", "StaffNotificationQueryService.cs");
        Assert.Contains("/Admin/AdminWorkShiftOpenApprovals#approval-", notifications, StringComparison.Ordinal);
        Assert.Contains("Xem và duyệt yêu cầu", notifications, StringComparison.Ordinal);

        var lateOpenApproval = Read("CafeChain", "Areas", "Admin", "Views", "AdminWorkShiftOpenApprovals", "Index.cshtml");
        Assert.Contains("name=\"Decision\"", lateOpenApproval, StringComparison.Ordinal);
        Assert.Contains("data-decision-value", lateOpenApproval, StringComparison.Ordinal);
        Assert.Contains("data-decision=\"REJECTED\"", lateOpenApproval, StringComparison.Ordinal);
        Assert.Contains("data-decision=\"CONVERTED_TO_OUTSIDE_SCHEDULE\"", lateOpenApproval, StringComparison.Ordinal);
        Assert.Contains("!item.CanApproveAsScheduled", lateOpenApproval, StringComparison.Ordinal);
        Assert.DoesNotContain("form.querySelectorAll('button').forEach", lateOpenApproval, StringComparison.Ordinal);

        var supplier = Read("CafeChain", "Areas", "Admin", "Views", "AdminPurchaseAdviceConsolidation", "Index.cshtml");
        Assert.Contains("renderSupplierComparison", supplier, StringComparison.Ordinal);
        Assert.Contains("Dữ liệu pilot (ShadowMode)", supplier, StringComparison.Ordinal);
        Assert.Contains("document.createElement", supplier, StringComparison.Ordinal);

        var anomaly = Read("CafeChain", "Areas", "Admin", "Views", "AdminOperationalAnomalies", "Index.cshtml");
        Assert.Contains("Giá trị ghi nhận", anomaly, StringComparison.Ordinal);
        Assert.Contains("Mức thông thường trước đây", anomaly, StringComparison.Ordinal);
        Assert.Contains("Thông tin kỹ thuật", anomaly, StringComparison.Ordinal);

        var anomalyClient = Read("CafeChain", "wwwroot", "js", "Admin", "Dashboard", "operational-anomalies.js");
        Assert.Contains("buildExplanationContent", anomalyClient, StringComparison.Ordinal);
        Assert.Contains("textContent", anomalyClient, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", anomalyClient, StringComparison.Ordinal);
    }

    [Fact]
    public void SeedAll_creates_non_destructive_supplier_pilot_settings_and_late_open_matrix()
    {
        var seed = Read("CafeChain", "Scripts", "SeedAll.sql");

        Assert.Contains("CafeChain Thủ Dầu Một", seed, StringComparison.Ordinal);
        Assert.Contains("supplier_intelligence_shadow_mode", seed, StringComparison.Ordinal);
        Assert.Contains("supplier_intelligence_full_rollout", seed, StringComparison.Ordinal);
        Assert.Contains("supplier_intelligence_store_allowlist", seed, StringComparison.Ordinal);
        Assert.Contains("IF NOT EXISTS(SELECT 1 FROM dbo.SystemSettings", seed, StringComparison.Ordinal);
        Assert.Contains("(N'POS.WorkShift.ApproveLateOpen',1,1,1,0,0,0,0,0)", seed, StringComparison.Ordinal);
    }

    [Fact]
    public void Late_open_business_documents_allow_30_to_45_and_block_after_45_minutes()
    {
        var staffHubGuide = Read("CafeChain", "Doc", "STAFFHUB_USER_BUSINESS_FLOWS.md");
        var terminalGuide = Read("CafeChain", "Doc", "POS_TERMINAL_USER_GUIDE.md");
        var businessRules = Read("CafeChain", "Doc", "STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md");

        Assert.Contains("30 đến 45 phút", staffHubGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("trên 45 phút", staffHubGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nút **Duyệt mở ca** bị khóa", staffHubGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("**Từ chối** hoặc **Chuyển ngoài lịch**", terminalGuide, StringComparison.Ordinal);
        Assert.Contains("LATE_OPEN_REQUIRES_OUTSIDE_SCHEDULE", businessRules, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([FindRoot(), .. parts]));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
