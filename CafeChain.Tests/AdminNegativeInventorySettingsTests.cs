using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Settings;
using CafeChain.Application.Services.Admin.Settings;
using CafeChain.Application.Services.Inventories;
using CafeChain.Infrastrusture.Repositories.Systems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CafeChain.Tests;

public sealed class AdminNegativeInventorySettingsTests : IntegrationTestBase
{
    private static readonly AdminActorContext OwnerActor = new()
    {
        StaffId = 1,
        RoleNames = new[] { RoleConstants.BusinessOwner }
    };

    [Fact]
    public async Task Seeded_store_3_ingredient_2_is_blocked_until_runtime_configuration_is_saved()
    {
        using var context = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AdminSettingService(context, cache);

        var result = await service.GetNegativeInventorySettingsAsync(OwnerActor);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.Data.IsConfigurationValid);
        Assert.False(result.Data.Enabled);
        Assert.Equal(0m, result.Data.DefaultMaxNegativeQuantity);
        var item = Assert.Single(result.Data.Items.Where(x => x.StoreId == 3 && x.ItemId == 2));
        Assert.Equal(4, item.StoreInventoryId);
        Assert.Null(item.MaxNegativeQty);
        Assert.Equal(0m, item.EffectiveMaxNegativeQty);
        Assert.False(item.CanRequestNegative);
        Assert.Equal("Feature đang tắt", item.EligibilityText);
    }

    [Fact]
    public async Task Owner_can_enable_feature_and_set_store_item_limit_with_audit_and_new_policy_version()
    {
        using var context = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AdminSettingService(context, cache);
        var before = await service.GetNegativeInventorySettingsAsync(OwnerActor);
        var item = Assert.Single(before.Data.Items.Where(x => x.StoreInventoryId == 4));

        var result = await service.UpdateNegativeInventorySettingsAsync(
            new UpdateNegativeInventorySettingsDTO
            {
                Enabled = true,
                DefaultMaxNegativeQuantity = 0,
                Items =
                [
                    new UpdateNegativeInventoryItemDTO
                    {
                        StoreInventoryId = 4,
                        LimitMode = NegativeInventoryLimitModes.Custom,
                        MaxNegativeQuantity = 5m,
                        RowVersion = item.RowVersion
                    }
                ]
            },
            OwnerActor);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.Data.Changed);
        Assert.NotEqual(before.Data.PolicyVersion, result.Data.PolicyVersion);
        Assert.Equal(5m, (await context.StoreInventories.SingleAsync(x => x.StoreInventoryId == 4)).MaxNegativeQty);
        Assert.Equal("true", (await context.SystemSettings.SingleAsync(
            x => x.SettingKey == InventoryIssueSettingsProvider.EnabledKey)).SettingValue);
        Assert.Equal("true", (await context.SystemSettings.SingleAsync(
            x => x.SettingKey == InventoryIssueSettingsProvider.ApprovalRequiredKey)).SettingValue);
        Assert.Equal(2, await context.AuditLogs.CountAsync(x =>
            x.Action == "UPDATE_NEGATIVE_POLICY" || x.Action == "UPDATE_MAX_NEGATIVE_QTY"));

        var runtimeSettings = await new InventoryIssueSettingsProvider(
            new SystemSettingRepository(context)).GetManualExternalExportSettingsAsync();
        Assert.True(runtimeSettings.IsValid);
        Assert.True(runtimeSettings.Enabled);
        Assert.True(runtimeSettings.ApprovalRequired);
        Assert.Equal(result.Data.PolicyVersion, runtimeSettings.PolicyVersion);
    }

    [Fact]
    public async Task No_op_update_does_not_change_policy_version_or_create_audit()
    {
        using var context = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AdminSettingService(context, cache);
        var before = await service.GetNegativeInventorySettingsAsync(OwnerActor);
        var item = Assert.Single(before.Data.Items.Where(x => x.StoreInventoryId == 4));

        var result = await service.UpdateNegativeInventorySettingsAsync(
            new UpdateNegativeInventorySettingsDTO
            {
                Enabled = false,
                DefaultMaxNegativeQuantity = 0,
                Items =
                [
                    new UpdateNegativeInventoryItemDTO
                    {
                        StoreInventoryId = 4,
                        LimitMode = NegativeInventoryLimitModes.Default,
                        RowVersion = item.RowVersion
                    }
                ]
            },
            OwnerActor);

        Assert.True(result.IsSuccess, result.Message);
        Assert.False(result.Data.Changed);
        Assert.Equal(before.Data.PolicyVersion, result.Data.PolicyVersion);
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Stale_row_version_returns_conflict_code_without_mutation()
    {
        using var context = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AdminSettingService(context, cache);

        var result = await service.UpdateNegativeInventorySettingsAsync(
            new UpdateNegativeInventorySettingsDTO
            {
                Enabled = true,
                DefaultMaxNegativeQuantity = 0,
                Items =
                [
                    new UpdateNegativeInventoryItemDTO
                    {
                        StoreInventoryId = 4,
                        LimitMode = NegativeInventoryLimitModes.Custom,
                        MaxNegativeQuantity = 5m,
                        RowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9 })
                    }
                ]
            },
            OwnerActor);

        Assert.False(result.IsSuccess);
        Assert.Equal("NEGATIVE_INVENTORY_SETTING_STALE", result.ErrorCode);
        Assert.Null((await context.StoreInventories.SingleAsync(x => x.StoreInventoryId == 4)).MaxNegativeQty);
        Assert.Equal("false", (await context.SystemSettings.SingleAsync(
            x => x.SettingKey == InventoryIssueSettingsProvider.EnabledKey)).SettingValue);
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Invalid_custom_limit_and_unauthorized_actor_are_rejected()
    {
        using var context = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AdminSettingService(context, cache);
        var before = await service.GetNegativeInventorySettingsAsync(OwnerActor);
        var item = Assert.Single(before.Data.Items.Where(x => x.StoreInventoryId == 4));

        var invalid = await service.UpdateNegativeInventorySettingsAsync(
            new UpdateNegativeInventorySettingsDTO
            {
                Items =
                [
                    new UpdateNegativeInventoryItemDTO
                    {
                        StoreInventoryId = 4,
                        LimitMode = NegativeInventoryLimitModes.Custom,
                        MaxNegativeQuantity = 0,
                        RowVersion = item.RowVersion
                    }
                ]
            },
            OwnerActor);
        var unauthorized = await service.GetNegativeInventorySettingsAsync(new AdminActorContext
        {
            StaffId = 3,
            RoleNames = new[] { RoleConstants.StoreManager }
        });

        Assert.False(invalid.IsSuccess);
        Assert.Equal("NEGATIVE_INVENTORY_SETTING_VALIDATION", invalid.ErrorCode);
        Assert.False(unauthorized.IsSuccess);
        Assert.Equal("FORBIDDEN", unauthorized.ErrorCode);
    }

    [Fact]
    public async Task Generic_settings_update_cannot_bypass_negative_inventory_action()
    {
        using var context = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AdminSettingService(context, cache);

        var result = await service.SaveSettingsAsync(new Dictionary<string, string>
        {
            [InventoryIssueSettingsProvider.EnabledKey] = "true"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("SETTING_KEY_NOT_ALLOWED", result.ErrorCode);
        Assert.Equal("false", (await context.SystemSettings.SingleAsync(
            x => x.SettingKey == InventoryIssueSettingsProvider.EnabledKey)).SettingValue);
    }
}
