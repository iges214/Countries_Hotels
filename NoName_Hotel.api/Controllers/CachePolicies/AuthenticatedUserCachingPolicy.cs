using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
namespace NoName_Hotel.api.Controllers.CachePolicies;
internal sealed class AuthenticatedUserCachingPolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cache = (HttpMethods.IsGet(context.HttpContext.Request.Method) ||
            HttpMethods.IsHead(context.HttpContext.Request.Method)) &&
            context.HttpContext.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(userId);
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = cache;
        context.AllowCacheStorage = cache;
        context.AllowLocking = true;
        context.CacheVaryByRules.QueryKeys = "*";
        if (cache) context.CacheVaryByRules.VaryByValues["UserId"] = userId!;
        return ValueTask.CompletedTask;
    }
    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        if (context.HttpContext.Response.StatusCode != StatusCodes.Status200OK ||
            !StringValues.IsNullOrEmpty(context.HttpContext.Response.Headers.SetCookie))
            context.AllowCacheStorage = false;
        return ValueTask.CompletedTask;
    }
}
