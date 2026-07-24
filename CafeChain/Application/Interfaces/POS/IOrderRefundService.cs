using CafeChain.Application.DTOs.POS;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.POS
{
    /// <summary>Issue #134 — full-order cash refund with inventory/COGS reversal.</summary>
    public interface IOrderRefundService
    {
        Task<ServiceResult<OrderRefundResultDto>> RequestFullRefundAsync(
            RequestFullOrderRefundDto dto,
            AdminActorContext actor);

        Task<ServiceResult<OrderRefundResultDto>> ConfirmCashRefundAsync(
            ConfirmCashRefundDto dto,
            AdminActorContext actor);
    }
}
