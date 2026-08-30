using CafeChain.Application.DTOs.AI;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Options;
using CafeChain.Application.Results;
using CafeChain.Application.Services.AI;
using CafeChain.Infrastructure.Interfaces.Analytics;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests;

public sealed class SupplierIntelligenceVietnamesePresentationTests
{
    private static readonly AdminActorContext Actor = new()
    {
        AccountId = 7,
        StaffId = 9,
        StoreId = 1,
        RoleNames = ["Chủ doanh nghiệp"]
    };

    [Theory]
    [InlineData("HIGH", "Cao")]
    [InlineData("MEDIUM", "Vừa phải")]
    [InlineData("INSUFFICIENT_DATA", "Chưa đủ dữ liệu")]
    [InlineData("SOMETHING_NEW", "Chưa xác định")]
    public void Confidence_codes_are_mapped_to_plain_vietnamese(string code, string expected)
    {
        Assert.Equal(expected, SupplierIntelligencePresentation.Confidence(code));
    }

    [Fact]
    public void Supplier_fallback_uses_vietnamese_component_names_and_no_technical_terms()
    {
        var result = SupplierIntelligencePresentation.BuildFallbackExplanation(
            new SupplierExplanationContextDto
            {
                SupplierId = 35,
                TotalScore = 92.5m,
                Confidence = "MEDIUM",
                ComponentScores = new Dictionary<string, decimal>
                {
                    ["price"] = 100,
                    ["onTime"] = 100,
                    ["fill"] = 100,
                    ["quality"] = 100,
                    ["leadTime"] = 50
                },
                Warnings = ["Kết quả chỉ hỗ trợ quyết định mua hàng."]
            });

        Assert.Contains("Mức độ tin cậy: vừa phải", result, StringComparison.Ordinal);
        Assert.Contains("giá mua", result, StringComparison.Ordinal);
        Assert.Contains("giao đúng hẹn", result, StringComparison.Ordinal);
        Assert.Contains("đáp ứng đủ số lượng", result, StringComparison.Ordinal);
        Assert.Contains("chất lượng hàng", result, StringComparison.Ordinal);
        Assert.Contains("thời gian giao", result, StringComparison.Ordinal);
        Assert.False(SupplierIntelligencePresentation.ContainsTechnicalTerms(result));
    }

    [Fact]
    public async Task No_rankable_supplier_explains_the_configured_receipt_requirement()
    {
        var result = await CreateService(
            [Offer(35, 97, 180_000m)],
            new Dictionary<int, int> { [35] = 0 }).CompareAsync(Actor, 1, 49, 11_000m);

        Assert.False(result.HasCompetitiveRanking);
        Assert.Contains("ít nhất 5 phiếu nhận đã xác nhận", result.RankingMessage, StringComparison.Ordinal);
        Assert.Contains(result.Candidates.Single().Warnings,
            warning => warning.Contains("0/5 phiếu nhận đã xác nhận trong 180 ngày gần nhất", StringComparison.Ordinal));
        Assert.Null(result.Candidates.Single().Rank);
    }

    [Fact]
    public async Task One_rankable_supplier_is_not_described_as_a_competitive_comparison()
    {
        var result = await CreateService(
            [Offer(35, 97, 180_000m), Offer(36, 98, 190_800m)],
            new Dictionary<int, int> { [35] = 5, [36] = 0 }).CompareAsync(Actor, 1, 49, 11_000m);

        Assert.False(result.HasCompetitiveRanking);
        Assert.Contains("Chỉ có một nhà cung cấp đủ dữ liệu", result.RankingMessage, StringComparison.Ordinal);
        Assert.Equal(1, result.Candidates.Single(item => item.SupplierId == 35).Rank);
        Assert.Null(result.Candidates.Single(item => item.SupplierId == 36).Rank);
    }

    [Fact]
    public async Task Two_suppliers_with_five_receipts_are_ranked_competitively()
    {
        var result = await CreateService(
            [Offer(35, 97, 180_000m), Offer(36, 98, 190_800m)],
            new Dictionary<int, int> { [35] = 5, [36] = 5 }).CompareAsync(Actor, 1, 49, 11_000m);

        Assert.True(result.HasCompetitiveRanking);
        Assert.Contains("Đã có từ hai nhà cung cấp đủ dữ liệu", result.RankingMessage, StringComparison.Ordinal);
        Assert.All(result.Candidates, candidate =>
        {
            Assert.True(candidate.Rankable);
            Assert.NotNull(candidate.Score);
            Assert.NotNull(candidate.Rank);
            Assert.Equal("MEDIUM", candidate.Confidence);
            Assert.Empty(candidate.Warnings);
        });
    }

    [Fact]
    public void Seed_and_popup_keep_the_supplier_comparison_contract_idempotent_and_vietnamese()
    {
        var root = FindRoot();
        var seed = File.ReadAllText(Path.Combine(root, "CafeChain", "Scripts", "SeedAll.sql"));
        var view = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views",
            "AdminPurchaseAdviceConsolidation", "Index.cshtml"));

        Assert.Contains("SUPPLIER_COMPARISON_HISTORY_V1", seed, StringComparison.Ordinal);
        Assert.Contains("(1,155),(2,125),(3,95),(4,65),(5,35)", seed, StringComparison.Ordinal);
        Assert.Contains("receiptLine.InventoryTransactionId IS NULL", seed, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", seed, StringComparison.Ordinal);
        Assert.Contains("HAVING COUNT(receipt.BranchReceiptId)<5", seed, StringComparison.Ordinal);
        Assert.Contains("Dữ liệu thử nghiệm — chế độ quan sát", view, StringComparison.Ordinal);
        Assert.Contains("Đủ điều kiện xếp hạng", view, StringComparison.Ordinal);
        Assert.Contains("[value] || 'Chưa xác định'", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Dữ liệu pilot (ShadowMode)", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Kết quả so sánh do backend tính toán", view, StringComparison.Ordinal);
    }

    private static SupplierIntelligenceService CreateService(
        List<IngredientSupplier> offers,
        IReadOnlyDictionary<int, int> receiptCounts)
    {
        var repository = new Mock<ISupplierIntelligenceRepository>();
        repository.Setup(item => item.GetOffersAsync(1, 49, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offers);

        var quality = new Mock<ISupplierQualityService>();
        quality.Setup(item => item.GetDashboardAsync(
                1, It.IsAny<int?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                Actor.StaffId, It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync((int storeId, int? supplierId, DateTime fromUtc, DateTime toUtc,
                int actorStaffId, IReadOnlyCollection<string> roles) =>
            {
                var count = receiptCounts.GetValueOrDefault(supplierId.GetValueOrDefault());
                return ServiceResult<SupplierQualityDashboardDto>.Success(new SupplierQualityDashboardDto
                {
                    StoreId = storeId,
                    SupplierId = supplierId,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    Performance = new SupplierPerformanceDto
                    {
                        StoreId = storeId,
                        SupplierId = supplierId.GetValueOrDefault(),
                        ConfirmedReceiptCount = count,
                        CompletedDeliveryCount = count,
                        ExpectedDateSampleCount = count,
                        OnTimeRate = count == 0 ? 0 : 100,
                        FillRate = count == 0 ? 0 : 100,
                        RejectionRate = 0,
                        IssueRate = 0
                    }
                });
            });

        var conversion = new Mock<IUnitConversionService>();
        conversion.Setup(item => item.ConvertAsync(49, It.IsAny<decimal>(), 1, null))
            .ReturnsAsync((int _, decimal quantity, int _, int? _) =>
                ServiceResult<decimal>.Success(quantity));

        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(item => item.CanAccessStoreAsync(Actor.StaffId, 1)).ReturnsAsync(true);

        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(item => item.HasPermissionAsync(Actor.AccountId, It.IsAny<string>(), 1))
            .ReturnsAsync(ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
            {
                AccountId = Actor.AccountId,
                Allowed = true
            }));

        var feature = new Mock<ISupplierIntelligenceFeatureGate>();
        feature.Setup(item => item.GetStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupplierIntelligenceFeatureState(
                true, true, false, new HashSet<int> { 1 }, "TEST"));

        return new SupplierIntelligenceService(
            repository.Object,
            quality.Object,
            conversion.Object,
            scope.Object,
            permissions.Object,
            Options.Create(new SupplierIntelligenceOptions
            {
                Enabled = true,
                ShadowMode = true,
                StoreAllowlist = [1],
                MediumConfidenceReceipts = 5,
                HighConfidenceReceipts = 20,
                PerformanceWindowDays = 180,
                PriceWeight = 30,
                OnTimeWeight = 20,
                FillWeight = 20,
                QualityWeight = 20,
                LeadTimeWeight = 10
            }),
            feature.Object);
    }

    private static IngredientSupplier Offer(int supplierId, int offerId, decimal price) => new()
    {
        IngredientSupplierId = offerId,
        IngredientId = 49,
        SupplierId = supplierId,
        UnitId = 1,
        PackageQuantity = 1_000,
        CurrentPrice = price,
        MinimumOrderPackageCount = 1,
        LeadTimeDays = supplierId == 35 ? 3 : 4,
        Active = true,
        Supplier = new Supplier
        {
            SupplierId = supplierId,
            Name = supplierId == 35
                ? "Đối tác Topping & Syrup TP.HCM"
                : "Đối tác Topping & Syrup Bình Dương",
            Active = true
        }
    };

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
