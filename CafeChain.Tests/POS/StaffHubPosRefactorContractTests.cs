using CafeChain.Application.DTOs.POS;

namespace CafeChain.Tests.POS;

public sealed class StaffHubPosRefactorContractTests
{
    [Fact]
    public void React_POS_only_submits_starting_cash_when_opening()
    {
        var page = Read("CafeChain.Frontend", "src", "pages", "ShiftSummary.tsx");

        Assert.Contains("'/api/v1/pos/shifts/open', {", page, StringComparison.Ordinal);
        Assert.Contains("startingCash,", page, StringComparison.Ordinal);
        Assert.DoesNotContain("open-assessment", page, StringComparison.Ordinal);
        Assert.DoesNotContain("OPEN_SHIFT_OUTSIDE_SCHEDULE", page, StringComparison.Ordinal);
        Assert.DoesNotContain("OPEN_SHIFT_LATE", page, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/pos/terminals/register", page, StringComparison.Ordinal);
    }

    [Fact]
    public void POS_open_endpoint_requires_exchange_context_and_has_no_public_assessment()
    {
        var controller = Read("CafeChain", "Controllers", "Api", "v1", "POSShiftController.cs");

        Assert.Contains("CurrentExchangeContextId", controller, StringComparison.Ordinal);
        Assert.Contains("PosOpenContextRequired", controller, StringComparison.Ordinal);
        Assert.Contains("[FromBody] OpenPosSessionRequestDto request", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("open-assessment", controller, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(RepoRoot(), "CafeChain", "Controllers", "Api", "v1", "POSTerminalController.cs")));
    }

    [Fact]
    public void Exchange_ticket_is_hash_only_one_time_and_has_distinct_errors()
    {
        var service = Read("CafeChain", "Application", "Services", "POS", "PosSessionExchangeService.cs")
            + Read("CafeChain", "Application", "DTOs", "POS", "PosSessionExchangeDtos.cs");

        Assert.Contains("TimeSpan.FromSeconds(60)", service, StringComparison.Ordinal);
        Assert.Contains("Hash(rawCode)", service, StringComparison.Ordinal);
        Assert.Contains(PosSessionExchangeErrorCodes.Expired, service, StringComparison.Ordinal);
        Assert.Contains(PosSessionExchangeErrorCodes.AlreadyUsed, service, StringComparison.Ordinal);
        Assert.Contains(PosSessionExchangeErrorCodes.Invalid, service, StringComparison.Ordinal);
        Assert.Contains("ticket.Status = \"EXPIRED\"", service, StringComparison.Ordinal);
        Assert.Contains("context.CancelledAtUtc", service, StringComparison.Ordinal);
        Assert.DoesNotContain("ticket.Status = \"CANCELLED\"", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Late_open_policy_starts_at_30_and_only_blocks_scheduled_approval_after_45()
    {
        var workShift = Read("CafeChain", "Application", "Services", "POS", "WorkShiftService.cs");
        var approvals = Read("CafeChain", "Application", "Services", "POS", "WorkShiftOpenApprovalService.cs");
        var options = Read("CafeChain", "Application", "Options", "WorkShiftOptions.cs");

        Assert.Contains("minutesLate >= _workShiftOptions.LateApprovalAfterMinutes", workShift, StringComparison.Ordinal);
        Assert.Contains("assessment.Data.MinutesLate < _options.LateApprovalAfterMinutes", approvals, StringComparison.Ordinal);
        Assert.Contains("approval.MinutesLate > _options.ResolveLateScheduledApprovalMaxMinutes()", approvals, StringComparison.Ordinal);
        Assert.Contains("LateScheduledApprovalMaxMinutes { get; set; } = 45", options, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_and_resume_responses_expose_explicit_result_codes()
    {
        var shiftController = Read("CafeChain", "Controllers", "Api", "v1", "POSShiftController.cs");
        var staffHubController = Read("CafeChain", "Controllers", "StaffHubController.cs");
        var dto = Read("CafeChain", "Application", "DTOs", "POS", "ShiftSummaryDto.cs");

        Assert.Contains("OpenedNewWorkShift", shiftController, StringComparison.Ordinal);
        Assert.Contains("RequiresOpeningCash = false", shiftController, StringComparison.Ordinal);
        Assert.Contains("ResumeExistingWorkShift", staffHubController, StringComparison.Ordinal);
        Assert.Contains("requiresOpeningCash = true", staffHubController, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requiresOpeningCash = false", staffHubController, StringComparison.Ordinal);
        Assert.Contains("ResultCode", dto, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_active_queries_are_deterministic_if_dirty_data_exists()
    {
        var repository = Read("CafeChain", "Infrastructure", "Repositories", "Admin", "POS", "WorkShiftRepository.cs");

        Assert.Contains("OrderByDescending(ws => ws.StartTimeUtc)", repository, StringComparison.Ordinal);
        Assert.Contains("ThenByDescending(ws => ws.ShiftId)", repository, StringComparison.Ordinal);
        Assert.Contains("OrderByDescending(x => x.StartTimeUtc)", repository, StringComparison.Ordinal);
        Assert.Contains("ThenByDescending(x => x.ShiftId)", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_database_guards_cover_all_active_responsibility_states()
    {
        var db = Read("CafeChain", "Data", "Configurations", "Stores", "StoreConfiguration.cs");

        Assert.Contains("UX_WorkShifts_ActiveStaff", db, StringComparison.Ordinal);
        Assert.Contains("UX_WorkShifts_ActiveTerminal", db, StringComparison.Ordinal);
        Assert.Contains("EXPIRED_PENDING_CLOSE", db, StringComparison.Ordinal);
        Assert.Contains("CLOSING", db, StringComparison.Ordinal);
        Assert.Contains("OPEN", db, StringComparison.Ordinal);
    }

    [Fact]
    public void Utc_wire_and_defensive_client_parser_preserve_the_same_instant()
    {
        var service = Read("CafeChain", "Application", "Services", "POS", "WorkShiftService.cs");
        var parser = Read("CafeChain.Frontend", "src", "utils", "utcDateTime.ts");

        Assert.Contains("StartTimeUtc = AsUtc(shift.StartTimeUtc)", service, StringComparison.Ordinal);
        Assert.Contains("AutoCloseAtUtc = AsUtc(shift.AutoCloseAtUtc)", service, StringComparison.Ordinal);
        Assert.Contains("ServerNowUtc = AsUtc", service, StringComparison.Ordinal);
        Assert.Contains("`${timestamp}Z`", parser, StringComparison.Ordinal);
    }

    [Fact]
    public void Staffhub_never_precreates_workshift_and_cash_commit_remains_in_pos()
    {
        var controller = Read("CafeChain", "Controllers", "StaffHubController.cs");
        var exchange = Read("CafeChain", "Application", "DTOs", "POS", "PosSessionExchangeDtos.cs");
        var shift = Read("CafeChain.Frontend", "src", "pages", "ShiftSummary.tsx");

        Assert.DoesNotContain("new WorkShift", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("StartingCash = 0m", controller, StringComparison.Ordinal);
        Assert.Contains("workShiftId = (int?)null", controller, StringComparison.Ordinal);
        Assert.Contains("committed later by /api/v1/pos/shifts/open", controller, StringComparison.Ordinal);
        Assert.Contains("RequiresOpeningCash", exchange, StringComparison.Ordinal);
        Assert.Contains("Xác nhận mở ca", shift, StringComparison.Ordinal);
    }

    [Fact]
    public void App_launcher_starts_vite_then_routes_opening_through_staffhub()
    {
        var view = Read("CafeChain", "Views", "AppLauncher", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "AppLauncher", "app-launcher.js");

        Assert.Contains("data-staffhub-pos-url", view, StringComparison.Ordinal);
        Assert.Contains("new { openPos = 1 }", view, StringComparison.Ordinal);
        Assert.Contains("root.dataset.staffhubPosUrl", script, StringComparison.Ordinal);
        Assert.DoesNotContain("IssuePosToken", script, StringComparison.Ordinal);
        Assert.DoesNotContain("pos_token", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Blocking_terminal_contract_distinguishes_owner_and_current_operator()
    {
        var service = Read("CafeChain", "Application", "Services", "POS", "WorkShiftService.cs");
        var script = Read("CafeChain", "wwwroot", "js", "StaffHub", "staffhub-schedule.js");
        var rules = Read("CafeChain", "Doc", "STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md");

        Assert.Contains("ResponsibleStaffName", service, StringComparison.Ordinal);
        Assert.Contains("IsOwnedByRequester", service, StringComparison.Ordinal);
        Assert.Contains("SwitchCurrentOperator", service, StringComparison.Ordinal);
        Assert.Contains("resumeButton.hidden = !isOwnedByRequester", script, StringComparison.Ordinal);
        Assert.Contains("Đổi Current Operator", script, StringComparison.Ordinal);
        Assert.Contains("WorkShift.UserId", rules, StringComparison.Ordinal);
        Assert.Contains("CurrentOperatorStaffId", rules, StringComparison.Ordinal);
    }

    [Fact]
    public void Seedall_adds_fixed_idempotent_sales_accounts_without_shared_pin()
    {
        var seed = Read("CafeChain", "Scripts", "SeedAll.sql");
        var login = Read("CafeChain", "Views", "Account", "Login.cshtml");

        Assert.Contains("AccountId=16", seed, StringComparison.Ordinal);
        Assert.Contains("AccountId=17", seed, StringComparison.Ordinal);
        Assert.Contains("salesstaff2@cafechain.vn", seed, StringComparison.Ordinal);
        Assert.Contains("salesstaff3@cafechain.vn", seed, StringComparison.Ordinal);
        Assert.Contains("PasswordHash<>@PosTestPasswordHash", seed, StringComparison.Ordinal);
        Assert.Contains("PosPinHash,PosPinFailedAttempts", seed, StringComparison.Ordinal);
        Assert.Contains("VALUES(16,16,N'Nhân viên bán hàng 2'", seed, StringComparison.Ordinal);
        Assert.Contains("VALUES(17,17,N'Nhân viên bán hàng 3'", seed, StringComparison.Ordinal);
        Assert.Contains("Bán hàng 2 – Store 1", login, StringComparison.Ordinal);
        Assert.Contains("Bán hàng 3 – Store 3", login, StringComparison.Ordinal);
    }

    [Fact]
    public void Otp_approved_state_hides_controls_and_uses_sweet_alert()
    {
        var view = Read("CafeChain", "Views", "StaffHub", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "StaffHub", "staffhub-schedule.js");
        var controller = Read("CafeChain", "Controllers", "StaffHubController.cs");

        Assert.Contains("openPosOtpVerification", view, StringComparison.Ordinal);
        Assert.Contains("openPosOtpVerified", view, StringComparison.Ordinal);
        Assert.Contains("sweetalert2", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("otpVerification.hidden = !(canEnterCode || isTerminalState)", script, StringComparison.Ordinal);
        Assert.Contains("otpVerified.hidden = !isApproved", script, StringComparison.Ordinal);
        Assert.Contains("GetOpenPosOtpState", controller, StringComparison.Ordinal);
        Assert.Contains("Status423Locked", controller, StringComparison.Ordinal);
        Assert.Contains("Status429TooManyRequests", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void StaffHub_terminal_and_resend_otp_ui_has_accessible_states()
    {
        var view = Read("CafeChain", "Views", "StaffHub", "Index.cshtml");
        var css = Read("CafeChain", "wwwroot", "css", "StaffHub", "staffhub.css");
        var script = Read("CafeChain", "wwwroot", "js", "StaffHub", "staffhub-schedule.js");

        Assert.Contains(".staffhub-terminal-picker > span", css, StringComparison.Ordinal);
        Assert.Contains("color: #fff", css, StringComparison.Ordinal);
        Assert.Contains(".staffhub-dialog-button.is-secondary:not(:disabled):hover", css, StringComparison.Ordinal);
        Assert.Contains("cursor: not-allowed", css, StringComparison.Ordinal);
        Assert.Contains("id=\"resendOpenPosOtp\" type=\"button\" class=\"staffhub-dialog-button is-secondary\" disabled", view, StringComparison.Ordinal);
        Assert.Contains("id=\"resendTerminalOtp\" type=\"button\" class=\"staffhub-dialog-button is-primary\" disabled hidden", view, StringComparison.Ordinal);
        Assert.Contains("resendAvailableInSeconds", script, StringComparison.Ordinal);
        Assert.Contains("startResendCountdown", script, StringComparison.Ordinal);
        Assert.Contains("clearResendCountdown", script, StringComparison.Ordinal);
        Assert.Contains("Gửi lại OTP (${formatCountdown(remaining)})", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Terminal_user_guide_explains_mandatory_selection_and_active_lock()
    {
        var guide = Read("CafeChain", "Doc", "POS_TERMINAL_USER_GUIDE.md");
        var rules = Read("CafeChain", "Doc", "STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md");

        Assert.Contains("bắt buộc phải chọn một terminal active", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TERMINAL_ALREADY_HAS_OPEN_SHIFT", guide, StringComparison.Ordinal);
        Assert.Contains("OPEN", guide, StringComparison.Ordinal);
        Assert.Contains("CLOSING", guide, StringComparison.Ordinal);
        Assert.Contains("EXPIRED_PENDING_CLOSE", guide, StringComparison.Ordinal);
        Assert.Contains("POS_TERMINAL_USER_GUIDE.md", rules, StringComparison.Ordinal);
    }

    [Fact]
    public void Resume_exchange_and_workshift_realtime_are_terminal_safe()
    {
        var controller = Read("CafeChain", "Controllers", "StaffHubController.cs");
        var script = Read("CafeChain", "wwwroot", "js", "StaffHub", "staffhub-schedule.js");
        var auth = Read("CafeChain", "Extensions", "Services", "AuthenticationServiceExtensions.cs");
        var main = Read("CafeChain.Frontend", "src", "main.tsx");
        var layout = Read("CafeChain.Frontend", "src", "POSLayout.tsx");
        var summary = Read("CafeChain.Frontend", "src", "pages", "ShiftSummary.tsx");

        Assert.Contains("StaffHubResumePosRequestDto request", controller, StringComparison.Ordinal);
        Assert.Contains("TerminalId: terminalSelect?.value", script, StringComparison.Ordinal);
        Assert.Contains("/hubs/workshifts", auth, StringComparison.Ordinal);
        Assert.Contains("clearPosAuthentication()", main, StringComparison.Ordinal);
        Assert.Contains("window.location.replace", main, StringComparison.Ordinal);
        Assert.DoesNotContain(".finally(() =>", main, StringComparison.Ordinal);
        Assert.Contains("queueMicrotask(() =>", layout, StringComparison.Ordinal);
        Assert.Contains("queueMicrotask(() =>", summary, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepoRoot(), .. path]));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
