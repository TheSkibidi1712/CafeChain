using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CafeChain.Data;
using CafeChain.Models.Customers;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

namespace CafeChain.Controllers
{
    [Authorize]
    [Route("[controller]/[action]")]
    public class CustomerAddressController : Controller
    {
        private readonly AppDbContext _context;

        public CustomerAddressController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> AddAddress([FromForm] int provinceId, [FromForm] int districtId, [FromForm] int wardId, [FromForm] string address, [FromForm] bool isDefault)
        {
            var customerIdStr = User.FindFirstValue("CustomerId");
            if (!int.TryParse(customerIdStr, out int customerId)) return Unauthorized();

            if (isDefault)
            {
                var existingAddresses = await _context.CustomersAddresses.Where(a => a.CustomerId == customerId).ToListAsync();
                foreach (var a in existingAddresses) a.IsDefault = false;
            }
            
            // Nếu là địa chỉ đầu tiên, luôn set làm mặc định
            var isFirst = !await _context.CustomersAddresses.AnyAsync(a => a.CustomerId == customerId && !a.IsDeleted);
            if(isFirst) isDefault = true;

            var newAddress = new CustomerAddress
            {
                CustomerId = customerId,
                ProvinceId = provinceId,
                DistrictId = districtId,
                WardId = wardId,
                Address = address,
                IsDefault = isDefault,
                IsDeleted = false
            };

            _context.CustomersAddresses.Add(newAddress);
            await _context.SaveChangesAsync();

            return Json(new { success = true, addressId = newAddress.CustomerAddressId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAddress([FromForm] int id)
        {
            var customerIdStr = User.FindFirstValue("CustomerId");
            if (!int.TryParse(customerIdStr, out int customerId)) return Unauthorized();

            var addr = await _context.CustomersAddresses.FirstOrDefaultAsync(a => a.CustomerAddressId == id && a.CustomerId == customerId);
            if (addr == null) return Json(new { success = false, message = "Địa chỉ không tồn tại hoặc không thuộc quyền sở hữu" });

            addr.IsDeleted = true;
            addr.IsDefault = false;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> SetDefaultAddress([FromForm] int id)
        {
            var customerIdStr = User.FindFirstValue("CustomerId");
            if (!int.TryParse(customerIdStr, out int customerId)) return Unauthorized();

            var addrs = await _context.CustomersAddresses.Where(a => a.CustomerId == customerId && !a.IsDeleted).ToListAsync();
            var target = addrs.FirstOrDefault(a => a.CustomerAddressId == id);
            
            if (target == null) return Json(new { success = false, message = "Địa chỉ không tồn tại hoặc không thuộc quyền sở hữu" });

            foreach(var a in addrs) a.IsDefault = false;
            target.IsDefault = true;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
