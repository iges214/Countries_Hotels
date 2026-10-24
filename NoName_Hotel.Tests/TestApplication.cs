using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NoName_Hotel.Common.JWT;
using NoName_Hotel.Domain.Data;
using NoName_Hotel.Domain.Entities.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NoName_Hotel.Tests;

public sealed class TestApplication : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public TestApplication() => connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<NoName_hotel_DbContext>();
            services.AddScoped<NoName_hotel_DbContext>(_ => new TestDbContext(
                new DbContextOptionsBuilder<NoName_hotel_DbContext>().UseSqlite(connection)
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options));
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
        await db.Database.EnsureCreatedAsync();
        db.Users.AddRange(
            new ApplicationUser { Id = "guest", UserName = "guest@example.com" },
            new ApplicationUser { Id = "other", UserName = "other@example.com" });
        db.Countries.Add(new Country { CountryId = 1, Name = "Nigeria", ShortName = "NG" });
        db.Hotels.Add(new Hotel
        {
            Id = 1, Name = "Test Hotel", Address = "Abuja", CountryId = 1, Rating = 4,
            PerNightRate = 100m, TotalRooms = 2
        });
        await db.SaveChangesAsync();
    }

    public HttpClient AuthenticatedClient(string id = "guest", string role = "User")
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var settings = Services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, id), new Claim(ClaimTypes.Role, role)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(settings.key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }

    private sealed class TestDbContext(DbContextOptions<NoName_hotel_DbContext> options) : NoName_hotel_DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // SQLite has no SQL Server rowversion generator. SQL-specific concurrency is checked separately.
            builder.Entity<Booking>().Property(b => b.RowVersion).IsConcurrencyToken(false)
                .ValueGeneratedNever();
        }
    }
}
