using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CafeChain.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class LegacyEntryPointGoneAttribute : Attribute, IAsyncActionFilter
{
    public Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        context.Result = new ObjectResult(new
        {
            code = "LEGACY_ENTRY_POINT_RETIRED",
            message = "Chức năng cũ đã ngừng hỗ trợ."
        })
        {
            StatusCode = StatusCodes.Status410Gone
        };

        return Task.CompletedTask;
    }
}
