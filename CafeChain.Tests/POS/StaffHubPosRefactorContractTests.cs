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
        Assert.Contains("id=\"resendTerminalOtp\" type=\"button\" class=\"staffhub-dialog-button is-secondary\" disabled", view, StringComparison.Ordinal);
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
