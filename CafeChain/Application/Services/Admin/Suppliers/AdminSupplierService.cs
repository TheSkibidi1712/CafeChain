using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Suppliers;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Locations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Linq;

namespace CafeChain.Application.Services.Admin.Suppliers
{
    public class AdminSupplierService : IAdminSupplierService
    {
        private readonly IAdminSupplierRepository _repo;
        private readonly AppDbContext _context;
        private readonly IIngredientSupplierPackageValidator _packageValidator;

        public AdminSupplierService(
            IAdminSupplierRepository repo,
            AppDbContext context,
            IIngredientSupplierPackageValidator packageValidator)
        {
            _repo = repo;
            _context = context;
            _packageValidator = packageValidator;
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
            return MapToDetailDTO(entity);
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
            var supplier = new Supplier
            {
                Code = await _repo.GenerateNextCodeAsync(),
                Name = dto.Name,
                Address = Clean(dto.Address),
                Note = Clean(dto.Note),
                Active = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,

                // Số điện thoại chính
                Phones = new List<SupplierPhone>
                {
                    new SupplierPhone
                    {
                        PhoneNumber = dto.PrimaryPhone.Trim(),
                        IsPrimary   = true
                    }
                },

                // Thông tin liên hệ chính
                Contacts = new List<SupplierContact>
                {
                    new SupplierContact
                    {
                        Name      = dto.PrimaryContactName.Trim(),
                        PhoneNumber = Clean(dto.PrimaryContactPhone),
                        Email     = dto.PrimaryContactEmail?.Trim(),
                        Position  = dto.PrimaryContactPosition?.Trim(),
                        IsPrimary = true
                    }
                }
            };

            // ===== SỐ ĐIỆN THOẠI PHỤ =====
            foreach (var ph in dto.AdditionalPhones
                .Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                supplier.Phones.Add(new SupplierPhone
                {
                    PhoneNumber = ph.Trim(),
                    IsPrimary = false
                });
            }

            // ===== NGƯỜI LIÊN HỆ PHỤ =====
            foreach (var ct in dto.AdditionalContacts
                .Where(c => !string.IsNullOrWhiteSpace(c.Name)))
            {
                supplier.Contacts.Add(new SupplierContact
                {
                    Name = ct.Name.Trim(),
                    PhoneNumber = Clean(ct.Phone),
                    Email = ct.Email?.Trim(),
                    Position = ct.Position?.Trim(),
                    IsPrimary = false
                });
            }

            await _repo.CreateAsync(supplier);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await _repo.SaveChangesAsync();
                    return supplier.SupplierId;
                }
                catch (DbUpdateException ex) when (IsUniqueCodeCollision(ex) && attempt < 2)
                {
                    supplier.Code = await _repo.GenerateNextCodeAsync();
                }
            }

            throw new InvalidOperationException("Không thể sinh mã nhà cung cấp duy nhất sau nhiều lần thử.");
        }

        // ===== UPDATE =====
        public async Task UpdateAsync(AdminSupplierUpdateDTO dto)
        {
            var entity = await _repo.GetByIdAsync(dto.SupplierId);
            if (entity == null)
                throw new Exception("Không tìm thấy nhà cung cấp");

            dto.Name = Normalize(dto.Name);

            if (!string.IsNullOrWhiteSpace(dto.RowVersion))
            {
                _context.Entry(entity).Property(x => x.RowVersion).OriginalValue =
                    Convert.FromBase64String(dto.RowVersion);
            }

            entity.Name = dto.Name;
            entity.Address = Clean(dto.Address);
            entity.Note = Clean(dto.Note);
            entity.Active = dto.Active;
            entity.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _repo.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException(
                    "Nhà cung cấp vừa được người khác cập nhật. Vui lòng tải lại dữ liệu.");
            }
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
                SupplierId = dto.SupplierId,
                PhoneNumber = dto.PhoneNumber.Trim(),
                IsPrimary = false   // phụ
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

        // ===== CONTACTS =====
        public async Task AddContactAsync(AdminSupplierContactCreateDTO dto)
        {
            var contact = new SupplierContact
            {
                SupplierId = dto.SupplierId,
                Name = dto.Name.Trim(),
                PhoneNumber = Clean(dto.Phone),
                Email = dto.Email?.Trim(),
                Position = dto.Position?.Trim(),
                IsPrimary = false   // phụ
            };
            await _repo.AddContactAsync(contact);
            await _repo.SaveChangesAsync();
        }

        public async Task UpdateContactAsync(AdminSupplierContactUpdateDTO dto)
        {
            var contact = await _repo.GetContactByIdAsync(dto.SupplierContactId);
            if (contact == null || contact.SupplierId != dto.SupplierId)
                throw new InvalidOperationException("Không tìm thấy người liên hệ của nhà cung cấp.");

            contact.Name = Normalize(dto.Name);
            contact.PhoneNumber = Clean(dto.Phone);
            contact.Email = Clean(dto.Email);
            contact.Position = Clean(dto.Position);
            contact.Active = dto.Active;
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

        public async Task SetPrimaryContactAsync(int supplierContactId)
        {
            var contact = await _repo.GetContactByIdAsync(supplierContactId);
            if (contact == null) throw new Exception("Không tìm thấy thông tin liên hệ");
            if (contact.IsPrimary) throw new Exception("Người liên hệ này đã là đầu mối chính");

            // Bỏ primary tất cả contact cùng supplier
            var allContacts = await _repo.GetContactsBySupplierIdAsync(contact.SupplierId);
            foreach (var c in allContacts)
                c.IsPrimary = false;

            // Đặt primary cho contact được chọn
            contact.IsPrimary = true;

            await _repo.SaveChangesAsync();
        }

        // =============================================================
        // ==================== PRIVATE HELPERS ========================
        // =============================================================

        private static AdminSupplierDTO MapToListDTO(Supplier x)
        {
            var primaryPhone = x.Phones.FirstOrDefault(p => p.IsPrimary);
            var primaryContact = x.Contacts.FirstOrDefault(c => c.IsPrimary);

            return new AdminSupplierDTO
            {
                SupplierId = x.SupplierId,
                Code = x.Code ?? "",
                Name = x.Name ?? "",
                Address = x.Address,
                Note = x.Note,
                Active = x.Active,
                PrimaryPhone = primaryPhone?.PhoneNumber,
                PrimaryContactName = primaryContact?.Name,
                PrimaryContactPhone = primaryContact?.PhoneNumber,
                ActiveOfferCount = x.IngredientSuppliers.Count(o => o.Active)
            };
        }

        private static AdminSupplierDetailDTO MapToDetailDTO(Supplier x)
        {
            return new AdminSupplierDetailDTO
            {
                SupplierId = x.SupplierId,
                Code = x.Code ?? "",
                Name = x.Name ?? "",
                Address = x.Address,
                Note = x.Note,
                Active = x.Active,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                RowVersion = Convert.ToBase64String(x.RowVersion),

                Phones = x.Phones.Select(p => new AdminSupplierPhoneDTO
                {
                    SupplierPhoneId = p.SupplierPhoneId,
                    PhoneNumber = p.PhoneNumber ?? "",
                    IsPrimary = p.IsPrimary
                }).ToList(),

                Contacts = x.Contacts.Select(c => new AdminSupplierContactDTO
                {
                    SupplierContactId = c.SupplierContactId,
                    Name = c.Name ?? "",
                    Phone = c.PhoneNumber,
                    Email = c.Email,
                    Position = c.Position,
                    IsPrimary = c.IsPrimary
                }).ToList()
            };
        }

        private static string Normalize(string text) => text?.Trim() ?? "";

        private static string? Clean(string? text) =>
            string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        private static bool IsUniqueCodeCollision(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("IX_Suppliers_Code", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("2601", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("2627", StringComparison.OrdinalIgnoreCase);
        }

        // ===== INGREDIENT SUPPLIER OFFERS (#111) =====

        public async Task<List<AdminIngredientSupplierDTO>> GetIngredientOffersAsync(int supplierId)
        {
            var offers = await _context.IngredientSuppliers
                .AsNoTracking()
                .Include(x => x.Ingredient).ThenInclude(i => i.BaseUnit)
                .Include(x => x.Unit)
                .Include(x => x.Supplier)
                .Where(x => x.SupplierId == supplierId)
                .OrderBy(x => x.Ingredient.Name)
                .ToListAsync();

            var result = new List<AdminIngredientSupplierDTO>();
            foreach (var offer in offers)
                result.Add(await MapIngredientOfferAsync(offer));
            return result;
        }

        public async Task<AdminIngredientSupplierDTO?> GetIngredientOfferByIdAsync(int ingredientSupplierId)
        {
            var offer = await LoadOfferTrackedAsync(ingredientSupplierId, asNoTracking: true);
            if (offer == null) return null;
            return await MapIngredientOfferAsync(offer);
        }

        public async Task<int> CreateIngredientOfferAsync(AdminIngredientSupplierSaveDTO dto)
        {
            var requirePackage = dto.Active;
            var validation = await _packageValidator.ValidateAsync(
                dto.IngredientId,
                dto.SupplierId,
                dto.UnitId,
                dto.PackageQuantity,
                dto.CurrentPrice,
                dto.Active,
                requirePackageQuantity: requirePackage);

            if (!validation.IsSuccess)
                throw new InvalidOperationException(validation.Message);

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                if (dto.IsPrimary)
                    await ClearPrimaryAsync(dto.IngredientId, excludeId: null);

                var entity = new IngredientSupplier
                {
                    IngredientId = dto.IngredientId,
                    SupplierId = dto.SupplierId,
                    UnitId = dto.UnitId,
                    PackageQuantity = dto.PackageQuantity,
                    CurrentPrice = dto.CurrentPrice,
                    MinimumOrderQuantity = dto.MinimumOrderQuantity,
                    LeadTimeDays = dto.LeadTimeDays,
                    IsPrimary = dto.IsPrimary,
                    Active = dto.Active,
                    Note = dto.Note?.Trim()
                };

                _context.IngredientSuppliers.Add(entity);
                await _context.SaveChangesAsync();

                _context.Set<IngredientSupplierPriceHistory>().Add(new IngredientSupplierPriceHistory
                {
                    IngredientSupplierId = entity.IngredientSupplierId,
                    Price = entity.CurrentPrice,
                    PackageQuantity = entity.PackageQuantity,
                    PackageUnitId = entity.UnitId,
                    EffectiveDate = DateTime.UtcNow,
                    IsCurrent = true,
                    Note = "Khởi tạo gói mua"
                });
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return entity.IngredientSupplierId;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateIngredientOfferAsync(AdminIngredientSupplierSaveDTO dto)
        {
            if (!dto.IngredientSupplierId.HasValue || dto.IngredientSupplierId.Value <= 0)
                throw new InvalidOperationException("Thiếu mã bảng giá gói mua.");

            var entity = await LoadOfferTrackedAsync(dto.IngredientSupplierId.Value, asNoTracking: false)
                ?? throw new InvalidOperationException("Không tìm thấy bảng giá gói mua.");

            var packageOrPriceChanged =
                entity.CurrentPrice != dto.CurrentPrice
                || entity.PackageQuantity != dto.PackageQuantity
                || entity.UnitId != dto.UnitId;

            // Require PackageQuantity when:
            // - re-activating, or
            // - Active and package/pricing fields change (remediation), or
            // - result is Active and package quantity is being set/required for new completeness
            var reactivating = dto.Active && !entity.Active;
            var requirePackage = reactivating
                || (dto.Active && packageOrPriceChanged)
                || (dto.Active && dto.PackageQuantity.HasValue); // if user supplies package while Active, validate it

            var validation = await _packageValidator.ValidateAsync(
                dto.IngredientId,
                dto.SupplierId,
                dto.UnitId,
                dto.PackageQuantity,
                dto.CurrentPrice,
                dto.Active,
                requirePackageQuantity: requirePackage,
                excludeIngredientSupplierId: entity.IngredientSupplierId);

            if (!validation.IsSuccess)
                throw new InvalidOperationException(validation.Message);

            // Supplier/ingredient identity should not silently change to another unique pair without validation
            if (entity.IngredientId != dto.IngredientId || entity.SupplierId != dto.SupplierId)
            {
                // Allow only if validation passed unique check
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                if (dto.IsPrimary)
                    await ClearPrimaryAsync(dto.IngredientId, excludeId: entity.IngredientSupplierId);

                if (packageOrPriceChanged)
                {
                    var currentHistories = await _context.Set<IngredientSupplierPriceHistory>()
                        .Where(h => h.IngredientSupplierId == entity.IngredientSupplierId && h.IsCurrent)
                        .ToListAsync();
                    foreach (var h in currentHistories)
                        h.IsCurrent = false;

                    _context.Set<IngredientSupplierPriceHistory>().Add(new IngredientSupplierPriceHistory
                    {
                        IngredientSupplierId = entity.IngredientSupplierId,
                        Price = dto.CurrentPrice,
                        PackageQuantity = dto.PackageQuantity,
                        PackageUnitId = dto.UnitId,
                        EffectiveDate = DateTime.UtcNow,
                        IsCurrent = true,
                        Note = "Cập nhật gói mua / giá"
                    });
                }

                entity.IngredientId = dto.IngredientId;
                entity.SupplierId = dto.SupplierId;
                entity.UnitId = dto.UnitId;
                entity.PackageQuantity = dto.PackageQuantity;
                entity.CurrentPrice = dto.CurrentPrice;
                entity.MinimumOrderQuantity = dto.MinimumOrderQuantity;
                entity.LeadTimeDays = dto.LeadTimeDays;
                entity.IsPrimary = dto.IsPrimary;
                entity.Active = dto.Active;
                entity.Note = dto.Note?.Trim();

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task ToggleIngredientOfferActiveAsync(int ingredientSupplierId, bool active)
        {
            var entity = await LoadOfferTrackedAsync(ingredientSupplierId, asNoTracking: false)
                ?? throw new InvalidOperationException("Không tìm thấy bảng giá gói mua.");

            if (active)
            {
                var validation = await _packageValidator.ValidateAsync(
                    entity.IngredientId,
                    entity.SupplierId,
                    entity.UnitId,
                    entity.PackageQuantity,
                    entity.CurrentPrice,
                    isActive: true,
                    requirePackageQuantity: true,
                    excludeIngredientSupplierId: entity.IngredientSupplierId);

                if (!validation.IsSuccess)
                    throw new InvalidOperationException(validation.Message);
            }

            entity.Active = active;
            await _context.SaveChangesAsync();
        }

        public async Task<List<object>> GetIngredientDropdownAsync()
        {
            var rows = await _context.Ingredients
                .AsNoTracking()
                .Include(i => i.BaseUnit)
                .Where(i => i.Active)
                .OrderBy(i => i.Name)
                .Select(i => new
                {
                    ingredientId = i.IngredientId,
                    code = i.Code,
                    name = i.Name,
                    baseUnitId = i.BaseUnitId,
                    baseUnitCode = i.BaseUnit != null ? i.BaseUnit.UnitCode : ""
                })
                .ToListAsync();

            return rows.Cast<object>().ToList();
        }

        public async Task<List<object>> GetContentUnitDropdownAsync()
        {
            var rows = await _context.Units
                .AsNoTracking()
                .Where(u => u.Active)
                .OrderBy(u => u.UnitCode)
                .Select(u => new
                {
                    unitId = u.UnitId,
                    unitCode = u.UnitCode,
                    name = u.Name,
                    type = (int)u.Type
                })
                .ToListAsync();

            return rows.Cast<object>().ToList();
        }

        private async Task ClearPrimaryAsync(int ingredientId, int? excludeId)
        {
            var others = await _context.IngredientSuppliers
                .Where(x =>
                    x.IngredientId == ingredientId &&
                    x.IsPrimary &&
                    (!excludeId.HasValue || x.IngredientSupplierId != excludeId.Value))
                .ToListAsync();

            foreach (var o in others)
                o.IsPrimary = false;
        }

        private async Task<IngredientSupplier?> LoadOfferTrackedAsync(int id, bool asNoTracking)
        {
            IQueryable<IngredientSupplier> q = _context.IngredientSuppliers
                .Include(x => x.Ingredient).ThenInclude(i => i.BaseUnit)
                .Include(x => x.Unit)
                .Include(x => x.Supplier);

            if (asNoTracking)
                q = q.AsNoTracking();

            return await q.FirstOrDefaultAsync(x => x.IngredientSupplierId == id);
        }

        private async Task<AdminIngredientSupplierDTO> MapIngredientOfferAsync(IngredientSupplier x)
        {
            var complete = await _packageValidator.HasCompletePackageDefinitionAsync(x);
            var unitCode = x.Unit?.UnitCode ?? "";
            var packageDisplay = complete
                ? $"{x.PackageQuantity?.ToString("0.####", CultureInfo.InvariantCulture)} {unitCode} / gói"
                : "Chưa đủ dữ liệu gói mua";

            return new AdminIngredientSupplierDTO
            {
                IngredientSupplierId = x.IngredientSupplierId,
                IngredientId = x.IngredientId,
                IngredientCode = x.Ingredient?.Code ?? "",
                IngredientName = x.Ingredient?.Name ?? "",
                SupplierId = x.SupplierId,
                SupplierName = x.Supplier?.Name ?? "",
                CurrentPrice = x.CurrentPrice,
                PackageQuantity = x.PackageQuantity,
                UnitId = x.UnitId,
                UnitCode = unitCode,
                UnitName = x.Unit?.Name ?? "",
                BaseUnitId = x.Ingredient?.BaseUnitId ?? 0,
                BaseUnitCode = x.Ingredient?.BaseUnit?.UnitCode ?? "",
                MinimumOrderQuantity = x.MinimumOrderQuantity,
                LeadTimeDays = x.LeadTimeDays,
                IsPrimary = x.IsPrimary,
                Active = x.Active,
                Note = x.Note,
                HasCompletePackageDefinition = complete,
                PackageDisplay = packageDisplay,
                PriceDisplay = $"{x.CurrentPrice.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} ₫ / gói mua"
            };
        }
    }
}
