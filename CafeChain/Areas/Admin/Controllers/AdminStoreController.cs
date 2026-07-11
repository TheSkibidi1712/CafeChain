using CafeChain.Data;
using CafeChain.Models.Stores;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminStoreController : AdminBaseController
    {
        private readonly AppDbContext _context;

        public AdminStoreController(AppDbContext context)
        {
            _context = context;
        }

        // 1. INDEX: Đổ toàn bộ Store ra bảng
        public IActionResult Index()
        {
            var stores = _context.Stores
                .Include(s => s.Province)
                .Include(s => s.District)
                .Include(s => s.Ward)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
                
            return View(stores);
        }

        // 2. CREATE (GET): Đẩy tập dữ liệu Tỉnh/TP xuống View
        public IActionResult Create()
        {
            ViewBag.Provinces = _context.Provinces
                .Select(p => new SelectListItem { Value = p.ProvinceId.ToString(), Text = p.Name })
                .ToList();
                
            return View();
        }

        // 2. CREATE (POST): Validate và insert dữ liệu xuống DB
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Store store)
        {
            // Loại bỏ các trường tự động tạo/không cần thiết khỏi ModelState check
            ModelState.Remove("Province");
            ModelState.Remove("District");
            ModelState.Remove("Ward");
            ModelState.Remove("Staffs");
            ModelState.Remove("Orders");
            ModelState.Remove("InventoryWriterConfiguration");

            if (ModelState.IsValid)
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var now = DateTime.UtcNow;
                    store.CreatedAt = now;
                    store.InventoryWriterConfiguration = new StoreInventoryWriterConfiguration
                    {
                        WriterMode = InventoryWriterMode.LegacyRecipe,
                        HasEverActivatedPreparedItem = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _context.Stores.Add(store);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, "Không thể tạo cửa hàng cùng cấu hình writer kho.");
                }
            }
            
            // XỬ LÝ LỖI: Trả về View cùng bộ Dropdown data để người dùng không phải chọn lại
            ViewBag.Provinces = _context.Provinces
                .Select(p => new SelectListItem { 
                    Value = p.ProvinceId.ToString(), 
                    Text = p.Name, 
                    Selected = (p.ProvinceId == store.ProvinceId) 
                }).ToList();
                
            return View(store);
        }

        // 3. EDIT (GET)
        public IActionResult Edit(int id)
        {
            var store = _context.Stores.Find(id);
            if (store == null) return NotFound();

            ViewBag.Provinces = new SelectList(_context.Provinces, "ProvinceId", "Name", store.ProvinceId);
            
            if (store.ProvinceId.HasValue)
                ViewBag.Districts = new SelectList(_context.Districts.Where(d => d.ProvinceId == store.ProvinceId), "DistrictId", "Name", store.DistrictId);
            else
                ViewBag.Districts = new SelectList(Enumerable.Empty<SelectListItem>());

            if (store.DistrictId.HasValue)
                ViewBag.Wards = new SelectList(_context.Wards.Where(w => w.DistrictId == store.DistrictId), "WardId", "Name", store.WardId);
            else
                ViewBag.Wards = new SelectList(Enumerable.Empty<SelectListItem>());
                
            return View(store);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Store store)
        {
            if (id != store.StoreId) return NotFound();

            ModelState.Remove("Province");
            ModelState.Remove("District");
            ModelState.Remove("Ward");
            ModelState.Remove("Staffs");
            ModelState.Remove("Orders");

            if (ModelState.IsValid)
            {
                _context.Stores.Update(store);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            
            // Nếu lỗi, nạp lại toàn bộ SelectLists để Dropdown 3 cấp phục hồi
            ViewBag.Provinces = new SelectList(_context.Provinces, "ProvinceId", "Name", store.ProvinceId);
            if (store.ProvinceId.HasValue)
                ViewBag.Districts = new SelectList(_context.Districts.Where(d => d.ProvinceId == store.ProvinceId), "DistrictId", "Name", store.DistrictId);
            else
                ViewBag.Districts = new SelectList(Enumerable.Empty<SelectListItem>());

            if (store.DistrictId.HasValue)
                ViewBag.Wards = new SelectList(_context.Wards.Where(w => w.DistrictId == store.DistrictId), "WardId", "Name", store.WardId);
            else
                ViewBag.Wards = new SelectList(Enumerable.Empty<SelectListItem>());
                
            return View(store);
        }

        // 4. TOGGLE STATUS (SOFT DELETE)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleStatus(int id)
        {
            var store = _context.Stores.Find(id);
            if (store == null) return NotFound();

            store.Active = !store.Active; // Đảo trạng thái Active
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }
    }
}

