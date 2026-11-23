using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NoName_Hotel.Common.JWT;
using NoName_Hotel.Infrastructure.Data;
using NoName_Hotel.Domain.Entities.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NoName_Hotel.Tests;

public sealed class TestApplication : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection;
    private readonly string? fileConnectionString;
    private readonly TimeProvider? clock;
    private readonly string environment;
    private readonly bool? healthChecksUiEnabled;

    public TestApplication(TimeProvider? clock = null, string environment = "Development", bool? healthChecksUiEnabled = null, string? databasePath = null)
    {
        fileConnectionString = databasePath is null ? null : new SqliteConnectionStringBuilder
        {
            DataSource = databasePath, DefaultTimeout = 30
        }.ToString();
        connection = new SqliteConnection(fileConnectionString ?? "Data Source=:memory:");
        this.clock = clock;
        this.environment = environment;
        this.healthChecksUiEnabled = healthChecksUiEnabled;
        connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        if (healthChecksUiEnabled.HasValue)
            builder.UseSetting("Monitoring:EnableHealthChecksUI", healthChecksUiEnabled.Value.ToString());
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:key"] = "IGES-Tests-Only-Signing-Key-At-Least-32-Bytes!"
        }));
        builder.ConfigureServices(services =>
        {
            if (clock is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
            }
            services.RemoveAll<NoName_hotel_DbContext>();
            services.AddScoped<NoName_hotel_DbContext>(_ =>
            {
                var options = new DbContextOptionsBuilder<NoName_hotel_DbContext>();
                if (fileConnectionString is null) options.UseSqlite(connection);
                else options.UseSqlite(fileConnectionString);
                return new TestDbContext(options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options);
            });
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
        db.Rooms.AddRange(SeedRoom(1, 1, 100), SeedRoom(2, 1, 101));
        await db.SaveChangesAsync();
    }

    public static Room SeedRoom(int id, int branch, int number) => new()
    {
        Id = id, HotelId = branch, Number = number, Description = "Spacious room with a private bathroom",
        BedType = RoomNumbering.IsTwinRoom(number) ? "Twin" : "King", Amenities = "Wi-Fi, air conditioning", MaxGuests = 2, SizeSquareMetres = 25,
        PerNightRate = 100, IsPublished = true,
        Photos = [new() { Kind = RoomPhotoKind.Room, Url = "https://example.com/room.jpg" },
            new() { Kind = RoomPhotoKind.Bathroom, Url = "https://example.com/bathroom.jpg" }]
    };

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
            // SQLite has no SQL Server rowversion generator. Generate equivalent test
            // versions below while retaining EF's real concurrency predicate.
            builder.Entity<Booking>().Property(b => b.RowVersion).IsConcurrencyToken()
                .ValueGeneratedNever();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ChangeTracker.DetectChanges();
            foreach (var entry in ChangeTracker.Entries<Booking>().Where(entry =>
                entry.State is EntityState.Added or EntityState.Modified))
                entry.Entity.RowVersion = System.Security.Cryptography.RandomNumberGenerator.GetBytes(8);
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
