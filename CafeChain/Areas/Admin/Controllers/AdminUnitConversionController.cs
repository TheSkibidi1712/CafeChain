using CafeChain.Data;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.ViewModels.Admin.UnitConversions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminUnitConversionController : AdminBaseController
    {
        private readonly AppDbContext _context;

        public AdminUnitConversionController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // INDEX: Danh sách tất cả quy đổi
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var conversions = await _context.UnitConversions
                .Include(uc => uc.Ingredient)
                .Include(uc => uc.FromUnit)
                .Include(uc => uc.ToUnit)
                .OrderBy(uc => uc.Ingredient.Name)
                .ThenBy(uc => uc.FromUnit.Name)
                .Select(uc => new UnitConversionVM
                {
                    UnitConversionId = uc.UnitConversionId,
                    IngredientId = uc.IngredientId,
                    IngredientName = uc.Ingredient.Name,
                    FromUnitId = uc.FromUnitId,
                    FromUnitName = uc.FromUnit.Name,
                    FromQuantity = uc.FromQuantity,
                    ToUnitId = uc.ToUnitId,
                    ToUnitName = uc.ToUnit.Name,
                    ToQuantity = uc.ToQuantity
                })
                .ToListAsync();

            return View(conversions);
        }

        // ============================================================
        // CREATE (GET): Form tạo mới
        // ============================================================
        [HttpGet]
        public IActionResult Create()
        {
            PopulateDropdowns();
            return View(new UnitConversionVM());
        }

        // ============================================================
        // CREATE (POST): Lưu quy đổi mới
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UnitConversionVM model)
        {
            // Validate: Đơn vị nguồn và đích không được trùng nhau
            if (model.FromUnitId == model.ToUnitId)
            {
                ModelState.AddModelError("ToUnitId", "Đơn vị nguồn và đơn vị đích không được giống nhau.");
            }

            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                return View(model);
            }

            // Kiểm tra trùng lặp (cùng Ingredient + cùng cặp đơn vị)
            var exists = await _context.UnitConversions.AnyAsync(uc =>
                uc.IngredientId == model.IngredientId &&
                uc.FromUnitId == model.FromUnitId &&
                uc.ToUnitId == model.ToUnitId);

            if (exists)
            {
                ModelState.AddModelError("", "Quy đổi cho cặp đơn vị này của nguyên liệu đã tồn tại.");
                PopulateDropdowns();
                return View(model);
            }

            var entity = new UnitConversion
            {
                IngredientId = model.IngredientId,
                FromUnitId = model.FromUnitId,
                FromQuantity = model.FromQuantity,
                ToUnitId = model.ToUnitId,
                ToQuantity = model.ToQuantity
            };

            _context.UnitConversions.Add(entity);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = $"Tạo quy đổi thành công: {model.FromQuantity} → {model.ToQuantity}";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // EDIT (GET)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var entity = await _context.UnitConversions
                .Include(uc => uc.Ingredient)
                .Include(uc => uc.FromUnit)
                .Include(uc => uc.ToUnit)
                .FirstOrDefaultAsync(uc => uc.UnitConversionId == id);

            if (entity == null) return NotFound();

            var vm = new UnitConversionVM
            {
                UnitConversionId = entity.UnitConversionId,
                IngredientId = entity.IngredientId,
                IngredientName = entity.Ingredient.Name,
                FromUnitId = entity.FromUnitId,
                FromUnitName = entity.FromUnit.Name,
                FromQuantity = entity.FromQuantity,
                ToUnitId = entity.ToUnitId,
                ToUnitName = entity.ToUnit.Name,
                ToQuantity = entity.ToQuantity
            };

            PopulateDropdowns();
            return View(vm);
        }

        // ============================================================
        // EDIT (POST)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UnitConversionVM model)
        {
            if (model.FromUnitId == model.ToUnitId)
            {
                ModelState.AddModelError("ToUnitId", "Đơn vị nguồn và đơn vị đích không được giống nhau.");
            }

            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                return View(model);
            }

            var entity = await _context.UnitConversions.FindAsync(model.UnitConversionId);
            if (entity == null) return NotFound();

            entity.IngredientId = model.IngredientId;
            entity.FromUnitId = model.FromUnitId;
            entity.FromQuantity = model.FromQuantity;
            entity.ToUnitId = model.ToUnitId;
            entity.ToQuantity = model.ToQuantity;

            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = "Cập nhật quy đổi thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // DELETE (POST)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.UnitConversions.FindAsync(id);
            if (entity == null) return NotFound();

            _context.UnitConversions.Remove(entity);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = "Đã xóa quy đổi đơn vị.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // HELPER: Nạp dữ liệu Dropdown
        // ============================================================
        private void PopulateDropdowns()
        {
            ViewBag.Ingredients = _context.Ingredients
                .Where(i => i.Active)
                .OrderBy(i => i.Name)
                .Select(i => new { i.IngredientId, i.Name })
                .ToList<object>();

            ViewBag.Units = _context.Units
                .Where(u => u.Active)
                .OrderBy(u => u.Name)
                .Select(u => new { u.UnitId, u.Name, u.UnitCode })
                .ToList<object>();
        }
    }
}
