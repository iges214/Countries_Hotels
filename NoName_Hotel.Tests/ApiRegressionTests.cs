using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoName_Hotel.Application.DTO.AuthDto;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Application.DTO.CountryDto;
using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Domain.Data;
using Xunit;

namespace NoName_Hotel.Tests;

public class ApiRegressionTests
{
    private static DateOnly Start => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);

    [Fact]
    public async Task Ordinary_names_register_and_privileged_registration_is_rejected()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var client = app.AuthenticatedClient();
        var normal = new RegisterUserDto
        {
            Email = "new@example.com", Password = "ValidPassword1!", FirstName = "Ada", LastName = "Obi"
        };
        var registered = await client.PostAsJsonAsync("/api/v1/auth/register", normal);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        Assert.Equal("User", (await registered.Content.ReadFromJsonAsync<RegisteredUserDto>())!.Role);
        normal.Email = "admin@example.com";
        normal.Role = "Administrator";
        var escalation = await client.PostAsJsonAsync("/api/v1/auth/register", normal);
        Assert.Equal(HttpStatusCode.BadRequest, escalation.StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Email == "admin@example.com"));
    }

    [Fact]
    public async Task Booking_total_guests_rooms_and_status_changes_are_persisted()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var guest = app.AuthenticatedClient();
        var response = await guest.PostAsJsonAsync("/api/v1/hotels/1/bookings",
            new CreateBookingDto(1, Start, Start.AddDays(3), 3, 2));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var booking = (await response.Content.ReadFromJsonAsync<GetBookingDto>())!;
        Assert.Equal(600m, booking.TotalPrice);
        Assert.Equal(3, booking.Guests);
        Assert.Equal(2, booking.Rooms);
        Assert.Equal("Test Hotel", booking.HotelName);
        using var admin = app.AuthenticatedClient("guest", "Administrator");
        var confirmed = await admin.PutAsync($"/api/v1/hotels/1/bookings/{booking.Id}/admin/confirm", null);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        var updated = await guest.PutAsJsonAsync($"/api/v1/hotels/1/bookings/{booking.Id}",
            new UpdateBookingDto(Start, Start.AddDays(2), 2, 1));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var cancelled = await guest.PostAsync($"/api/v1/hotels/1/bookings/{booking.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        using var scope = app.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>().Bookings.SingleAsync();
        Assert.Equal(NoName_Hotel.Common.Enum.BookingStatus.Cancelled, saved.Status);
        Assert.Equal(200m, saved.Totalprice);
        Assert.Equal(2, saved.Guest);
        Assert.Equal(1, saved.Rooms);
    }

    [Fact]
    public async Task Capacity_route_mismatch_and_ownership_are_enforced()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var guest = app.AuthenticatedClient();
        var mismatch = await guest.PostAsJsonAsync("/api/hotels/2/bookings",
            new CreateBookingDto(1, Start, Start.AddDays(2), 1));
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        var first = await guest.PostAsJsonAsync("/api/hotels/1/bookings",
            new CreateBookingDto(1, Start, Start.AddDays(2), 2, 2));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var booking = (await first.Content.ReadFromJsonAsync<GetBookingDto>())!;
        using var other = app.AuthenticatedClient("other");
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsJsonAsync("/api/hotels/1/bookings",
            new CreateBookingDto(1, Start, Start.AddDays(2), 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync(
            $"/api/hotels/1/bookings/{booking.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsync(
            $"/api/hotels/1/bookings/{booking.Id}/admin/confirm", null)).StatusCode);
    }

    [Fact]
    public async Task Patch_validates_input_and_invalidates_cached_country_reads()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var admin = app.AuthenticatedClient("guest", "Administrator");
        Assert.Equal("Nigeria", (await admin.GetFromJsonAsync<GetCountryDto>("/api/v1/countries/1"))!.Name);
        async Task<HttpResponseMessage> Patch(string body) => await admin.PatchAsync("/api/v1/countries/1",
            new StringContent(body, Encoding.UTF8, "application/json-patch+json"));
        Assert.Equal(HttpStatusCode.BadRequest, (await Patch(
            "[{\"op\":\"remove\",\"path\":\"/name\"}]")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Patch(
            "[{\"op\":\"replace\",\"path\":\"/id\",\"value\":2}]")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Patch(
            "[{\"op\":\"test\",\"path\":\"/name\",\"value\":\"Wrong\"}]")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Patch(
            "[{\"op\":\"replace\",\"path\":\"/name\",\"value\":\"Updated Nigeria\"}]")).StatusCode);
        Assert.Equal("Updated Nigeria", (await admin.GetFromJsonAsync<GetCountryDto>("/api/v1/countries/1"))!.Name);
    }

    [Fact]
    public async Task Hotel_prices_country_names_search_and_inventory_are_exposed()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var admin = app.AuthenticatedClient("guest", "Administrator");
        var response = await admin.PostAsJsonAsync("/api/v1/hotels", new CreateHotelDto
        {
            Name = "Second Hotel", Address = "Lagos", CountryId = 1, Rating = 3,
            PerNightRate = 125.50m, TotalRooms = 3
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var hotel = (await response.Content.ReadFromJsonAsync<GetHotelDto>())!;
        Assert.Equal("Nigeria", hotel.CountryName);
        Assert.Equal(125.50m, hotel.PerNightRate);
        var read = await admin.GetFromJsonAsync<GetHotelDto>($"/api/v1/hotels/{hotel.Id}");
        Assert.Equal(hotel, read);
        var location = await admin.GetAsync("/api/v1/hotels?Location=Lagos");
        Assert.Equal(HttpStatusCode.OK, location.StatusCode);
        var search = await admin.GetStringAsync("/api/v1/hotels?Search=Second");
        Assert.Contains("Second Hotel", search);
        Assert.DoesNotContain("Test Hotel", search);
    }

    [Fact]
    public async Task Authentication_rate_limiting_and_liveness_are_active()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/countries")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/v1/auth/login", new { Email = "missing@example.com", Password = "Wrong1!" });
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { Email = "missing@example.com", Password = "Wrong1!" })).StatusCode);
    }

    [Fact]
    public async Task Unexpected_database_errors_are_sanitized_and_have_a_trace_id()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>()
                .Database.ExecuteSqlRawAsync("DROP TABLE Countries");
        using var client = app.AuthenticatedClient();
        var response = await client.GetAsync("/api/v1/countries/1");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("traceId", body);
        Assert.DoesNotContain("SQLite", body);
        Assert.DoesNotContain("SELECT", body);
    }

    [Fact]
    public async Task Repeated_bad_passwords_lock_the_account_even_outside_the_http_rate_limit()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<NoName_Hotel.Application.Contracts.AuthContracts.IUserServices>();
        Assert.True((await service.RegisterAsync(new RegisterUserDto
        {
            Email = "locked@example.com", Password = "ValidPassword1!", FirstName = "Ada", LastName = "Obi"
        })).IsSuccess);
        for (var i = 0; i < 5; i++)
            Assert.False((await service.LoginUserAsync(new LoginUserDto
                { Email = "locked@example.com", Password = "Wrong1!" })).IsSuccess);
        Assert.False((await service.LoginUserAsync(new LoginUserDto
            { Email = "locked@example.com", Password = "ValidPassword1!" })).IsSuccess);
    }
}
