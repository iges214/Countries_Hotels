using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Common.Enum;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Infrastructure.Data;
using Xunit;

namespace NoName_Hotel.Tests;

public class GuestAccountTests
{
    [Fact]
    public async Task Guest_booking_list_spans_branches_and_only_exposes_their_own_stays()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            db.Hotels.Add(new Hotel { Id = 2, Name = "Second branch", CountryId = 1, Rating = 4 });
            db.Bookings.AddRange(
                new Booking { Id = 31, UserId = "guest", HotelId = 1, CheckIn = today, CheckOut = today.AddDays(2), Guest = 2, Status = BookingStatus.Confirmed, ActualCheckInAt = now },
                new Booking { Id = 32, UserId = "guest", HotelId = 2, CheckIn = today.AddDays(-3), CheckOut = today, Guest = 1, Status = BookingStatus.Confirmed, ActualCheckInAt = now.AddDays(-3), ActualCheckOutAt = now },
                new Booking { Id = 33, UserId = "other", HotelId = 1, CheckIn = today, CheckOut = today.AddDays(2), Guest = 1 });
            await db.SaveChangesAsync();
        }

        using var guest = app.AuthenticatedClient();
        var bookings = (await guest.GetFromJsonAsync<PageResult<GetBookingDto>>("/api/v1/account/bookings"))!.Data;
        Assert.Equal(new[] { 31, 32 }, bookings.Select(b => b.Id).Order().ToArray());
        Assert.True(bookings.Single(b => b.Id == 31).IsCheckedIn);
        Assert.True(bookings.Single(b => b.Id == 32).IsCheckedOut);
        Assert.Equal(HttpStatusCode.Conflict, (await guest.PostAsync("/api/v1/branches/1/bookings/31/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await guest.PutAsJsonAsync("/api/v1/branches/1/bookings/31",
            new UpdateBookingDto(today, today.AddDays(3), 2, 1, [1]))).StatusCode);

        using var anonymous = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/account/bookings")).StatusCode);
    }
}
