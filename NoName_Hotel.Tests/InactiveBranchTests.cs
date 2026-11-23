using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Common.Enum;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Infrastructure.Data;
using Xunit;

namespace NoName_Hotel.Tests;

public sealed class InactiveBranchTests
{
    private static DateOnly Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Africa/Lagos"));

    [Theory]
    [InlineData("/api/v1/branches")]
    [InlineData("/api/v1/hotels")]
    [InlineData("/api/HotelCountroller")]
    public async Task Public_branch_discovery_excludes_inactive_branches(string root)
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        await AddSecondBranch(app);
        using var system = app.AuthenticatedClient(role: "Administrator");
        await Deactivate(system);
        using var anonymous = app.CreateClient();

        var branches = await anonymous.GetFromJsonAsync<PageResult<GetHotelDto>>(root);
        Assert.Equal(2, Assert.Single(branches!.Data).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(root + "/1")).StatusCode);
    }

    [Fact]
    public async Task Inactive_inventory_is_available_only_to_authorized_branch_administrators()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        await AddSecondBranch(app);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            db.HotelAdmins.Add(new HotelAdmin { HotelId = 1, UserId = "guest" });
            await db.SaveChangesAsync();
        }
        using var system = app.AuthenticatedClient(role: "Administrator");
        await Deactivate(system);
        using var assigned = app.AuthenticatedClient();
        using var unrelated = app.AuthenticatedClient("other");
        using var anonymous = app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/branches?includeInactive=true")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await unrelated.GetAsync("/api/v1/branches?includeInactive=true")).StatusCode);
        var managed = await assigned.GetFromJsonAsync<PageResult<GetHotelDto>>("/api/v1/branches?includeInactive=true");
        Assert.Equal(1, Assert.Single(managed!.Data).Id);
        Assert.False(managed.Data.Single().IsActive);
        var all = await system.GetFromJsonAsync<PageResult<GetHotelDto>>("/api/v1/branches?includeInactive=true");
        Assert.Equal(2, all!.Data.Count());
        Assert.Equal(HttpStatusCode.OK, (await assigned.GetAsync("/api/v1/branches/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unrelated.GetAsync("/api/v1/branches/1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await assigned.GetAsync("/api/v1/branches/1/admin/rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await assigned.GetAsync("/api/v1/branches/1/admin/offerings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await unrelated.GetAsync("/api/v1/branches/1/admin/rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await assigned.PutAsJsonAsync("/api/v1/branches/1/rooms/2", new
        {
            number = 101, description = "Updated while inactive", bedType = "King", amenities = "Wi-Fi",
            maxGuests = 2, sizeSquareMetres = 25, perNightRate = 125, isPublished = true, isOperational = true
        })).StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/branches")]
    [InlineData("/api/v1/hotels")]
    public async Task Public_room_and_offering_catalogues_reject_inactive_branches(string root)
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var system = app.AuthenticatedClient(role: "Administrator");
        await Deactivate(system);
        using var anonymous = app.CreateClient();
        foreach (var suffix in new[] { "/rooms", "/rooms/1", "/offerings" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(root + "/1" + suffix)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await system.GetAsync(root + "/1" + suffix)).StatusCode);
        }
    }

    [Theory]
    [InlineData("/api/v1/branches")]
    [InlineData("/api/v1/hotels")]
    [InlineData("/api/hotels")]
    public async Task New_bookings_cannot_bypass_branch_deactivation(string root)
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var system = app.AuthenticatedClient(role: "Administrator");
        await Deactivate(system);
        using var owner = app.AuthenticatedClient();
        var request = new CreateBookingDto(1, Today, Today.AddDays(2), 1, RoomIds: [1]);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(root + "/1/bookings", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await system.PostAsJsonAsync(root + "/1/bookings", request)).StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>().Bookings.ToListAsync());
    }

    [Fact]
    public async Task Owners_keep_history_editing_and_cancellation_after_deactivation()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var owner = app.AuthenticatedClient();
        var response = await owner.PostAsJsonAsync("/api/v1/branches/1/bookings",
            new CreateBookingDto(1, Today, Today.AddDays(2), 1, RoomIds: [1]));
        response.EnsureSuccessStatusCode();
        var booking = (await response.Content.ReadFromJsonAsync<GetBookingDto>())!;
        using var system = app.AuthenticatedClient(role: "Administrator");
        await Deactivate(system);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/v1/branches/1")).StatusCode);
        var history = await owner.GetFromJsonAsync<PageResult<GetBookingDto>>("/api/v1/account/bookings");
        Assert.Equal(booking.Id, Assert.Single(history!.Data).Id);
        var edited = await owner.PutAsJsonAsync($"/api/v1/branches/1/bookings/{booking.Id}",
            new UpdateBookingDto(Today, Today.AddDays(3), 1, RoomIds: [1]));
        edited.EnsureSuccessStatusCode();
        Assert.Equal(300m, (await edited.Content.ReadFromJsonAsync<GetBookingDto>())!.TotalPrice);
        Assert.Equal(HttpStatusCode.NoContent,
            (await system.PutAsync($"/api/v1/branches/1/bookings/{booking.Id}/admin/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.PostAsync($"/api/v1/branches/1/bookings/{booking.Id}/cancel", null)).StatusCode);
        using var scope = app.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>().Bookings.SingleAsync();
        Assert.Equal(BookingStatus.Cancelled, saved.Status);
        Assert.Equal(booking.Id, saved.Id);
    }

    [Fact]
    public async Task Country_discovery_hides_inactive_branches_and_reactivation_restores_booking()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var system = app.AuthenticatedClient(role: "Administrator");
        await Deactivate(system);
        using var owner = app.AuthenticatedClient();
        var country = await owner.GetFromJsonAsync<NoName_Hotel.Application.DTO.CountryDto.GetCountryHotelsDto>(
            "/api/v1/countries/1/hotels");
        Assert.Empty(country!.Hotels.Data);
        var countryDetails = await owner.GetFromJsonAsync<NoName_Hotel.Application.DTO.CountryDto.GetCountryDto>(
            "/api/v1/countries/1");
        Assert.Empty(countryDetails!.Hotels);
        Assert.Equal(HttpStatusCode.NoContent,
            (await system.PutAsJsonAsync("/api/v1/branches/1/active", new { isActive = true })).StatusCode);
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/v1/branches/1/rooms")).StatusCode);
        countryDetails = await owner.GetFromJsonAsync<NoName_Hotel.Application.DTO.CountryDto.GetCountryDto>(
            "/api/v1/countries/1");
        Assert.Single(countryDetails!.Hotels);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync("/api/v1/branches/1/bookings",
                new CreateBookingDto(1, Today, Today.AddDays(2), 1, RoomIds: [1]))).StatusCode);
    }

    [Fact]
    public async Task Assigned_staff_can_check_in_and_check_out_an_existing_stay_after_deactivation()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var owner = app.AuthenticatedClient();
        var response = await owner.PostAsJsonAsync("/api/v1/branches/1/bookings",
            new CreateBookingDto(1, Today, Today.AddDays(2), 1, RoomIds: [1]));
        response.EnsureSuccessStatusCode();
        var booking = (await response.Content.ReadFromJsonAsync<GetBookingDto>())!;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            db.HotelAdmins.Add(new HotelAdmin { HotelId = 1, UserId = "other" });
            await db.SaveChangesAsync();
        }
        using var system = app.AuthenticatedClient(role: "Administrator");
        await Deactivate(system);
        using var staff = app.AuthenticatedClient("other");
        Assert.Equal(HttpStatusCode.NoContent,
            (await staff.PutAsync($"/api/v1/branches/1/bookings/{booking.Id}/admin/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await staff.PostAsJsonAsync($"/api/v1/branches/1/admin/bookings/{booking.Id}/check-in",
                new { rooms = new[] { new { roomId = 1, guests = 1 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await staff.PostAsync($"/api/v1/branches/1/admin/bookings/{booking.Id}/check-out", null)).StatusCode);
    }

    private static async Task Deactivate(HttpClient system)
    {
        var response = await system.PutAsJsonAsync("/api/v1/branches/1/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task AddSecondBranch(TestApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
        db.Hotels.Add(new Hotel { Id = 2, Name = "Active branch", Address = "Lagos", CountryId = 1, Rating = 4 });
        await db.SaveChangesAsync();
    }
}
