using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using NoName_Hotel.Application.Mappers;
using NoName_Hotel.Application.Services;
using NoName_Hotel.Infrastructure.Data;
using NoName_Hotel.Domain.Entities.Models;
using Xunit;

namespace NoName_Hotel.Tests;
public class ModelAndAvailabilityTests
{
    private static readonly DateOnly Day = new(2027, 1, 1);
    private static Booking Reservation(int start, int end, int rooms) => new()
    {
        HotelId = 1, UserId = "guest", CheckIn = Day.AddDays(start), CheckOut = Day.AddDays(end), Rooms = rooms
    };

    [Fact]
    public void Availability_counts_each_night_and_allows_adjacent_bookings()
    {
        Assert.True(BookingAvailability.HasCapacity(2, 1, Day, Day.AddDays(4),
            [Reservation(0, 2, 1), Reservation(2, 4, 1)]));
        Assert.False(BookingAvailability.HasCapacity(2, 1, Day, Day.AddDays(4),
            [Reservation(0, 3, 1), Reservation(2, 4, 1)]));
        Assert.True(BookingAvailability.HasCapacity(2, 2, Day.AddDays(2), Day.AddDays(3),
            [Reservation(0, 2, 2)]));
        Assert.False(BookingAvailability.HasCapacity(0, 1, Day, Day.AddDays(1), []));
        Assert.False(BookingAvailability.HasCapacity(1, 0, Day, Day.AddDays(2), [Reservation(0, 2, 2)]));
    }

    [Fact]
    public void SqlServer_mappings_build_queries_without_custom_resolvers()
    {
        var config = new MapperConfiguration(c =>
        {
            c.AddProfile<HotelMappingProfile>();
            c.AddProfile<BookingMappingProfile>();
        }, NullLoggerFactory.Instance);
        config.AssertConfigurationIsValid();
        using var db = new NoName_hotel_DbContext(new DbContextOptionsBuilder<NoName_hotel_DbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=True;TrustServerCertificate=True").Options);
        var hotelSql = AutoMapper.QueryableExtensions.Extensions.ProjectTo<NoName_Hotel.Application.DTO.HotelDto.GetHotelDto>(
            db.Hotels, config).ToQueryString();
        var bookingSql = AutoMapper.QueryableExtensions.Extensions.ProjectTo<NoName_Hotel.Application.DTO.BookingsDto.GetBookingDto>(
            db.Bookings, config).ToQueryString();
        Assert.Contains("PerNightRate", hotelSql);
        Assert.Contains("Guest", bookingSql);
        Assert.True(db.Model.FindEntityType(typeof(Booking))!.FindProperty(nameof(Booking.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(db.Model.FindEntityType(typeof(ApiKey))!.GetIndexes(), i => i.IsUnique);
    }

    [Fact]
    public void Seeded_roles_do_not_generate_random_concurrency_stamps()
    {
        using var db = new NoName_hotel_DbContext(new DbContextOptionsBuilder<NoName_hotel_DbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=True;TrustServerCertificate=True").Options);
        var roles = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(IdentityRole))!.GetSeedData().ToList();
        Assert.Equal(3, roles.Count);
        Assert.All(roles, role =>
        {
            role.TryGetValue(nameof(IdentityRole.ConcurrencyStamp), out var stamp);
            Assert.Null(stamp);
        });
    }

    [Fact]
    public void AutoMapper_limits_recursion_through_record_constructor_parameters()
    {
        var configuration = new MapperConfiguration(c => c.CreateMap<RecursiveRecord, RecursiveRecord>().MaxDepth(8),
            NullLoggerFactory.Instance);
        var source = new RecursiveRecord(0, null);
        for (var i = 0; i < 100; i++) source = new RecursiveRecord(i, source);
        var mapped = configuration.CreateMapper().Map<RecursiveRecord>(source);
        var depth = 0;
        for (var current = mapped; current is not null; current = current.Next) depth++;
        Assert.InRange(depth, 1, 8);
    }

    private sealed record RecursiveRecord(int Value, RecursiveRecord? Next);

    [Fact]
    public void Infrastructure_preserves_migration_history_and_matches_the_current_model()
    {
        using var db = new NoName_hotel_DbContext(new DbContextOptionsBuilder<NoName_hotel_DbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=True;TrustServerCertificate=True").Options);
        var migrations = db.Database.GetMigrations().ToList();
        Assert.Equal(13, migrations.Count);
        Assert.Contains("20260923110457_FirstMigration", migrations);
        Assert.Contains("20261024162737_StabilizeBookingsAndIndexes", migrations);
        Assert.Contains("20261101124606_IgesBranchRoomsAndOfferings", migrations);
        Assert.Contains("20261103092932_AdministrationWorkspace", migrations);
        Assert.Contains("20261104043000_PopulateCountries", migrations);
        Assert.Contains("20261104070132_BrowserSessionRenewal", migrations);
        Assert.EndsWith("_ExplicitDefaultBranch", migrations.Last());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal("NoName_Hotel.Infrastructure", typeof(NoName_hotel_DbContext).Assembly.GetName().Name);
        var script = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().GenerateScript(
            "20261024162737_StabilizeBookingsAndIndexes", options: Microsoft.EntityFrameworkCore.Migrations.MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [Rooms]", script);
        Assert.Contains("CREATE TABLE [BookingRooms]", script);
        Assert.Contains("INSERT INTO dbo.Countries", script);
        Assert.DoesNotContain("DROP TABLE", script);
    }
}
