using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Inventories;
using CafeChain.Infrastrusture.Interfaces.Systems;
using Moq;

namespace CafeChain.Tests;

public sealed class InventoryIssuePolicyTests
{
    [Theory]
    [InlineData(InventoryIssueOperation.AdjustmentOut, InventoryIssueReasonCodes.AdjustmentOutNegativeForbidden)]
    [InlineData(InventoryIssueOperation.Waste, InventoryIssueReasonCodes.WasteNegativeForbidden)]
    [InlineData(InventoryIssueOperation.ProductionOut, InventoryIssueReasonCodes.ProductionOutNegativeForbidden)]
    [InlineData(InventoryIssueOperation.TransferDispatch, InventoryIssueReasonCodes.TransferSourceNegativeForbidden)]
    public async Task Strict_operations_are_blocked_when_projected_stock_is_negative(
        InventoryIssueOperation operation,
        string expectedReason)
    {
        var policy = CreatePolicy(ValidSettings(enabled: true, limit: 100));

        var result = await policy.EvaluateAsync(Request(operation, before: 2, issue: 3));

        Assert.Equal(InventoryIssueOutcome.Blocked, result.Outcome);
        Assert.Equal(expectedReason, result.ReasonCode);
    }

    [Theory]
    [InlineData("SALE")]
    [InlineData("GIFT")]
    [InlineData("DEBT")]
    [InlineData("SAMPLE")]
    public async Task Allowed_manual_purposes_require_approval_at_exact_limit(string purpose)
    {
        var policy = CreatePolicy(ValidSettings(enabled: true, limit: 3));

        var result = await policy.EvaluateAsync(Request(
            InventoryIssueOperation.ManualExternalExport,
            before: 2,
            issue: 5,
            purpose: purpose,
            reason: "Hàng đã giao thực tế"));

        Assert.Equal(InventoryIssueOutcome.ApprovalRequired, result.Outcome);
        Assert.Equal(-3, result.ProjectedAfterQty);
        Assert.Equal(3, result.EffectiveMaxNegativeQty);
    }

    [Fact]
    public async Task Manual_feature_is_fail_closed_when_disabled()
    {
        var policy = CreatePolicy(ValidSettings(enabled: false, limit: 10));

        var result = await policy.EvaluateAsync(Request(
            InventoryIssueOperation.ManualExternalExport,
            before: 0,
            issue: 1,
            purpose: "SALE",
            reason: "Đã giao"));

        Assert.Equal(InventoryIssueReasonCodes.ManualNegativeFeatureDisabled, result.ReasonCode);
        Assert.Equal(InventoryIssueOutcome.Blocked, result.Outcome);
    }

    [Fact]
    public async Task Pos_blind_sale_does_not_read_manual_settings()
    {
        var settings = new Mock<IInventoryIssueSettingsProvider>(MockBehavior.Strict);
        var policy = new InventoryIssuePolicy(settings.Object);

        var result = await policy.EvaluateAsync(Request(
            InventoryIssueOperation.PosBlindSale,
            before: 0,
            issue: 1));

        Assert.Equal(InventoryIssueOutcome.Allowed, result.Outcome);
        Assert.Equal("pos-adr-0001", result.PolicyVersion);
        settings.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Item_limit_overrides_default_and_over_limit_is_blocked()
    {
        var policy = CreatePolicy(ValidSettings(enabled: true, limit: 20));
        var request = Request(
            InventoryIssueOperation.ManualExternalExport,
            before: 0,
            issue: 3,
            purpose: "SALE",
            reason: "Đã giao") with { ItemMaxNegativeQty = 2 };

        var result = await policy.EvaluateAsync(request);

        Assert.Equal(InventoryIssueReasonCodes.ManualNegativeLimitExceeded, result.ReasonCode);
    }

    [Fact]
    public async Task Malformed_settings_are_fail_closed()
    {
        var policy = CreatePolicy(new(false, false, true, 0, string.Empty));

        var result = await policy.EvaluateAsync(Request(
            InventoryIssueOperation.ManualExternalExport,
            before: 0,
            issue: 1,
            purpose: "SALE",
            reason: "Đã giao"));

        Assert.Equal(InventoryIssueReasonCodes.NegativeSettingInvalid, result.ReasonCode);
    }

    [Fact]
    public async Task Provider_reads_repository_on_every_request_and_rejects_unsafe_configuration()
    {
        var repository = new Mock<ISystemSettingRepository>();
        repository.SetupSequence(x => x.GetValuesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(Values(enabled: false, approvalRequired: true, limit: "0"))
            .ReturnsAsync(Values(enabled: true, approvalRequired: false, limit: "5"));
        var provider = new InventoryIssueSettingsProvider(repository.Object);

        var first = await provider.GetManualExternalExportSettingsAsync();
        var second = await provider.GetManualExternalExportSettingsAsync();

        Assert.True(first.IsValid);
        Assert.False(second.IsValid);
        repository.Verify(x => x.GetValuesAsync(It.IsAny<IEnumerable<string>>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Available_quantity_is_used_directly()
    {
        var policy = CreatePolicy(ValidSettings(enabled: false, limit: 0));

        var result = await policy.EvaluateAsync(Request(
            InventoryIssueOperation.TransferDispatch,
            before: 5,
            issue: 5));

        Assert.Equal(InventoryIssueOutcome.Allowed, result.Outcome);
        Assert.Equal(0, result.ProjectedAfterQty);
    }

    [Fact]
    public async Task Approval_is_stale_when_reason_or_inventory_row_version_changes()
    {
        var policy = CreatePolicy(ValidSettings(enabled: true, limit: 3));
        var evidence = new InventoryApprovalEvidence(
            1, 1, 10, null, 2, -1, 3, "manual-export-v1",
            "requester", "approver", true, true, 3, "Giao hàng thực tế", [1]);
        var approved = Request(
            InventoryIssueOperation.ManualExternalExport,
            before: 2,
            issue: 3,
            purpose: "SALE",
            reason: "Giao hàng thực tế") with
        {
            Approval = evidence,
            InventoryRowVersion = [1]
        };

        Assert.Equal(InventoryIssueOutcome.Allowed, (await policy.EvaluateAsync(approved)).Outcome);
        Assert.Equal(
            InventoryIssueReasonCodes.ApprovalStale,
            (await policy.EvaluateAsync(approved with { Reason = "Lý do đã đổi" })).ReasonCode);
        Assert.Equal(
            InventoryIssueReasonCodes.ApprovalStale,
            (await policy.EvaluateAsync(approved with { InventoryRowVersion = [2] })).ReasonCode);
    }

    private static InventoryIssuePolicy CreatePolicy(InventoryManualNegativeSettings settings)
    {
        var provider = new Mock<IInventoryIssueSettingsProvider>();
        provider.Setup(x => x.GetManualExternalExportSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        return new InventoryIssuePolicy(provider.Object);
    }

    private static InventoryManualNegativeSettings ValidSettings(bool enabled, decimal limit) =>
        new(true, enabled, true, limit, "manual-export-v1");

    private static InventoryIssueRequest Request(
        InventoryIssueOperation operation,
        decimal before,
        decimal issue,
        string? purpose = null,
        string? reason = null) =>
        new(operation, 1, 10, null, before, issue, null, purpose, reason, null, null);

    private static Dictionary<string, string> Values(bool enabled, bool approvalRequired, string limit) =>
        new()
        {
            [InventoryIssueSettingsProvider.EnabledKey] = enabled.ToString(),
            [InventoryIssueSettingsProvider.ApprovalRequiredKey] = approvalRequired.ToString(),
            [InventoryIssueSettingsProvider.DefaultLimitKey] = limit,
            [InventoryIssueSettingsProvider.PolicyVersionKey] = "manual-export-v1"
        };
}
