using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CafeChain.Data;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Authorization;

namespace CafeChain.Areas.Admin.Controllers
{
    // Kế thừa AdminBaseController (Đã bật sắn RequireAdminPanelAccess)
    public class AdminStoreIPController : AdminBaseController
    {
        private readonly AppDbContext _context;

        public AdminStoreIPController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/AdminStoreIP
        public async Task<IActionResult> Index()
        {
            // Lấy danh sách IP kèm theo thông tin cửa hàng
            var storeIPs = await _context.StoreIPs
                .Include(s => s.Store)
                .OrderByDescending(s => s.Id)
                .ToListAsync();
            return View(storeIPs);
        }

        // GET: Admin/AdminStoreIP/Create
        public IActionResult Create()
        {
            ViewBag.Stores = _context.Stores.Where(s => s.Active).ToList();
            return View();
        }

        // POST: Admin/AdminStoreIP/Create
        [HttpPost]
        [ValidateAntiForgeryToken] // CHỐNG CSRF TRÊN FORM CREATE
        public async Task<IActionResult> Create(StoreIP storeIP)
        {
            ModelState.Remove("Store"); // Ngăn EF check Validate trường Navigation

            if (ModelState.IsValid)
            {
                _context.Add(storeIP);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Stores = _context.Stores.Where(s => s.Active).ToList();
            return View(storeIP);
        }

        // GET: Admin/AdminStoreIP/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var storeIP = await _context.StoreIPs.FindAsync(id);
            if (storeIP == null) return NotFound();

            ViewBag.Stores = _context.Stores.Where(s => s.Active).ToList();
            return View(storeIP);
        }

        // POST: Admin/AdminStoreIP/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken] // CHỐNG CSRF TRÊN FORM EDIT
        public async Task<IActionResult> Edit(int id, StoreIP storeIP)
        {
            if (id != storeIP.Id) return NotFound();
            
            ModelState.Remove("Store");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(storeIP);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StoreIPExists(storeIP.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Stores = _context.Stores.Where(s => s.Active).ToList();
            return View(storeIP);
        }

        // POST: Admin/AdminStoreIP/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken] // CHỐNG CSRF CHO XÓA
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var storeIP = await _context.StoreIPs.FindAsync(id);
            if (storeIP != null)
            {
                _context.StoreIPs.Remove(storeIP);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool StoreIPExists(int id)
        {
            return _context.StoreIPs.Any(e => e.Id == id);
        }
    }
}
