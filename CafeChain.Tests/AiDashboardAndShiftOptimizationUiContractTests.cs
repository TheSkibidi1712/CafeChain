namespace CafeChain.Tests;

public sealed class AiDashboardAndShiftOptimizationUiContractTests
{
    [Fact]
    public void DashboardFrontend_RendersExecutiveSectionsAndVietnameseStatuses()
    {
        var script = Read(
            "CafeChain",
            "wwwroot",
            "js",
            "Admin",
            "Dashboard",
            "dashboard-intelligence.js");

        Assert.Contains("Điểm đáng chú ý", script, StringComparison.Ordinal);
        Assert.Contains("Kết luận", script, StringComparison.Ordinal);
        Assert.Contains("data.overview || []", script, StringComparison.Ordinal);
        Assert.Contains("Complete: \"Đầy đủ\"", script, StringComparison.Ordinal);
        Assert.Contains("Partial: \"Một phần\"", script, StringComparison.Ordinal);
        Assert.Contains("Insufficient: \"Chưa đủ dữ liệu\"", script, StringComparison.Ordinal);
        Assert.Contains("Fallback: \"Chế độ dự phòng\"", script, StringComparison.Ordinal);
        Assert.Contains("localizedLabel", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduleConfigurationForm_HasFourLabeledGroupsAndNoProposalActions()
    {
        var view = Read(
            "CafeChain",
            "Areas",
            "Admin",
            "Views",
            "AdminShiftOptimization",
            "Index.cshtml");
        var script = Read(
            "CafeChain",
            "wwwroot",
            "js",
            "Admin",
            "StaffShift",
            "shift-optimization.js");

        Assert.Contains("<fieldset", view, StringComparison.Ordinal);
        Assert.Contains("<legend", view, StringComparison.Ordinal);
        Assert.Contains("aria-describedby", view, StringComparison.Ordinal);
        Assert.Contains("shiftOptimizationSetup", view, StringComparison.Ordinal);
        Assert.Contains("shiftOptimizationConfig", view, StringComparison.Ordinal);
        Assert.Contains("Cấu hình lịch &amp; cảnh báo", view, StringComparison.Ordinal);
        Assert.Contains("Thứ 2", view, StringComparison.Ordinal);
        Assert.Contains("renderConfig", script, StringComparison.Ordinal);
        Assert.Contains("configOutput", script, StringComparison.Ordinal);
        Assert.Contains("statusLabels", script, StringComparison.Ordinal);
        Assert.DoesNotContain("data-generate", view, StringComparison.Ordinal);
        Assert.DoesNotContain("data-explain", view, StringComparison.Ordinal);
        Assert.DoesNotContain("data-apply", view, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"proposal\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Tạo đề xuất", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Giải thích đề xuất", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Áp dụng lịch", script, StringComparison.Ordinal);
        Assert.DoesNotContain("reasonCodeLabels", script, StringComparison.Ordinal);
        Assert.DoesNotContain("shiftProposalResult", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduleProposalApisAndAiSkill_AreRemovedWhileHistoricalEntitiesRemain()
    {
        var controller = Read(
            "CafeChain", "Areas", "Admin", "Controllers",
            "AdminShiftOptimizationController.cs");
        var serviceInterface = Read(
            "CafeChain", "Application", "Interfaces", "Admin", "Staffs",
            "IShiftOptimizationService.cs");
        var repositoryInterface = Read(
            "CafeChain", "Infrastructure", "Interfaces", "Admin", "Staffs",
            "IShiftOptimizationRepository.cs");
        var aiInterface = Read(
            "CafeChain", "Application", "Interfaces", "AI", "IAIService.cs");
        var skillCatalog = Read(
            "CafeChain", "Application", "Services", "AI", "AISkillCatalog.cs");
        var settings = Read("CafeChain", "appsettings.json");
        var models = Read("CafeChain", "Models", "Staffs", "ShiftIntelligenceModels.cs");
        var dbContext = Read("CafeChain", "Data", "AppDbContext.cs");

        Assert.DoesNotContain(" Generate(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain(" Proposal(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain(" Explain(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain(" Apply(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("GenerateAsync", serviceInterface, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyAsync", serviceInterface, StringComparison.Ordinal);
        Assert.DoesNotContain("GetProposalAsync", repositoryInterface, StringComparison.Ordinal);
        Assert.DoesNotContain("GetScheduledStatusAsync", repositoryInterface, StringComparison.Ordinal);
        Assert.DoesNotContain("ExplainShiftProposalAsync", aiInterface, StringComparison.Ordinal);
        Assert.DoesNotContain("shift-proposal-explanation", skillCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ShiftOptimization\"", settings, StringComparison.Ordinal);

        Assert.Contains("class ScheduleOptimizationProposal", models, StringComparison.Ordinal);
        Assert.Contains("class ScheduleOptimizationAssignment", models, StringComparison.Ordinal);
        Assert.Contains("DbSet<ScheduleOptimizationProposal>", dbContext, StringComparison.Ordinal);
        Assert.Contains("DbSet<ScheduleOptimizationAssignment>", dbContext, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduleGapNotification_IsFeatureFlaggedPersistedAndReusesSignalRHub()
    {
        var worker = Read(
            "CafeChain",
            "Application",
            "Workers",
            "StaffScheduleGapNotificationWorker.cs");
        var service = Read(
            "CafeChain",
            "Application",
            "Services",
            "Admin",
            "Staffs",
            "StaffScheduleGapNotificationService.cs");
        var realtime = Read(
            "CafeChain",
            "wwwroot",
            "js",
            "Admin",
            "Notifications",
            "inventory-notification-realtime.js");
        var settings = Read("CafeChain", "appsettings.json");

        Assert.Contains("_options.Enabled", worker, StringComparison.Ordinal);
        Assert.Contains("GetLocalNow().Date.AddDays(1)", worker, StringComparison.Ordinal);
        Assert.Contains("LookaheadDays", worker, StringComparison.Ordinal);
        Assert.Contains("StaffScheduleNotificationTypes.Gap", service, StringComparison.Ordinal);
        Assert.Contains("ResolveByDeduplicationKeyAsync", service, StringComparison.Ordinal);
        Assert.Contains("ReminderCooldownHours", service, StringComparison.Ordinal);
        Assert.Contains("CanAccessStoreAsync", service, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.ShiftView", service, StringComparison.Ordinal);
        Assert.Contains("STAFF_SCHEDULE_GAP", realtime, StringComparison.Ordinal);
        Assert.Contains("Thiếu lịch nhân sự", realtime, StringComparison.Ordinal);
        Assert.Contains("\"StaffScheduleNotifications\"", settings, StringComparison.Ordinal);
        Assert.Contains("\"Enabled\": false", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplanationSchema_RequiresRecommendationPriorityAndVerifyCondition()
    {
        var schema = Read(
            "CafeChain",
            "Resources",
            "AI",
            "schemas",
            "dashboard-insight-explanation.schema.json");

        Assert.Contains("\"priority\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"verifyCondition\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"additionalProperties\": false", schema, StringComparison.Ordinal);
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
