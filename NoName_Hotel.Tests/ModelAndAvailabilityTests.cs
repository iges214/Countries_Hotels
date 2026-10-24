using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoName_Hotel.Application.Mappers;
using NoName_Hotel.Application.Services;
using NoName_Hotel.Domain.Data;
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
}
