using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Suppliers;
using CafeChain.Models.Inventories;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastrusture.Repositories.Admin.Suppliers
{
    public class AdminSupplierRepository : IAdminSupplierRepository
    {
        private readonly AppDbContext _context;

        public AdminSupplierRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Supplier>> GetAllSuppliersAsync()
        {
            return await _context.Suppliers
                .OrderByDescending(s => s.SupplierId)
                .ToListAsync();
        }

        public async Task<Supplier> GetSupplierByIdAsync(int id)
        {
            return await _context.Suppliers.FindAsync(id);
        }

        public async Task CreateSupplierAsync(Supplier supplier)
        {
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateSupplierAsync(Supplier supplier)
        {
            // Entity đã được track bởi DbContext qua FindAsync, chỉ cần SaveChanges.
            await _context.SaveChangesAsync();
        }

        public async Task ToggleSupplierStatusAsync(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                supplier.Active = !supplier.Active;
                await _context.SaveChangesAsync();
            }
        }

        public async Task AdjustDebtAsync(int id, decimal amount)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                supplier.DebtAmount += amount;
                if (supplier.DebtAmount < 0) supplier.DebtAmount = 0; // Không cho âm
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsSupplierCodeExistsAsync(string code, int? excludeId = null)
        {
            var query = _context.Suppliers.Where(s => s.Code.ToLower() == code.ToLower());
            if (excludeId.HasValue)
                query = query.Where(s => s.SupplierId != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task<bool> IsSupplierNameExistsAsync(string name, int? excludeId = null)
        {
            var query = _context.Suppliers.Where(s => s.Name.ToLower() == name.ToLower());
            if (excludeId.HasValue)
                query = query.Where(s => s.SupplierId != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task<bool> IsSupplierPhoneExistsAsync(string phone, int? excludeId = null)
        {
            var query = _context.Suppliers.Where(s => !string.IsNullOrEmpty(s.Phone) && s.Phone == phone);
            if (excludeId.HasValue)
                query = query.Where(s => s.SupplierId != excludeId.Value);
            return await query.AnyAsync();
        }
    }
}
