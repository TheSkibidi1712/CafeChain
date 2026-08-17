using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Constants;
using CafeChain.Application.Exceptions;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Suppliers;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Locations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CafeChain.Application.Services.Admin.Suppliers
{
    public class AdminSupplierService : IAdminSupplierService
    {
        private readonly IAdminSupplierRepository _repo;
        private readonly AppDbContext _context;
        private readonly IIngredientSupplierPackageValidator _packageValidator;
        private readonly IUnitConversionService _unitConversion;

        public AdminSupplierService(
            IAdminSupplierRepository repo,
            AppDbContext context,
            IIngredientSupplierPackageValidator packageValidator,
            IUnitConversionService? unitConversion = null)
        {
            _repo = repo;
            _context = context;
            _packageValidator = packageValidator;
            _unitConversion = unitConversion ?? new UnitConversionService(
                context,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<UnitConversionService>.Instance,
                new PhysicalUnitConversionService(
                    context,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<PhysicalUnitConversionService>.Instance));
        }

        // ===== GET ALL =====
        public async Task<List<AdminSupplierDTO>> GetAllAsync(
            string? search,
            bool? status,
            IReadOnlyCollection<int>? storeScope = null)
        {
            var data = await _repo.GetAllAsync(search, status, storeScope);
            return data.Select(MapToListDTO).ToList();
        }

        public Task<AdminSupplierIndexPageDTO> GetPagedAsync(
            string? search,
            bool? status,
            int page,
            int pageSize,
            IReadOnlyCollection<int>? storeScope = null) =>
            _repo.GetPagedAsync(search, status, page, pageSize, storeScope);

        // ===== GET BY ID =====
        public async Task<AdminSupplierDetailDTO?> GetByIdAsync(
            int id,
            IReadOnlyCollection<int>? storeScope = null)
        {
            var entity = await _repo.GetByIdAsync(id, storeScope);
            if (entity == null) return null;
            return MapToDetailDTO(entity);
        }

        public async Task<List<AdminSupplierAuditDTO>> GetAuditHistoryAsync(
            int supplierId,
            IReadOnlyCollection<int>? storeScope = null)
        {
            if (!await _repo.ExistsInScopeAsync(supplierId, storeScope))
                return new List<AdminSupplierAuditDTO>();

            var rows = await _context.AuditLogs
                .AsNoTracking()
                .Where(x => x.TableName == "Suppliers" && x.RecordId == supplierId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(50)
                .ToListAsync();

            var actorIds = rows.Where(x => x.UserId > 0).Select(x => x.UserId).Distinct().ToArray();
            var actors = await _context.Staffs
                .AsNoTracking()
                .Where(x => actorIds.Contains(x.StaffId))
                .Select(x => new
                {
                    x.StaffId,
                    x.FullName,
                    Role = x.Account.AccountRoles
                        .OrderBy(ar => ar.RoleId)
                        .Select(ar => ar.Role.Name)
                        .FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.StaffId);

            return rows.Select(row =>
            {
                actors.TryGetValue(row.UserId, out var actor);
                return new AdminSupplierAuditDTO
                {
                    Action = row.Action,
                    Title = SupplierAuditTitle(row.Action),
                    ActorName = actor?.FullName ?? "Hệ thống",
                    ActorRole = actor?.Role,
                    Changes = BuildBusinessAuditChanges(row.OldData, row.NewData),
                    CreatedAt = row.CreatedAt
                };
            }).ToList();
        }

        // ===== GENERATE NEXT CODE =====
        public async Task<string> GenerateNextCodeAsync()
        {
            return await _repo.GenerateNextCodeAsync();
        }

        // ===== CREATE =====
        public async Task<int> CreateAsync(AdminSupplierCreateDTO dto, int actorStaffId = 0)
        {
            dto.Name = Normalize(dto.Name);
            dto.TaxCode = SupplierTaxCodeNormalizer.Normalize(dto.TaxCode);
            await EnsureTaxCodeAvailableAsync(dto.TaxCode);

            var payloadHash = BuildPayloadHash(dto);
            var matches = await FindSoftDuplicateMatchesAsync(dto);
            SupplierDuplicateWarning? warning = null;

            if (matches.Count > 0 && !dto.DuplicateWarningId.HasValue)
            {
                warning = await CreateWarningAsync(actorStaffId, payloadHash, matches);
                throw new SupplierDomainException(
                    SupplierIdentityConstants.PossibleDuplicate,
                    "Có nhà cung cấp có thông tin nhận diện tương tự. Vui lòng kiểm tra trước khi tạo mới.",
                    ToWarningDto(warning, matches));
            }

            if (dto.DuplicateWarningId.HasValue)
                warning = await ValidateWarningAsync(dto, actorStaffId, payloadHash, matches);

            var supplier = BuildSupplier(dto);
            await _repo.CreateAsync(supplier);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await using var transaction = await BeginTransactionAsync(
                    warning == null ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable);
                try
                {
                    supplier.Code = await _repo.GenerateNextCodeAsync();
                    await _repo.SaveChangesAsync();

                    _context.AuditLogs.Add(NewSupplierAudit(
                        supplier.SupplierId,
                        "SUPPLIER_CREATED",
                        actorStaffId,
                        null,
                        JsonSerializer.Serialize(new { supplier.Code, supplier.Name, supplier.TaxCode })));

                    if (warning != null)
                    {
                        warning.Status = SupplierIdentityConstants.WarningUsed;
                        warning.UsedAtUtc = DateTime.UtcNow;
                        warning.OverrideReason = Clean(dto.DuplicateOverrideReason);
                        warning.CreatedSupplierId = supplier.SupplierId;
                        _context.AuditLogs.Add(NewSupplierAudit(
                            supplier.SupplierId,
                            "SUPPLIER_DUPLICATE_OVERRIDE",
                            actorStaffId,
                            null,
                            JsonSerializer.Serialize(new
                            {
                                warningId = warning.PublicId,
                                reason = warning.OverrideReason,
                                warning.WarningFingerprint,
                                warning.MatchedSupplierIdsJson,
                                warning.MatchedSignalsJson
                            })));
                    }

                    await _context.SaveChangesAsync();
                    if (transaction != null) await transaction.CommitAsync();
                    return supplier.SupplierId;
                }
                catch (Exception ex)
                {
                    await TryRollbackAsync(transaction);
                    if (warning != null && ex is DbUpdateConcurrencyException)
                    {
                        throw new SupplierDomainException(
                            SupplierIdentityConstants.WarningInvalid,
                            "Cảnh báo trùng đã được sử dụng bởi yêu cầu khác. Vui lòng kiểm tra lại.");
                    }
                    if (IsTaxCodeCollision(ex))
                    {
                        if (transaction != null) await transaction.DisposeAsync();
                        throw await TaxCodeDuplicateAsync(dto.TaxCode);
                    }
                    if ((IsUniqueCodeCollision(ex) || IsSqlDeadlock(ex)) && attempt < 2)
                    {
                        await Task.Delay(40 * (attempt + 1));
                        continue;
                    }
                    throw;
                }
            }

            throw new InvalidOperationException("Không thể sinh mã nhà cung cấp duy nhất sau nhiều lần thử.");
        }

        public async Task<AdminSupplierDuplicateWarningDTO?> PrepareDuplicateWarningAsync(
            AdminSupplierCreateDTO dto,
            int actorStaffId = 0)
        {
            dto.Name = Normalize(dto.Name);
            dto.TaxCode = SupplierTaxCodeNormalizer.Normalize(dto.TaxCode);
            await EnsureTaxCodeAvailableAsync(dto.TaxCode);
            var matches = await FindSoftDuplicateMatchesAsync(dto);
            if (matches.Count == 0) return null;
            var warning = await CreateWarningAsync(actorStaffId, BuildPayloadHash(dto), matches);
            return ToWarningDto(warning, matches);
        }

        public async Task<bool> IsDuplicateWarningValidAsync(
            AdminSupplierCreateDTO dto,
            int actorStaffId = 0)
        {
            try
            {
                dto.Name = Normalize(dto.Name);
                dto.TaxCode = SupplierTaxCodeNormalizer.Normalize(dto.TaxCode);
                await EnsureTaxCodeAvailableAsync(dto.TaxCode);
                var matches = await FindSoftDuplicateMatchesAsync(dto);
                if (matches.Count == 0) return true;
                if (!dto.DuplicateWarningId.HasValue) return false;
                await ValidateWarningAsync(dto, actorStaffId, BuildPayloadHash(dto), matches);
                return true;
            }
            catch (SupplierDomainException)
            {
                return false;
            }
        }

        public async Task<List<AdminSupplierDuplicateMatchDTO>> FindDuplicateMatchesAsync(
            AdminSupplierCreateDTO dto)
        {
            dto.Name = Normalize(dto.Name);
            dto.TaxCode = SupplierTaxCodeNormalizer.Normalize(dto.TaxCode);
            var matches = await FindSoftDuplicateMatchesAsync(dto);
            return matches.Select(ToMatchDto).ToList();
        }

        public async Task<IReadOnlyList<List<AdminSupplierDuplicateMatchDTO>>> FindDuplicateMatchesBatchAsync(
            IReadOnlyList<AdminSupplierCreateDTO> requests)
        {
            if (requests.Count == 0) return [];
            var candidates = await LoadSoftDuplicateCandidatesAsync();
            return requests.Select(dto =>
            {
                dto.Name = Normalize(dto.Name);
                dto.TaxCode = SupplierTaxCodeNormalizer.Normalize(dto.TaxCode);
                return FindSoftDuplicateMatches(dto, candidates).Select(ToMatchDto).ToList();
            }).ToList();
        }

        // ===== UPDATE =====
        public async Task UpdateAsync(AdminSupplierUpdateDTO dto, int actorStaffId = 0)
        {
            var entity = await _repo.GetByIdAsync(dto.SupplierId);
            if (entity == null)
                throw new Exception("Không tìm thấy nhà cung cấp");

            dto.Name = Normalize(dto.Name);
            dto.TaxCode = SupplierTaxCodeNormalizer.Normalize(dto.TaxCode);
            await EnsureTaxCodeAvailableAsync(dto.TaxCode, dto.SupplierId);

            if (string.IsNullOrWhiteSpace(dto.RowVersion))
                throw new SupplierDomainException(
                    SupplierIdentityConstants.StaleVersion,
                    "Phiên bản dữ liệu là bắt buộc. Vui lòng tải lại nhà cung cấp.");

            try
            {
                _context.Entry(entity).Property(x => x.RowVersion).OriginalValue =
                    Convert.FromBase64String(dto.RowVersion);
            }
            catch (FormatException)
            {
                throw new SupplierDomainException(
                    SupplierIdentityConstants.StaleVersion,
                    "Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại nhà cung cấp.");
            }

            var oldTaxCode = entity.TaxCode;
            entity.Name = dto.Name;
            entity.TaxCode = dto.TaxCode;
            entity.Address = Clean(dto.Address);
            entity.Note = Clean(dto.Note);
            entity.Active = dto.Active;
            entity.UpdatedAt = DateTime.UtcNow;

            try
            {
                if (!string.Equals(oldTaxCode, entity.TaxCode, StringComparison.Ordinal))
                {
                    _context.AuditLogs.Add(NewSupplierAudit(
                        entity.SupplierId,
                        "SUPPLIER_TAX_CODE_UPDATED",
                        actorStaffId,
                        JsonSerializer.Serialize(new { taxCode = oldTaxCode }),
                        JsonSerializer.Serialize(new { taxCode = entity.TaxCode })));
                }
                await _repo.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new SupplierDomainException(
                    SupplierIdentityConstants.StaleVersion,
                    "Nhà cung cấp vừa được người khác cập nhật. Vui lòng tải lại dữ liệu.");
            }
            catch (DbUpdateException ex) when (IsTaxCodeCollision(ex))
            {
                throw await TaxCodeDuplicateAsync(dto.TaxCode, dto.SupplierId);
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
                TaxCode = x.TaxCode,
                Address = x.Address,
                Note = x.Note,
                Active = x.Active,
                PrimaryPhone = primaryPhone?.PhoneNumber,
                PrimaryContactName = primaryContact?.Name,
                PrimaryContactPhone = primaryContact?.PhoneNumber,
                ActiveOfferCount = x.IngredientSuppliers.Count(o => o.Active),
                ActiveStoreCount = x.SupplierStores.Count(o => o.Active)
            };
        }

        private static AdminSupplierDetailDTO MapToDetailDTO(Supplier x)
        {
            return new AdminSupplierDetailDTO
            {
                SupplierId = x.SupplierId,
                Code = x.Code ?? "",
                Name = x.Name ?? "",
                TaxCode = x.TaxCode,
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

        private static AdminSupplierStoreDTO MapSupplierStore(SupplierStore x) => new()
        {
            SupplierStoreId = x.SupplierStoreId,
            SupplierId = x.SupplierId,
            StoreId = x.StoreId,
            StoreName = x.Store?.Name ?? "",
            Active = x.Active,
            LeadTimeOverrideDays = x.LeadTimeOverrideDays,
            DeliverySchedule = x.DeliverySchedule,
            Note = x.Note,
            RowVersion = Convert.ToBase64String(x.RowVersion)
        };

        private static string Normalize(string text) => text?.Trim() ?? "";

        private static string? Clean(string? text) =>
            string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        private async Task EnsureTaxCodeAvailableAsync(string? taxCode, int? excludeSupplierId = null)
        {
            if (taxCode == null) return;

            var owner = await _context.Suppliers
                .AsNoTracking()
                .Where(x => x.TaxCode == taxCode
                            && (!excludeSupplierId.HasValue || x.SupplierId != excludeSupplierId.Value))
                .Select(x => new { x.SupplierId, x.Code, x.Name, x.Active })
                .FirstOrDefaultAsync();

            if (owner != null)
            {
                throw new SupplierDomainException(
                    SupplierIdentityConstants.TaxCodeDuplicate,
                    "Mã số thuế này đã được sử dụng bởi Nhà cung cấp khác.",
                    new { existingSupplier = owner });
            }
        }

        private async Task<SupplierDomainException> TaxCodeDuplicateAsync(
            string? taxCode,
            int? excludeSupplierId = null)
        {
            var owner = taxCode == null
                ? null
                : await _context.Suppliers
                    .AsNoTracking()
                    .Where(x => x.TaxCode == taxCode
                                && (!excludeSupplierId.HasValue
                                    || x.SupplierId != excludeSupplierId.Value))
                    .Select(x => new { x.SupplierId, x.Code, x.Name, x.Active })
                    .FirstOrDefaultAsync();

            object payload = owner != null
                ? new { existingSupplier = owner }
                : new { taxCode, supplierId = excludeSupplierId };

            return new SupplierDomainException(
                SupplierIdentityConstants.TaxCodeDuplicate,
                "Mã số thuế này đã được sử dụng bởi Nhà cung cấp khác.",
                payload);
        }

        private async Task<List<SoftDuplicateMatch>> FindSoftDuplicateMatchesAsync(AdminSupplierCreateDTO dto)
        {
            var candidates = await LoadSoftDuplicateCandidatesAsync();
            return FindSoftDuplicateMatches(dto, candidates);
        }

        private async Task<List<Supplier>> LoadSoftDuplicateCandidatesAsync() =>
            await _context.Suppliers
                .AsNoTracking()
                .Include(x => x.Phones)
                .Include(x => x.Contacts)
                .OrderBy(x => x.SupplierId)
                .ToListAsync();

        private static List<SoftDuplicateMatch> FindSoftDuplicateMatches(
            AdminSupplierCreateDTO dto,
            IReadOnlyList<Supplier> candidates)
        {
            var incomingName = NormalizeIdentityText(dto.Name);
            var incomingAddress = NormalizeIdentityText(dto.Address);
            var incomingHotlines = dto.AdditionalPhones
                .Append(dto.PrimaryPhone)
                .Select(NormalizePhone)
                .Where(x => x.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
            var incomingContactPhones = dto.AdditionalContacts
                .Select(x => x.Phone)
                .Append(dto.PrimaryContactPhone)
                .Select(NormalizePhone)
                .Where(x => x.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
            var incomingEmails = dto.AdditionalContacts
                .Select(x => NormalizeEmail(x.Email))
                .Append(NormalizeEmail(dto.PrimaryContactEmail))
                .Where(x => x.Length > 0)
                .ToHashSet(StringComparer.Ordinal);

            var matches = new List<SoftDuplicateMatch>();
            foreach (var supplier in candidates)
            {
                var signals = new List<string>();
                if (incomingName.Length > 0 && NormalizeIdentityText(supplier.Name) == incomingName)
                    signals.Add("Tên nhà cung cấp");
                if (incomingHotlines.Count > 0 && supplier.Phones.Any(x => incomingHotlines.Contains(NormalizePhone(x.PhoneNumber))))
                    signals.Add("Hotline");
                if (incomingContactPhones.Count > 0 && supplier.Contacts.Any(x => incomingContactPhones.Contains(NormalizePhone(x.PhoneNumber))))
                    signals.Add("Số điện thoại liên hệ");
                if (incomingAddress.Length > 0 && NormalizeIdentityText(supplier.Address) == incomingAddress)
                    signals.Add("Địa chỉ");
                if (incomingEmails.Count > 0 && supplier.Contacts.Any(x => incomingEmails.Contains(NormalizeEmail(x.Email))))
                    signals.Add("Email liên hệ");

                if (signals.Count > 0)
                {
                    matches.Add(new SoftDuplicateMatch(
                        supplier.SupplierId,
                        supplier.Code ?? "",
                        supplier.Name ?? "",
                        supplier.Active,
                        signals.OrderBy(x => x, StringComparer.Ordinal).ToList()));
                }
            }

            return matches;
        }

        private async Task<SupplierDuplicateWarning> CreateWarningAsync(
            int actorStaffId,
            string payloadHash,
            List<SoftDuplicateMatch> matches)
        {
            var now = DateTime.UtcNow;
            var warning = new SupplierDuplicateWarning
            {
                PublicId = Guid.NewGuid(),
                RequestedByStaffId = actorStaffId,
                Status = SupplierIdentityConstants.WarningPending,
                PayloadHash = payloadHash,
                WarningFingerprint = BuildWarningFingerprint(matches),
                MatchedSupplierIdsJson = JsonSerializer.Serialize(matches.Select(x => x.SupplierId)),
                MatchedSignalsJson = JsonSerializer.Serialize(matches.Select(x => new { x.SupplierId, x.Signals })),
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(10)
            };
            _context.SupplierDuplicateWarnings.Add(warning);
            await _context.SaveChangesAsync();
            return warning;
        }

        private async Task<SupplierDuplicateWarning> ValidateWarningAsync(
            AdminSupplierCreateDTO dto,
            int actorStaffId,
            string payloadHash,
            List<SoftDuplicateMatch> matches)
        {
            if (string.IsNullOrWhiteSpace(dto.DuplicateOverrideReason))
            {
                throw new SupplierDomainException(
                    SupplierIdentityConstants.OverrideReasonRequired,
                    "Vui lòng nhập lý do vẫn tạo nhà cung cấp mới.");
            }

            var warning = await _context.SupplierDuplicateWarnings
                .SingleOrDefaultAsync(x => x.PublicId == dto.DuplicateWarningId);
            if (warning == null
                || warning.RequestedByStaffId != actorStaffId
                || warning.Status != SupplierIdentityConstants.WarningPending
                || warning.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw new SupplierDomainException(
                    SupplierIdentityConstants.WarningInvalid,
                    "Cảnh báo trùng không còn hợp lệ. Vui lòng kiểm tra lại dữ liệu.");
            }

            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(warning.PayloadHash),
                    Convert.FromHexString(payloadHash))
                || !string.Equals(
                    warning.WarningFingerprint,
                    BuildWarningFingerprint(matches),
                    StringComparison.Ordinal))
            {
                throw new SupplierDomainException(
                    SupplierIdentityConstants.WarningStale,
                    "Dữ liệu hoặc kết quả kiểm tra trùng đã thay đổi. Vui lòng kiểm tra lại.");
            }

            return warning;
        }

        private static Supplier BuildSupplier(AdminSupplierCreateDTO dto)
        {
            var now = DateTime.UtcNow;
            var supplier = new Supplier
            {
                Name = dto.Name,
                TaxCode = dto.TaxCode,
                Address = Clean(dto.Address),
                Note = Clean(dto.Note),
                Active = true,
                CreatedAt = now,
                UpdatedAt = now,
                Phones = new List<SupplierPhone>
                {
                    new() { PhoneNumber = dto.PrimaryPhone.Trim(), IsPrimary = true }
                },
                Contacts = new List<SupplierContact>
                {
                    new()
                    {
                        Name = dto.PrimaryContactName.Trim(),
                        PhoneNumber = Clean(dto.PrimaryContactPhone),
                        Email = Clean(dto.PrimaryContactEmail),
                        Position = Clean(dto.PrimaryContactPosition),
                        IsPrimary = true
                    }
                }
            };

            foreach (var phone in dto.AdditionalPhones.Where(x => !string.IsNullOrWhiteSpace(x)))
                supplier.Phones.Add(new SupplierPhone { PhoneNumber = phone.Trim(), IsPrimary = false });

            foreach (var contact in dto.AdditionalContacts.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                supplier.Contacts.Add(new SupplierContact
                {
                    Name = contact.Name.Trim(),
                    PhoneNumber = Clean(contact.Phone),
                    Email = Clean(contact.Email),
                    Position = Clean(contact.Position),
                    IsPrimary = false
                });
            }

            return supplier;
        }

        private static string BuildPayloadHash(AdminSupplierCreateDTO dto) => Hash(JsonSerializer.Serialize(new
        {
            name = NormalizeIdentityText(dto.Name),
            taxCode = dto.TaxCode,
            address = NormalizeIdentityText(dto.Address),
            note = Clean(dto.Note),
            primaryPhone = NormalizePhone(dto.PrimaryPhone),
            primaryContactName = NormalizeIdentityText(dto.PrimaryContactName),
            primaryContactPhone = NormalizePhone(dto.PrimaryContactPhone),
            primaryContactEmail = NormalizeEmail(dto.PrimaryContactEmail),
            primaryContactPosition = NormalizeIdentityText(dto.PrimaryContactPosition),
            additionalPhones = dto.AdditionalPhones.Select(NormalizePhone).OrderBy(x => x).ToArray(),
            additionalContacts = dto.AdditionalContacts.Select(x => new
            {
                name = NormalizeIdentityText(x.Name),
                phone = NormalizePhone(x.Phone),
                email = NormalizeEmail(x.Email),
                position = NormalizeIdentityText(x.Position)
            }).OrderBy(x => x.name).ThenBy(x => x.phone).ThenBy(x => x.email).ToArray()
        }));

        private static string BuildWarningFingerprint(List<SoftDuplicateMatch> matches) => Hash(
            JsonSerializer.Serialize(matches
                .OrderBy(x => x.SupplierId)
                .Select(x => new { x.SupplierId, Signals = x.Signals.OrderBy(s => s).ToArray() })));

        private static AdminSupplierDuplicateWarningDTO ToWarningDto(
            SupplierDuplicateWarning warning,
            List<SoftDuplicateMatch> matches) => new()
        {
            WarningId = warning.PublicId,
            ExpiresAtUtc = warning.ExpiresAtUtc,
            Matches = matches.Select(ToMatchDto).ToList()
        };

        private static AdminSupplierDuplicateMatchDTO ToMatchDto(SoftDuplicateMatch match) => new()
        {
            SupplierId = match.SupplierId,
            Code = match.Code,
            Name = match.Name,
            Active = match.Active,
            MatchedSignals = match.Signals.ToList()
        };

        private static string NormalizeIdentityText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            var pendingSpace = false;
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                    continue;
                if (char.IsLetterOrDigit(character))
                {
                    if (pendingSpace && builder.Length > 0) builder.Append(' ');
                    builder.Append(char.ToUpperInvariant(character));
                    pendingSpace = false;
                }
                else
                {
                    pendingSpace = true;
                }
            }
            return builder.ToString();
        }

        private static string NormalizePhone(string? value) =>
            new((value ?? "").Where(char.IsDigit).ToArray());

        private static string NormalizeEmail(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();

        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        private static AuditLog NewSupplierAudit(
            int supplierId,
            string action,
            int actorStaffId,
            string? oldData,
            string? newData) => new()
        {
            TableName = "Suppliers",
            RecordId = supplierId,
            Action = action,
            OldData = oldData,
            NewData = newData,
            UserId = actorStaffId,
            CreatedAt = DateTime.UtcNow
        };

        private static string SupplierAuditTitle(string action) => action switch
        {
            "SUPPLIER_CREATED" => "Tạo nhà cung cấp",
            "SUPPLIER_TAX_CODE_UPDATED" => "Cập nhật mã số thuế",
            "SUPPLIER_DUPLICATE_OVERRIDE" => "Xác nhận tạo dù có cảnh báo trùng",
            "SUPPLIER_OFFER_CREATED" => "Thêm gói mua",
            "SUPPLIER_OFFER_UPDATED" => "Cập nhật gói mua",
            "SUPPLIER_OFFER_STATUS_CHANGED" => "Thay đổi trạng thái gói mua",
            "SUPPLIER_OFFER_PRICE_CHANGED" => "Cập nhật giá gói mua",
            "SUPPLIER_STORE_SCOPE_UPDATED" => "Cập nhật phạm vi cửa hàng",
            _ => "Cập nhật nhà cung cấp"
        };

        private static List<AdminSupplierAuditChangeDTO> BuildBusinessAuditChanges(
            string? oldData,
            string? newData)
        {
            var before = ReadBusinessAuditValues(oldData);
            var after = ReadBusinessAuditValues(newData);
            return before.Keys.Union(after.Keys)
                .Select(key => new AdminSupplierAuditChangeDTO
                {
                    Label = SupplierAuditFieldLabel(key),
                    Before = before.GetValueOrDefault(key),
                    After = after.GetValueOrDefault(key)
                })
                .Where(x => x.Before != x.After)
                .ToList();
        }

        private static Dictionary<string, string?> ReadBusinessAuditValues(string? json)
        {
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return result;
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object) return result;
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (SupplierAuditFieldLabel(property.Name) == string.Empty) continue;
                    result[property.Name] = FormatAuditValue(property.Value);
                }
            }
            catch (JsonException)
            {
                // Legacy malformed payloads stay available in developer logs, never in the business UI.
            }
            return result;
        }

        private static string SupplierAuditFieldLabel(string field) => field.ToLowerInvariant() switch
        {
            "code" => "Mã nhà cung cấp",
            "name" => "Tên nhà cung cấp",
            "taxcode" => "Mã số thuế",
            "packagequantity" => "Lượng trong gói",
            "packageprice" => "Giá một gói",
            "minimumorderpackagecount" => "MOQ theo gói",
            "leadtimedays" => "Thời gian giao mặc định",
            "isprimary" => "Nguồn cung chính",
            "active" => "Trạng thái",
            "allowsloosepurchase" => "Cho phép mua lẻ",
            "looseunitprice" => "Đơn giá mua lẻ",
            "loosepricemode" => "Cách xác định giá lẻ",
            "looseminimumorderquantity" => "MOQ mua lẻ",
            "loosequantitystep" => "Bước số lượng mua lẻ",
            "deliveryschedule" => "Lịch giao hàng",
            "note" => "Ghi chú",
            "reason" => "Lý do",
            _ => string.Empty
        };

        private static string? FormatAuditValue(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => "Có",
            JsonValueKind.False => "Không",
            JsonValueKind.String when value.GetString() == LoosePurchasePriceModes.Derived => "Tự tính từ giá gói",
            JsonValueKind.String when value.GetString() == LoosePurchasePriceModes.Independent => "Nhập giá riêng",
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };

        private async Task<IDbContextTransaction?> BeginTransactionAsync(IsolationLevel isolationLevel) =>
            _context.Database.IsRelational() && _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(isolationLevel)
                : null;

        private static async Task TryRollbackAsync(IDbContextTransaction? transaction)
        {
            if (transaction == null) return;
            try { await transaction.RollbackAsync(); }
            catch { /* SQL Server already rolls back a deadlock victim. */ }
        }

        private static bool IsUniqueCodeCollision(Exception ex)
        {
            var message = FlattenExceptionMessages(ex);
            return message.Contains("UX_Suppliers_Code", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("Suppliers.Code", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTaxCodeCollision(Exception ex)
        {
            var message = FlattenExceptionMessages(ex);
            return message.Contains("UX_Suppliers_TaxCode", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("Suppliers.TaxCode", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSqlDeadlock(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
                if (current is SqlException { Number: 1205 }) return true;
            return false;
        }

        private static string FlattenExceptionMessages(Exception exception)
        {
            var messages = new StringBuilder();
            for (var current = exception; current != null; current = current.InnerException)
                messages.Append(' ').Append(current.Message);
            return messages.ToString();
        }

        private sealed record SoftDuplicateMatch(
            int SupplierId,
            string Code,
            string Name,
            bool Active,
            List<string> Signals);

        // ===== INGREDIENT SUPPLIER OFFERS (#111) =====

        public async Task<List<AdminIngredientSupplierDTO>> GetIngredientOffersAsync(int supplierId)
        {
            var offers = await _context.IngredientSuppliers
                .AsNoTracking()
                .Include(x => x.Ingredient).ThenInclude(i => i.BaseUnit)
                .Include(x => x.Unit)
                .Include(x => x.LooseProcurementUnit)
                .Include(x => x.Supplier)
                .Where(x => x.SupplierId == supplierId)
                .OrderBy(x => x.Ingredient.Name)
                .ToListAsync();

            var result = new List<AdminIngredientSupplierDTO>();
            var readinessById = await _packageValidator.EvaluateReadinessAsync(offers);
            foreach (var offer in offers)
                result.Add(MapIngredientOffer(
                    offer,
                    readinessById[offer.IngredientSupplierId]));
            return result;
        }

        public async Task<AdminIngredientSupplierDTO?> GetIngredientOfferByIdAsync(int ingredientSupplierId)
        {
            var offer = await LoadOfferTrackedAsync(ingredientSupplierId, asNoTracking: true);
            if (offer == null) return null;
            var readiness = await _packageValidator.EvaluateReadinessAsync(offer);
            return MapIngredientOffer(offer, readiness);
        }

        public Task<int> CreateIngredientOfferAsync(AdminIngredientSupplierSaveDTO dto) =>
            CreateIngredientOfferAsync(dto, 0);

        public async Task<int> CreateIngredientOfferAsync(
            AdminIngredientSupplierSaveDTO dto,
            int actorStaffId)
        {
            ValidateOfferOperationalTerms(dto);

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

            var looseUnitPrice = await ResolveLooseUnitPriceAsync(dto);

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
                    MinimumOrderPackageCount = dto.MinimumOrderPackageCount,
                    LeadTimeDays = dto.LeadTimeDays,
                    IsPrimary = dto.IsPrimary,
                    Active = dto.Active,
                    AllowsLoosePurchase = dto.AllowsLoosePurchase,
                    CurrentProcurementUnitPrice = dto.AllowsLoosePurchase
                        ? looseUnitPrice
                        : null,
                    LooseProcurementUnitId = dto.AllowsLoosePurchase
                        ? dto.LooseProcurementUnitId
                        : null,
                    LoosePriceMode = dto.AllowsLoosePurchase
                        ? dto.LoosePriceMode
                        : LoosePurchasePriceModes.Independent,
                    LooseMinimumOrderQuantity = dto.AllowsLoosePurchase
                        ? dto.LooseMinimumOrderQuantity
                        : null,
                    LooseQuantityStep = dto.AllowsLoosePurchase
                        ? dto.LooseQuantityStep
                        : null,
                    Note = dto.Note?.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
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
                    Note = "Khởi tạo gói mua",
                    CreatedAtUtc = DateTime.UtcNow
                });
                _context.AuditLogs.Add(NewSupplierAudit(
                    entity.SupplierId,
                    "SUPPLIER_OFFER_CREATED",
                    actorStaffId,
                    null,
                    SerializeOfferAudit(entity)));
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

        public Task UpdateIngredientOfferAsync(AdminIngredientSupplierSaveDTO dto) =>
            UpdateIngredientOfferAsync(dto, 0);

        public async Task UpdateIngredientOfferAsync(
            AdminIngredientSupplierSaveDTO dto,
            int actorStaffId)
        {
            if (!dto.IngredientSupplierId.HasValue || dto.IngredientSupplierId.Value <= 0)
                throw new InvalidOperationException("Thiếu mã bảng giá gói mua.");

            var entity = await LoadOfferTrackedAsync(dto.IngredientSupplierId.Value, asNoTracking: false)
                ?? throw new InvalidOperationException("Không tìm thấy bảng giá gói mua.");

            var expectedVersion = ParseRequiredRowVersion(dto.RowVersion);
            EnsureRowVersionMatches(entity.RowVersion, expectedVersion);

            ValidateOfferOperationalTerms(dto);

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
                || dto.Active;

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

            var looseUnitPrice = await ResolveLooseUnitPriceAsync(dto);

            if (entity.IngredientId != dto.IngredientId || entity.SupplierId != dto.SupplierId)
                throw new InvalidOperationException("Không được đổi nhà cung cấp hoặc nguyên liệu của gói đã tạo.");

            _context.Entry(entity).Property(x => x.RowVersion).OriginalValue = expectedVersion;
            var oldAudit = SerializeOfferAudit(entity);

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                if (dto.IsPrimary)
                    await ClearPrimaryAsync(dto.IngredientId, excludeId: entity.IngredientSupplierId);

                if (packageOrPriceChanged)
                {
                    var currentHistories = await _context.IngredientSupplierPriceHistories
                        .Where(x => x.IngredientSupplierId == entity.IngredientSupplierId && x.IsCurrent)
                        .ToListAsync();
                    foreach (var history in currentHistories)
                        history.IsCurrent = false;

                    await _context.SaveChangesAsync();

                    var now = DateTime.UtcNow;
                    _context.IngredientSupplierPriceHistories.Add(new IngredientSupplierPriceHistory
                    {
                        IngredientSupplierId = entity.IngredientSupplierId,
                        Price = dto.CurrentPrice,
                        PackageQuantity = dto.PackageQuantity,
                        PackageUnitId = dto.UnitId,
                        EffectiveDate = now,
                        IsCurrent = true,
                        Note = "Cập nhật gói mua / giá",
                        CreatedAtUtc = now
                    });
                }

                entity.IngredientId = dto.IngredientId;
                entity.SupplierId = dto.SupplierId;
                entity.UnitId = dto.UnitId;
                entity.PackageQuantity = dto.PackageQuantity;
                entity.CurrentPrice = dto.CurrentPrice;
                entity.MinimumOrderPackageCount = dto.MinimumOrderPackageCount;
                entity.LeadTimeDays = dto.LeadTimeDays;
                entity.IsPrimary = dto.IsPrimary;
                entity.Active = dto.Active;
                entity.AllowsLoosePurchase = dto.AllowsLoosePurchase;
                entity.CurrentProcurementUnitPrice = dto.AllowsLoosePurchase
                    ? looseUnitPrice
                    : null;
                entity.LooseProcurementUnitId = dto.AllowsLoosePurchase
                    ? dto.LooseProcurementUnitId
                    : null;
                entity.LoosePriceMode = dto.AllowsLoosePurchase
                    ? dto.LoosePriceMode
                    : LoosePurchasePriceModes.Independent;
                entity.LooseMinimumOrderQuantity = dto.AllowsLoosePurchase
                    ? dto.LooseMinimumOrderQuantity
                    : null;
                entity.LooseQuantityStep = dto.AllowsLoosePurchase
                    ? dto.LooseQuantityStep
                    : null;
                entity.Note = dto.Note?.Trim();
                entity.UpdatedAt = DateTime.UtcNow;

                _context.AuditLogs.Add(NewSupplierAudit(
                    entity.SupplierId,
                    "SUPPLIER_OFFER_UPDATED",
                    actorStaffId,
                    oldAudit,
                    SerializeOfferAudit(entity)));
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                throw new InvalidOperationException(
                    "Gói mua vừa được người khác cập nhật. Vui lòng tải lại dữ liệu.");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public Task ToggleIngredientOfferActiveAsync(
            int ingredientSupplierId,
            bool active,
            string? rowVersion) =>
            ToggleIngredientOfferActiveAsync(ingredientSupplierId, active, rowVersion, 0);

        public async Task ToggleIngredientOfferActiveAsync(
            int ingredientSupplierId,
            bool active,
            string? rowVersion,
            int actorStaffId)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);
            try
            {
                var entity = await LoadOfferTrackedAsync(ingredientSupplierId, asNoTracking: false)
                    ?? throw new InvalidOperationException("Không tìm thấy gói mua.");
                var expectedVersion = ParseRequiredRowVersion(rowVersion);
                EnsureRowVersionMatches(entity.RowVersion, expectedVersion);
                _context.Entry(entity).Property(x => x.RowVersion).OriginalValue = expectedVersion;

                if (active)
                {
                    var readiness = await _packageValidator.EvaluateReadinessAsync(entity);
                    if (!readiness.IsReady)
                    {
                        throw new InvalidOperationException(
                            $"Không thể kích hoạt gói mua. {readiness.Message} Hãy cập nhật quy cách trước khi kích hoạt.");
                    }
                }

                if (entity.Active == active)
                {
                    await tx.CommitAsync();
                    return;
                }

                var oldActive = entity.Active;
                entity.Active = active;
                entity.UpdatedAt = DateTime.UtcNow;
                _context.AuditLogs.Add(NewSupplierAudit(
                    entity.SupplierId,
                    "SUPPLIER_OFFER_STATUS_CHANGED",
                    actorStaffId,
                    JsonSerializer.Serialize(new { active = oldActive }),
                    JsonSerializer.Serialize(new { active })));
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                throw new InvalidOperationException(
                    "Gói mua vừa được người khác cập nhật. Vui lòng tải lại dữ liệu.");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task ChangeIngredientOfferPriceAsync(
            AdminIngredientSupplierPriceChangeDTO dto,
            int actorStaffId)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
                throw new InvalidOperationException("Lý do đổi giá là bắt buộc.");

            await using var tx = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);
            try
            {
                var entity = await _context.IngredientSuppliers
                    .SingleOrDefaultAsync(x => x.IngredientSupplierId == dto.IngredientSupplierId)
                    ?? throw new InvalidOperationException("Không tìm thấy gói cung cấp.");

                var expectedVersion = ParseRequiredRowVersion(dto.RowVersion);
                EnsureRowVersionMatches(entity.RowVersion, expectedVersion);
                _context.Entry(entity).Property(x => x.RowVersion).OriginalValue = expectedVersion;

                var validation = await _packageValidator.ValidateAsync(
                    entity.IngredientId,
                    entity.SupplierId,
                    dto.PackageUnitId,
                    dto.PackageQuantity,
                    dto.PackagePrice,
                    entity.Active,
                    requirePackageQuantity: true,
                    excludeIngredientSupplierId: entity.IngredientSupplierId);
                if (!validation.IsSuccess)
                    throw new InvalidOperationException(validation.Message);

                var currentRows = await _context.IngredientSupplierPriceHistories
                    .Where(x => x.IngredientSupplierId == entity.IngredientSupplierId && x.IsCurrent)
                    .ToListAsync();
                foreach (var current in currentRows)
                    current.IsCurrent = false;
                await _context.SaveChangesAsync();

                var now = DateTime.UtcNow;
                _context.IngredientSupplierPriceHistories.Add(new IngredientSupplierPriceHistory
                {
                    IngredientSupplierId = entity.IngredientSupplierId,
                    Price = dto.PackagePrice,
                    PackageQuantity = dto.PackageQuantity,
                    PackageUnitId = dto.PackageUnitId,
                    EffectiveDate = now,
                    IsCurrent = true,
                    Note = dto.Reason.Trim(),
                    CreatedByStaffId = actorStaffId > 0 ? actorStaffId : null,
                    CreatedAtUtc = now
                });

                entity.CurrentPrice = dto.PackagePrice;
                entity.PackageQuantity = dto.PackageQuantity;
                entity.UnitId = dto.PackageUnitId;
                if (entity.AllowsLoosePurchase
                    && entity.LoosePriceMode == LoosePurchasePriceModes.Derived)
                {
                    entity.CurrentProcurementUnitPrice = await ResolveLooseUnitPriceAsync(
                        new AdminIngredientSupplierSaveDTO
                        {
                            IngredientId = entity.IngredientId,
                            SupplierId = entity.SupplierId,
                            UnitId = dto.PackageUnitId,
                            PackageQuantity = dto.PackageQuantity,
                            CurrentPrice = dto.PackagePrice,
                            AllowsLoosePurchase = true,
                            LooseProcurementUnitId = entity.LooseProcurementUnitId,
                            LoosePriceMode = LoosePurchasePriceModes.Derived,
                            LooseMinimumOrderQuantity = entity.LooseMinimumOrderQuantity,
                            LooseQuantityStep = entity.LooseQuantityStep
                        });
                }
                entity.UpdatedAt = now;
                _context.AuditLogs.Add(NewSupplierAudit(
                    entity.SupplierId,
                    "SUPPLIER_OFFER_PRICE_CHANGED",
                    actorStaffId,
                    null,
                    JsonSerializer.Serialize(new
                    {
                        entity.IngredientSupplierId,
                        packagePrice = dto.PackagePrice,
                        packageQuantity = dto.PackageQuantity,
                        looseUnitPrice = entity.CurrentProcurementUnitPrice,
                        reason = dto.Reason.Trim()
                    })));
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                throw new InvalidOperationException(
                    "Gói mua vừa được người khác cập nhật. Vui lòng tải lại trước khi đổi giá.");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<List<AdminIngredientSupplierPriceHistoryDTO>> GetIngredientOfferPriceHistoryAsync(
            int ingredientSupplierId)
        {
            return await _context.IngredientSupplierPriceHistories
                .AsNoTracking()
                .Include(x => x.PackageUnit)
                .Where(x => x.IngredientSupplierId == ingredientSupplierId)
                .OrderByDescending(x => x.EffectiveDate)
                .ThenByDescending(x => x.IngredientSupplierPriceHistoryId)
                .Select(x => new AdminIngredientSupplierPriceHistoryDTO
                {
                    IngredientSupplierPriceHistoryId = x.IngredientSupplierPriceHistoryId,
                    Price = x.Price,
                    PackageQuantity = x.PackageQuantity,
                    PackageUnitId = x.PackageUnitId,
                    PackageUnitName = x.PackageUnit != null ? x.PackageUnit.Name : "",
                    EffectiveDateUtc = x.EffectiveDate,
                    IsCurrent = x.IsCurrent,
                    Note = x.Note,
                    CreatedByStaffId = x.CreatedByStaffId,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToListAsync();
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

        public async Task<List<object>> GetCompatibleUnitDropdownAsync(int ingredientId)
        {
            var result = await _unitConversion.GetActiveUnitOptionsAsync(ingredientId);
            if (!result.IsSuccess || result.Data == null)
                throw new InvalidOperationException(result.Message);

            return ProcurementUnitPolicy.Filter(result.Data)
                .Select(x => new
                {
                    unitId = x.UnitId,
                    unitCode = x.UnitCode,
                    name = x.UnitName,
                    conversionFactorToBase = x.ConversionFactorToBase,
                    isBaseUnit = x.IsBaseUnit
                })
                .Cast<object>()
                .ToList();
        }

        // ===== STORE SCOPE =====

        public async Task<List<AdminSupplierStoreDTO>> GetSupplierStoresAsync(
            int supplierId,
            IReadOnlyCollection<int>? storeScope = null)
        {
            var supplierExists = await _context.Suppliers
                .AsNoTracking()
                .AnyAsync(x => x.SupplierId == supplierId);
            if (!supplierExists)
                throw new InvalidOperationException("Không tìm thấy nhà cung cấp.");

            var query = _context.SupplierStores
                .AsNoTracking()
                .Include(x => x.Store)
                .Where(x => x.SupplierId == supplierId);

            if (storeScope != null)
            {
                if (storeScope.Count == 0)
                    return new List<AdminSupplierStoreDTO>();
                query = query.Where(x => storeScope.Contains(x.StoreId));
            }

            return await query
                .OrderBy(x => x.Store.Name)
                .Select(x => new AdminSupplierStoreDTO
                {
                    SupplierStoreId = x.SupplierStoreId,
                    SupplierId = x.SupplierId,
                    StoreId = x.StoreId,
                    StoreName = x.Store.Name,
                    Active = x.Active,
                    LeadTimeOverrideDays = x.LeadTimeOverrideDays,
                    DeliverySchedule = x.DeliverySchedule,
                    Note = x.Note,
                    RowVersion = Convert.ToBase64String(x.RowVersion)
                })
                .ToListAsync();
        }

        public async Task<List<object>> GetStoreDropdownAsync(
            IReadOnlyCollection<int>? storeScope = null)
        {
            var query = _context.Stores.AsNoTracking().Where(x => x.Active);
            if (storeScope != null)
            {
                if (storeScope.Count == 0)
                    return new List<object>();
                query = query.Where(x => storeScope.Contains(x.StoreId));
            }

            var stores = await query
                .OrderBy(x => x.Name)
                .Select(x => new { storeId = x.StoreId, name = x.Name })
                .ToListAsync();
            return stores.Cast<object>().ToList();
        }

        public Task SaveSupplierStoreAsync(AdminSupplierStoreSaveDTO dto) =>
            SaveSupplierStoreAsync(dto, 0);

        public async Task SaveSupplierStoreAsync(
            AdminSupplierStoreSaveDTO dto,
            int actorStaffId)
        {
            var supplierActive = await _context.Suppliers
                .AnyAsync(x => x.SupplierId == dto.SupplierId && x.Active);
            if (!supplierActive)
                throw new InvalidOperationException("Nhà cung cấp không tồn tại hoặc đang ngừng hoạt động.");

            var storeActive = await _context.Stores
                .AnyAsync(x => x.StoreId == dto.StoreId && x.Active);
            if (!storeActive)
                throw new InvalidOperationException("Cửa hàng không tồn tại hoặc đang ngừng hoạt động.");

            var entity = await _context.SupplierStores
                .FirstOrDefaultAsync(x => x.SupplierId == dto.SupplierId && x.StoreId == dto.StoreId);
            var now = DateTime.UtcNow;
            string? oldAudit = null;
            if (entity == null)
            {
                entity = new SupplierStore
                {
                    SupplierId = dto.SupplierId,
                    StoreId = dto.StoreId,
                    CreatedAt = now
                };
                _context.SupplierStores.Add(entity);
            }
            else if (!string.IsNullOrWhiteSpace(dto.RowVersion))
            {
                oldAudit = SerializeStoreAudit(entity);
                _context.Entry(entity).Property(x => x.RowVersion).OriginalValue =
                    Convert.FromBase64String(dto.RowVersion);
            }

            entity.Active = dto.Active;
            entity.LeadTimeOverrideDays = dto.LeadTimeOverrideDays;
            entity.DeliverySchedule = Clean(dto.DeliverySchedule);
            entity.Note = Clean(dto.Note);
            entity.UpdatedAt = now;

            _context.AuditLogs.Add(NewSupplierAudit(
                dto.SupplierId,
                "SUPPLIER_STORE_SCOPE_UPDATED",
                actorStaffId,
                oldAudit,
                SerializeStoreAudit(entity)));

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException(
                    "Phạm vi cửa hàng vừa được người khác cập nhật. Vui lòng tải lại dữ liệu.");
            }
            catch (DbUpdateException ex) when (IsSupplierStoreUniqueCollision(ex))
            {
                throw new InvalidOperationException("Nhà cung cấp đã được gán cho cửa hàng này.");
            }
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

        private static void ValidateOfferOperationalTerms(AdminIngredientSupplierSaveDTO dto)
        {
            if (dto.MinimumOrderPackageCount.HasValue && dto.MinimumOrderPackageCount.Value <= 0)
                throw new InvalidOperationException("MOQ phải là số gói lớn hơn 0.");

            if (dto.LeadTimeDays.HasValue && dto.LeadTimeDays.Value < 0)
                throw new InvalidOperationException("Thời gian giao hàng không được âm.");

            if (dto.LooseMinimumOrderQuantity.HasValue
                && dto.LooseMinimumOrderQuantity.Value < 0m)
                throw new InvalidOperationException("MOQ mua lẻ không được âm.");

            if (dto.LooseQuantityStep.HasValue
                && dto.LooseQuantityStep.Value <= 0m)
                throw new InvalidOperationException("Bước số lượng mua lẻ phải lớn hơn 0.");
        }

        private async Task<decimal?> ResolveLooseUnitPriceAsync(AdminIngredientSupplierSaveDTO dto)
        {
            if (!dto.AllowsLoosePurchase)
                return null;

            dto.LoosePriceMode = string.IsNullOrWhiteSpace(dto.LoosePriceMode)
                ? LoosePurchasePriceModes.Independent
                : dto.LoosePriceMode.Trim().ToUpperInvariant();

            if (!LoosePurchasePriceModes.IsValid(dto.LoosePriceMode))
                throw new InvalidOperationException("Cách xác định giá mua lẻ không hợp lệ.");

            if (!dto.LooseProcurementUnitId.HasValue)
                throw new InvalidOperationException("Mua lẻ phải chọn đơn vị phù hợp với nguyên liệu.");

            var options = await _unitConversion.GetActiveUnitOptionsAsync(dto.IngredientId);
            if (!options.IsSuccess || options.Data == null)
                throw new InvalidOperationException(options.Message);

            if (!ProcurementUnitPolicy.Filter(options.Data)
                .Any(x => x.UnitId == dto.LooseProcurementUnitId.Value))
                throw new InvalidOperationException(
                    "Đơn vị mua lẻ không phù hợp với nguyên liệu hoặc chưa có quy đổi hợp lệ.");

            if (dto.LoosePriceMode == LoosePurchasePriceModes.Independent)
            {
                if (!dto.CurrentProcurementUnitPrice.HasValue
                    || dto.CurrentProcurementUnitPrice.Value <= 0m)
                    throw new InvalidOperationException("Đơn giá mua lẻ phải lớn hơn 0.");
                return dto.CurrentProcurementUnitPrice.Value;
            }

            if (!dto.PackageQuantity.HasValue || dto.PackageQuantity.Value <= 0m || dto.CurrentPrice <= 0m)
                throw new InvalidOperationException(
                    "Cần đủ lượng trong gói và giá gói để tự tính giá mua lẻ.");

            var converted = await _unitConversion.ConvertAsync(
                dto.IngredientId,
                dto.PackageQuantity.Value,
                dto.UnitId,
                dto.LooseProcurementUnitId.Value);
            if (!converted.IsSuccess || converted.Data <= 0m)
                throw new InvalidOperationException(
                    converted.Message ?? "Không thể quy đổi lượng trong gói sang đơn vị mua lẻ.");

            return decimal.Round(
                dto.CurrentPrice / converted.Data,
                2,
                MidpointRounding.AwayFromZero);
        }

        private static byte[] ParseRequiredRowVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(
                    "Thiếu phiên bản dữ liệu gói mua. Vui lòng tải lại.");

            try
            {
                var parsed = Convert.FromBase64String(value);
                if (parsed.Length == 0)
                    throw new FormatException();
                return parsed;
            }
            catch (FormatException)
            {
                throw new InvalidOperationException(
                    "Phiên bản dữ liệu gói mua không hợp lệ. Vui lòng tải lại.");
            }
        }

        private static void EnsureRowVersionMatches(byte[] current, byte[] expected)
        {
            if (!(current ?? Array.Empty<byte>()).SequenceEqual(expected))
                throw new InvalidOperationException(
                    "Gói mua vừa được người khác cập nhật. Vui lòng tải lại dữ liệu.");
        }

        private static string SerializeOfferAudit(IngredientSupplier entity) =>
            JsonSerializer.Serialize(new
            {
                entity.IngredientSupplierId,
                entity.IngredientId,
                entity.UnitId,
                entity.PackageQuantity,
                packagePrice = entity.CurrentPrice,
                entity.MinimumOrderPackageCount,
                entity.LeadTimeDays,
                entity.IsPrimary,
                entity.Active,
                entity.AllowsLoosePurchase,
                entity.LooseProcurementUnitId,
                looseUnitPrice = entity.CurrentProcurementUnitPrice,
                entity.LoosePriceMode,
                entity.LooseMinimumOrderQuantity,
                entity.LooseQuantityStep
            });

        private static string SerializeStoreAudit(SupplierStore entity) =>
            JsonSerializer.Serialize(new
            {
                entity.SupplierStoreId,
                entity.StoreId,
                entity.Active,
                entity.LeadTimeOverrideDays,
                entity.DeliverySchedule,
                entity.Note
            });

        private static bool IsSupplierStoreUniqueCollision(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("SupplierStores", StringComparison.OrdinalIgnoreCase)
                   && (message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                       || message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                       || message.Contains("2601", StringComparison.OrdinalIgnoreCase)
                       || message.Contains("2627", StringComparison.OrdinalIgnoreCase));
        }

        private async Task<IngredientSupplier?> LoadOfferTrackedAsync(int id, bool asNoTracking)
        {
            IQueryable<IngredientSupplier> q = _context.IngredientSuppliers
                .Include(x => x.Ingredient).ThenInclude(i => i.BaseUnit)
                .Include(x => x.Unit)
                .Include(x => x.LooseProcurementUnit)
                .Include(x => x.Supplier);

            if (asNoTracking)
                q = q.AsNoTracking();

            return await q.FirstOrDefaultAsync(x => x.IngredientSupplierId == id);
        }

        private static AdminIngredientSupplierDTO MapIngredientOffer(
            IngredientSupplier x,
            SupplierPackageReadinessResult readiness)
        {
            var unitCode = x.Unit?.UnitCode ?? "";
            var packageDisplay = readiness.HasValidPackageDefinition
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
                MinimumOrderPackageCount = x.MinimumOrderPackageCount,
                LeadTimeDays = x.LeadTimeDays,
                IsPrimary = x.IsPrimary,
                Active = x.Active,
                AllowsLoosePurchase = x.AllowsLoosePurchase,
                CurrentProcurementUnitPrice = x.CurrentProcurementUnitPrice,
                LooseProcurementUnitId = x.LooseProcurementUnitId,
                LooseProcurementUnitName = x.LooseProcurementUnit?.Name,
                LoosePriceMode = x.LoosePriceMode,
                LooseMinimumOrderQuantity = x.LooseMinimumOrderQuantity,
                LooseQuantityStep = x.LooseQuantityStep,
                Note = x.Note,
                UpdatedAt = x.UpdatedAt,
                RowVersion = Convert.ToBase64String(x.RowVersion),
                HasCompletePackageDefinition = readiness.HasValidPackageDefinition,
                IsProcurementReady = readiness.IsReady && x.Active,
                ProcurementReadinessLabel = readiness.IsReady && x.Active
                    ? "Sẵn sàng mua hàng"
                    : "Chưa sẵn sàng mua hàng",
                ProcurementReadinessMessage = !x.Active && readiness.IsReady
                    ? "Quy cách hợp lệ nhưng gói mua đang ngừng sử dụng."
                    : readiness.Message,
                PackageDisplay = packageDisplay,
                PriceDisplay = x.AllowsLoosePurchase
                    ? $"{x.CurrentPrice.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} ₫ / gói · "
                      + $"{x.CurrentProcurementUnitPrice.GetValueOrDefault().ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} ₫ / {x.LooseProcurementUnit?.Name}"
                    : $"{x.CurrentPrice.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} ₫ / gói mua"
            };
        }
    }
}
