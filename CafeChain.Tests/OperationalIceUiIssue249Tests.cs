using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Stores;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class OperationalIceUiIssue249Tests : IntegrationTestBase
{
    [Fact]
    public void IceUi_UsesVietnameseLabels()
    {
        var index = Read("CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Index.cshtml");
        var details = Read("CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Details.cshtml");

        Assert.Contains("Quản lý đá theo ca", index);
        Assert.Contains("Ngày kinh doanh", index);
        Assert.Contains("Xác nhận cấp", index);
        Assert.Contains("Đang vận hành", index);
        Assert.Contains("Cần đối soát", details);
        Assert.Contains("Bàn giao cùng ngày", details);
        Assert.Contains("Gửi chốt ca", details);
        Assert.Contains("Duyệt hao hụt", details);
    }

    [Fact]
    public void IceUi_UsesCafeChainTokens()
    {
        var css = Read("CafeChain", "wwwroot", "css", "Admin", "OperationalIce", "operational-ice.css");

        Assert.Contains("#F7F3EE", css);
        Assert.Contains("#FFFFFF", css);
        Assert.Contains("#6F4E37", css);
        Assert.Contains("#5C3F2B", css);
        Assert.Contains("#C67A45", css);
        Assert.Contains("#1F2937", css);
        Assert.Contains("#475569", css);
        Assert.Contains("#2F6F5E", css);
        Assert.Contains("#99623B", css);
        Assert.Contains("#991B1B", css);
    }

    [Fact]
    public void IceUi_HasNoEmptyDangerAlert()
    {
        var index = Read("CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Index.cshtml");
        var details = Read("CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Details.cshtml");
        var css = Read("CafeChain", "wwwroot", "css", "Admin", "OperationalIce", "operational-ice.css");

        Assert.Contains("!string.IsNullOrWhiteSpace(error)", index);
        Assert.Contains("!string.IsNullOrWhiteSpace(error)", details);
        Assert.Contains(".ice-notice-danger:empty", css);
        Assert.DoesNotContain("<div class=\"ice-notice ice-notice-danger\"></div>", index);
        Assert.DoesNotContain("<div class=\"ice-notice ice-notice-danger\"></div>", details);
    }

    [Fact]
    public void IceUi_NoHorizontalOverflow_1366x768()
    {
        var css = Read("CafeChain", "wwwroot", "css", "Admin", "OperationalIce", "operational-ice.css");

        Assert.Contains(".ice-shell", css);
        Assert.Contains("min-width: 0", css);
        Assert.Contains(".ice-table-scroll", css);
        Assert.Contains("max-width: 100%", css);
        Assert.Contains("overflow-x: auto", css);
        Assert.Contains("@media (max-width: 1180px)", css);
        Assert.Contains("grid-template-columns: minmax(0, 1fr)", css);
    }

    [Fact]
    public void Navigation_RequiresOperationalIceViewPermission()
    {
        var layout = Read("CafeChain", "Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml");

        Assert.Contains("effectivePermissions.Contains(PermissionConstants.OperationalIceView)", layout);
        Assert.Contains("asp-controller=\"AdminOperationalIce\"", layout);
        Assert.Contains("Quản lý đá theo ca", layout);
    }

    [Fact]
    public async Task IcePolicy_RequiresKgToGramConversion()
    {
        using var context = CreateDbContext();
        context.Stores.Add(new Store { StoreId = 9201, Name = "Cửa hàng test đá", Active = true, CreatedAt = DateTime.UtcNow });
        context.StoreInventories.Add(new StoreInventory
        {
            StoreId = 9201,
            IngredientId = 7,
            AvailableQty = 50_000m,
            ReservedQty = 0m,
            LastUpdated = DateTime.UtcNow,
            RowVersion = [0]
        });
        await context.SaveChangesAsync();

        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(9205, 9201)).ReturnsAsync(true);
        var conversion = new Mock<IUnitConversionService>();
        conversion.Setup(x => x.ConvertAsync(7, 1m, 2, null))
            .ReturnsAsync(ServiceResult<decimal>.Failure("Thiếu quy đổi."));
        var service = new OperationalIceService(context, scope.Object, unitConversionService: conversion.Object);

        var result = await service.SavePolicyAsync(
            new SaveIcePolicyRequest
            {
                StoreId = 9201,
                IngredientId = 7,
                DisplayUnitId = 2,
                SuggestedDailyQuantity = 30_000m,
                SuggestedShiftQuantity = 10_000m
            },
            new AdminActorContext
            {
                StaffId = 9205,
                StoreId = 9201,
                RoleNames = [RoleConstants.StoreManager]
            });

        Assert.False(result.IsSuccess);
        Assert.Contains("Chưa cấu hình quy đổi", result.Message);
        Assert.Empty(context.IcePolicies);
    }

    [Fact]
    public void IceUi_ConvertsDisplayQuantitiesAtControllerBoundary()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminOperationalIceController.cs");

        Assert.Contains("SuggestedShiftQuantity = request.SuggestedShiftQuantity * factor.Data", controller);
        Assert.Contains("InitialIssuedQuantity = converted.Data", controller);
        Assert.Contains("Quantity = converted.Data", controller);
        Assert.Contains("ReturnedQuantity = converted.Data", controller);
        Assert.Contains("/ detailDisplayToBaseFactor", controller);
    }

    [Fact]
    public void IceUi_ActionsFollowAllocationStateAndPermissions()
    {
        var details = Read("CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Details.cshtml");

        Assert.Contains("isOpen && Model.CanManage", details);
        Assert.Contains("isPendingApproval && Model.CanApprove", details);
        Assert.Contains("needsReconciliation && Model.CanApprove", details);
        Assert.Contains("Model.CanApprove", details);
        Assert.Contains("asp-action=\"ApproveVariance\"", details);
        Assert.Contains("asp-action=\"ReconcileVariance\"", details);
        Assert.Contains("asp-action=\"CancelAllocation\"", details);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { FindRepoRoot() }.Concat(parts).ToArray()));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Không tìm thấy root CafeChain.");
    }
}
