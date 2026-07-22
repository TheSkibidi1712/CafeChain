using CafeChain.Application.Results;
using CafeChain.ViewModels.Admin.Staffs;

namespace CafeChain.Application.Interfaces.Admin.Staffs;

public interface IAdminStaffShiftService
{
    Task<StaffShiftManagementVM> GetPageAsync(
        int storeId,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<StaffShiftStoreOptionVM> stores,
        IReadOnlySet<string> effectivePermissions,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> AssignAsync(int storeId, int actorStaffId, AssignStaffShiftRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAssignmentAsync(int storeId, int actorStaffId, UpdateStaffShiftRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> CancelAsync(int storeId, int actorStaffId, CancelStaffShiftRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> CreateTemplateAsync(int storeId, int actorStaffId, CreateShiftTemplateRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateTemplateAsync(int storeId, int actorStaffId, UpdateShiftTemplateRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> ToggleTemplateAsync(int storeId, int actorStaffId, ToggleShiftTemplateRequest request, CancellationToken cancellationToken = default);
}
