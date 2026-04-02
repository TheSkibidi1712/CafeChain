using System.Security.Claims;
using CafeChain.Application.Interfaces.Admin.Staff;
using CafeChain.Application.Results;
using CafeChain.Infrastrusture.Interfaces.Admin.Staff;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.ViewModels.Admin.Staff;
using CafeChain.ViewModels.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CafeChain.Application.Services.Admin.Staff
{
    public class AdminStaffService : IAdminStaffService
    {
        private readonly IAdminStaffRepository _repository;
        private readonly IWebHostEnvironment _env;

        // 🔥 Role ID Constants (theo Seed Data)
        private const int ROLE_CASHIER = 1;
        private const int ROLE_STORE_MANAGER = 2;
        private const int ROLE_ADMIN_SYSTEM = 5;
        private const int ROLE_CUSTOMER = 6;

        // 🔥 Forbidden roles cho Store Manager
        private static readonly int[] FORBIDDEN_ROLES_FOR_STORE_MANAGER = { ROLE_STORE_MANAGER, ROLE_ADMIN_SYSTEM };

        public AdminStaffService(IAdminStaffRepository repository, IWebHostEnvironment env)
        {
            _repository = repository;
            _env = env;
        }

        // ==================== MASTER DATA (Thin Controller) ====================
        public async Task<StaffFormMasterDataVM> GetMasterDataForFormAsync(int? storeId)
        {
            var result = new StaffFormMasterDataVM
            {
                Roles = await _repository.GetRolesForDropdownAsync(storeId),
                ScopeTypes = await _repository.GetScopeTypesAsync(),
                IsStoreManager = storeId.HasValue
            };

            if (storeId.HasValue)
            {
                var store = await _repository.GetStoreByIdAsync(storeId.Value);
                result.Stores = null;
                result.CurrentStoreId = storeId.Value;
                result.CurrentStoreName = store?.Name ?? "Cửa hàng";
            }
            else
            {
                result.Stores = await _repository.GetActiveStoresAsync();
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
                var roleIds = s.Account?.AccountRoles?.Select(ar => ar.RoleId).ToList() ?? new List<int>();
                
                // 🔥 BUG 2: Frontend Security
                bool canEdit = true;
                if (isStoreManager && !isAdmin)
                {
                    if (roleIds.Any(r => r >= 2)) canEdit = false;
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
                Salary = staff.Salary,
                DateOfBirth = staff.DateOfBirth,
                StoreId = staff.StoreId,
                SelectedRoleIds = staff.Account?.AccountRoles?.Select(ar => ar.RoleId).ToList() ?? new List<int>(),
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
                foreach (var roleId in model.SelectedRoleIds)
                {
                    if (FORBIDDEN_ROLES_FOR_STORE_MANAGER.Contains(roleId))
                    {
                        return ServiceResult.Failure("Hành vi không hợp lệ! Bạn không có quyền cấp phát chức vụ này.");
                    }
                }
            }

            // === BƯỚC 4: 🔥 RULE 3 (Advanced) — Role-Scope Alignment ===
            if (model.ScopeTypeId == 1) // COUNTRY (HQ)
            {
                if (model.SelectedRoleIds.Contains(ROLE_CASHIER))
                {
                    return ServiceResult.Failure("Thu ngân (Cashier) bắt buộc phải gắn với Cửa hàng (STORE), không thể gán cho Trụ sở chính (HQ).");
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
                    return ServiceResult.Failure($"Mã số thuế/CCCD '{model.TaxCode}' đã tồn tại trong hệ thống.");
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
                TaxCode = model.TaxCode ?? "",
                Salary = model.Salary,
                DateOfBirth = model.DateOfBirth,
                StoreId = model.StoreId,
                Active = true,
                AvatarUrl = avatarUrl,
                CreatedAt = DateTime.Now
            };

            var accountRoles = model.SelectedRoleIds
                .Where(id => id != ROLE_CUSTOMER)
                .Select(id => new AccountRole { RoleId = id })
                .ToList();

            var staffScopes = new List<StaffScope>
            {
                new StaffScope
                {
                    ScopeTypeId = model.ScopeTypeId > 0 ? model.ScopeTypeId : 4,
                    ScopeRefId = model.ScopeRefId > 0 ? model.ScopeRefId : model.StoreId
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
            var (isAdmin, isStoreManager, currentStoreId) = ExtractUserClaims(user);

            var existingStaff = await _repository.GetStaffByIdAsync(model.StaffId);
            if (existingStaff == null)
                return ServiceResult.Failure("Không tìm thấy nhân viên.");

            // === 🔥 BUG 1: Bảo mật - Chặn Leo Quyền (Cross-check Target Staff) ===
            var targetRoleIds = existingStaff.Account?.AccountRoles?.Select(ar => ar.RoleId).ToList() ?? new List<int>();
            if (isStoreManager && !isAdmin)
            {
                // Nếu nhân viên đang bị chỉnh sửa có RoleId >= 2 (Store Manager, Ward Manager, Province Manager, Admin System)
                if (targetRoleIds.Any(r => r >= 2))
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

            // === 🔥 RULE 2 — Security Check ===
            if (isStoreManager && !isAdmin)
            {
                foreach (var roleId in model.SelectedRoleIds)
                {
                    if (FORBIDDEN_ROLES_FOR_STORE_MANAGER.Contains(roleId))
                    {
                        return ServiceResult.Failure("Hành vi không hợp lệ! Bạn không có quyền cấp phát chức vụ này.");
                    }
                }
            }

            // === 🔥 RULE 3 (Advanced) — Role-Scope Alignment ===
            if (model.ScopeTypeId == 1 && model.SelectedRoleIds.Contains(ROLE_CASHIER))
            {
                return ServiceResult.Failure("Thu ngân bắt buộc phải gắn với Cửa hàng.");
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
                    return ServiceResult.Failure($"Mã số thuế/CCCD '{model.TaxCode}' đã tồn tại.");
                }
            }

            // === Avatar Upload (xử lý trong Service) ===
            if (avatarFile != null && avatarFile.Length > 0)
            {
                existingStaff.AvatarUrl = await SaveAvatarAsync(avatarFile);
            }

            // === Update entities ===
            existingStaff.FullName = model.FullName;
            existingStaff.TaxCode = model.TaxCode ?? "";
            existingStaff.Salary = model.Salary;
            existingStaff.DateOfBirth = model.DateOfBirth;
            existingStaff.StoreId = model.StoreId;

            existingStaff.Account.Email = model.Email;

            // Password update (optional)
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                existingStaff.Account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            }

            var accountRoles = model.SelectedRoleIds
                .Where(id => id != ROLE_CUSTOMER)
                .Select(id => new AccountRole { RoleId = id })
                .ToList();

            var staffScopes = new List<StaffScope>
            {
                new StaffScope
                {
                    ScopeTypeId = model.ScopeTypeId > 0 ? model.ScopeTypeId : 4,
                    ScopeRefId = model.ScopeRefId > 0 ? model.ScopeRefId : model.StoreId
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

            // === 🔥 BUG 1: Bảo mật - Chặn Leo Quyền (Cross-check Target Staff) ===
            var targetRoleIds = staff.Account?.AccountRoles?.Select(ar => ar.RoleId).ToList() ?? new List<int>();
            if (isStoreManager && !isAdmin)
            {
                // Nếu nhân viên đang bị khóa/mở khóa có RoleId >= 2
                if (targetRoleIds.Any(r => r >= 2))
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

            var isAdmin = roles.Contains("Admin System");
            var isStoreManager = roles.Contains("Store Manager");

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
