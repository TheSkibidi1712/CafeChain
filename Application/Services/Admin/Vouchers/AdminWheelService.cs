using CafeChain.Data;
using CafeChain.Models.Vouchers;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Vouchers
{
    public interface IAdminWheelService
    {
        Task<List<WheelConfig>> GetAllConfigsAsync();
        Task<WheelConfig?> GetActiveConfigAsync();
        Task<WheelConfig?> GetConfigByIdAsync(int id);
        Task<bool> CreateConfigAsync(WheelConfig config);
        Task<bool> UpdateConfigAsync(WheelConfig config);
        Task<bool> ToggleStatusAsync(int id);
        Task<bool> SavePrizesAsync(int configId, List<WheelPrize> prizes);
        Task<List<Voucher>> GetAvailableVouchersAsync();
    }

    public class AdminWheelService : IAdminWheelService
    {
        private readonly AppDbContext _context;

        public AdminWheelService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<WheelConfig>> GetAllConfigsAsync()
        {
            return await _context.WheelConfigs
                .Include(w => w.Prizes)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();
        }

        public async Task<WheelConfig?> GetActiveConfigAsync()
        {
            return await _context.WheelConfigs
                .Include(w => w.Prizes)
                .ThenInclude(p => p.Voucher)
                .FirstOrDefaultAsync(w => w.Active);
        }

        public async Task<WheelConfig?> GetConfigByIdAsync(int id)
        {
            return await _context.WheelConfigs
                .Include(w => w.Prizes)
                .ThenInclude(p => p.Voucher)
                .FirstOrDefaultAsync(w => w.WheelConfigId == id);
        }

        public async Task<bool> CreateConfigAsync(WheelConfig config)
        {
            // Nếu config mới là Active, tắt các config khác
            if (config.Active)
            {
                await DeactivateAllConfigs();
            }

            _context.WheelConfigs.Add(config);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateConfigAsync(WheelConfig config)
        {
            var existing = await _context.WheelConfigs.FindAsync(config.WheelConfigId);
            if (existing == null) return false;

            existing.Name = config.Name;
            existing.SpinCost = config.SpinCost;
            existing.SlotCount = config.SlotCount;
            existing.Active = config.Active;

            if (existing.Active)
            {
                await DeactivateAllConfigs(existing.WheelConfigId);
            }

            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> ToggleStatusAsync(int id)
        {
            var config = await _context.WheelConfigs.FindAsync(id);
            if (config == null) return false;

            config.Active = !config.Active;
            if (config.Active)
            {
                await DeactivateAllConfigs(id);
            }

            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> SavePrizesAsync(int configId, List<WheelPrize> prizes)
        {
            var existingPrizes = await _context.WheelPrizes
                .Where(p => p.WheelConfigId == configId)
                .ToListAsync();

            _context.WheelPrizes.RemoveRange(existingPrizes);
            
            foreach (var p in prizes)
            {
                p.WheelConfigId = configId;
            }

            _context.WheelPrizes.AddRange(prizes);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<List<Voucher>> GetAvailableVouchersAsync()
        {
            return await _context.Vouchers
                .Where(v => v.Active && v.EndDate >= DateTime.Now)
                .ToListAsync();
        }

        private async Task DeactivateAllConfigs(int? exceptId = null)
        {
            var configs = await _context.WheelConfigs
                .Where(w => w.Active && w.WheelConfigId != exceptId)
                .ToListAsync();
            
            foreach (var c in configs)
            {
                c.Active = false;
            }
        }
    }
}
