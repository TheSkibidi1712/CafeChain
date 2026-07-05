using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Infrastrusture.Interfaces.Systems;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace CafeChain.Application.Services.Inventories
{
    public class NegativeInventoryService : INegativeInventoryService
    {
        private const string AllowNegativeStockKey = "inventory_allow_negative_stock";
        private const string RequireApprovalKey = "inventory_require_manager_approval_for_negative_stock";
        private const string DefaultThresholdKey = "inventory_default_max_negative_quantity";
        private const string CacheKey = "Inventory_NegativeStock_Settings";

        private readonly ISystemSettingRepository _settingRepository;
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public NegativeInventoryService(
            ISystemSettingRepository settingRepository,
            IMemoryCache cache,
            IHttpContextAccessor httpContextAccessor)
        {
            _settingRepository = settingRepository;
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<NegativeStockValidationResult> ValidateIssueAsync(
            StoreInventory inventory,
            decimal issueQuantity,
            string ingredientName)
        {
            if (issueQuantity <= 0)
            {
                return new NegativeStockValidationResult
                {
                    IsAllowed = false,
                    Message = "Số lượng xuất/chuyển phải lớn hơn 0."
                };
            }

            var settings = await GetSettingsAsync();
            var beforeQty = inventory.AvailableQty;
            var afterQty = beforeQty - issueQuantity;

            if (afterQty >= 0)
            {
                return new NegativeStockValidationResult
                {
                    IsAllowed = true,
                    BeforeQty = beforeQty,
                    IssueQuantity = issueQuantity,
                    AfterQty = afterQty,
                    StockStatus = InventoryStockStatus.NORMAL
                };
            }

            if (!settings.AllowNegativeStock)
            {
                return new NegativeStockValidationResult
                {
                    IsAllowed = false,
                    IsNegative = true,
                    BeforeQty = beforeQty,
                    IssueQuantity = issueQuantity,
                    AfterQty = afterQty,
                    StockStatus = InventoryStockStatus.NEGATIVE_PENDING,
                    Message = $"Không đủ tồn kho cho {ingredientName}. Tồn sau giao dịch sẽ âm {FormatQuantity(afterQty)}."
                };
            }

            var threshold = inventory.MaxNegativeQty ?? settings.DefaultMaxNegativeQuantity ?? 0;

            if (afterQty < -threshold)
            {
                return new NegativeStockValidationResult
                {
                    IsAllowed = false,
                    IsNegative = true,
                    BeforeQty = beforeQty,
                    IssueQuantity = issueQuantity,
                    AfterQty = afterQty,
                    ThresholdQuantity = threshold,
                    StockStatus = InventoryStockStatus.NEGATIVE_PENDING,
                    Message = $"Tồn âm của {ingredientName} vượt ngưỡng cho phép {FormatQuantity(threshold)}."
                };
            }

            var requiresApproval = settings.RequireManagerApprovalForNegativeStock;

            if (requiresApproval && !CurrentUserCanApproveNegativeStock())
            {
                return new NegativeStockValidationResult
                {
                    IsAllowed = false,
                    IsNegative = true,
                    RequiresApproval = true,
                    BeforeQty = beforeQty,
                    IssueQuantity = issueQuantity,
                    AfterQty = afterQty,
                    ThresholdQuantity = threshold,
                    StockStatus = InventoryStockStatus.NEGATIVE_PENDING,
                    Message = $"Giao dịch làm âm kho {ingredientName} cần quản lý hoặc admin xác nhận."
                };
            }

            return new NegativeStockValidationResult
            {
                IsAllowed = true,
                IsNegative = true,
                RequiresApproval = requiresApproval,
                BeforeQty = beforeQty,
                IssueQuantity = issueQuantity,
                AfterQty = afterQty,
                ThresholdQuantity = threshold,
                StockStatus = InventoryStockStatus.NEGATIVE_CONFIRMED,
                Message = $"Cho phép âm kho {ingredientName}: tồn sau giao dịch {FormatQuantity(afterQty)}."
            };
        }

        private async Task<NegativeInventorySettings> GetSettingsAsync()
        {
            if (_cache.TryGetValue(CacheKey, out NegativeInventorySettings? cached) && cached != null)
            {
                return cached;
            }

            var values = await _settingRepository.GetValuesAsync(
                new[]
                {
                    AllowNegativeStockKey,
                    RequireApprovalKey,
                    DefaultThresholdKey
                });

            var settings = new NegativeInventorySettings
            {
                AllowNegativeStock = ReadBool(values, AllowNegativeStockKey, false),
                RequireManagerApprovalForNegativeStock = ReadBool(values, RequireApprovalKey, false),
                DefaultMaxNegativeQuantity = ReadDecimal(values, DefaultThresholdKey)
            };

            _cache.Set(CacheKey, settings, TimeSpan.FromMinutes(10));

            return settings;
        }

        private bool CurrentUserCanApproveNegativeStock()
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user == null)
            {
                return false;
            }

            return user.IsInRole(RoleConstants.SuperAdmin)
                || user.IsInRole(RoleConstants.CEO)
                || user.IsInRole(RoleConstants.OperationsManager)
                || user.IsInRole(RoleConstants.AreaManager)
                || user.IsInRole(RoleConstants.StoreManager)
                || user.IsInRole(RoleConstants.WarehouseKeeper);
        }

        private static bool ReadBool(
            IReadOnlyDictionary<string, string> values,
            string key,
            bool defaultValue)
        {
            if (!values.TryGetValue(key, out var value))
            {
                return defaultValue;
            }

            return bool.TryParse(value, out var parsed)
                ? parsed
                : value == "1";
        }

        private static decimal? ReadDecimal(
            IReadOnlyDictionary<string, string> values,
            string key)
        {
            if (!values.TryGetValue(key, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : null;
        }

        private static string FormatQuantity(decimal quantity)
        {
            return quantity.ToString("#,0.###", CultureInfo.GetCultureInfo("vi-VN"));
        }

        private sealed class NegativeInventorySettings
        {
            public bool AllowNegativeStock { get; set; }
            public bool RequireManagerApprovalForNegativeStock { get; set; }
            public decimal? DefaultMaxNegativeQuantity { get; set; }
        }
    }
}
