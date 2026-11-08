using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Application.DTO.Rooms;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Infrastructure.Data;
using Xunit;

namespace NoName_Hotel.Tests;

public class RoomLayoutTests
{
    private static DateOnly Start => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
    private static string Stay => $"checkIn={Start:yyyy-MM-dd}&checkOut={Start.AddDays(2):yyyy-MM-dd}";
    private static SaveRoomDto Details(int number, string bedType = "King", bool honeymoon = false, int guests = 2) => new()
    {
        Number = number, BedType = bedType, IsHoneymoonSuite = honeymoon, MaxGuests = guests,
        Description = "Bedroom and private bathroom", Amenities = "Wi-Fi, air conditioning",
        SizeSquareMetres = 30, PerNightRate = 100
    };

    [Theory]
    [InlineData(159, "King", false, 1, HttpStatusCode.OK)]
    [InlineData(160, "Twin", false, 2, HttpStatusCode.OK)]
    [InlineData(200, "twin", false, 2, HttpStatusCode.OK)]
    [InlineData(160, "King", false, 2, HttpStatusCode.BadRequest)]
    [InlineData(160, "Twin", false, 1, HttpStatusCode.BadRequest)]
    [InlineData(210, "Twin", false, 2, HttpStatusCode.BadRequest)]
    [InlineData(350, "King", true, 2, HttpStatusCode.OK)]
    [InlineData(400, "King", true, 2, HttpStatusCode.OK)]
    [InlineData(480, "King", true, 2, HttpStatusCode.OK)]
    [InlineData(500, "King", true, 2, HttpStatusCode.OK)]
    [InlineData(300, "King", true, 2, HttpStatusCode.BadRequest)]
    [InlineData(200, "Twin", true, 2, HttpStatusCode.BadRequest)]
    [InlineData(350, "King", true, 1, HttpStatusCode.BadRequest)]
    public async Task Administrators_configure_beds_and_honeymoon_suites_on_the_correct_floors(
        int number, string bedType, bool honeymoon, int guests, HttpStatusCode expected)
    {
        using var app = new TestApplication(); await app.SeedAsync();
        using var admin = app.AuthenticatedClient("guest", "Administrator");
        var response = await admin.PostAsJsonAsync("/api/v1/branches/1/rooms", Details(number, bedType, honeymoon, guests));
        Assert.Equal(expected, response.StatusCode);
        if (expected != HttpStatusCode.OK) return;
        var saved = (await response.Content.ReadFromJsonAsync<RoomDto>())!;
        Assert.Equal(number, saved.Number);
        Assert.Equal(honeymoon, saved.IsHoneymoonSuite);
        Assert.Equal(number is >= 160 and <= 200, saved.IsTwinRoom);
        if (saved.IsTwinRoom) { Assert.Equal("Twin", saved.BedType); Assert.Equal(1, saved.Floor); }
        if (honeymoon) Assert.Contains(saved.Floor, new[] { 3, 4 });
    }

    private static async Task SeedLayouts(TestApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
        var vvip = TestApplication.SeedRoom(5, 1, 350); vvip.IsHoneymoonSuite = true;
        var platinum = TestApplication.SeedRoom(6, 1, 480); platinum.IsHoneymoonSuite = true; platinum.PerNightRate = 1000;
        db.Rooms.AddRange(TestApplication.SeedRoom(3, 1, 160), TestApplication.SeedRoom(4, 1, 200),
            vvip, platinum, TestApplication.SeedRoom(7, 1, 351));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Public_search_finds_twin_rooms_and_both_honeymoon_tiers_with_normal_booking_prices_and_availability()
    {
        using var app = new TestApplication(); await app.SeedAsync(); await SeedLayouts(app);
        using var publicClient = app.CreateClient(new() { BaseAddress = new("https://localhost") });
        var twins = (await publicClient.GetFromJsonAsync<PageResult<RoomDto>>($"/api/v1/branches/1/rooms?{Stay}&isTwinRoom=true"))!;
        Assert.Equal(new[] { 160, 200 }, twins.Data.Select(r => r.Number));
        Assert.All(twins.Data, r => { Assert.Equal(1, r.Floor); Assert.Equal("Normal", r.Category); Assert.Equal("Twin", r.BedType); });
        var suites = (await publicClient.GetFromJsonAsync<PageResult<RoomDto>>($"/api/v1/branches/1/rooms?{Stay}&isHoneymoonSuite=true"))!;
        Assert.Equal(new[] { 350, 480 }, suites.Data.Select(r => r.Number));
        var platinumOnly = (await publicClient.GetFromJsonAsync<PageResult<RoomDto>>($"/api/v1/branches/1/rooms?{Stay}&isHoneymoonSuite=true&category=Platinum"))!;
        Assert.Equal(480, Assert.Single(platinumOnly.Data).Number);
        var standardVvip = (await publicClient.GetFromJsonAsync<PageResult<RoomDto>>($"/api/v1/branches/1/rooms?{Stay}&isHoneymoonSuite=false&category=Vvip"))!;
        Assert.Equal(351, Assert.Single(standardVvip.Data).Number);

        using var guest = app.AuthenticatedClient();
        var response = await guest.PostAsJsonAsync("/api/v1/branches/1/bookings", new CreateBookingDto(1, Start, Start.AddDays(2), 2, RoomIds: [6]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var booking = (await response.Content.ReadFromJsonAsync<GetBookingDto>())!;
        Assert.Equal(2000m, booking.TotalPrice);
        suites = (await publicClient.GetFromJsonAsync<PageResult<RoomDto>>($"/api/v1/branches/1/rooms?{Stay}&isHoneymoonSuite=true"))!;
        Assert.Equal(350, Assert.Single(suites.Data).Number);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"/api/v1/branches/1/rooms/6?{Stay}")).StatusCode);
        await guest.PostAsync($"/api/v1/branches/1/bookings/{booking.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync($"/api/v1/branches/1/rooms/6?{Stay}")).StatusCode);
    }

    [Fact]
    public async Task Reserved_suite_cannot_lose_its_honeymoon_designation_or_change_its_bed_layout()
    {
        using var app = new TestApplication(); await app.SeedAsync(); await SeedLayouts(app);
        using var guest = app.AuthenticatedClient();
        Assert.Equal(HttpStatusCode.OK, (await guest.PostAsJsonAsync("/api/v1/branches/1/bookings", new CreateBookingDto(1, Start, Start.AddDays(2), 2, RoomIds: [5]))).StatusCode);
        using var admin = app.AuthenticatedClient("guest", "Administrator");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync("/api/v1/branches/1/rooms/5", Details(350))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync("/api/v1/branches/1/rooms/5", Details(350, "Double", true))).StatusCode);
    }

    [Theory]
    [InlineData(160, "King", false)]
    [InlineData(210, "Twin", false)]
    [InlineData(300, "King", true)]
    public async Task Database_constraints_also_protect_room_layout_rules(int number, string bedType, bool honeymoon)
    {
        using var app = new TestApplication(); await app.SeedAsync();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
        var invalid = TestApplication.SeedRoom(3, 1, number); invalid.BedType = bedType; invalid.IsHoneymoonSuite = honeymoon;
        db.Rooms.Add(invalid);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
