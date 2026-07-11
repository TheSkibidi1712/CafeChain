using System.Security.Claims;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Results;
using CafeChain.Infrastrusture.Interfaces.Admin.Staffs;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.ViewModels.Admin.Staffs;
using CafeChain.ViewModels.Shared;
using Microsoft.AspNetCore.Hosting;
using CafeChain.Application.Constants;
using Microsoft.AspNetCore.Http;

namespace CafeChain.Application.Services.Admin.Staffs
{
    public class AdminStaffService : IAdminStaffService
    {
        private readonly IAdminStaffRepository _repository;
        private readonly IWebHostEnvironment _env;
        private readonly IScopeAuthorizationService _scopeAuthorizationService;

        // 🔥 Role ID Constants (CHÍNH XÁC theo Seed Data)
        private const int ROLE_BUSINESS_OWNER = 1;          // Chủ doanh nghiệp
        private const int ROLE_AREA_MANAGER = 2;            // Quản lý vùng
        private const int ROLE_STORE_MANAGER = 3;           // Quản lý chi nhánh
        private const int ROLE_SALES_STAFF = 4;             // Nhân viên bán hàng
        private const int ROLE_ACCOUNTANT_WAREHOUSE = 5;    // Kế toán/kho
        private const int ROLE_SYSTEM_ADMIN = 6;            // Quản trị hệ thống
        private const int ROLE_CUSTOMER = 7;                // Khách hàng
        private const int ROLE_SHIFT_SUPERVISOR = 8;        // Ca trưởng

        private const int SCOPE_COUNTRY = 1;
        private const int SCOPE_PROVINCE = 2;
        private const int SCOPE_DISTRICT = 3;
        private const int SCOPE_STORE = 5;

        // 🔥 Forbidden roles cho Store Manager (cấp HQ + Area + chính mình)
        private static readonly int[] FORBIDDEN_ROLES_FOR_STORE_MANAGER =
        {
            ROLE_BUSINESS_OWNER,
            ROLE_AREA_MANAGER,
            ROLE_STORE_MANAGER,
            ROLE_SYSTEM_ADMIN,
            ROLE_CUSTOMER
        };

        // AreaManager may only assign store-operational roles (Issue #94 — fix fragile RoleId range).
        private static readonly int[] AREA_MANAGER_ASSIGNABLE_ROLES =
        {
            ROLE_STORE_MANAGER,
            ROLE_SALES_STAFF,
            ROLE_SHIFT_SUPERVISOR,
            ROLE_ACCOUNTANT_WAREHOUSE
        };

        // StoreManager assignable roles (also used by dropdown filter).
        private static readonly int[] STORE_MANAGER_ASSIGNABLE_ROLES =
        {
            ROLE_SALES_STAFF,
            ROLE_SHIFT_SUPERVISOR,
            ROLE_ACCOUNTANT_WAREHOUSE
        };

        /// <summary>
        /// SOLID Helper: Ánh xạ cứng Role → ScopeType bắt buộc.
        /// Thay thế toàn bộ chuỗi if/else rải rác trong Create/Update.
        /// </summary>
        private static int GetRequiredScopeTypeForRole(int roleId) => roleId switch
        {
            >= 1 and <= 6 => SCOPE_COUNTRY,
            7 => SCOPE_PROVINCE,
            _ => SCOPE_STORE
        };

        public AdminStaffService(IAdminStaffRepository repository, IWebHostEnvironment env, IScopeAuthorizationService scopeAuthorizationService)
        {
            _repository = repository;
            _env = env;
            _scopeAuthorizationService = scopeAuthorizationService;
        }

        // ==================== MASTER DATA (Thin Controller) ====================
        public async Task<StaffFormMasterDataVM> GetMasterDataForFormAsync(ClaimsPrincipal user)
        {
            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);

            var currentStaffId = 0;
            var staffIdClaim = user.FindFirst("StaffId")?.Value;

            if (!string.IsNullOrWhiteSpace(staffIdClaim))
            {
                int.TryParse(staffIdClaim, out currentStaffId);
            }

            // Lấy toàn bộ role
            var roles = await _repository.GetRolesForDropdownAsync(null);

            // Form Quản lý Nhân sự không hiển thị Khách hàng
            roles = roles
                .Where(r => r.RoleId != ROLE_CUSTOMER)
                .ToList();

            if (isAdmin)
            {
                // Admin/SystemAdmin: toàn bộ role nội bộ (gồm Ca trưởng), trừ Khách hàng.
            }
            else if (isStoreManager)
            {
                // Quản lý chi nhánh: NV bán hàng, Ca trưởng, Kế toán/kho
                roles = roles
                    .Where(r => STORE_MANAGER_ASSIGNABLE_ROLES.Contains(r.RoleId))
                    .ToList();
            }
            else
            {
                // Quản lý vùng (và role trung gian tương tự): store-operational only
                roles = roles
                    .Where(r => AREA_MANAGER_ASSIGNABLE_ROLES.Contains(r.RoleId))
                    .ToList();
            }

            var result = new StaffFormMasterDataVM
            {
                Roles = roles,
                ScopeTypes = await _repository.GetScopeTypesAsync(),
                IsStoreManager = isStoreManager && !isAdmin
            };

            if (isStoreManager && !isAdmin)
            {
                var store = await _repository.GetStoreByIdAsync(currentStoreId);
                result.Stores = null;
                result.CurrentStoreId = currentStoreId;
                result.CurrentStoreName = store?.Name ?? "Cửa hàng";
            }
            else
            {
                if (isAdmin)
                {
                    result.Stores = await _repository.GetActiveStoresAsync();
                }
                else
                {
                    result.Stores = await _scopeAuthorizationService.GetAllowedStoresAsync(currentStaffId);
                }
                result.CurrentStoreId = 0;
                result.CurrentStoreName = null;
            }

            return result;
        }

        // ==================== AVATAR UPLOAD ====================
        public async Task<string> SaveAvatarAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return "/Images/avatars/avtdf.jpg";

            var uploadsDir = Path.Combine(_env.WebRootPath, "Images", "avatars");
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/Images/avatars/{fileName}";
        }

        // ==================== DROPDOWN DATA (AJAX) ====================
        public async Task<IEnumerable<object>> GetScopeReferencesAsync(int scopeTypeId, int? parentId = null)
        {
            try
            {
                if (scopeTypeId == SCOPE_COUNTRY) // HQ
                {
                    return new[] { new { id = 1, name = "Trụ sở chính" } };
                }
                if (scopeTypeId == SCOPE_STORE) // Store
                {
                    var stores = await _repository.GetActiveStoresAsync();
                    return stores.Select(s => new { id = s.StoreId, name = s.Name });
                }
                if (scopeTypeId == SCOPE_PROVINCE) // Province
                {
                    var provinces = await _repository.GetProvincesAsync();
                    return provinces.Select(p => new { id = p.ProvinceId, name = p.Name });
                }
                if (scopeTypeId == SCOPE_DISTRICT && parentId.HasValue) // District
                {
                    var districts = await _repository.GetDistrictsAsync(parentId.Value);
                    return districts.Select(d => new { id = d.DistrictId, name = d.Name });
                }
                return new object[] { };
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi fetch DB locations: " + ex.Message);
                return new object[] { };
            }
        }

        // ==================== INDEX ====================
        public async Task<StaffIndexPageVM> GetStaffIndexPageAsync(int page, int pageSize, int? storeId, string search, int? roleFilter, ClaimsPrincipal user)
        {
            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);

            var (items, totalCount) = await _repository.GetPaginatedStaffsAsync(page, pageSize, storeId, search, roleFilter);
            var (total, active, inactive) = await _repository.GetStaffCountsAsync(storeId);

            var viewModels = items.Select(s => {
                var roleIds = s.Account?.AccountRoles?.Select(ar => ar.RoleId).ToList() ?? new List<int>();

                // 🔥 FIX: role ≤ Store Manager = cấp cao → không cho Store Manager edit
                bool canEdit = true;
                if (isStoreManager && !isAdmin)
                {
                    if (roleIds.Any(r => r <= ROLE_STORE_MANAGER)) canEdit = false;
                    if (s.StoreId != currentStoreId) canEdit = false;
                }

                return new StaffIndexVM
                {
                    StaffId = s.StaffId,
                    FullName = s.FullName,
                    Email = s.Account?.Email ?? "",
                    AvatarUrl = s.AvatarUrl ?? "/Images/avatars/avtdf.jpg",
                    StoreName = s.Store?.Name ?? "",
                    RoleNames = s.Account?.AccountRoles?.Select(ar => ar.Role?.Name ?? "").ToList() ?? new List<string>(),
                    RoleIds = roleIds,
                    Active = s.Active,
                    DefaultPhone = s.StaffPhones?.FirstOrDefault(p => p.IsDefault)?.Phone ?? "",
                    CanEdit = canEdit
                };
            }).ToList();

            return new StaffIndexPageVM
            {
                StaffList = new PaginatedListViewModel<StaffIndexVM>(viewModels, totalCount, page, pageSize),
                TotalStaff = total,
                ActiveCount = active,
                InactiveCount = inactive,
                SearchTerm = search,
                RoleFilter = roleFilter
            };
        }

        // ==================== GET FOR EDIT ====================
        public async Task<StaffEditVM> GetStaffForEditAsync(int staffId)
        {
            var staff = await _repository.GetStaffByIdAsync(staffId);
            if (staff == null) return null;

            return new StaffEditVM
            {
                StaffId = staff.StaffId,
                AccountId = staff.AccountId,
                FullName = staff.FullName,
                Email = staff.Account?.Email ?? "",
                TaxCode = staff.TaxCode ?? "",
                CCCD = staff.CCCD ?? "",
                BaseSalary = staff.BaseSalary,
                DateOfBirth = staff.DateOfBirth,
                StoreId = staff.StoreId,
                SelectedRoleId = staff.Account?.AccountRoles?.FirstOrDefault()?.RoleId ?? ROLE_SALES_STAFF,
                ScopeTypeId = staff.StaffScopes?.FirstOrDefault()?.ScopeTypeId ?? SCOPE_STORE,
                ScopeRefId = staff.StaffScopes?.FirstOrDefault()?.ScopeRefId ?? staff.StoreId,
                Phones = staff.StaffPhones?.OrderByDescending(p => p.IsDefault).Select(p => p.Phone).ToList() ?? new List<string>(),
                Addresses = staff.StaffAddresses?.OrderByDescending(a => a.IsDefault).Select(a => a.Address).ToList() ?? new List<string>(),
                Banks = staff.StaffBanks?.Select((b, index) => new StaffBankVM
                {
                    BankName = b.BankName,
                    AccountNumber = b.AccountNumber,
                    AccountHolderName = b.AccountHolderName,
                    IsPrimary = b.IsPrimary
                }).ToList() ?? new List<StaffBankVM>(),
                PrimaryBankIndex = staff.StaffBanks?.ToList().FindIndex(b => b.IsPrimary) ?? 0,
                CurrentAvatarUrl = staff.AvatarUrl ?? "/Images/avatars/avtdf.jpg",
                Active = staff.Active
            };
        }

        // ==================== CREATE ====================
        public async Task<ServiceResult> CreateStaffAsync(StaffCreateVM model, ClaimsPrincipal user, IFormFile file)
        {
            model.TaxCode = string.IsNullOrWhiteSpace(model.TaxCode) ? null : model.TaxCode.Trim();
            model.CCCD = string.IsNullOrWhiteSpace(model.CCCD) ? null : model.CCCD.Trim();
            model.Email = model.Email?.Trim() ?? string.Empty;

            // === BƯỚC 1: Đọc Claims ===
            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);

            // === BƯỚC 2: 🔥 RULE 2 — Hard-Override cho Store Manager ===
            if (isStoreManager && !isAdmin)
            {
                model.StoreId = currentStoreId;
                model.ScopeTypeId = SCOPE_STORE; // STORE
                model.ScopeRefId = currentStoreId;
            }

            // === BƯỚC 3: 🔥 RULE 2 — Security Check: Chặn leo quyền ===
            if (isStoreManager && !isAdmin)
            {
                if (FORBIDDEN_ROLES_FOR_STORE_MANAGER.Contains(model.SelectedRoleId))
                {
                    return ServiceResult.Failure("Hành vi không hợp lệ! Bạn không có quyền cấp phát chức vụ này.");
                }
            }

            // === BƯỚC 4: 🔥 RULE 3 — Role-Scope Alignment (SOLID Helper) ===
            int requiredScope = GetRequiredScopeTypeForRole(model.SelectedRoleId);
            model.ScopeTypeId = requiredScope;

            if (requiredScope == 1) // HQ-level: tự gán
            {
                model.ScopeRefId = 1;
            }
            else if (requiredScope == SCOPE_STORE) // Store-level: bắt buộc chọn cửa hàng
            {
                if (!model.StoreId.HasValue || model.StoreId <= 0)
                    return ServiceResult.Failure("Vai trò này yêu cầu phải chọn một Cửa hàng vật lý cụ thể.");
                model.ScopeRefId = model.StoreId.Value;
            }
            // Province-level (Area Manager): ScopeRefId do form frontend gửi lên (dropdown Tỉnh/TP)

            // === GUARD CLAUSE AREA MANAGER ===
            var rolesStr = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
            bool isAreaManager = rolesStr.Contains(RoleConstants.AreaManager);
            if (isAreaManager && !isAdmin)
            {
                // Lớp 1: allow-list store-operational roles only (Issue #94)
                if (!AREA_MANAGER_ASSIGNABLE_ROLES.Contains(model.SelectedRoleId))
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền cấp phát tài khoản ngang hàng hoặc cấp cao hơn!");
                }

                // Lớp 2: Chống vượt rào ngang
                if (model.StoreId.HasValue)
                {
                    var staffIdClaim = user.FindFirst("StaffId")?.Value;
                    int.TryParse(staffIdClaim, out int currentStaffId);
                    var allowedStores = await _scopeAuthorizationService.GetAllowedStoresAsync(currentStaffId);
                    if (!allowedStores.Any(s => s.StoreId == model.StoreId.Value))
                    {
                        throw new UnauthorizedAccessException("Bạn không có quyền thao tác với nhân sự thuộc Cửa hàng này (ngoài phạm vi quản lý)!");
                    }
                }
            }

            // === BƯỚC 5: 🔥 RULE 1 (Advanced) — Identity Integrity ===
            if (await _repository.EmailExistsAsync(model.Email))
            {
                return ServiceResult.Failure("Email đã tồn tại trong hệ thống.");
            }

            // Filter phones/addresses: BỎ rỗng/null, limit 3
            var validPhones = FilterAndLimit(model.Phones, 3);
            var validAddresses = FilterAndLimit(model.Addresses, 3);

            if (validPhones.Any())
            {
                if (await _repository.DefaultPhoneExistsAsync(validPhones[0]))
                {
                    return ServiceResult.Failure($"Số điện thoại mặc định '{validPhones[0]}' đã tồn tại trong hệ thống.");
                }
            }

            if (!string.IsNullOrWhiteSpace(model.TaxCode))
            {
                if (await _repository.TaxCodeExistsAsync(model.TaxCode))
                {
                    return ServiceResult.Failure($"Mã số thuế '{model.TaxCode}' đã tồn tại trong hệ thống.");
                }
            }

            // 🔥 CCCD Uniqueness (Nullable Unique — chỉ check nếu có nhập)
            if (!string.IsNullOrWhiteSpace(model.CCCD))
            {
                if (await _repository.CCCDExistsAsync(model.CCCD))
                {
                    return ServiceResult.Failure($"Số CCCD '{model.CCCD}' đã tồn tại trong hệ thống.");
                }
            }

            // === BƯỚC 6: 🔥 RULE 4 (Advanced) — Default Password ===
            var password = model.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                string suffix = "0000";
                if (!string.IsNullOrWhiteSpace(model.CCCD) && model.CCCD.Length >= 4)
                    suffix = model.CCCD.Substring(model.CCCD.Length - 4);
                else if (validPhones.Any() && validPhones[0].Length >= 4)
                    suffix = validPhones[0].Substring(validPhones[0].Length - 4);

                password = $"CafeChain@{suffix}";
            }

            // === BƯỚC 7: Avatar Upload (xử lý trong Service, không phải Controller) ===
            var avatarUrl = await SaveAvatarAsync(file);

            // === BƯỚC 8: Build Entities ===
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var account = new Account
            {
                Email = model.Email,
                PasswordHash = passwordHash,
                Active = true,
                RequiresPasswordChange = true, // Bắt buộc đổi pass lần đầu theo yêu cầu Kiosk Security
                CreatedAt = DateTime.Now
            };

            var staff = new Models.Staffs.Staff
            {
                FullName = model.FullName,
                TaxCode = string.IsNullOrWhiteSpace(model.TaxCode) ? null : model.TaxCode.Trim(),
                CCCD = string.IsNullOrWhiteSpace(model.CCCD) ? null : model.CCCD.Trim(),
                BaseSalary = model.BaseSalary,
                DateOfBirth = model.DateOfBirth,
                StoreId = model.StoreId ?? 1,
                Active = true,
                AvatarUrl = avatarUrl,
                CreatedAt = DateTime.Now
            };

            var accountRoles = model.SelectedRoleId != ROLE_CUSTOMER
                ? new List<AccountRole> { new AccountRole { RoleId = model.SelectedRoleId } }
                : new List<AccountRole>();

            var staffScopes = new List<StaffScope>
            {
                new StaffScope
                {
                    ScopeTypeId = model.ScopeTypeId > 0 ? model.ScopeTypeId : SCOPE_STORE,
                    ScopeRefId = model.ScopeRefId > 0 ? model.ScopeRefId : (model.StoreId ?? 1)
                }
            };

            // Build StaffPhones với IsDefault logic
            var staffPhones = validPhones.Select((phone, index) => new StaffPhone
            {
                Phone = phone,
                IsDefault = index == 0  // 🔥 Phần tử đầu tiên = Default
            }).ToList();

            var staffAddresses = validAddresses.Select((address, index) => new StaffAddress
            {
                Address = address,
                IsDefault = index == 0
            }).ToList();

            // Build StaffBanks
            var staffBanks = new List<StaffBank>();
            if (model.Banks != null && model.Banks.Any())
            {
                staffBanks = model.Banks.Select((b, index) => new StaffBank
                {
                    BankName = b.BankName,
                    AccountNumber = b.AccountNumber,
                    AccountHolderName = b.AccountHolderName,
                    IsPrimary = index == model.PrimaryBankIndex
                }).ToList();
            }

            // Build StaffDependents
            var staffDependents = new List<StaffDependent>();
            if (model.Dependents != null && model.Dependents.Any())
            {
                staffDependents = model.Dependents.Select(d => new StaffDependent
                {
                    FullName = d.FullName,
                    DateOfBirth = d.DateOfBirth,
                    TaxCode = d.TaxCode,
                    Relationship = d.Relationship,
                    CreatedAt = DateTime.Now
                }).ToList();
            }

            // === BƯỚC 9: Gọi Repository (Transaction) ===
            try
            {
                await _repository.CreateStaffTransactionAsync(staff, account, accountRoles, staffScopes, staffPhones, staffAddresses, staffBanks, staffDependents);
            }
            catch (InvalidOperationException ex)
            {
                // 🔥 Bắt lỗi trùng lặp dữ liệu từ Repository (Duplicate Key)
                return ServiceResult.Failure(ex.Message);
            }

            return ServiceResult.Success("Thêm nhân viên thành công!");
        }

        // ==================== UPDATE ====================
        public async Task<ServiceResult> UpdateStaffAsync(StaffEditVM model, ClaimsPrincipal user, IFormFile file)
        {
            model.TaxCode = string.IsNullOrWhiteSpace(model.TaxCode) ? null : model.TaxCode.Trim();
            model.CCCD = string.IsNullOrWhiteSpace(model.CCCD) ? null : model.CCCD.Trim();
            model.Email = model.Email?.Trim() ?? string.Empty;

            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);

            var existingStaff = await _repository.GetStaffByIdAsync(model.StaffId);
            if (existingStaff == null)
                return ServiceResult.Failure("Không tìm thấy nhân viên.");

            // === 🔥 FIX: Bảo mật - Chặn Leo Quyền (Cross-check Target Staff) ===
            var targetRoleIds = existingStaff.Account?.AccountRoles?.Select(ar => ar.RoleId).ToList() ?? new List<int>();
            if (isStoreManager && !isAdmin)
            {
                // Nếu nhân viên đang bị chỉnh sửa có role ≤ Store Manager → cấp cao
                if (targetRoleIds.Any(r => r <= ROLE_STORE_MANAGER))
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa tài khoản cấp cao hơn hoặc ngang hàng!");
                }

                // Extra check: Nhân viên phải thuộc cửa hàng của quản lý
                if (existingStaff.StoreId != currentStoreId)
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa nhân viên của cửa hàng khác!");
                }
            }

            // === 🔥 RULE 2 — Hard-Override cho Store Manager ===
            if (isStoreManager && !isAdmin)
            {
                model.StoreId = currentStoreId;
                model.ScopeTypeId = SCOPE_STORE;
                model.ScopeRefId = currentStoreId;
            }

            // === 🔥 RULE 2 — Security Check ===
            if (isStoreManager && !isAdmin)
            {
                if (FORBIDDEN_ROLES_FOR_STORE_MANAGER.Contains(model.SelectedRoleId))
                {
                    return ServiceResult.Failure("Hành vi không hợp lệ! Bạn không có quyền cấp phát chức vụ này.");
                }
            }

            // === 🔥 RULE 3 — Role-Scope Alignment (SOLID Helper) ===
            int requiredScopeEdit = GetRequiredScopeTypeForRole(model.SelectedRoleId);
            model.ScopeTypeId = requiredScopeEdit;

            if (requiredScopeEdit == 1) // HQ-level: tự gán
            {
                model.ScopeRefId = 1;
            }
            else if (requiredScopeEdit == SCOPE_STORE) // Store-level: bắt buộc chọn cửa hàng
            {
                if (!model.StoreId.HasValue || model.StoreId <= 0)
                    return ServiceResult.Failure("Vai trò này yêu cầu phải chọn một Cửa hàng vật lý cụ thể.");
                model.ScopeRefId = model.StoreId.Value;
            }
            // Province-level (Area Manager): ScopeRefId do form frontend gửi lên (dropdown Tỉnh/TP)

            // === GUARD CLAUSE AREA MANAGER (Lớp 1 & Lớp 2) — Issue #94 allow-list ===
            var rolesStr = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
            bool isAreaManager = rolesStr.Contains(RoleConstants.AreaManager);
            if (isAreaManager && !isAdmin)
            {
                if (!AREA_MANAGER_ASSIGNABLE_ROLES.Contains(model.SelectedRoleId))
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền sửa đổi cấp phát Role ngang hàng hoặc cấp cao hơn!");
                }

                if (model.StoreId.HasValue)
                {
                    var staffIdClaim = user.FindFirst("StaffId")?.Value;
                    int.TryParse(staffIdClaim, out int currentStaffId);
                    var allowedStores = await _scopeAuthorizationService.GetAllowedStoresAsync(currentStaffId);
                    if (!allowedStores.Any(s => s.StoreId == model.StoreId.Value))
                    {
                        throw new UnauthorizedAccessException("Bạn không có quyền chuyển nhân sự sang Cửa hàng ngoài phạm vi quản lý!");
                    }
                }
            }

            // === 🔥 RULE 3 (Advanced) — Salary Lock cho Store Manager ===
            if (isStoreManager && !isAdmin)
            {
                model.BaseSalary = existingStaff.BaseSalary; // Ép cứng salary cũ
            }

            // === Identity Integrity ===
            if (await _repository.EmailExistsAsync(model.Email, existingStaff.AccountId))
            {
                return ServiceResult.Failure("Email đã tồn tại trong hệ thống.");
            }

            var validPhones = FilterAndLimit(model.Phones, 3);
            var validAddresses = FilterAndLimit(model.Addresses, 3);

            if (validPhones.Any())
            {
                if (await _repository.DefaultPhoneExistsAsync(validPhones[0], existingStaff.StaffId))
                {
                    return ServiceResult.Failure($"Số điện thoại mặc định '{validPhones[0]}' đã tồn tại.");
                }
            }

            if (!string.IsNullOrWhiteSpace(model.TaxCode))
            {
                if (await _repository.TaxCodeExistsAsync(model.TaxCode, existingStaff.StaffId))
                {
                    return ServiceResult.Failure($"Mã số thuế '{model.TaxCode}' đã tồn tại.");
                }
            }

            // 🔥 CCCD Uniqueness (Nullable Unique — chỉ check nếu có nhập)
            if (!string.IsNullOrWhiteSpace(model.CCCD))
            {
                if (await _repository.CCCDExistsAsync(model.CCCD, existingStaff.StaffId))
                {
                    return ServiceResult.Failure($"Số CCCD '{model.CCCD}' đã tồn tại.");
                }
            }

            // === Avatar Upload (xử lý trong Service) ===
            if (file != null && file.Length > 0)
            {
                existingStaff.AvatarUrl = await SaveAvatarAsync(file);
            }

            // === Update entities ===
            existingStaff.FullName = model.FullName;
            existingStaff.TaxCode = string.IsNullOrWhiteSpace(model.TaxCode) ? null : model.TaxCode.Trim();
            existingStaff.CCCD = string.IsNullOrWhiteSpace(model.CCCD) ? null : model.CCCD.Trim();
            existingStaff.BaseSalary = model.BaseSalary;
            existingStaff.DateOfBirth = model.DateOfBirth;
            existingStaff.StoreId = model.StoreId ?? existingStaff.StoreId;

            existingStaff.Account.Email = model.Email;

            // Password update (optional)
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                existingStaff.Account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            }

            var accountRoles = model.SelectedRoleId != ROLE_CUSTOMER
                ? new List<AccountRole> { new AccountRole { RoleId = model.SelectedRoleId } }
                : new List<AccountRole>();

            var staffScopes = new List<StaffScope>
            {
                new StaffScope
                {
                    ScopeTypeId = model.ScopeTypeId > 0 ? model.ScopeTypeId : SCOPE_STORE,
                    ScopeRefId = model.ScopeRefId > 0 ? model.ScopeRefId : (model.StoreId ?? existingStaff.StoreId)
                }
            };

            var staffPhones = validPhones.Select((phone, index) => new StaffPhone
            {
                Phone = phone,
                IsDefault = index == 0
            }).ToList();

            var staffAddresses = validAddresses.Select((address, index) => new StaffAddress
            {
                Address = address,
                IsDefault = index == 0
            }).ToList();

            var staffBanks = new List<StaffBank>();
            if (model.Banks != null && model.Banks.Any())
            {
                staffBanks = model.Banks.Select((b, index) => new StaffBank
                {
                    BankName = b.BankName,
                    AccountNumber = b.AccountNumber,
                    AccountHolderName = b.AccountHolderName,
                    IsPrimary = index == model.PrimaryBankIndex
                }).ToList();
            }

            // Build StaffDependents
            var staffDependents = new List<StaffDependent>();
            if (model.Dependents != null && model.Dependents.Any())
            {
                staffDependents = model.Dependents.Select(d => new StaffDependent
                {
                    FullName = d.FullName,
                    DateOfBirth = d.DateOfBirth,
                    TaxCode = d.TaxCode,
                    Relationship = d.Relationship,
                    CreatedAt = DateTime.Now
                }).ToList();
            }

            try
            {
                await _repository.UpdateStaffTransactionAsync(existingStaff, existingStaff.Account, accountRoles, staffScopes, staffPhones, staffAddresses, staffBanks, staffDependents);
            }
            catch (InvalidOperationException ex)
            {
                // 🔥 Bắt lỗi trùng lặp dữ liệu từ Repository (Duplicate Key)
                return ServiceResult.Failure(ex.Message);
            }

            return ServiceResult.Success("Cập nhật nhân viên thành công!");
        }

        // ==================== TOGGLE STATUS ====================
        public async Task<ServiceResult> ToggleStaffStatusAsync(int staffId, ClaimsPrincipal user)
        {
            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);

            var staff = await _repository.GetStaffByIdAsync(staffId);
            if (staff == null)
                return ServiceResult.Failure("Không tìm thấy nhân viên.");

            // === 🔥 FIX: Bảo mật - Chặn Leo Quyền (Cross-check Target Staff) ===
            var targetRoleIds = staff.Account?.AccountRoles?.Select(ar => ar.RoleId).ToList() ?? new List<int>();
            if (isStoreManager && !isAdmin)
            {
                // Nếu nhân viên có role ≤ Store Manager → cấp cao
                if (targetRoleIds.Any(r => r <= ROLE_STORE_MANAGER))
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền khóa/mở khóa tài khoản cấp cao hơn hoặc ngang hàng!");
                }

                // Extra check: Nhân viên phải thuộc cửa hàng của quản lý
                if (staff.StoreId != currentStoreId)
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền khóa/mở khóa nhân viên của cửa hàng khác!");
                }
            }

            // 🔥 RULE 2 (Advanced) — Deactivation Lock: Chỉ check khi ĐANG khóa (Active → Inactive)
            if (staff.Active)
            {
                if (await _repository.HasOpenCashSessionAsync(staffId))
                {
                    return ServiceResult.Failure("Không thể khóa tài khoản! Nhân viên này chưa kết thúc ca thu ngân (đóng két).");
                }

                if (await _repository.HasActiveShiftAsync(staffId))
                {
                    return ServiceResult.Failure("Không thể khóa tài khoản! Nhân viên này chưa check-out khỏi ca làm việc hiện tại.");
                }
            }

            var result = await _repository.ToggleStatusAsync(staffId);
            if (!result)
                return ServiceResult.Failure("Không thể thay đổi trạng thái nhân viên.");

            var newStatus = !staff.Active ? "đã được kích hoạt" : "đã bị khóa";
            return ServiceResult.Success($"Tài khoản nhân viên {staff.FullName} {newStatus}.");
        }

        // ==================== HELPER ====================
        private (bool isAdmin, bool isStoreManager, int storeId) ExtractUserClaims(ClaimsPrincipal user)
        {
            var roles = user.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            // Admin hệ thống hiện tại gồm:
            // - Chủ doanh nghiệp
            // - Quản trị hệ thống
            var isAdmin =
                roles.Contains(RoleConstants.BusinessOwner) ||
                roles.Contains(RoleConstants.SystemAdmin);

            var isStoreManager =
                roles.Contains(RoleConstants.StoreManager);

            var storeIdClaim = user.FindFirst("StoreId")?.Value;
            int.TryParse(storeIdClaim, out var storeId);

            return (isAdmin, isStoreManager, storeId);
        }

        private List<string> FilterAndLimit(List<string> items, int maxCount)
        {
            if (items == null) return new List<string>();
            return items
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Take(maxCount)
                .ToList();
        }
    }
}
