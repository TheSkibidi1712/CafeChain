using CafeChain.Application.DTOs.Admin.Profiles;
using CafeChain.Application.Results;
using CafeChain.ViewModels.Profile;

namespace CafeChain.Application.Interfaces.Admin.Profiles;

public interface IAdminProfileService
{
    Task<MyProfileVM?> GetMyProfileAsync(int accountId);
    Task<ServiceResult<AdminProfileUpdateResult>> UpdateMyProfileAsync(int accountId, UpdateProfileVM model);
}
