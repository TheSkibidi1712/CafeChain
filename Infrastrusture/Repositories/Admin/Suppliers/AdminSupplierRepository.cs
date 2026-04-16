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

        // ===== GET ALL =====
        public async Task<List<Supplier>> GetAllAsync(string? search, bool? status)
        {
            var query = _context.Suppliers
                .Include(x => x.Phones)
                .Include(x => x.BankAccounts)
                .Include(x => x.Contacts)
                .AsQueryable();

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
        public async Task<Supplier?> GetByIdAsync(int id)
        {
            return await _context.Suppliers
                .Include(x => x.Phones)
                .Include(x => x.BankAccounts)
                .Include(x => x.Contacts)
                .FirstOrDefaultAsync(x => x.SupplierId == id);
        }

        // ===== CREATE =====
        public async Task CreateAsync(Supplier supplier)
        {
            await _context.Suppliers.AddAsync(supplier);
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

        // ===== BANK ACCOUNTS =====
        public async Task AddBankAccountAsync(SupplierBankAccount bankAccount)
        {
            await _context.SupplierBankAccounts.AddAsync(bankAccount);
        }

        public async Task<SupplierBankAccount?> GetBankAccountByIdAsync(int supplierBankAccountId)
        {
            return await _context.SupplierBankAccounts.FindAsync(supplierBankAccountId);
        }

        public Task DeleteBankAccountAsync(SupplierBankAccount bankAccount)
        {
            _context.SupplierBankAccounts.Remove(bankAccount);
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
