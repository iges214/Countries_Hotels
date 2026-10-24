using Microsoft.Extensions.Diagnostics.HealthChecks;
using NoName_Hotel.Domain.Data;
namespace NoName_Hotel.api.Health;
public sealed class DatabaseHealthCheck(NoName_hotel_DbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        return await db.Database.CanConnectAsync(timeout.Token)
            ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database unavailable.");
    }
}
