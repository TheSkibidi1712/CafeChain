using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces.Admin.Settings;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CafeChain.Application.Services.Admin.Settings
{
    public class AdminSettingService : IAdminSettingService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private const string CACHE_KEY = "App_Settings_Dict";

        public AdminSettingService(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<Dictionary<string, string>> GetSettingsDictionaryAsync()
        {
            if (_cache.TryGetValue(CACHE_KEY, out Dictionary<string, string> cachedSettings))
            {
                return cachedSettings;
            }

            var settingsArray = await _context.SystemSettings.ToListAsync();
            var dict = settingsArray.GroupBy(s => s.SettingKey).ToDictionary(g => g.Key, g => g.First().SettingValue ?? "");

            // Auto-Seeding required UI keys if missing
            var requiredKeys = new[] { 
                "company_brand_name", "company_tax_code", "company_address", "company_hotline", "receipt_footer",
                "pos_vat_rate", "pos_auto_lock_mins", "pos_grace_period_days",
                "timekeep_late_buffer", "timekeep_face_auth", "timekeep_ip_bypass",
                "api_momo", "api_vnpay"
            };

            bool changed = false;
            foreach (var key in requiredKeys)
            {
                if (!dict.ContainsKey(key))
                {
                    var newSetting = new SystemSetting { SettingKey = key, SettingValue = "" };
                    _context.SystemSettings.Add(newSetting);
                    dict[key] = "";
                    changed = true;
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }

            _cache.Set(CACHE_KEY, dict, System.TimeSpan.FromHours(24));
            return dict;
        }

        public async Task<ServiceResult> SaveSettingsAsync(Dictionary<string, string> settings)
        {
            if (settings == null || !settings.Any())
                return ServiceResult.Success("Không có cấu hình nào cần lưu.");

            var existingSettings = await _context.SystemSettings.ToListAsync();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var kvp in settings)
                {
                    var setting = existingSettings.FirstOrDefault(s => s.SettingKey == kvp.Key);
                    if (setting != null)
                    {
                        setting.SettingValue = kvp.Value;
                        _context.SystemSettings.Update(setting);
                    }
                    else
                    {
                        _context.SystemSettings.Add(new SystemSetting { SettingKey = kvp.Key, SettingValue = kvp.Value });
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache so it rebuilds on next request
                _cache.Remove(CACHE_KEY);

                return ServiceResult.Success("Đã cập nhật hệ thống thành công.");
            }
            catch (System.Exception ex)
            {
                await transaction.RollbackAsync();
                return ServiceResult.Failure("Lưu thất bại: " + ex.Message);
            }
        }
    }
}
