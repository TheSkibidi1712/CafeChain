using Microsoft.EntityFrameworkCore;
namespace CafeChain.Data
{
    public class AppDbContext : DbContext
    {   
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
    }
}
