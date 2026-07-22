using CafeChain.Application.Results;
using CafeChain.ViewModels.Admin.Stores;
using System.Security.Claims;

namespace CafeChain.Application.Interfaces.Admin.Stores;

public interface IAdminStoreService
{
    Task<IReadOnlyList<AdminStoreIndexItemVM>> GetAllAsync(ClaimsPrincipal actor);
    Task<AdminStoreFormDataVM> GetCreateFormAsync();
    Task<AdminStoreFormDataVM?> GetEditFormAsync(int storeId, ClaimsPrincipal actor);
    Task<ServiceResult> CreateAsync(AdminStoreFormVM model, ClaimsPrincipal actor);
    Task<ServiceResult> UpdateAsync(AdminStoreFormVM model, ClaimsPrincipal actor);
    Task<ServiceResult> ToggleStatusAsync(int storeId, ClaimsPrincipal actor);
}
