using CafeChain.Application.DTOs.Admin.Actor;

namespace CafeChain.Application.Interfaces.Security;

public enum OrderAccessDecision
{
    Allowed = 0,
    Forbidden = 1,
    NotFound = 2
}
public interface IOrderAccessAuthorizationService
{
    OrderAccessDecision AuthorizeAction(AdminActorContext actor, string action);

    Task<OrderAccessDecision> AuthorizeAsync(
        AdminActorContext actor,
        string action,
        int targetStoreId);
}
