using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Staffs;

namespace CafeChain.Application.Interfaces.Admin.Staffs;

public interface IShiftOptimizationService
{
    Task<ShiftOptimizationSetupDto> GetSetupAsync(AdminActorContext actor, int storeId, CancellationToken cancellationToken = default);
    Task<ShiftOptimizationProposalDto> GenerateAsync(AdminActorContext actor, ShiftOptimizationInputDto input, CancellationToken cancellationToken = default);
    Task<ShiftOptimizationProposalDto> GetAsync(AdminActorContext actor, Guid proposalId, CancellationToken cancellationToken = default);
    Task ApplyAsync(AdminActorContext actor, ApplyScheduleProposalDto input, CancellationToken cancellationToken = default);
    Task SaveAvailabilityAsync(AdminActorContext actor, SaveAvailabilityRuleDto input, CancellationToken cancellationToken = default);
    Task SaveConstraintAsync(AdminActorContext actor, SaveWorkConstraintDto input, CancellationToken cancellationToken = default);
    Task SaveRequirementAsync(AdminActorContext actor, SaveStaffingRequirementDto input, CancellationToken cancellationToken = default);
    Task SaveTimeOffAsync(AdminActorContext actor, SaveTimeOffDto input, CancellationToken cancellationToken = default);
}
