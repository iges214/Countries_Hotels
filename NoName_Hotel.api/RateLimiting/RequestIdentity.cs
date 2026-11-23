using System.Security.Claims;

namespace NoName_Hotel.api.RateLimiting;

public static class RequestIdentity
{
    public static string? Subject(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true) return null;
        var client = context.User.FindFirstValue("client_id");
        if (!string.IsNullOrWhiteSpace(client)) return "client:" + client;
        var user = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(user) ? null : "user:" + user;
    }

    public static string ClientAddress(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
        return "ip:" + (address?.ToString() ?? "unknown");
    }

    public static string Partition(HttpContext context) => Subject(context) ?? ClientAddress(context);
}
