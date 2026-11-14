using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace NoName_Hotel.Tests;

public class HealthMonitoringTests
{
    [Theory]
    [InlineData("Production", null, false)]
    [InlineData("Development", null, true)]
    [InlineData("Production", true, true)]
    [InlineData("Development", false, false)]
    public void Background_health_monitoring_is_opt_in_outside_development(
        string environment, bool? enabled, bool expected)
    {
        using var app = new TestApplication(environment: environment, healthChecksUiEnabled: enabled);
        var pollingServices = app.Services.GetServices<IHostedService>().Where(service =>
            service.GetType().Assembly.GetName().Name == "HealthChecks.UI");
        Assert.Equal(expected, pollingServices.Any());
    }

    [Fact]
    public async Task Production_liveness_does_not_probe_the_database_but_readiness_does()
    {
        using var app = new TestApplication(environment: "Production");
        var databaseProbes = 0;
        using var configured = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
                options.Registrations.Add(new HealthCheckRegistration("database",
                    new RecordingCheck(() => Interlocked.Increment(ref databaseProbes)),
                    HealthStatus.Unhealthy, ["db"]));
            })));
        using var client = configured.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(0, databaseProbes);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(1, databaseProbes);
    }

    private sealed class RecordingCheck(Action record) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            record();
            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}
