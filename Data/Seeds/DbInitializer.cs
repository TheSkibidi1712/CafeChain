using CafeChain.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace CafeChain.Data.Seeds
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(AppDbContext context, IWebHostEnvironment env)
        {
            // Kiểm tra bảng Provinces xem đã có data chưa
            if (!context.Provinces.Any())
            {
                var sqlFilePath = Path.Combine(env.ContentRootPath, "Data", "Seeds", "vietnam_locations.sql");
                if (File.Exists(sqlFilePath))
                {
                    var sqlCode = await File.ReadAllTextAsync(sqlFilePath);
                    await context.Database.ExecuteSqlRawAsync(sqlCode);
                }
            }
        }
    }
}
