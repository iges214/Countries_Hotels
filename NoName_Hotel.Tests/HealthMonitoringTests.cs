using System.Net;
using System.Net.Http.Json;
using HealthChecks.UI.Core.HostedService;
using HealthChecks.UI.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Xunit;

namespace NoName_Hotel.Tests;

public class HealthMonitoringTests
{
    [Theory]
    [InlineData("Production", null, false)]
    [InlineData("Development", null, false)]
    [InlineData("Staging", null, false)]
    [InlineData("Production", true, true)]
    [InlineData("Development", true, true)]
    [InlineData("Staging", true, true)]
    [InlineData("Production", false, false)]
    [InlineData("Development", false, false)]
    public async Task Background_health_monitoring_respects_packaged_defaults_and_explicit_overrides(
        string environment, bool? enabled, bool expected)
    {
        using var app = new TestApplication(environment: environment, healthChecksUiEnabled: enabled);
        var pollingServices = app.Services.GetServices<IHostedService>().Where(service =>
            service.GetType().Assembly.GetName().Name == "HealthChecks.UI");
        Assert.Equal(expected, pollingServices.Any());

        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(expected ? HttpStatusCode.OK : HttpStatusCode.NotFound,
            (await client.GetAsync("/healthchecks-ui")).StatusCode);
        if (expected)
        {
            using var scope = app.Services.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<HealthChecksDb>();
            Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", storage.Database.ProviderName);
            // Exercise the actual storage provider; registration alone missed the EF version mismatch.
            Assert.True(await storage.Configurations.AnyAsync());
        }
    }

    [Fact]
    public async Task Enabled_monitoring_collects_and_persists_a_health_report()
    {
        using var app = new TestApplication(environment: "Production", healthChecksUiEnabled: true);
        var requests = 0;
        using var configured = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureAll<HttpClientFactoryOptions>(options =>
                options.HttpMessageHandlerBuilderActions.Add(handler =>
                    handler.PrimaryHandler = new HealthyReportHandler(() => Interlocked.Increment(ref requests))))));
        using var scope = configured.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<HealthChecksDb>();
        // TestServer has no listening address. An absolute test endpoint exercises the real
        // collector, while only its outbound HTTP transport is replaced with a local response.
        storage.Configurations.RemoveRange(storage.Configurations);
        storage.Configurations.Add(new HealthCheckConfiguration
        {
            Id = 4746, Name = "Collector regression", Uri = "https://monitoring.example.test/healths"
        });
        await storage.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<IHealthCheckReportCollector>().Collect(CancellationToken.None);

        Assert.True(requests > 0);
        var report = await storage.Executions.SingleAsync(execution => execution.Name == "Collector regression");
        Assert.Equal("Healthy", report.Status.ToString());
        Assert.Equal("Healthy", Assert.Single(report.Entries).Status.ToString());
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

    private sealed class HealthyReportHandler(Action record) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            record();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    status = "Healthy", totalDuration = "00:00:00.001",
                    entries = new { database = new { status = "Healthy", duration = "00:00:00.001", data = new { } } }
                })
            });
        }
    }
}
