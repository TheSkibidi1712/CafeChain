using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Systems;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastrusture.Repositories.Systems
{
    public class SystemSettingRepository : ISystemSettingRepository
    {
        private readonly AppDbContext _context;

        public SystemSettingRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<string, string>> GetValuesAsync(IEnumerable<string> keys)
        {
            var normalizedKeys = keys
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (normalizedKeys.Count == 0)
            {
                return [];
            }

            return await _context.SystemSettings
                .AsNoTracking()
                .Where(x => normalizedKeys.Contains(x.SettingKey))
                .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);
        }
    }
}
