using System.Security.Claims;
using CafeChain.Application.Results;
using CafeChain.ViewModels.Admin.Staffs;
using Microsoft.AspNetCore.Http;

namespace CafeChain.Application.Interfaces.Admin.Staffs
{
    public interface IAdminStaffService
    {
        Task<StaffIndexPageVM> GetStaffIndexPageAsync(int page, int pageSize, int? storeId, string search, int? roleFilter, ClaimsPrincipal user);
        Task<StaffEditVM> GetStaffForEditAsync(int staffId);
        Task<ServiceResult> CreateStaffAsync(StaffCreateVM model, ClaimsPrincipal user, IFormFile avatarFile);
        Task<ServiceResult> UpdateStaffAsync(StaffEditVM model, ClaimsPrincipal user, IFormFile avatarFile);
        Task<ServiceResult> ToggleStaffStatusAsync(int staffId, ClaimsPrincipal user);

        // 🔥 Thin Controller: Master data cho dropdown forms
        Task<StaffFormMasterDataVM> GetMasterDataForFormAsync(ClaimsPrincipal user);

        // 🔥 Thin Controller: Avatar upload logic di chuyển xuống Service
        Task<string> SaveAvatarAsync(IFormFile file);
    }
}
