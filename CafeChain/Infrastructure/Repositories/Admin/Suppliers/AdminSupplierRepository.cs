using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Suppliers;
using CafeChain.Models.Inventories.Suppliers;
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

        // ===== GET ALL =====
        public async Task<List<Supplier>> GetAllAsync(
            string? search,
            bool? status,
            IReadOnlyCollection<int>? storeScope = null)
        {
            var query = _context.Suppliers
                .Include(x => x.Phones)
                .Include(x => x.Contacts)
                .Include(x => x.IngredientSuppliers)
                .Include(x => x.SupplierStores)
                .AsQueryable();

            if (storeScope != null)
            {
                if (storeScope.Count == 0)
                    return new List<Supplier>();

                query = query.Where(x => x.SupplierStores.Any(ss =>
                    ss.Active && storeScope.Contains(ss.StoreId)));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(x =>
                    x.Code.ToLower().Contains(search) ||
                    x.Name.ToLower().Contains(search));
            }

            if (status.HasValue)
                query = query.Where(x => x.Active == status.Value);

            return await query.OrderBy(x => x.SupplierId).ToListAsync();
        }

        // ===== GET BY ID (full detail) =====
        public async Task<Supplier?> GetByIdAsync(
            int id,
            IReadOnlyCollection<int>? storeScope = null)
        {
            var query = _context.Suppliers
                .Include(x => x.Phones)
                .Include(x => x.Contacts)
                .Include(x => x.SupplierStores).ThenInclude(x => x.Store)
                .AsQueryable();

            if (storeScope != null)
            {
                if (storeScope.Count == 0)
                    return null;
                query = query.Where(x => x.SupplierStores.Any(ss =>
                    ss.Active && storeScope.Contains(ss.StoreId)));
            }

            return await query.FirstOrDefaultAsync(x => x.SupplierId == id);
        }

        // ===== CREATE =====
        public async Task CreateAsync(Supplier supplier)
        {
            await _context.Suppliers.AddAsync(supplier);
        }

        // ===== GENERATE NEXT CODE =====
        // Format: NCC00001, NCC00002, ... (5 chữ số, có thể mở rộng nếu vượt 99999)
        public async Task<string> GenerateNextCodeAsync()
        {
            const string PREFIX = "NCC";
            // Lấy tất cả mã bắt đầu bằng PREFIX
            var existingCodes = await _context.Suppliers
                .Where(s => s.Code != null && s.Code.StartsWith(PREFIX))
                .Select(s => s.Code!)
                .ToListAsync();

            int maxNum = 0;
            foreach (var code in existingCodes)
            {
                // Cắt bỏ PREFIX và parse phần số
                var numPart = code.Substring(PREFIX.Length);
                if (int.TryParse(numPart, out int num) && num > maxNum)
                    maxNum = num;
            }

            int nextNum = maxNum + 1;
            // Format 5 chữ số, tự động mở rộng nếu vượt 99999
            string paddedNum = nextNum <= 99999
                ? nextNum.ToString("D5")
                : nextNum.ToString();

            return PREFIX + paddedNum;
        }

        // ===== CHECK CODE =====
        public async Task<bool> IsCodeExists(string code, int? excludeId = null)
        {
            var query = _context.Suppliers
                .Where(x => x.Code.ToLower() == code.ToLower());

            if (excludeId.HasValue)
                query = query.Where(x => x.SupplierId != excludeId.Value);

            return await query.AnyAsync();
        }

        // ===== TOGGLE STATUS =====
        public async Task ToggleStatus(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) throw new Exception("Không tìm thấy nhà cung cấp");
            supplier.Active = !supplier.Active;
            supplier.UpdatedAt = DateTime.UtcNow;
        }

        // ===== PHONES =====
        public async Task AddPhoneAsync(SupplierPhone phone)
        {
            await _context.SupplierPhones.AddAsync(phone);
        }

        public async Task<SupplierPhone?> GetPhoneByIdAsync(int supplierPhoneId)
        {
            return await _context.SupplierPhones.FindAsync(supplierPhoneId);
        }

        public Task DeletePhoneAsync(SupplierPhone phone)
        {
            _context.SupplierPhones.Remove(phone);
            return Task.CompletedTask;
        }

        // ===== CONTACTS =====
        public async Task AddContactAsync(SupplierContact contact)
        {
            await _context.SupplierContacts.AddAsync(contact);
        }

        public async Task<SupplierContact?> GetContactByIdAsync(int supplierContactId)
        {
            return await _context.SupplierContacts.FindAsync(supplierContactId);
        }

        public async Task<List<SupplierContact>> GetContactsBySupplierIdAsync(int supplierId)
        {
            return await _context.SupplierContacts
                .Where(c => c.SupplierId == supplierId)
                .ToListAsync();
        }

        public Task DeleteContactAsync(SupplierContact contact)
        {
            _context.SupplierContacts.Remove(contact);
            return Task.CompletedTask;
        }

        // ===== SAVE =====
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
