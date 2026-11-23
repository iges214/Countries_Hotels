using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace NoName_Hotel.api.RateLimiting;

public static class ApiRateLimiting
{
    public static IServiceCollection AddHotelRateLimiting(this IServiceCollection services) => services.AddRateLimiter(options =>
    {
        // Authentication stays IP-limited: caller-supplied emails cannot create fresh quotas.
        options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(RequestIdentity.ClientAddress(context), _ =>
            new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 5, QueueLimit = 0 }));
        options.AddPolicy("contact", context => RateLimitPartition.GetFixedWindowLimiter(RequestIdentity.Partition(context), _ =>
            new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 5, QueueLimit = 0 }));
        options.AddPolicy("fixed", context => RateLimitPartition.GetFixedWindowLimiter(RequestIdentity.Partition(context), _ =>
            new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(5), PermitLimit = 10, QueueLimit = 0 }));
        options.AddPolicy("perUser", context => RateLimitPartition.GetSlidingWindowLimiter(RequestIdentity.Partition(context), _ =>
            new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1), PermitLimit = 50, SegmentsPerWindow = 5, QueueLimit = 0
            }));
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(RequestIdentity.Partition(context), _ =>
                new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 200, QueueLimit = 0 }));
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : 1;
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status429TooManyRequests;
            response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            response.Headers.CacheControl = "private, no-store";
            await response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 429, Title = "Too many requests.", Detail = "Please wait before trying again.",
                Extensions = { ["retryAfter"] = seconds, ["traceId"] = context.HttpContext.TraceIdentifier }
            }, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
        };
    });
}
