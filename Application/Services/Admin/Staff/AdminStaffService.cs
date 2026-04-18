using System.Security.Claims;
using System.Linq;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Interfaces.Admin.Staff;
using CafeChain.Application.Results;
using CafeChain.Infrastrusture.Interfaces.Admin.Staff;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.ViewModels.Admin.Staff;
using CafeChain.ViewModels.Shared;
using Microsoft.AspNetCore.Hosting;
using CafeChain.Application.Constants;
using Microsoft.AspNetCore.Http;

namespace CafeChain.Application.Services.Admin.Staff
{
    public class AdminStaffService : IAdminStaffService
    {
        private readonly IAdminStaffRepository _repository;
        private readonly IWebHostEnvironment _env;
        private readonly IScopeAuthorizationService _scopeAuthorizationService;

        // 🔥 Role Name-based Forbidden roles cho Store Manager (Chỉ được quản lý nhân viên cấp dưới)
        private static readonly string[] FORBIDDEN_ROLES_FOR_STORE_MANAGER = { 
            RoleConstants.SuperAdmin, 
            RoleConstants.CEO, 
            RoleConstants.CFO, 
            RoleConstants.MarketingManager, 
            RoleConstants.OperationsManager, 
            RoleConstants.HRManager, 
            RoleConstants.AreaManager, 
            RoleConstants.StoreManager 
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
            int currentStaffId = 0;
            var staffIdClaim = user.FindFirst("StaffId")?.Value;
            if (staffIdClaim != null) int.TryParse(staffIdClaim, out currentStaffId);

            // Filter Roles dựa theo quyền hạn thực tế (Business Logic Layer)
            var allRoles = await _repository.GetRolesForDropdownAsync();
            
            // ❌ Bỏ trường Khách hàng ra khỏi form Quản lý Nhân sự
            var roles = allRoles.Where(r => r.Name != RoleConstants.Customer).ToList();

            if (isStoreManager && !isAdmin)
            {
                // Store Manager chỉ được tạo: Shift Supervisor và Cashier
                roles = roles.Where(r => r.Name == RoleConstants.ShiftSupervisor || r.Name == RoleConstants.Cashier).ToList();
            }
            else if (!isAdmin)
            {
                // Rule: Không cho phép tạo Role ngang hàng hoặc cao hơn (dựa trên tên hoặc logic cấp bậc)
                // Tuy nhiên, yêu cầu cơ bản là loại bỏ Customer và giữ nguyên logic cho Admin System
                roles = roles.Where(r => r.Name != RoleConstants.Customer).ToList();
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

        // ==================== INDEX ====================
        public async Task<StaffIndexPageVM> GetStaffIndexPageAsync(int page, int pageSize, int? storeId, string search, int? roleFilter, ClaimsPrincipal user)
        {
            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);

            var (items, totalCount) = await _repository.GetPaginatedStaffsAsync(page, pageSize, storeId, search, roleFilter);
            var (total, active, inactive) = await _repository.GetStaffCountsAsync(storeId);

            var viewModels = items.Select(s => {
                var rolesData = s.Account?.AccountRoles?.Select(ar => ar.Role).ToList() ?? new List<Role>();
                var roleNames = rolesData.Select(r => r?.Name ?? "").ToList();
                var roleIds = rolesData.Select(r => r?.RoleId ?? 0).ToList();
                
                // 🔥 FIX: role ≤ Store Manager = cấp cao → không cho Store Manager edit
                bool canEdit = true;
                if (isStoreManager && !isAdmin)
                {
                    // Chống Store Manager sửa tài khoản cấp trên bằng cách check danh sách tên cấm
                    if (roleNames.Any(name => FORBIDDEN_ROLES_FOR_STORE_MANAGER.Contains(name))) canEdit = false;
                    if (s.StoreId != currentStoreId) canEdit = false;
                }

                return new StaffIndexVM
                {
                    StaffId = s.StaffId,
                    FullName = s.FullName,
                    Email = s.Account?.Email ?? "",
                    AvatarUrl = s.AvatarUrl ?? "/Images/avatars/avtdf.jpg",
                    StoreName = s.Store?.Name ?? "",
                    RoleNames = roleNames,
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
                Salary = staff.Salary,
                DateOfBirth = staff.DateOfBirth,
                StoreId = staff.StoreId,
                SelectedRoleId = staff.Account?.AccountRoles?.FirstOrDefault()?.RoleId ?? 0,
                ScopeTypeId = staff.StaffScopes?.FirstOrDefault()?.ScopeTypeId ?? 4,
                ScopeRefId = staff.StaffScopes?.FirstOrDefault()?.ScopeRefId ?? staff.StoreId,
                Phones = staff.StaffPhones?.OrderByDescending(p => p.IsDefault).Select(p => p.Phone).ToList() ?? new List<string>(),
                Addresses = staff.StaffAddresses?.OrderByDescending(a => a.IsDefault).Select(a => a.Address).ToList() ?? new List<string>(),
                CurrentAvatarUrl = staff.AvatarUrl ?? "/Images/avatars/avtdf.jpg",
                Active = staff.Active
            };
        }

        // ==================== CREATE ====================
        public async Task<ServiceResult> CreateStaffAsync(StaffCreateVM model, ClaimsPrincipal user, IFormFile avatarFile)
        {
            model.TaxCode = string.IsNullOrWhiteSpace(model.TaxCode) ? null : model.TaxCode.Trim();
            model.CCCD = string.IsNullOrWhiteSpace(model.CCCD) ? null : model.CCCD.Trim();

            // === BƯỚC 1: Đọc Claims ===
            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);

            // === BƯỚC 2: 🔥 RULE 2 — Hard-Override cho Store Manager ===
            if (isStoreManager && !isAdmin)
            {
                model.StoreId = currentStoreId;
                model.ScopeTypeId = 4; // STORE
                model.ScopeRefId = currentStoreId;
            }

            // === BƯỚC 3: 🔥 RULE 2 — Security Check: Chặn leo quyền ===
            if (isStoreManager && !isAdmin)
            {
                var allRolesForCheck = await _repository.GetRolesForDropdownAsync();
                var targetRoleMapping = allRolesForCheck.FirstOrDefault(r => r.RoleId == model.SelectedRoleId);
                
                if (targetRoleMapping != null && FORBIDDEN_ROLES_FOR_STORE_MANAGER.Contains(targetRoleMapping.Name))
                {
                    return ServiceResult.Failure("Hành vi không hợp lệ! Bạn không có quyền cấp phát chức vụ này.");
                }
            }

            // === BƯỚC 4: 🔥 RULE 3 (Advanced) — Role-Scope Alignment ===
            var allRolesData = await _repository.GetRolesForDropdownAsync();
            var selectedRole = allRolesData.FirstOrDefault(r => r.RoleId == model.SelectedRoleId);
            var selectedRoleName = selectedRole?.Name ?? "";

            if (selectedRoleName == RoleConstants.Cashier || 
                selectedRoleName == RoleConstants.ShiftSupervisor || 
                selectedRoleName == RoleConstants.StoreManager)
            {
                model.ScopeTypeId = 4;
                if (!model.StoreId.HasValue || model.StoreId <= 0)
                    return ServiceResult.Failure("Vai trò này yêu cầu phải chọn một Cửa hàng vật lý cụ thể.");
                model.ScopeRefId = model.StoreId.Value;
            }

            // === GUARD CLAUSE AREA MANAGER ===
            var rolesStr = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
            bool isAreaManager = rolesStr.Contains(RoleConstants.AreaManager);
            if (isAreaManager && !isAdmin)
            {
                // Lớp 1: Chống leo quyền dọc
                if (model.SelectedRoleId >= 1 && model.SelectedRoleId <= 7)
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
                var defaultPhone = validPhones.FirstOrDefault() ?? "0000000000";
                password = $"Cfc@{defaultPhone}";
            }

            // === BƯỚC 7: Avatar Upload (xử lý trong Service, không phải Controller) ===
            var avatarUrl = await SaveAvatarAsync(avatarFile);

            // === BƯỚC 8: Build Entities ===
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var account = new Account
            {
                Email = model.Email,
                PasswordHash = passwordHash,
                Active = true,
                CreatedAt = DateTime.Now
            };

            var staff = new Models.Staffs.Staff
            {
                FullName = model.FullName,
                TaxCode = string.IsNullOrWhiteSpace(model.TaxCode) ? null : model.TaxCode.Trim(),
                CCCD = string.IsNullOrWhiteSpace(model.CCCD) ? null : model.CCCD.Trim(),
                Salary = model.Salary,
                DateOfBirth = model.DateOfBirth,
                StoreId = model.StoreId ?? 1,
                Active = true,
                AvatarUrl = avatarUrl,
                CreatedAt = DateTime.Now
            };

            var allRolesDataForCreate = await _repository.GetRolesForDropdownAsync();
            var targetRoleForCreate = allRolesDataForCreate.FirstOrDefault(r => r.RoleId == model.SelectedRoleId);

            var accountRoles = (targetRoleForCreate != null && targetRoleForCreate.Name != RoleConstants.Customer)
                ? new List<AccountRole> { new AccountRole { RoleId = model.SelectedRoleId } }
                : new List<AccountRole>();

            var staffScopes = new List<StaffScope>
            {
                new StaffScope
                {
                    ScopeTypeId = model.ScopeTypeId > 0 ? model.ScopeTypeId : 4,
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

            // === BƯỚC 9: Gọi Repository (Transaction) ===
            await _repository.CreateStaffTransactionAsync(staff, account, accountRoles, staffScopes, staffPhones, staffAddresses);

            return ServiceResult.Success("Thêm nhân viên thành công!");
        }

        // ==================== UPDATE ====================
        public async Task<ServiceResult> UpdateStaffAsync(StaffEditVM model, ClaimsPrincipal user, IFormFile avatarFile)
        {
            model.TaxCode = string.IsNullOrWhiteSpace(model.TaxCode) ? null : model.TaxCode.Trim();
            model.CCCD = string.IsNullOrWhiteSpace(model.CCCD) ? null : model.CCCD.Trim();

            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);

            var existingStaff = await _repository.GetStaffByIdAsync(model.StaffId);
            if (existingStaff == null)
                return ServiceResult.Failure("Không tìm thấy nhân viên.");

            // === 🔥 FIX: Bảo mật - Chặn Leo Quyền (Cross-check Target Staff) ===
            var targetRoles = existingStaff.Account?.AccountRoles?.Select(ar => ar.Role).ToList() ?? new List<Role>();
            var targetRoleNames = targetRoles.Select(r => r?.Name ?? "").ToList();

            if (isStoreManager && !isAdmin)
            {
                // Nếu nhân viên đang bị chỉnh sửa có role thuộc diện cấm → cấp cao
                if (targetRoleNames.Any(name => FORBIDDEN_ROLES_FOR_STORE_MANAGER.Contains(name)))
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
                model.ScopeTypeId = 4;
                model.ScopeRefId = currentStoreId;
            }

            // === 🔥 MASTER DATA FOR VALIDATION ===
            var allRolesForUpdate = await _repository.GetRolesForDropdownAsync();
            var selectedRoleForUpdate = allRolesForUpdate.FirstOrDefault(r => r.RoleId == model.SelectedRoleId);
            var selectedRoleNameForUpdate = selectedRoleForUpdate?.Name ?? "";

            // === 🔥 RULE 2 — Security Check ===
            if (isStoreManager && !isAdmin)
            {
                if (selectedRoleForUpdate != null && FORBIDDEN_ROLES_FOR_STORE_MANAGER.Contains(selectedRoleNameForUpdate))
                {
                    return ServiceResult.Failure("Hành vi không hợp lệ! Bạn không có quyền cấp phát chức vụ này.");
                }
            }

            // === 🔥 RULE 3 (Advanced) — Role-Scope Alignment ===
            if (selectedRoleNameForUpdate == RoleConstants.Cashier || 
                selectedRoleNameForUpdate == RoleConstants.ShiftSupervisor || 
                selectedRoleNameForUpdate == RoleConstants.StoreManager)
            {
                model.ScopeTypeId = 4;
                if (!model.StoreId.HasValue || model.StoreId <= 0)
                    return ServiceResult.Failure("Vai trò này yêu cầu phải chọn một Cửa hàng vật lý cụ thể.");
                model.ScopeRefId = model.StoreId.Value;
            }

            // === GUARD CLAUSE AREA MANAGER KHÔNG XIN PHÉP (Lớp 1 & Lớp 2) ===
            var rolesStr = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
            bool isAreaManager = rolesStr.Contains(RoleConstants.AreaManager);
            if (isAreaManager && !isAdmin)
            {
                var allRolesForCheckArea = await _repository.GetRolesForDropdownAsync();
                var selectedRoleForArea = allRolesForCheckArea.FirstOrDefault(r => r.RoleId == model.SelectedRoleId);
                var selectedRoleAreaName = selectedRoleForArea?.Name ?? "";

                if (FORBIDDEN_ROLES_FOR_STORE_MANAGER.Contains(selectedRoleAreaName))
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
                model.Salary = existingStaff.Salary; // Ép cứng salary cũ
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
            if (avatarFile != null && avatarFile.Length > 0)
            {
                existingStaff.AvatarUrl = await SaveAvatarAsync(avatarFile);
            }

            // === Update entities ===
            existingStaff.FullName = model.FullName;
            existingStaff.TaxCode = string.IsNullOrWhiteSpace(model.TaxCode) ? null : model.TaxCode.Trim();
            existingStaff.CCCD = string.IsNullOrWhiteSpace(model.CCCD) ? null : model.CCCD.Trim();
            existingStaff.Salary = model.Salary;
            existingStaff.DateOfBirth = model.DateOfBirth;
            existingStaff.StoreId = model.StoreId ?? existingStaff.StoreId;

            existingStaff.Account.Email = model.Email;

            // Password update (optional)
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                existingStaff.Account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            }

            var targetRoleForFinalUpdate = allRolesForUpdate.FirstOrDefault(r => r.RoleId == model.SelectedRoleId);

            var accountRoles = (targetRoleForFinalUpdate != null && targetRoleForFinalUpdate.Name != RoleConstants.Customer)
                ? new List<AccountRole> { new AccountRole { RoleId = model.SelectedRoleId } }
                : new List<AccountRole>();

            var staffScopes = new List<StaffScope>
            {
                new StaffScope
                {
                    ScopeTypeId = model.ScopeTypeId > 0 ? model.ScopeTypeId : 4,
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

            await _repository.UpdateStaffTransactionAsync(existingStaff, existingStaff.Account, accountRoles, staffScopes, staffPhones, staffAddresses);

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
            var targetRolesForStatus = staff.Account?.AccountRoles?.Select(ar => ar.Role).ToList() ?? new List<Role>();
            var targetRoleNamesForStatus = targetRolesForStatus.Select(r => r?.Name ?? "").ToList();

            if (isStoreManager && !isAdmin)
            {
                // Nếu nhân viên có role thuộc diện cấm → cấp cao
                if (targetRoleNamesForStatus.Any(name => FORBIDDEN_ROLES_FOR_STORE_MANAGER.Contains(name)))
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

            // 🔥 FIX: Dùng ĐÚNG tên role tiếng Việt từ RoleConstants (khớp với Claims lúc Login)
            var isAdmin = roles.Contains(RoleConstants.SuperAdmin);       // "Super Admin"
            var isStoreManager = roles.Contains(RoleConstants.StoreManager); // "Cửa hàng trưởng"

            var storeIdClaim = user.FindFirst("StoreId")?.Value;
            int.TryParse(storeIdClaim, out int storeId);

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
