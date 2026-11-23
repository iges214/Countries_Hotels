using Microsoft.AspNetCore.Authorization;

namespace NoName_Hotel.api.MiddleWare;

public static class PrivateApiResponses
{
    public static IApplicationBuilder UsePrivateApiResponses(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var sensitive = context.User.Identity?.IsAuthenticated == true || context.Request.Headers.ContainsKey("Authorization") ||
            context.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0 ||
            context.Request.Path.StartsWithSegments("/api/v1/auth") || context.Request.Path.StartsWithSegments("/api/v1/account");
        void SetPrivateHeaders()
        {
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
        }
        if (sensitive) SetPrivateHeaders();
        context.Response.OnStarting(() =>
        {
            if (sensitive || context.Response.StatusCode >= 400) SetPrivateHeaders();
            return Task.CompletedTask;
        });
        await next();
    });
}
