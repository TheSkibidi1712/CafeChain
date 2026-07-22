using System.Security.Claims;
using CafeChain.Application.Results;
using CafeChain.ViewModels.Admin.Staffs;
using Microsoft.AspNetCore.Http;

namespace CafeChain.Application.Interfaces.Admin.Staffs
{
    public interface IAdminStaffService
    {
        Task<StaffIndexPageVM> GetStaffIndexPageAsync(int page, int pageSize, int? storeId, string search, int? roleFilter, ClaimsPrincipal user);
        Task<StaffEditVM> GetStaffForEditAsync(int staffId, ClaimsPrincipal actor);
        Task<ServiceResult> CreateStaffAsync(StaffCreateVM model, ClaimsPrincipal user, IFormFile? file);
        Task<ServiceResult> UpdateStaffAsync(StaffEditVM model, ClaimsPrincipal user, IFormFile? file);
        Task<ServiceResult> ToggleStaffStatusAsync(int staffId, ClaimsPrincipal user);
        Task<ServiceResult> ResetPasswordAsync(int accountId, string newPassword, ClaimsPrincipal actor);

        // 🔥 Thin Controller: Master data cho dropdown forms
        Task<StaffFormMasterDataVM> GetMasterDataForFormAsync(ClaimsPrincipal user);

        // 🔥 Thin Controller: Dynamic data cho dropdown
        Task<IEnumerable<object>> GetScopeReferencesAsync(
            int scopeTypeId,
            ClaimsPrincipal actor,
            int? parentId = null);
        Task<IEnumerable<object>> GetDistrictsAsync(int provinceId);
        Task<IEnumerable<object>> GetWardsAsync(int districtId);

        // 🔥 Thin Controller: Avatar upload logic di chuyển xuống Service
    }
}
