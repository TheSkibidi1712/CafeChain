using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Suppliers;
using CafeChain.Models.Inventories;
using CafeChain.Models.Locations;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Suppliers
{
    public class AdminSupplierService : IAdminSupplierService
    {
        private readonly IAdminSupplierRepository _repo;
        private readonly AppDbContext _context;

        public AdminSupplierService(IAdminSupplierRepository repo, AppDbContext context)
        {
            _repo    = repo;
            _context = context;
        }

        // ===== GET ALL =====
        public async Task<List<AdminSupplierDTO>> GetAllAsync(string? search, bool? status)
        {
            var data = await _repo.GetAllAsync(search, status);
            return data.Select(MapToListDTO).ToList();
        }

        // ===== GET BY ID =====
        public async Task<AdminSupplierDetailDTO?> GetByIdAsync(int id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return null;
            return await MapToDetailDTOAsync(entity);
        }

        // ===== GENERATE NEXT CODE =====
        public async Task<string> GenerateNextCodeAsync()
        {
            return await _repo.GenerateNextCodeAsync();
        }

        // ===== CREATE =====
        public async Task<int> CreateAsync(AdminSupplierCreateDTO dto)
        {
            dto.Name = Normalize(dto.Name);

            // Sinh mã tự động
            var code = await _repo.GenerateNextCodeAsync();

            // Ghép địa chỉ đầy đủ từ 3 cấp
            string? fullAddress = await BuildFullAddressAsync(
                dto.ProvinceId, dto.DistrictId, dto.WardId, dto.StreetAddress);

            var supplier = new Supplier
            {
                Code       = code,
                Name       = dto.Name,
                TaxCode    = dto.TaxCode?.Trim(),
                Website    = dto.Website?.Trim(),
                Address    = fullAddress,
                DebtAmount = 0,
                Active     = true,

                // Số điện thoại chính
                Phones = new List<SupplierPhone>
                {
                    new SupplierPhone
                    {
                        PhoneNumber = dto.PrimaryPhone.Trim(),
                        IsPrimary   = true
                    }
                },

                // Tài khoản ngân hàng chính
                BankAccounts = new List<SupplierBankAccount>
                {
                    new SupplierBankAccount
                    {
                        BankName      = dto.PrimaryBankName.Trim(),
                        AccountNumber = dto.PrimaryAccountNumber.Trim(),
                        AccountHolder = dto.PrimaryAccountHolder.Trim(),
                        IsPrimary     = true
                    }
                },

                // Thông tin liên hệ chính
                Contacts = new List<SupplierContact>
                {
                    new SupplierContact
                    {
                        Name      = dto.PrimaryContactName.Trim(),
                        Phone     = dto.PrimaryContactPhone?.Trim(),
                        Email     = dto.PrimaryContactEmail?.Trim(),
                        Position  = dto.PrimaryContactPosition?.Trim(),
                        IsPrimary = true
                    }
                }
            };

            await _repo.CreateAsync(supplier);
            await _repo.SaveChangesAsync();
            return supplier.SupplierId;
        }

        // ===== UPDATE =====
        public async Task UpdateAsync(AdminSupplierUpdateDTO dto)
        {
            var entity = await _repo.GetByIdAsync(dto.SupplierId);
            if (entity == null)
                throw new Exception("Không tìm thấy nhà cung cấp");

            dto.Name = Normalize(dto.Name);

            // Ghép địa chỉ đầy đủ từ 3 cấp
            string? fullAddress = await BuildFullAddressAsync(
                dto.ProvinceId, dto.DistrictId, dto.WardId, dto.StreetAddress);

            entity.Name    = dto.Name;
            entity.TaxCode = dto.TaxCode?.Trim();
            entity.Website = dto.Website?.Trim();
            entity.Address = fullAddress;
            entity.Active  = dto.Active;

            await _repo.SaveChangesAsync();
        }

        // ===== TOGGLE STATUS =====
        public async Task ToggleStatusAsync(int id)
        {
            await _repo.ToggleStatus(id);
            await _repo.SaveChangesAsync();
        }

        // ===== PHONES =====
        public async Task AddPhoneAsync(AdminSupplierPhoneCreateDTO dto)
        {
            var phone = new SupplierPhone
            {
                SupplierId  = dto.SupplierId,
                PhoneNumber = dto.PhoneNumber.Trim(),
                IsPrimary   = false   // phụ
            };
            await _repo.AddPhoneAsync(phone);
            await _repo.SaveChangesAsync();
        }

        public async Task DeletePhoneAsync(int supplierPhoneId)
        {
            var phone = await _repo.GetPhoneByIdAsync(supplierPhoneId);
            if (phone == null) throw new Exception("Không tìm thấy số điện thoại");
            if (phone.IsPrimary) throw new Exception("Không thể xoá số điện thoại chính");

            await _repo.DeletePhoneAsync(phone);
            await _repo.SaveChangesAsync();
        }

        // ===== BANK ACCOUNTS =====
        public async Task AddBankAccountAsync(AdminSupplierBankAccountCreateDTO dto)
        {
            var bank = new SupplierBankAccount
            {
                SupplierId    = dto.SupplierId,
                BankName      = dto.BankName.Trim(),
                AccountNumber = dto.AccountNumber.Trim(),
                AccountHolder = dto.AccountHolder.Trim(),
                IsPrimary     = false   // phụ
            };
            await _repo.AddBankAccountAsync(bank);
            await _repo.SaveChangesAsync();
        }

        public async Task DeleteBankAccountAsync(int supplierBankAccountId)
        {
            var bank = await _repo.GetBankAccountByIdAsync(supplierBankAccountId);
            if (bank == null) throw new Exception("Không tìm thấy tài khoản ngân hàng");
            if (bank.IsPrimary) throw new Exception("Không thể xoá tài khoản ngân hàng chính");

            await _repo.DeleteBankAccountAsync(bank);
            await _repo.SaveChangesAsync();
        }

        // ===== CONTACTS =====
        public async Task AddContactAsync(AdminSupplierContactCreateDTO dto)
        {
            var contact = new SupplierContact
            {
                SupplierId = dto.SupplierId,
                Name       = dto.Name.Trim(),
                Phone      = dto.Phone?.Trim(),
                Email      = dto.Email?.Trim(),
                Position   = dto.Position?.Trim(),
                IsPrimary  = false   // phụ
            };
            await _repo.AddContactAsync(contact);
            await _repo.SaveChangesAsync();
        }

        public async Task DeleteContactAsync(int supplierContactId)
        {
            var contact = await _repo.GetContactByIdAsync(supplierContactId);
            if (contact == null) throw new Exception("Không tìm thấy thông tin liên hệ");
            if (contact.IsPrimary) throw new Exception("Không thể xoá người liên hệ chính");

            await _repo.DeleteContactAsync(contact);
            await _repo.SaveChangesAsync();
        }

        // ===== LOCATION =====
        public async Task<List<Province>> GetProvincesAsync()
        {
            return await _context.Provinces.OrderBy(p => p.Name).ToListAsync();
        }

        public async Task<List<District>> GetDistrictsByProvinceAsync(int provinceId)
        {
            return await _context.Districts
                .Where(d => d.ProvinceId == provinceId)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<List<Ward>> GetWardsByDistrictAsync(int districtId)
        {
            return await _context.Wards
                .Where(w => w.DistrictId == districtId)
                .OrderBy(w => w.Name)
                .ToListAsync();
        }

        // =============================================================
        // ==================== PRIVATE HELPERS ========================
        // =============================================================

        /// <summary>
        /// Ghép địa chỉ đầy đủ từ 3 cấp + số nhà.
        /// Ví dụ: "123 Nguyễn Văn Linh, Phường Bình Chánh, Quận 8, TP. Hồ Chí Minh"
        /// </summary>
        private async Task<string?> BuildFullAddressAsync(
            int? provinceId, int? districtId, int? wardId, string? streetAddress)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(streetAddress))
                parts.Add(streetAddress.Trim());

            if (wardId.HasValue)
            {
                var ward = await _context.Wards.FindAsync(wardId.Value);
                if (ward != null) parts.Add(ward.Name);
            }

            if (districtId.HasValue)
            {
                var district = await _context.Districts.FindAsync(districtId.Value);
                if (district != null) parts.Add(district.Name);
            }

            if (provinceId.HasValue)
            {
                var province = await _context.Provinces.FindAsync(provinceId.Value);
                if (province != null) parts.Add(province.Name);
            }

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private static AdminSupplierDTO MapToListDTO(Supplier x)
        {
            var primaryPhone   = x.Phones.FirstOrDefault(p => p.IsPrimary);
            var primaryBank    = x.BankAccounts.FirstOrDefault(b => b.IsPrimary);
            var primaryContact = x.Contacts.FirstOrDefault(c => c.IsPrimary);

            return new AdminSupplierDTO
            {
                SupplierId           = x.SupplierId,
                Code                 = x.Code   ?? "",
                Name                 = x.Name   ?? "",
                TaxCode              = x.TaxCode,
                Website              = x.Website,
                Address              = x.Address,
                DebtAmount           = x.DebtAmount,
                Active               = x.Active,
                PrimaryPhone         = primaryPhone?.PhoneNumber,
                PrimaryContactName   = primaryContact?.Name,
                PrimaryContactPhone  = primaryContact?.Phone,
                PrimaryBankName      = primaryBank?.BankName,
                PrimaryAccountNumber = primaryBank?.AccountNumber,
            };
        }

        private async Task<AdminSupplierDetailDTO> MapToDetailDTOAsync(Supplier x)
        {
            // Parse lại địa chỉ để lấy ID từng cấp (nếu có)
            // Vì Address là chuỗi ghép, ta không reverse-parse; thay vào đó
            // cần lưu ID riêng → hiện tại ta dùng lookup ngược theo tên (best-effort).
            // Cách chính xác hơn: lưu riêng ProvinceId/DistrictId/WardId vào Supplier entity.
            // Tạm thời trả về null cho các ID để FE biết cần chọn lại nếu muốn thay đổi địa chỉ.
            return new AdminSupplierDetailDTO
            {
                SupplierId    = x.SupplierId,
                Code          = x.Code   ?? "",
                Name          = x.Name   ?? "",
                TaxCode       = x.TaxCode,
                Website       = x.Website,
                Address       = x.Address,
                DebtAmount    = x.DebtAmount,
                Active        = x.Active,

                // Location IDs — null vì Supplier model chưa lưu riêng
                // Frontend sẽ hiển thị Address text và cho phép chọn lại 3 cấp khi edit
                ProvinceId    = null,
                ProvinceName  = null,
                DistrictId    = null,
                DistrictName  = null,
                WardId        = null,
                WardName      = null,
                StreetAddress = null,

                Phones = x.Phones.Select(p => new AdminSupplierPhoneDTO
                {
                    SupplierPhoneId = p.SupplierPhoneId,
                    PhoneNumber     = p.PhoneNumber ?? "",
                    IsPrimary       = p.IsPrimary
                }).ToList(),

                BankAccounts = x.BankAccounts.Select(b => new AdminSupplierBankAccountDTO
                {
                    SupplierBankAccountId = b.SupplierBankAccountId,
                    BankName              = b.BankName      ?? "",
                    AccountNumber         = b.AccountNumber ?? "",
                    AccountHolder         = b.AccountHolder ?? "",
                    IsPrimary             = b.IsPrimary
                }).ToList(),

                Contacts = x.Contacts.Select(c => new AdminSupplierContactDTO
                {
                    SupplierContactId = c.SupplierContactId,
                    Name              = c.Name ?? "",
                    Phone             = c.Phone,
                    Email             = c.Email,
                    Position          = c.Position,
                    IsPrimary         = c.IsPrimary
                }).ToList()
            };
        }

        private static string Normalize(string text) => text?.Trim() ?? "";
    }
}
