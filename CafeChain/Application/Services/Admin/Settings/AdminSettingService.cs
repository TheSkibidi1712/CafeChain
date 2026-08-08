using System.Globalization;
using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Settings;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Admin.Settings;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Inventories.Approvals;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Stores;
using CafeChain.Models.Systems;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CafeChain.Application.Services.Admin.Settings;

public sealed class AdminSettingService : IAdminSettingService
{
    private const string CacheKey = "App_Settings_Dict";
    private const decimal MaxSupportedQuantity = 999999999999999.999m;

    private static readonly string[] NegativeSettingKeys =
    [
        InventoryIssueSettingsProvider.EnabledKey,
        InventoryIssueSettingsProvider.ApprovalRequiredKey,
        InventoryIssueSettingsProvider.DefaultLimitKey,
        InventoryIssueSettingsProvider.PolicyVersionKey
    ];

    private static readonly HashSet<string> GeneralSettingAllowList = new(StringComparer.Ordinal)
    {
        "company_brand_name", "company_tax_code", "company_address", "company_hotline", "receipt_footer",
        "pos_vat_rate", "pos_auto_lock_mins", "pos_grace_period_days",
        "timekeep_late_buffer", "timekeep_face_auth", "timekeep_ip_bypass",
        "api_momo", "api_vnpay"
    };

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IUnitConversionService? _unitConversionService;

    public AdminSettingService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public AdminSettingService(
        AppDbContext context,
        IMemoryCache cache,
        IUnitConversionService unitConversionService)
        : this(context, cache)
    {
        _unitConversionService = unitConversionService;
    }

    public async Task<Dictionary<string, string>> GetSettingsDictionaryAsync()
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, string>? cachedSettings)
            && cachedSettings != null)
        {
            return cachedSettings;
        }

        var settingsArray = await _context.SystemSettings.ToListAsync();
        var dict = settingsArray
            .GroupBy(s => s.SettingKey)
            .ToDictionary(g => g.Key, g => g.First().SettingValue ?? string.Empty);

        var changed = false;
        foreach (var key in GeneralSettingAllowList)
        {
            if (dict.ContainsKey(key))
                continue;

            _context.SystemSettings.Add(new SystemSetting { SettingKey = key, SettingValue = string.Empty });
            dict[key] = string.Empty;
            changed = true;
        }

        if (changed)
            await _context.SaveChangesAsync();

        _cache.Set(CacheKey, dict, TimeSpan.FromHours(24));
        return dict;
    }

    public async Task<ServiceResult> SaveSettingsAsync(Dictionary<string, string> settings)
    {
        if (settings == null || settings.Count == 0)
            return ServiceResult.Success("Không có cấu hình nào cần lưu.");

        var forbiddenKeys = settings.Keys
            .Where(key => !GeneralSettingAllowList.Contains(key))
            .OrderBy(key => key)
            .ToList();
        if (forbiddenKeys.Count > 0)
        {
            return ServiceResult.Failure(
                "Yêu cầu chứa khóa cấu hình không được phép cập nhật tại biểu mẫu này.",
                forbiddenKeys,
                "SETTING_KEY_NOT_ALLOWED");
        }

        var existingSettings = await _context.SystemSettings.ToListAsync();
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var (key, value) in settings)
            {
                var setting = existingSettings.FirstOrDefault(s => s.SettingKey == key);
                if (setting != null)
                {
                    setting.SettingValue = value ?? string.Empty;
                }
                else
                {
                    _context.SystemSettings.Add(new SystemSetting
                    {
                        SettingKey = key,
                        SettingValue = value ?? string.Empty
                    });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            _cache.Remove(CacheKey);
            return ServiceResult.Success("Đã cập nhật hệ thống thành công.");
        }
        catch
        {
            await transaction.RollbackAsync();
            return ServiceResult.Failure("Lưu cấu hình thất bại.", errorCode: "SETTING_UPDATE_FAILED");
        }
    }

    public async Task<ServiceResult<NegativeInventorySettingsDTO>> GetNegativeInventorySettingsAsync(
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageNegativeInventory(actor))
        {
            return ServiceResult<NegativeInventorySettingsDTO>.Failure(
                "Bạn không có quyền xem cấu hình âm kho.",
                errorCode: "FORBIDDEN");
        }

        var rawSettings = await _context.SystemSettings
            .AsNoTracking()
            .Where(x => NegativeSettingKeys.Contains(x.SettingKey))
            .ToListAsync(cancellationToken);
        var state = ParseNegativeSettings(rawSettings);

        var rawItems = await _context.StoreInventories
            .AsNoTracking()
            .Where(x => (x.IngredientId != null && x.PreparedItemId == null)
                        || (x.IngredientId == null && x.PreparedItemId != null))
            .Select(x => new
            {
                Inventory = x,
                StoreName = x.Store.Name,
                StoreActive = x.Store.Active,
                ItemType = x.IngredientId != null ? "Nguyên liệu" : "Bán thành phẩm",
                ItemId = x.IngredientId ?? x.PreparedItemId!.Value,
                ItemCode = x.IngredientId != null ? x.Ingredient.Code : x.PreparedItem!.Code,
                ItemName = x.IngredientId != null ? x.Ingredient.Name : x.PreparedItem!.Name,
                ItemActive = x.IngredientId != null ? x.Ingredient.Active : x.PreparedItem!.Active,
                BaseUnitId = x.IngredientId != null
                    ? x.Ingredient.BaseUnitId
                    : x.PreparedItem!.BaseUnitId,
                BaseUnitCode = x.IngredientId != null
                    ? x.Ingredient.BaseUnit.UnitCode
                    : x.PreparedItem!.BaseUnit.UnitCode
            })
            .OrderBy(x => x.Inventory.StoreId)
            .ThenBy(x => x.ItemType)
            .ThenBy(x => x.ItemCode)
            .ToListAsync(cancellationToken);

        var unitOptionsByIngredient = new Dictionary<int, IReadOnlyList<InventoryUnitOptionDTO>>();
        foreach (var ingredientId in rawItems
            .Where(x => x.Inventory.IngredientId.HasValue)
            .Select(x => x.Inventory.IngredientId!.Value)
            .Distinct())
        {
            if (_unitConversionService == null)
                continue;
            var options = await _unitConversionService.GetActiveUnitOptionsAsync(ingredientId, cancellationToken);
            if (!options.IsSuccess)
            {
                return ServiceResult<NegativeInventorySettingsDTO>.Failure(
                    options.Message,
                    errorCode: options.ErrorCode);
            }
            unitOptionsByIngredient[ingredientId] = options.Data;
        }

        var pendingApprovalCount = await _context.InventoryNegativeApprovals
            .AsNoTracking()
            .CountAsync(x => x.Status == InventoryNegativeApprovalStatuses.Requested, cancellationToken);

        var items = rawItems.Select(x =>
        {
            var effectiveLimit = x.Inventory.MaxNegativeQty ?? state.DefaultLimit;
            var active = x.StoreActive && x.ItemActive;
            var eligible = state.IsValid && state.Enabled && state.ApprovalRequired
                           && active && effectiveLimit > 0;
            var eligibilityText = !state.IsValid
                ? "Cấu hình lỗi"
                : !active
                    ? "Item/cửa hàng ngừng hoạt động"
                    : !state.Enabled
                        ? "Tính năng đang tắt"
                        : effectiveLimit <= 0
                            ? "Bị chặn"
                            : $"Có thể xin xuất âm tối đa {FormatQuantity(effectiveLimit)} {x.BaseUnitCode}";

            var unitOptions = x.Inventory.IngredientId.HasValue
                && unitOptionsByIngredient.TryGetValue(x.Inventory.IngredientId.Value, out var configuredOptions)
                    ? configuredOptions
                    : new List<InventoryUnitOptionDTO>
                    {
                        new()
                        {
                            UnitId = x.BaseUnitId,
                            UnitCode = x.BaseUnitCode,
                            UnitName = x.BaseUnitCode,
                            ConversionFactorToBase = 1m,
                            IsBaseUnit = true
                        }
                    };

            return new NegativeInventoryStoreItemDTO
            {
                StoreInventoryId = x.Inventory.StoreInventoryId,
                StoreId = x.Inventory.StoreId,
                StoreName = x.StoreName,
                ItemType = x.ItemType,
                ItemId = x.ItemId,
                ItemCode = x.ItemCode,
                ItemName = x.ItemName,
                BaseUnitId = x.BaseUnitId,
                BaseUnitCode = x.BaseUnitCode,
                DisplayUnitId = x.BaseUnitId,
                UnitOptions = unitOptions,
                StoreActive = x.StoreActive,
                ItemActive = x.ItemActive,
                AvailableQty = x.Inventory.AvailableQty,
                ReservedQty = x.Inventory.ReservedQty,
                MaxNegativeQty = x.Inventory.MaxNegativeQty,
                EffectiveMaxNegativeQty = effectiveLimit,
                LimitMode = x.Inventory.MaxNegativeQty switch
                {
                    null => NegativeInventoryLimitModes.Default,
                    0 => NegativeInventoryLimitModes.Blocked,
                    _ => NegativeInventoryLimitModes.Custom
                },
                CanRequestNegative = eligible,
                EligibilityText = eligibilityText,
                RowVersion = Convert.ToBase64String(x.Inventory.RowVersion ?? [])
            };
        }).ToList();

        return ServiceResult<NegativeInventorySettingsDTO>.Success(new NegativeInventorySettingsDTO
        {
            IsConfigurationValid = state.IsValid,
            ConfigurationError = state.Error,
            Enabled = state.Enabled,
            ApprovalRequired = state.ApprovalRequired,
            DefaultMaxNegativeQuantity = state.DefaultLimit,
            PolicyVersion = state.PolicyVersion,
            PendingApprovalCount = pendingApprovalCount,
            Items = items
        });
    }

    public async Task<ServiceResult<NegativeInventorySettingsUpdateResultDTO>> UpdateNegativeInventorySettingsAsync(
        UpdateNegativeInventorySettingsDTO request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageNegativeInventory(actor))
        {
            return ServiceResult<NegativeInventorySettingsUpdateResultDTO>.Failure(
                "Bạn không có quyền thay đổi cấu hình âm kho.",
                errorCode: "FORBIDDEN");
        }

        if (request == null)
        {
            return ValidationFailure("Dữ liệu cấu hình không hợp lệ.");
        }

        if (!IsSupportedQuantity(request.DefaultMaxNegativeQuantity))
        {
            return ValidationFailure("Hạn mức mặc định phải từ 0, tối đa 3 chữ số thập phân.");
        }

        if (request.Items.GroupBy(x => x.StoreInventoryId).Any(group => group.Count() > 1))
        {
            return ValidationFailure("Danh sách có StoreInventoryId bị trùng.");
        }

        var settings = await _context.SystemSettings
            .Where(x => NegativeSettingKeys.Contains(x.SettingKey))
            .ToListAsync(cancellationToken);
        var currentState = ParseNegativeSettings(settings);
        if (!currentState.IsValid)
        {
            return ServiceResult<NegativeInventorySettingsUpdateResultDTO>.Failure(
                currentState.Error ?? "Bộ setting âm kho không hợp lệ.",
                errorCode: "NEGATIVE_SETTING_INVALID");
        }

        var requestedIds = request.Items.Select(x => x.StoreInventoryId).Distinct().ToList();
        var inventories = await _context.StoreInventories
            .Include(x => x.Ingredient)
            .Include(x => x.PreparedItem)
            .Where(x => requestedIds.Contains(x.StoreInventoryId))
            .ToDictionaryAsync(x => x.StoreInventoryId, cancellationToken);
        if (inventories.Count != requestedIds.Count)
            return ValidationFailure("Có item tồn kho không còn tồn tại.");

        var normalizedUpdates = new List<(StoreInventory Inventory, decimal? NewLimit)>();
        foreach (var item in request.Items)
        {
            var inventory = inventories[item.StoreInventoryId];
            if (!HasSupportedPolicyIdentity(inventory))
                return ValidationFailure($"StoreInventoryId {item.StoreInventoryId} không có identity hỗ trợ xuất âm.");

            if (!TryResolveLimit(item, out var newLimit, out var error))
                return ValidationFailure($"StoreInventoryId {item.StoreInventoryId}: {error}");

            if (item.LimitMode?.Trim().ToUpperInvariant() == NegativeInventoryLimitModes.Custom)
            {
                if (inventory.IngredientId.HasValue)
                {
                    var selectedUnitId = item.DisplayUnitId > 0
                        ? item.DisplayUnitId
                        : inventory.Ingredient!.BaseUnitId;
                    if (_unitConversionService != null)
                    {
                        var converted = await _unitConversionService.ConvertAsync(
                            inventory.IngredientId.Value,
                            newLimit!.Value,
                            selectedUnitId,
                            inventory.Ingredient.BaseUnitId);
                        if (!converted.IsSuccess || !IsSupportedQuantity(converted.Data) || converted.Data <= 0m)
                            return ValidationFailure($"StoreInventoryId {item.StoreInventoryId}: {converted.Message}");
                        newLimit = converted.Data;
                    }
                    else if (selectedUnitId != inventory.Ingredient.BaseUnitId)
                    {
                        return ValidationFailure($"StoreInventoryId {item.StoreInventoryId}: không thể xác thực đơn vị đã chọn.");
                    }
                }
                else
                {
                    var baseUnitId = inventory.PreparedItem!.BaseUnitId;
                    if (item.DisplayUnitId > 0 && item.DisplayUnitId != baseUnitId)
                        return ValidationFailure($"StoreInventoryId {item.StoreInventoryId}: bán thành phẩm chỉ hỗ trợ đơn vị cơ sở.");
                }
            }

            if (inventory.MaxNegativeQty == newLimit)
                continue;

            if (!TryDecodeRowVersion(item.RowVersion, out var expectedRowVersion)
                || !inventory.RowVersion.SequenceEqual(expectedRowVersion))
            {
                return ServiceResult<NegativeInventorySettingsUpdateResultDTO>.Failure(
                    $"Dữ liệu StoreInventoryId {item.StoreInventoryId} đã thay đổi. Hãy tải lại trang.",
                    errorCode: "NEGATIVE_INVENTORY_SETTING_STALE");
            }

            normalizedUpdates.Add((inventory, newLimit));
        }

        var globalChanged = currentState.Enabled != request.Enabled
                            || currentState.DefaultLimit != request.DefaultMaxNegativeQuantity
                            || !currentState.ApprovalRequired;
        var changed = globalChanged || normalizedUpdates.Count > 0;
        if (!changed)
        {
            return ServiceResult<NegativeInventorySettingsUpdateResultDTO>.Success(
                new NegativeInventorySettingsUpdateResultDTO
                {
                    Changed = false,
                    PolicyVersion = currentState.PolicyVersion
                },
                "Cấu hình không thay đổi.");
        }

        var newPolicyVersion = CreatePolicyVersion();
        var oldGlobalData = new
        {
            currentState.Enabled,
            currentState.ApprovalRequired,
            DefaultMaxNegativeQuantity = currentState.DefaultLimit,
            currentState.PolicyVersion
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            FindSetting(settings, InventoryIssueSettingsProvider.EnabledKey).SettingValue =
                request.Enabled.ToString().ToLowerInvariant();
            FindSetting(settings, InventoryIssueSettingsProvider.ApprovalRequiredKey).SettingValue = "true";
            FindSetting(settings, InventoryIssueSettingsProvider.DefaultLimitKey).SettingValue =
                request.DefaultMaxNegativeQuantity.ToString(CultureInfo.InvariantCulture);
            FindSetting(settings, InventoryIssueSettingsProvider.PolicyVersionKey).SettingValue = newPolicyVersion;

            _context.AuditLogs.Add(new AuditLog
            {
                TableName = "SystemSettings",
                RecordId = FindSetting(settings, InventoryIssueSettingsProvider.EnabledKey).SettingId,
                Action = "UPDATE_NEGATIVE_POLICY",
                OldData = JsonSerializer.Serialize(oldGlobalData),
                NewData = JsonSerializer.Serialize(new
                {
                    request.Enabled,
                    ApprovalRequired = true,
                    request.DefaultMaxNegativeQuantity,
                    PolicyVersion = newPolicyVersion
                }),
                UserId = actor.StaffId,
                CreatedAt = DateTime.UtcNow
            });

            foreach (var (inventory, newLimit) in normalizedUpdates)
            {
                var oldLimit = inventory.MaxNegativeQty;
                inventory.MaxNegativeQty = newLimit;
                _context.AuditLogs.Add(new AuditLog
                {
                    TableName = "StoreInventories",
                    RecordId = inventory.StoreInventoryId,
                    Action = "UPDATE_MAX_NEGATIVE_QTY",
                    OldData = JsonSerializer.Serialize(new { MaxNegativeQty = oldLimit }),
                    NewData = JsonSerializer.Serialize(new { MaxNegativeQty = newLimit, PolicyVersion = newPolicyVersion }),
                    UserId = actor.StaffId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _cache.Remove(CacheKey);

            return ServiceResult<NegativeInventorySettingsUpdateResultDTO>.Success(
                new NegativeInventorySettingsUpdateResultDTO
                {
                    Changed = true,
                    PolicyVersion = newPolicyVersion
                },
                request.Enabled
                    ? "Đã bật và cập nhật cấu hình xuất âm có kiểm soát."
                    : "Đã tắt xuất âm thủ công. Kill switch có hiệu lực từ request kế tiếp.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<NegativeInventorySettingsUpdateResultDTO>.Failure(
                "Cấu hình vừa được người khác cập nhật. Hãy tải lại trang.",
                errorCode: "NEGATIVE_INVENTORY_SETTING_STALE");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<NegativeInventorySettingsUpdateResultDTO>.Failure(
                "Không thể lưu cấu hình âm kho.",
                errorCode: "NEGATIVE_INVENTORY_SETTING_UPDATE_FAILED");
        }
    }

    private static bool CanManageNegativeInventory(AdminActorContext actor) =>
        actor.StaffId > 0
        && actor.RoleNames.Any(role => role == RoleConstants.BusinessOwner || role == RoleConstants.SystemAdmin);

    private static bool HasSupportedPolicyIdentity(StoreInventory inventory) =>
        (inventory.IngredientId.HasValue && !inventory.PreparedItemId.HasValue)
        || (!inventory.IngredientId.HasValue && inventory.PreparedItemId.HasValue);

    private static bool TryResolveLimit(
        UpdateNegativeInventoryItemDTO item,
        out decimal? limit,
        out string error)
    {
        limit = null;
        error = string.Empty;
        var mode = item.LimitMode?.Trim().ToUpperInvariant();
        switch (mode)
        {
            case NegativeInventoryLimitModes.Blocked:
                limit = 0;
                return true;
            case NegativeInventoryLimitModes.Default:
                limit = null;
                return true;
            case NegativeInventoryLimitModes.Custom:
                if (!item.MaxNegativeQuantity.HasValue
                    || item.MaxNegativeQuantity.Value <= 0
                    || !IsSupportedQuantity(item.MaxNegativeQuantity.Value))
                {
                    error = "Hạn mức riêng phải lớn hơn 0 và có tối đa 3 chữ số thập phân.";
                    return false;
                }

                limit = item.MaxNegativeQuantity.Value;
                return true;
            default:
                error = "Chế độ hạn mức không hợp lệ.";
                return false;
        }
    }

    private static bool IsSupportedQuantity(decimal value) =>
        value >= 0 && value <= MaxSupportedQuantity && decimal.Round(value, 3) == value;

    private static string FormatQuantity(decimal value) =>
        value.ToString("#,##0.###", CultureInfo.CurrentCulture);

    private static bool TryDecodeRowVersion(string value, out byte[] rowVersion)
    {
        try
        {
            rowVersion = Convert.FromBase64String(value ?? string.Empty);
            return true;
        }
        catch (FormatException)
        {
            rowVersion = [];
            return false;
        }
    }

    private static string CreatePolicyVersion() =>
        $"manual-export-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";

    private static SystemSetting FindSetting(IEnumerable<SystemSetting> settings, string key) =>
        settings.Single(x => x.SettingKey == key);

    private static NegativeSettingsState ParseNegativeSettings(IReadOnlyCollection<SystemSetting> settings)
    {
        if (NegativeSettingKeys.Any(key => settings.Count(x => x.SettingKey == key) != 1))
        {
            return NegativeSettingsState.Invalid(
                "Bốn setting âm kho phải tồn tại đúng một bản ghi cho mỗi key. Liên hệ DBA để đối soát.");
        }

        var values = settings.ToDictionary(x => x.SettingKey, x => x.SettingValue);
        if (!bool.TryParse(values[InventoryIssueSettingsProvider.EnabledKey], out var enabled)
            || !bool.TryParse(values[InventoryIssueSettingsProvider.ApprovalRequiredKey], out var approvalRequired)
            || !decimal.TryParse(
                values[InventoryIssueSettingsProvider.DefaultLimitKey],
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var defaultLimit)
            || !IsSupportedQuantity(defaultLimit)
            || string.IsNullOrWhiteSpace(values[InventoryIssueSettingsProvider.PolicyVersionKey]))
        {
            return NegativeSettingsState.Invalid(
                "Giá trị setting âm kho không hợp lệ. Feature đang fail-closed.");
        }

        if (!approvalRequired)
        {
            return NegativeSettingsState.Invalid(
                "approval_required phải bằng true. Feature đang fail-closed.");
        }

        return new NegativeSettingsState(
            true,
            enabled,
            approvalRequired,
            defaultLimit,
            values[InventoryIssueSettingsProvider.PolicyVersionKey].Trim(),
            null);
    }

    private static ServiceResult<NegativeInventorySettingsUpdateResultDTO> ValidationFailure(string message) =>
        ServiceResult<NegativeInventorySettingsUpdateResultDTO>.Failure(
            message,
            errorCode: "NEGATIVE_INVENTORY_SETTING_VALIDATION");

    private sealed record NegativeSettingsState(
        bool IsValid,
        bool Enabled,
        bool ApprovalRequired,
        decimal DefaultLimit,
        string PolicyVersion,
        string? Error)
    {
        public static NegativeSettingsState Invalid(string error) =>
            new(false, false, true, 0, string.Empty, error);
    }
}
