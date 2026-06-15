using CafeChain.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using CafeChain.Data;
using CafeChain.Models.Stores;

namespace CafeChain.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ConfigController(AppDbContext context) => _context = context;

        [HttpPost("UpdateCurrentStoreIP")]
        [Authorize] // Có thể thêm (Roles = "Store Manager, Super Admin")
        public async Task<IActionResult> UpdateCurrentStoreIP(int storeId)
        {
            string currentIp = HttpContext.GetClientIP();
            if (currentIp == "Unknown")
                return BadRequest(new { success = false, message = "Không xác định được IP thiết bị của bạn." });

            var existingIp = await _context.StoreIPs.FirstOrDefaultAsync(x => x.StoreId == storeId);
            if (existingIp != null)
            {
                existingIp.IPAddress = currentIp;
                existingIp.IsActive = true;
                _context.Update(existingIp);
            }
            else
            {
                _context.StoreIPs.Add(new StoreIP 
                { 
                    StoreId = storeId, 
                    IPAddress = currentIp, 
                    IsPublicNetwork = true, 
                    IsActive = true 
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, ip = currentIp, message = $"Lưu IP mạng WiFi ({currentIp}) cho cửa hàng thành công!" });
        }
    }
}
