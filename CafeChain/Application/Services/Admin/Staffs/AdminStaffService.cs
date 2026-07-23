using System.Security.Claims;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Application.Results;
using CafeChain.Application.Constants.Cloudinaries;
using CafeChain.Application.DTOs.Common;
using CafeChain.Infrastrusture.Interfaces.Admin.Staffs;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.ViewModels.Admin.Staffs;
using CafeChain.ViewModels.Shared;
using CafeChain.Application.Constants;
using Microsoft.AspNetCore.Http;
using CafeChain.Models.Enums.Cloudinaries;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Admin.Staffs
{
    public class AdminStaffService : IAdminStaffService
    {
        private readonly IAdminStaffRepository _repository;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IScopeAuthorizationService _scopeAuthorizationService;
        private readonly ILogger<AdminStaffService> _logger;

        // 🔥 Role ID Constants (CHÍNH XÁC theo Seed Data)
        private const int ROLE_BUSINESS_OWNER = 1;          // Chủ doanh nghiệp
        private const int ROLE_AREA_MANAGER = 2;            // Quản lý vùng
        private const int ROLE_STORE_MANAGER = 3;           // Quản lý chi nhánh
        private const int ROLE_SALES_STAFF = 4;             // Nhân viên bán hàng
        private const int ROLE_ACCOUNTANT_WAREHOUSE = 5;    // Kế toán/kho
        private const int ROLE_SYSTEM_ADMIN = 6;            // Quản trị hệ thống
        private const int ROLE_CUSTOMER = 7;                // Khách hàng
        private const int ROLE_SHIFT_SUPERVISOR = 8;        // Ca trưởng

        private const int SCOPE_COUNTRY = (int)ScopeLevel.Country;
        private const int SCOPE_PROVINCE = (int)ScopeLevel.Province;
        private const int SCOPE_DISTRICT = (int)ScopeLevel.District;
        private const int SCOPE_WARD = (int)ScopeLevel.Ward;
        private const int SCOPE_STORE = (int)ScopeLevel.Store;

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
        private static int GetRequiredScopeTypeForRole(int roleId, int requestedScopeTypeId) => roleId switch
        {
            ROLE_BUSINESS_OWNER or ROLE_SYSTEM_ADMIN => SCOPE_COUNTRY,
            ROLE_AREA_MANAGER when requestedScopeTypeId is >= SCOPE_PROVINCE and <= SCOPE_STORE => requestedScopeTypeId,
            ROLE_STORE_MANAGER or ROLE_SALES_STAFF or ROLE_ACCOUNTANT_WAREHOUSE or ROLE_SHIFT_SUPERVISOR => SCOPE_STORE,
            _ => 0
        };

        private static int GetRoleRank(string roleName) => roleName switch
        {
            RoleConstants.BusinessOwner or RoleConstants.SystemAdmin => 0,
            RoleConstants.AreaManager => 10,
            RoleConstants.StoreManager => 20,
            RoleConstants.AccountantWarehouse => 30,
            RoleConstants.ShiftSupervisor => 40,
            RoleConstants.SalesStaff => 50,
            _ => int.MaxValue
        };

        private static int GetRoleRank(int roleId) => roleId switch
        {
            ROLE_BUSINESS_OWNER or ROLE_SYSTEM_ADMIN => 0,
            ROLE_AREA_MANAGER => 10,
            ROLE_STORE_MANAGER => 20,
            ROLE_ACCOUNTANT_WAREHOUSE => 30,
            ROLE_SHIFT_SUPERVISOR => 40,
            ROLE_SALES_STAFF => 50,
            _ => int.MaxValue
        };

        private static int GetActorRank(ClaimsPrincipal actor) => actor.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => GetRoleRank(c.Value))
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        public AdminStaffService(
            IAdminStaffRepository repository,
            ICloudinaryService cloudinaryService,
            IScopeAuthorizationService scopeAuthorizationService,
            ILogger<AdminStaffService> logger)
        {
            _repository = repository;
            _cloudinaryService = cloudinaryService;
            _scopeAuthorizationService = scopeAuthorizationService;
            _logger = logger;
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
                .Where(r => r.RoleId != ROLE_CUSTOMER && GetRoleRank(r.RoleId) > GetActorRank(user))
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

            var scopeTypes = await _repository.GetScopeTypesAsync();
            var result = new StaffFormMasterDataVM
            {
                Roles = roles,
                ScopeTypes = scopeTypes
                    .OrderBy(x => x.ScopeTypeId)
                    .Select(x => new StaffScopeTypeOptionVM
                    {
                        ScopeTypeId = x.ScopeTypeId,
                        Code = x.Code,
                        DisplayName = ScopeTypeDisplayNames.FromCode(x.Code)
                    })
                    .ToList(),
                Provinces = await _repository.GetProvincesAsync(),
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
        private async Task<UploadImageResult?> UploadAvatarAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return null;

            return await _cloudinaryService.UploadAsync(
                file,
                ImageFolder.Staffs,
                ImageCategory.Avatar);
        }

        private async Task DeleteCloudinaryAvatarBestEffortAsync(string? publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId)
                || string.Equals(publicId, DefaultImages.StaffAvatarPublicId, StringComparison.Ordinal)
                || string.Equals(publicId, "staffs/default-avatar", StringComparison.Ordinal))
                return;

            try
            {
                await _cloudinaryService.DeleteAsync(publicId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể xóa avatar Cloudinary {PublicId}.", publicId);
            }
        }

        // ==================== DROPDOWN DATA (AJAX) ====================
        public async Task<IEnumerable<object>> GetScopeReferencesAsync(
            int scopeTypeId,
            ClaimsPrincipal actor,
            int? parentId = null)
        {
            try
            {
                if (scopeTypeId == SCOPE_COUNTRY) // HQ
                {
                    var (isAdmin, _, _) = ExtractUserClaims(actor);
                    return isAdmin
                        ? new[] { new { id = 1, name = ScopeTypeDisplayNames.Country } }
                        : Array.Empty<object>();
                }

                var (actorIsAdmin, _, _) = ExtractUserClaims(actor);
                var allowedStores = actorIsAdmin
                    ? await _repository.GetActiveStoresAsync()
                    : await GetAllowedStoresForActorAsync(actor);

                if (scopeTypeId == SCOPE_STORE) // Store
                {
                    return allowedStores.Select(s => new { id = s.StoreId, name = s.Name });
                }
                if (scopeTypeId == SCOPE_PROVINCE) // Province
                {
                    var provinces = await _repository.GetProvincesAsync();
                    if (!actorIsAdmin)
                    {
                        var allowedIds = allowedStores
                            .Where(s => s.ProvinceId.HasValue)
                            .Select(s => s.ProvinceId!.Value)
                            .ToHashSet();
                        provinces = provinces.Where(p => allowedIds.Contains(p.ProvinceId)).ToList();
                    }
                    return provinces.Select(p => new { id = p.ProvinceId, name = p.Name });
                }
                if (scopeTypeId == SCOPE_DISTRICT && parentId.HasValue) // District
                {
                    var districts = await _repository.GetDistrictsAsync(parentId.Value);
                    if (!actorIsAdmin)
                    {
                        var allowedIds = allowedStores
                            .Where(s => s.DistrictId.HasValue)
                            .Select(s => s.DistrictId!.Value)
                            .ToHashSet();
                        districts = districts.Where(d => allowedIds.Contains(d.DistrictId)).ToList();
                    }
                    return districts.Select(d => new { id = d.DistrictId, name = d.Name });
                }
                if (scopeTypeId == SCOPE_WARD && parentId.HasValue)
                {
                    var wards = await _repository.GetWardsAsync(parentId.Value);
                    if (!actorIsAdmin)
                    {
                        var allowedIds = allowedStores
                            .Where(s => s.WardId.HasValue)
                            .Select(s => s.WardId!.Value)
                            .ToHashSet();
                        wards = wards.Where(w => allowedIds.Contains(w.WardId)).ToList();
                    }
                    return wards.Select(w => new { id = w.WardId, name = w.Name });
                }
                return new object[] { };
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi fetch DB locations: " + ex.Message);
                return new object[] { };
            }
        }

        public async Task<IEnumerable<object>> GetDistrictsAsync(int provinceId)
        {
            if (provinceId <= 0) return Array.Empty<object>();
            var districts = await _repository.GetDistrictsAsync(provinceId);
            return districts.Select(x => new { id = x.DistrictId, name = x.Name });
        }

        public async Task<IEnumerable<object>> GetWardsAsync(int districtId)
        {
            if (districtId <= 0) return Array.Empty<object>();
            var wards = await _repository.GetWardsAsync(districtId);
            return wards.Select(x => new { id = x.WardId, name = x.Name });
        }

        private async Task<List<CafeChain.Models.Stores.Store>> GetAllowedStoresForActorAsync(
            ClaimsPrincipal actor)
        {
            if (!int.TryParse(actor.FindFirstValue("StaffId"), out var actorStaffId)
                || actorStaffId <= 0)
                return new List<CafeChain.Models.Stores.Store>();

            return (await _scopeAuthorizationService.GetAllowedStoresAsync(actorStaffId))
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .ToList();
        }

        // ==================== INDEX ====================
        public async Task<StaffIndexPageVM> GetStaffIndexPageAsync(int page, int pageSize, int? storeId, string search, int? roleFilter, ClaimsPrincipal user)
        {
            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);
            IReadOnlyCollection<int>? scopedStoreIds = storeId.HasValue ? new[] { storeId.Value } : null;
            if (!isAdmin)
            {
                var actorStaffId = int.TryParse(user.FindFirstValue("StaffId"), out var parsedStaffId)
                    ? parsedStaffId : 0;
                var allowedIds = (await _scopeAuthorizationService.GetAllowedStoresAsync(actorStaffId))
                    .Select(x => x.StoreId).Distinct().ToList();
                scopedStoreIds = storeId.HasValue
                    ? allowedIds.Where(x => x == storeId.Value).ToList()
                    : allowedIds;
            }

            var (items, totalCount) = await _repository.GetPaginatedStaffsAsync(page, pageSize, scopedStoreIds, search, roleFilter);
            var (total, active, inactive) = await _repository.GetStaffCountsAsync(scopedStoreIds);

            var viewModels = items.Select(s => {
                var roleIds = s.Account?.AccountRoles?.Select(ar => ar.RoleId).ToList() ?? new List<int>();

                // 🔥 FIX: role ≤ Store Manager = cấp cao → không cho Store Manager edit
                var targetRank = roleIds.Select(GetRoleRank).DefaultIfEmpty(int.MaxValue).Min();
                var canEdit = targetRank > GetActorRank(user);

                return new StaffIndexVM
                {
                    StaffId = s.StaffId,
                    AccountId = s.AccountId,
                    FullName = s.FullName,
                    Email = s.Account?.Email ?? "",
                    AvatarUrl = string.IsNullOrWhiteSpace(s.AvatarUrl) ? DefaultImages.StaffAvatarUrl : s.AvatarUrl,
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
        public async Task<StaffEditVM> GetStaffForEditAsync(int staffId, ClaimsPrincipal actor)
        {
            var staff = await _repository.GetStaffByIdAsync(staffId);
            if (staff == null) return null;
            var (isAdmin, isStoreManager, _) = ExtractUserClaims(actor);
            if (!isAdmin)
            {
                var actorStaffId = int.TryParse(actor.FindFirstValue("StaffId"), out var parsedStaffId)
                    ? parsedStaffId : 0;
                var allowedStoreIds = (await _scopeAuthorizationService.GetAllowedStoresAsync(actorStaffId))
                    .Select(x => x.StoreId).ToHashSet();
                if (!allowedStoreIds.Contains(staff.StoreId))
                    throw new UnauthorizedAccessException("Bạn không có quyền xem hoặc sửa nhân viên này.");
            }
            var targetRank = staff.Account?.AccountRoles?
                .Select(x => GetRoleRank(x.Role?.Name ?? string.Empty))
                .DefaultIfEmpty(int.MaxValue).Min() ?? int.MaxValue;
            if (targetRank <= GetActorRank(actor))
                throw new UnauthorizedAccessException("Bạn không có quyền xem hoặc sửa tài khoản ngang cấp hoặc cấp cao hơn.");

            return new StaffEditVM
            {
                StaffId = staff.StaffId,
                AccountId = staff.AccountId,
                FullName = staff.FullName,
                Email = staff.Account?.Email ?? "",
                CCCD = staff.CCCD ?? "",
                Gender = staff.Gender,
                StartDate = staff.StartDate,
                EmployeeStatus = staff.EmployeeStatus,
                DateOfBirth = staff.DateOfBirth,
                StoreId = staff.StoreId,
                SelectedRoleId = staff.Account?.AccountRoles?.FirstOrDefault()?.RoleId ?? ROLE_SALES_STAFF,
                ScopeTypeId = staff.StaffScopes?.FirstOrDefault()?.ScopeTypeId ?? SCOPE_STORE,
                ScopeRefId = staff.StaffScopes?.FirstOrDefault()?.ScopeRefId ?? staff.StoreId,
                Phones = staff.StaffPhones?.OrderByDescending(p => p.IsDefault).Select(p => p.Phone).ToList() ?? new List<string>(),
                ProvinceId = staff.StaffAddresses?.OrderByDescending(a => a.IsDefault).FirstOrDefault()?.ProvinceId,
                DistrictId = staff.StaffAddresses?.OrderByDescending(a => a.IsDefault).FirstOrDefault()?.DistrictId,
                WardId = staff.StaffAddresses?.OrderByDescending(a => a.IsDefault).FirstOrDefault()?.WardId,
                Address = staff.StaffAddresses?.OrderByDescending(a => a.IsDefault).FirstOrDefault()?.Address ?? string.Empty,
                CurrentAvatarUrl = string.IsNullOrWhiteSpace(staff.AvatarUrl) ? DefaultImages.StaffAvatarUrl : staff.AvatarUrl,
                Active = staff.Active
            };
        }

        // ==================== CREATE ====================
        public async Task<ServiceResult> CreateStaffAsync(StaffCreateVM model, ClaimsPrincipal user, IFormFile? file)
        {
            model.CCCD = string.IsNullOrWhiteSpace(model.CCCD) ? null : model.CCCD.Trim();
            model.Email = model.Email?.Trim() ?? string.Empty;

            // === BƯỚC 1: Đọc Claims ===
            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);

            var actorIsOwner = user.IsInRole(RoleConstants.BusinessOwner);
            var actorIsSystemAdmin = user.IsInRole(RoleConstants.SystemAdmin);
            if ((actorIsOwner && model.SelectedRoleId == ROLE_SYSTEM_ADMIN)
                || (actorIsSystemAdmin && model.SelectedRoleId == ROLE_BUSINESS_OWNER)
                || model.SelectedRoleId == ROLE_CUSTOMER)
                return ServiceResult.Failure("Không được gán vai trò cấp cao chéo hoặc vai trò Khách hàng cho nhân viên.");

            if (GetRoleRank(model.SelectedRoleId) <= GetActorRank(user))
                return ServiceResult.Failure("Bạn chỉ được gán vai trò thấp hơn vai trò của chính mình.");

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
            int requiredScope = GetRequiredScopeTypeForRole(model.SelectedRoleId, model.ScopeTypeId);
            if (requiredScope == 0)
                return ServiceResult.Failure("Vai trò hoặc loại phạm vi không hợp lệ.");
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
            if (!model.StoreId.HasValue || model.StoreId.Value <= 0)
                return ServiceResult.Failure("Vui lòng chọn cửa hàng làm việc chính.");
            if (!isAdmin)
            {
                var actorStaffId = int.TryParse(user.FindFirstValue("StaffId"), out var parsedActorStaffId)
                    ? parsedActorStaffId : 0;
                var actorStoreIds = (await _scopeAuthorizationService.GetAllowedStoresAsync(actorStaffId))
                    .Select(s => s.StoreId).ToHashSet();
                if (!actorStoreIds.Contains(model.StoreId.Value))
                    throw new UnauthorizedAccessException("Cửa hàng làm việc chính nằm ngoài phạm vi được cấp.");
            }
            if (!await _repository.ScopeCoversStoreAsync(
                    model.ScopeTypeId, model.ScopeRefId, model.StoreId.Value))
                return ServiceResult.Failure("Cửa hàng làm việc chính phải nằm trong phạm vi được cấp.");
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
            if (!model.ProvinceId.HasValue || !model.DistrictId.HasValue || !model.WardId.HasValue
                || string.IsNullOrWhiteSpace(model.Address))
                return ServiceResult.Failure("Vui lòng nhập đầy đủ Tỉnh/Thành phố, Quận/Huyện, Phường/Xã và địa chỉ chi tiết.");
            if (!await _repository.IsAddressHierarchyValidAsync(
                    model.ProvinceId.Value, model.DistrictId.Value, model.WardId.Value))
                return ServiceResult.Failure("Địa chỉ không hợp lệ: Quận/Huyện hoặc Phường/Xã không thuộc cấp địa giới đã chọn.");

            if (validPhones.Any())
            {
                if (await _repository.DefaultPhoneExistsAsync(validPhones[0]))
                {
                    return ServiceResult.Failure($"Số điện thoại mặc định '{validPhones[0]}' đã tồn tại trong hệ thống.");
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
            if (!model.StoreId.HasValue || model.StoreId.Value <= 0)
                return ServiceResult.Failure("Vui lòng chọn cửa hàng làm việc chính.");

            UploadImageResult? uploadedAvatar;
            try
            {
                uploadedAvatar = await UploadAvatarAsync(file);
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure(ex.Message);
            }

            // === BƯỚC 8: Build Entities ===
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var account = new Account
            {
                Email = model.Email,
                PasswordHash = passwordHash,
                Active = true,
                RequiresPasswordChange = false,
                CreatedAt = DateTime.Now
            };

            var staff = new Models.Staffs.Staff
            {
                FullName = model.FullName,
                CCCD = string.IsNullOrWhiteSpace(model.CCCD) ? null : model.CCCD.Trim(),
                Gender = model.Gender,
                StartDate = model.StartDate,
                EmployeeStatus = model.EmployeeStatus,
                DateOfBirth = model.DateOfBirth,
                StoreId = model.StoreId.Value,
                Active = true,
                AvatarUrl = uploadedAvatar?.Url ?? DefaultImages.StaffAvatarUrl,
                AvatarPublicId = uploadedAvatar?.PublicId ?? DefaultImages.StaffAvatarPublicId,
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
                    ScopeRefId = model.ScopeRefId > 0 ? model.ScopeRefId : model.StoreId.Value
                }
            };

            // Build StaffPhones với IsDefault logic
            var staffPhones = validPhones.Select((phone, index) => new StaffPhone
            {
                Phone = phone,
                IsDefault = index == 0  // 🔥 Phần tử đầu tiên = Default
            }).ToList();

            var staffAddresses = new List<StaffAddress>
            {
                new()
                {
                    ProvinceId = model.ProvinceId!.Value,
                    DistrictId = model.DistrictId!.Value,
                    WardId = model.WardId!.Value,
                    Address = model.Address.Trim(),
                    IsDefault = true
                }
            };

            // === BƯỚC 9: Gọi Repository (Transaction) ===
            try
            {
                await _repository.CreateStaffTransactionAsync(staff, account, accountRoles, staffScopes, staffPhones, staffAddresses);
            }
            catch (InvalidOperationException ex)
            {
                await DeleteCloudinaryAvatarBestEffortAsync(uploadedAvatar?.PublicId);
                // 🔥 Bắt lỗi trùng lặp dữ liệu từ Repository (Duplicate Key)
                return ServiceResult.Failure(ex.Message);
            }
            catch
            {
                await DeleteCloudinaryAvatarBestEffortAsync(uploadedAvatar?.PublicId);
                throw;
            }

            var createResult = ServiceResult.Success("Thêm nhân viên thành công!");
            createResult.EntityId = staff.StaffId;
            return createResult;
        }

        // ==================== UPDATE ====================
        public async Task<ServiceResult> UpdateStaffAsync(StaffEditVM model, ClaimsPrincipal user, IFormFile? file)
        {
            model.CCCD = string.IsNullOrWhiteSpace(model.CCCD) ? null : model.CCCD.Trim();
            model.Email = model.Email?.Trim() ?? string.Empty;

            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);

            var existingStaff = await _repository.GetStaffByIdAsync(model.StaffId);
            if (existingStaff == null)
                return ServiceResult.Failure("Không tìm thấy nhân viên.");

            // Profile update never changes AccountRole, StaffScope or primary Store.
            // Non-global actors may only edit staff already inside their own scope.
            var targetRoleNames = existingStaff.Account?.AccountRoles?
                .Select(ar => ar.Role?.Name).OfType<string>()
                .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
            if (!isAdmin)
            {
                var actorStaffId = int.TryParse(user.FindFirstValue("StaffId"), out var parsedStaffId)
                    ? parsedStaffId : 0;
                var allowedStoreIds = (await _scopeAuthorizationService.GetAllowedStoresAsync(actorStaffId))
                    .Select(s => s.StoreId).ToHashSet();
                if (!allowedStoreIds.Contains(existingStaff.StoreId))
                    throw new UnauthorizedAccessException("Nhân viên nằm ngoài phạm vi cửa hàng được cấp.");

            }
            if (targetRoleNames.Select(GetRoleRank).DefaultIfEmpty(int.MaxValue).Min() <= GetActorRank(user))
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa tài khoản cấp cao hơn hoặc ngang hàng.");

            // === Identity Integrity ===
            if (await _repository.EmailExistsAsync(model.Email, existingStaff.AccountId))
            {
                return ServiceResult.Failure("Email đã tồn tại trong hệ thống.");
            }

            var validPhones = FilterAndLimit(model.Phones, 3);
            if (!model.ProvinceId.HasValue || !model.DistrictId.HasValue || !model.WardId.HasValue
                || string.IsNullOrWhiteSpace(model.Address))
                return ServiceResult.Failure("Vui lòng nhập đầy đủ Tỉnh/Thành phố, Quận/Huyện, Phường/Xã và địa chỉ chi tiết.");
            if (!await _repository.IsAddressHierarchyValidAsync(
                    model.ProvinceId.Value, model.DistrictId.Value, model.WardId.Value))
                return ServiceResult.Failure("Địa chỉ không hợp lệ: Quận/Huyện hoặc Phường/Xã không thuộc cấp địa giới đã chọn.");

            if (validPhones.Any())
            {
                if (await _repository.DefaultPhoneExistsAsync(validPhones[0], existingStaff.StaffId))
                {
                    return ServiceResult.Failure($"Số điện thoại mặc định '{validPhones[0]}' đã tồn tại.");
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
            var previousAvatarPublicId = existingStaff.AvatarPublicId;
            UploadImageResult? newlyUploadedAvatar = null;
            if (file != null && file.Length > 0)
            {
                try
                {
                    newlyUploadedAvatar = await UploadAvatarAsync(file);
                    existingStaff.AvatarUrl = newlyUploadedAvatar!.Url;
                    existingStaff.AvatarPublicId = newlyUploadedAvatar.PublicId;
                }
                catch (Exception ex)
                {
                    return ServiceResult.Failure(ex.Message);
                }
            }

            // === Update entities ===
            existingStaff.FullName = model.FullName;
            existingStaff.CCCD = string.IsNullOrWhiteSpace(model.CCCD) ? null : model.CCCD.Trim();
            existingStaff.Gender = model.Gender;
            existingStaff.StartDate = model.StartDate;
            existingStaff.EmployeeStatus = model.EmployeeStatus;
            existingStaff.DateOfBirth = model.DateOfBirth;

            existingStaff.Account.Email = model.Email;

            var staffPhones = validPhones.Select((phone, index) => new StaffPhone
            {
                Phone = phone,
                IsDefault = index == 0
            }).ToList();

            var staffAddresses = new List<StaffAddress>
            {
                new()
                {
                    ProvinceId = model.ProvinceId!.Value,
                    DistrictId = model.DistrictId!.Value,
                    WardId = model.WardId!.Value,
                    Address = model.Address.Trim(),
                    IsDefault = true
                }
            };

            try
            {
                await _repository.UpdateStaffProfileTransactionAsync(
                    existingStaff,
                    existingStaff.Account,
                    staffPhones,
                    staffAddresses);
            }
            catch (InvalidOperationException ex)
            {
                await DeleteCloudinaryAvatarBestEffortAsync(newlyUploadedAvatar?.PublicId);
                // 🔥 Bắt lỗi trùng lặp dữ liệu từ Repository (Duplicate Key)
                return ServiceResult.Failure(ex.Message);
            }
            catch
            {
                await DeleteCloudinaryAvatarBestEffortAsync(newlyUploadedAvatar?.PublicId);
                throw;
            }

            if (newlyUploadedAvatar != null)
                await DeleteCloudinaryAvatarBestEffortAsync(previousAvatarPublicId);
            return ServiceResult.Success("Cập nhật nhân viên thành công!");
        }

        // ==================== TOGGLE STATUS ====================
        public async Task<ServiceResult> ResetPasswordAsync(int accountId, string newPassword, ClaimsPrincipal actor)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return ServiceResult.Failure("Mật khẩu mới phải có ít nhất 6 ký tự.");

            var target = await _repository.GetStaffByAccountIdAsync(accountId);
            if (target == null) return ServiceResult.Failure("Không tìm thấy tài khoản nhân viên.");
            var targetRoles = target.Account.AccountRoles.Select(x => x.Role.Name).ToHashSet();
            var actorStaffId = int.TryParse(actor.FindFirstValue("StaffId"), out var parsedActorStaffId)
                ? parsedActorStaffId : 0;
            var isGlobalActor = actor.IsInRole(RoleConstants.BusinessOwner) || actor.IsInRole(RoleConstants.SystemAdmin);
            if (!isGlobalActor)
            {
                var allowedStoreIds = (await _scopeAuthorizationService.GetAllowedStoresAsync(actorStaffId))
                    .Select(s => s.StoreId).ToHashSet();
                if (!allowedStoreIds.Contains(target.StoreId))
                    throw new UnauthorizedAccessException("Nhân viên nằm ngoài phạm vi được cấp.");
            }
            if (targetRoles.Select(GetRoleRank).DefaultIfEmpty(int.MaxValue).Min() <= GetActorRank(actor))
                throw new UnauthorizedAccessException("Bạn không được thao tác tài khoản ngang cấp hoặc cấp cao hơn.");
            if ((actor.IsInRole(RoleConstants.BusinessOwner) && targetRoles.Contains(RoleConstants.SystemAdmin))
                || (actor.IsInRole(RoleConstants.SystemAdmin) && targetRoles.Contains(RoleConstants.BusinessOwner)))
                return ServiceResult.Failure("Không được đặt lại mật khẩu của tài khoản cấp cao chéo.");

            var updated = await _repository.ResetPasswordAsync(
                accountId,
                BCrypt.Net.BCrypt.HashPassword(newPassword));
            return updated
                ? ServiceResult.Success("Đã cập nhật mật khẩu mới.")
                : ServiceResult.Failure("Không tìm thấy tài khoản nhân viên.");
        }

        public async Task<ServiceResult> ToggleStaffStatusAsync(int staffId, ClaimsPrincipal user)
        {
            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);

            var staff = await _repository.GetStaffByIdAsync(staffId);
            if (staff == null)
                return ServiceResult.Failure("Không tìm thấy nhân viên.");

            var targetRoles = staff.Account?.AccountRoles?.Select(ar => ar.Role?.Name).OfType<string>().ToHashSet()
                ?? new HashSet<string>();
            if (!isAdmin)
            {
                var actorStaffId = int.TryParse(user.FindFirstValue("StaffId"), out var parsedStaffId)
                    ? parsedStaffId : 0;
                var allowedStoreIds = (await _scopeAuthorizationService.GetAllowedStoresAsync(actorStaffId))
                    .Select(x => x.StoreId).ToHashSet();
                if (!allowedStoreIds.Contains(staff.StoreId))
                    throw new UnauthorizedAccessException("Không được đổi trạng thái nhân viên ngoài phạm vi hoặc cấp cao hơn/ngang hàng.");
            }
            if (targetRoles.Select(GetRoleRank).DefaultIfEmpty(int.MaxValue).Min() <= GetActorRank(user))
                throw new UnauthorizedAccessException("Không được đổi trạng thái tài khoản cấp cao hơn hoặc ngang hàng.");

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
