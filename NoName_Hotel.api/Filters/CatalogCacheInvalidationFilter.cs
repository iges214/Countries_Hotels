using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OutputCaching;
using NoName_Hotel.Common.Constants;
namespace NoName_Hotel.api.Filters;
public sealed class CatalogCacheInvalidationFilter(IOutputCacheStore cache) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var completed = await next();
        var request = context.HttpContext.Request;
        if (completed.Exception is null && context.HttpContext.Response.StatusCode < 400 &&
            !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            await cache.EvictByTagAsync(CacheConstants.CatalogTag, CancellationToken.None);
    }
}
