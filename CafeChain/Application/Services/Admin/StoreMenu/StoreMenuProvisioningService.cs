using System.Data;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CafeChain.Application.Services.Admin.StoreMenu;

public sealed class StoreMenuProvisioningService : IStoreMenuProvisioningService
{
    private readonly AppDbContext _context;
    private readonly IStoreMenuBackfillPlanner _planner;
    private readonly IAdminPermissionService _permissions;

    public StoreMenuProvisioningService(
        AppDbContext context,
        IStoreMenuBackfillPlanner planner,
        IAdminPermissionService permissions)
    {
        _context = context;
        _planner = planner;
        _permissions = permissions;
    }

    public async Task<ServiceResult<StoreMenuProvisioningResultDto>> ProvisionMissingAsync(
        int storeId,
        int actorAccountId,
        int actorStaffId,
        CancellationToken cancellationToken = default)
    {
        var permission = await _permissions.HasPermissionAsync(
            actorAccountId,
            PermissionConstants.StoreMenuUpdate,
            storeId);
        if (!permission.IsSuccess || permission.Data?.Allowed != true)
        {
            return ServiceResult<StoreMenuProvisioningResultDto>.Failure(
                "Bạn không có quyền chuẩn bị menu cho cửa hàng này.",
                errorCode: "STORE_MENU_PROVISION_FORBIDDEN");
        }

        if (!await _context.Stores.AsNoTracking().AnyAsync(
                x => x.StoreId == storeId && x.Active,
                cancellationToken))
        {
            return ServiceResult<StoreMenuProvisioningResultDto>.Failure(
                "Không tìm thấy cửa hàng đang hoạt động.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            if (!await TryAcquireWriterLockAsync(storeId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<StoreMenuProvisioningResultDto>.Failure(
                    "Menu cửa hàng đang được cập nhật. Vui lòng thử lại.",
                    errorCode: "STORE_MENU_CHANGED_BY_ANOTHER_USER");
            }

            var candidates = await _planner.BuildStoreProvisioningPlanAsync(storeId, cancellationToken);
            if (candidates.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return ServiceResult<StoreMenuProvisioningResultDto>.Success(new()
                {
                    StoreId = storeId
                }, "Menu cửa hàng đã có đầy đủ SKU đang hoạt động.");
            }

            var candidateDrinkIds = candidates.Select(x => x.DrinkId).Distinct().ToArray();
            var existingDrinkIds = await _context.StoreDrinks
                .Where(x => x.StoreId == storeId && candidateDrinkIds.Contains(x.DrinkId))
                .Select(x => x.DrinkId)
                .ToListAsync(cancellationToken);
            var existingDrinkIdSet = existingDrinkIds.ToHashSet();
            var newStoreDrinks = candidateDrinkIds
                .Where(x => !existingDrinkIdSet.Contains(x))
                .Select(drinkId => new StoreDrink
                {
                    StoreId = storeId,
                    DrinkId = drinkId,
                    Active = false
                })
                .ToList();
            _context.StoreDrinks.AddRange(newStoreDrinks);

            var now = DateTime.UtcNow;
            _context.StoreMenuItems.AddRange(candidates.Select(candidate => new StoreMenuItem
            {
                StoreId = storeId,
                DrinkSizeId = candidate.DrinkSizeId,
                IsEnabled = false,
                DisplayOrder = candidate.DisplayOrder,
                Note = "Được chuẩn bị từ danh mục sản phẩm; cần đăng bán theo cửa hàng.",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }));
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<StoreMenuProvisioningResultDto>.Success(new()
            {
                StoreId = storeId,
                CreatedStoreDrinkCount = newStoreDrinks.Count,
                CreatedCount = candidates.Count
            }, $"Đã chuẩn bị {candidates.Count} SKU mới ở trạng thái bản nháp.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<StoreMenuProvisioningResultDto>.Failure(
                "Menu cửa hàng vừa được đồng bộ bởi thao tác khác. Vui lòng tải lại.",
                errorCode: "STORE_MENU_CHANGED_BY_ANOTHER_USER");
        }
    }

    private async Task<bool> TryAcquireWriterLockAsync(int storeId, CancellationToken cancellationToken)
    {
        if (!_context.Database.IsSqlServer())
            return true;

        var currentTransaction = _context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Store menu provisioning requires an active transaction.");
        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = currentTransaction.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 0;
            SELECT @result;
            """;
        var resource = command.CreateParameter();
        resource.ParameterName = "@resource";
        resource.Value = $"CafeChain:StoreMenuCatalog:{storeId}";
        command.Parameters.Add(resource);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null && Convert.ToInt32(result) >= 0;
    }
}
