using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CafeChain.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DevelopmentOnlyLegacyEntryPointAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var environment = context.HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>();

        if (!environment.IsDevelopment())
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }
}
