using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Stores;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.Stores;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.Stores;
using Microsoft.AspNetCore.Mvc.Rendering;
using CafeChain.Application.Interfaces.Security;
using System.Security.Claims;

namespace CafeChain.Application.Services.Admin.Stores;

public sealed class AdminStoreService : IAdminStoreService
{
    private readonly IAdminStoreRepository _repository;
    private readonly IScopeAuthorizationService _scopeAuthorizationService;
    public AdminStoreService(
        IAdminStoreRepository repository,
        IScopeAuthorizationService scopeAuthorizationService)
    {
        _repository = repository;
        _scopeAuthorizationService = scopeAuthorizationService;
    }

    public async Task<IReadOnlyList<AdminStoreIndexItemVM>> GetAllAsync(ClaimsPrincipal actor)
    {
        var stores = await _repository.GetAllAsync();
        if (!IsGlobalActor(actor))
        {
            var allowed = (await _scopeAuthorizationService.GetAllowedStoresAsync(GetActorStaffId(actor)))
                .Select(x => x.StoreId).ToHashSet();
            stores = stores.Where(x => allowed.Contains(x.StoreId)).ToList();
        }
        return stores.Select(x => new AdminStoreIndexItemVM
        {
            StoreId = x.StoreId,
            Name = x.Name,
            Address = x.Address,
            Phone = x.Phone,
            Active = x.Active,
            CreatedAt = x.CreatedAt,
            ProvinceName = x.Province?.Name,
            WardName = x.Ward?.Name,
            Latitude = x.Latitude,
            Longitude = x.Longitude,
            StaffCount = x.Staffs.Count,
            ManagerNames = x.Staffs
                .Where(s => s.Active && s.Account.Active
                    && s.Account.AccountRoles.Any(r => r.Role.Active && r.Role.Name == RoleConstants.StoreManager))
                .Select(s => s.FullName).OrderBy(n => n).ToList()
        }).ToList();
    }

    public async Task<AdminStoreFormDataVM> GetCreateFormAsync() => new()
    {
        Store = new AdminStoreFormVM { Active = true },
        Provinces = await GetProvinceOptionsAsync()
    };

    public async Task<AdminStoreFormDataVM?> GetEditFormAsync(int storeId, ClaimsPrincipal actor)
    {
        await EnsureStoreAccessAsync(storeId, actor);
        var store = await _repository.GetTrackedAsync(storeId);
        if (store == null) return null;
        return new AdminStoreFormDataVM
        {
            Store = Map(store),
            Provinces = await GetProvinceOptionsAsync(store.ProvinceId)
        };
    }

    public async Task<ServiceResult> CreateAsync(AdminStoreFormVM model, ClaimsPrincipal actor)
    {
        if (!IsGlobalActor(actor))
            return ServiceResult.Failure("Bạn không có quyền tạo cửa hàng.");
        var validation = await ValidateAsync(model);
        if (validation != null) return validation;
        var now = DateTime.UtcNow;
        var store = new Store
        {
            Name = model.Name.Trim(), Address = model.Address.Trim(), Phone = model.Phone.Trim(),
            ProvinceId = model.ProvinceId, WardId = model.WardId,
            Latitude = model.Latitude, Longitude = model.Longitude, Active = true, CreatedAt = now,
            InventoryWriterConfiguration = new StoreInventoryWriterConfiguration
            {
                WriterMode = InventoryWriterMode.LegacyRecipe,
                HasEverActivatedPreparedItem = false,
                CreatedAt = now,
                UpdatedAt = now
            }
        };
        await _repository.AddAsync(store);
        // Store and writer configuration are persisted by one atomic SaveChanges.
        await _repository.SaveChangesAsync();
        return ServiceResult.Success("Đã tạo cửa hàng.");
    }

    public async Task<ServiceResult> UpdateAsync(AdminStoreFormVM model, ClaimsPrincipal actor)
    {
        await EnsureStoreAccessAsync(model.StoreId, actor);
        var validation = await ValidateAsync(model);
        if (validation != null) return validation;
        var store = await _repository.GetTrackedAsync(model.StoreId);
        if (store == null) return ServiceResult.Failure("Không tìm thấy cửa hàng.");
        store.Name = model.Name.Trim();
        store.Address = model.Address.Trim();
        store.Phone = model.Phone.Trim();
        store.ProvinceId = model.ProvinceId;
        store.WardId = model.WardId;
        store.Latitude = model.Latitude;
        store.Longitude = model.Longitude;
        await _repository.SaveChangesAsync();
        return ServiceResult.Success("Đã cập nhật cửa hàng.");
    }

    public async Task<ServiceResult> ToggleStatusAsync(int storeId, ClaimsPrincipal actor)
    {
        await EnsureStoreAccessAsync(storeId, actor);
        var store = await _repository.GetTrackedAsync(storeId);
        if (store == null) return ServiceResult.Failure("Không tìm thấy cửa hàng.");
        store.Active = !store.Active;
        await _repository.SaveChangesAsync();
        return ServiceResult.Success(store.Active ? "Đã kích hoạt cửa hàng." : "Đã ngừng hoạt động cửa hàng.");
    }

    private async Task<ServiceResult?> ValidateAsync(AdminStoreFormVM model)
    {
        if (!model.ProvinceId.HasValue || !model.WardId.HasValue)
            return ServiceResult.Failure("Vui lòng chọn đầy đủ Tỉnh/Thành phố và Phường/Xã/Đặc khu.");
        if (!await _repository.IsLocationHierarchyValidAsync(
                model.ProvinceId.Value, model.WardId.Value))
            return ServiceResult.Failure("Phường/Xã/Đặc khu không thuộc Tỉnh/Thành phố đã chọn.");
        return null;
    }

    private async Task<IReadOnlyList<SelectListItem>> GetProvinceOptionsAsync(int? selected = null) =>
        (await _repository.GetProvincesAsync()).Select(x => new SelectListItem(
            x.Name, x.ProvinceId.ToString(), x.ProvinceId == selected)).ToList();

    private static AdminStoreFormVM Map(Store x) => new()
    {
        StoreId = x.StoreId, Name = x.Name, Address = x.Address, Phone = x.Phone,
        ProvinceId = x.ProvinceId, WardId = x.WardId,
        Latitude = x.Latitude, Longitude = x.Longitude, Active = x.Active, CreatedAt = x.CreatedAt
    };

    private static bool IsGlobalActor(ClaimsPrincipal actor) =>
        actor.IsInRole(RoleConstants.BusinessOwner) || actor.IsInRole(RoleConstants.SystemAdmin);

    private static int GetActorStaffId(ClaimsPrincipal actor) =>
        int.TryParse(actor.FindFirstValue("StaffId"), out var staffId) ? staffId : 0;

    private async Task EnsureStoreAccessAsync(int storeId, ClaimsPrincipal actor)
    {
        if (IsGlobalActor(actor)) return;
        if (!await _scopeAuthorizationService.CheckIfStoreIsWithinManagerScopeAsync(
                GetActorStaffId(actor), storeId))
            throw new UnauthorizedAccessException("Cửa hàng nằm ngoài phạm vi được cấp.");
    }
}
