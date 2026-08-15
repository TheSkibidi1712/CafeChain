using CafeChain.Application.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace CafeChain.Areas.Admin.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AIImportRequestSizeLimitAttribute : Attribute, IFilterFactory, IOrderedFilter
{
    public bool IsReusable => false;
    public int Order => int.MinValue + 100;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        ActivatorUtilities.CreateInstance<AIImportRequestSizeLimitFilter>(serviceProvider);
}

public sealed class AIImportRequestSizeLimitFilter(IOptions<AIImportOptions> options) : IAsyncResourceFilter
{
    private const long MultipartOverheadBytes = 64 * 1024;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var requestLimit = checked(options.Value.MaxTotalUploadBytesPerSession
                                   + MultipartOverheadBytes * options.Value.MaxFilesPerSession);
        var bodyFeature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyFeature is { IsReadOnly: false }) bodyFeature.MaxRequestBodySize = requestLimit;

        if (!context.HttpContext.Request.HasFormContentType)
        {
            await next();
            return;
        }

        var formOptions = new FormOptions { MultipartBodyLengthLimit = requestLimit };
        context.HttpContext.Features.Set<IFormFeature>(new FormFeature(context.HttpContext.Request, formOptions));
        await next();
    }
}
