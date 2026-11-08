using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Application.DTO.Offerings;
using NoName_Hotel.Application.DTO.Rooms;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Infrastructure.Data;
using Xunit;

namespace NoName_Hotel.Tests;

public class IgesHotelTests
{
    private static DateOnly Start => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20);
    private static string Stay => $"checkIn={Start:yyyy-MM-dd}&checkOut={Start.AddDays(2):yyyy-MM-dd}";
    private static SaveRoomDto Draft(int number = 210) => new()
    {
        Number = number, Description = "VIP suite with private bathroom", BedType = "King", Amenities = "Wi-Fi, AC",
        MaxGuests = 3, SizeSquareMetres = 40, PerNightRate = 250
    };

    [Theory]
    [InlineData(100, RoomCategory.Normal)] [InlineData(200, RoomCategory.Normal)]
    [InlineData(210, RoomCategory.Vip)] [InlineData(300, RoomCategory.Vip)]
    [InlineData(350, RoomCategory.Vvip)] [InlineData(400, RoomCategory.Vvip)]
    [InlineData(480, RoomCategory.Platinum)] [InlineData(500, RoomCategory.Platinum)]
    [InlineData(201, null)] [InlineData(301, null)] [InlineData(401, null)] [InlineData(501, null)]
    public void Room_ranges_match_IGES_floors(int number, RoomCategory? category) => Assert.Equal(category, RoomNumbering.CategoryFor(number));

    [Fact]
    public async Task Public_catalog_hides_reserved_rooms_for_the_stay_and_cancellation_releases_them()
    {
        using var app = new TestApplication(); await app.SeedAsync();
        using var publicClient = app.CreateClient(new() { BaseAddress = new("https://localhost") });
        Assert.Contains("IGES Hotel and Suites", await publicClient.GetStringAsync("/api/v1/hotel"));
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync("/api/v1/branches")).StatusCode);
        var before = (await publicClient.GetFromJsonAsync<PageResult<RoomDto>>($"/api/v1/branches/1/rooms?{Stay}"))!;
        Assert.Equal(2, before.Metadata.TotalCount);
        Assert.All(before.Data, r => Assert.Equal(2, r.Photos.Count));
        using var guest = app.AuthenticatedClient();
        var booked = await guest.PostAsJsonAsync("/api/v1/branches/1/bookings", new CreateBookingDto(1, Start, Start.AddDays(2), 2, RoomIds: [1]));
        Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
        var reservation = (await booked.Content.ReadFromJsonAsync<GetBookingDto>())!;
        Assert.Equal([1], reservation.RoomIds);
        Assert.Equal("NGN", reservation.CurrencyCode);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"/api/v1/branches/1/rooms/1?{Stay}")).StatusCode);
        var remaining = (await publicClient.GetFromJsonAsync<PageResult<RoomDto>>($"/api/v1/branches/1/rooms?{Stay}"))!;
        Assert.Equal(2, Assert.Single(remaining.Data).Id);
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync($"/api/v1/branches/1/rooms/1?checkIn={Start.AddDays(2):yyyy-MM-dd}&checkOut={Start.AddDays(3):yyyy-MM-dd}")).StatusCode);
        using var other = app.AuthenticatedClient("other");
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsJsonAsync("/api/v1/branches/1/bookings", new CreateBookingDto(1, Start, Start.AddDays(2), 1, RoomIds: [1]))).StatusCode);
        await guest.PostAsync($"/api/v1/branches/1/bookings/{reservation.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync($"/api/v1/branches/1/rooms/1?{Stay}")).StatusCode);
    }

    [Fact]
    public async Task Branches_can_reuse_room_numbers_and_admins_cannot_manage_other_branches()
    {
        using var app = new TestApplication(); await app.SeedAsync();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            db.Hotels.Add(new Hotel { Id = 2, Name = "Lagos branch", Address = "Lagos", CountryId = 1, TotalRooms = 1, PerNightRate = 100 });
            db.Rooms.Add(TestApplication.SeedRoom(3, 2, 100));
            db.HotelAdmins.Add(new HotelAdmin { HotelId = 1, UserId = "guest" });
            await db.SaveChangesAsync();
        }
        using var admin = app.AuthenticatedClient("guest", "Hotel Admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/branches/1/admin/rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/branches/2/admin/rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/v1/branches/2/rooms", Draft())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/branches/1/bookings", new CreateBookingDto(1, Start, Start.AddDays(2), 1, RoomIds: [3]))).StatusCode);
        var first = await admin.PostAsJsonAsync("/api/v1/branches/1/bookings", new CreateBookingDto(1, Start, Start.AddDays(2), 1, RoomIds: [1]));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var other = app.AuthenticatedClient("other");
        Assert.Equal(HttpStatusCode.OK, (await other.PostAsJsonAsync("/api/v1/branches/2/bookings", new CreateBookingDto(2, Start, Start.AddDays(2), 1, RoomIds: [3]))).StatusCode);
    }

    [Fact]
    public async Task Admin_uploads_room_and_bathroom_photos_before_publishing()
    {
        using var app = new TestApplication(); await app.SeedAsync();
        using var admin = app.AuthenticatedClient("guest", "Administrator");
        var dto = Draft();
        var created = await admin.PostAsJsonAsync("/api/v1/branches/1/rooms", dto);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var room = (await created.Content.ReadFromJsonAsync<RoomDto>())!;
        dto.IsPublished = true;
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/branches/1/rooms/{room.Id}", dto)).StatusCode);
        using var guest = app.AuthenticatedClient("other");
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PostAsJsonAsync("/api/v1/branches/1/rooms", Draft(211))).StatusCode);
        var photoIds = new List<int>();
        foreach (var kind in new[] { 1, 2 })
        {
            using var body = new MultipartFormDataContent();
            // A valid 1×1 PNG; filename is deliberately untrusted.
            body.Add(new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1cAAAAASUVORK5CYII=")), "file", "../../room.png");
            body.Add(new StringContent(kind.ToString()), "kind");
            var uploaded = await admin.PostAsync($"/api/v1/branches/1/rooms/{room.Id}/photos", body);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
            var photo = (await uploaded.Content.ReadFromJsonAsync<RoomPhotoDto>())!;
            Assert.StartsWith("/uploads/", photo.Url);
            Assert.DoesNotContain("..", photo.Url);
            var image = await admin.GetAsync(photo.Url);
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
            photoIds.Add(photo.Id);
        }
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/branches/1/rooms/{room.Id}", dto)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/branches/1/rooms/{room.Id}/photos/{photoIds[0]}")).StatusCode);
        using var invalid = new MultipartFormDataContent();
        invalid.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("<script>alert(1)</script>")), "file", "fake.png");
        invalid.Add(new StringContent("1"), "kind");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/v1/branches/1/rooms/{room.Id}/photos", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/branches/1/rooms", Draft(205))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/branches/1/rooms", Draft(210))).StatusCode);
    }

    [Fact]
    public async Task Dining_and_activities_are_searchable_publicly_but_drafts_stay_private()
    {
        using var app = new TestApplication(); await app.SeedAsync();
        using var admin = app.AuthenticatedClient("guest", "Administrator");
        foreach (var kind in Enum.GetValues<OfferingKind>())
        {
            var response = await admin.PostAsJsonAsync("/api/v1/branches/1/offerings", new SaveOfferingDto
            {
                Kind = kind, Name = kind == OfferingKind.Meal ? "Banga soup" : kind.ToString(),
                Description = "Available to guests and the public", OpeningHours = "10:00–22:00", IsPublished = kind != OfferingKind.Lounge
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var publicClient = app.CreateClient(new() { BaseAddress = new("https://localhost") });
        var meals = (await publicClient.GetFromJsonAsync<PageResult<OfferingDto>>("/api/v1/branches/1/offerings?kind=Meal&search=Banga"))!;
        Assert.Equal("Banga soup", Assert.Single(meals.Data).Name);
        var all = (await publicClient.GetFromJsonAsync<PageResult<OfferingDto>>("/api/v1/branches/1/offerings"))!;
        Assert.Equal(4, all.Metadata.TotalCount);
        Assert.DoesNotContain(all.Data, x => x.Kind == "Lounge");
        Assert.Equal(HttpStatusCode.Unauthorized, (await publicClient.GetAsync("/api/v1/branches/1/admin/offerings")).StatusCode);
    }

    [Fact]
    public async Task Invalid_room_selections_and_queries_are_rejected()
    {
        using var app = new TestApplication(); await app.SeedAsync();
        using var guest = app.AuthenticatedClient();
        foreach (var ids in new int[]?[] { null, [], [1, 1], [0] })
            Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/branches/1/bookings", new CreateBookingDto(1, Start, Start.AddDays(2), 1, RoomIds: ids))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await guest.PostAsJsonAsync("/api/v1/branches/1/bookings", new CreateBookingDto(1, Start, Start.AddDays(2), 3, RoomIds: [1]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.GetAsync($"/api/v1/branches/1/rooms?checkIn={Start:yyyy-MM-dd}")).StatusCode);
    }

    [Fact]
    public async Task Existing_quotes_survive_price_changes_and_reserved_rooms_cannot_be_disabled()
    {
        using var app = new TestApplication(); await app.SeedAsync();
        using var guest = app.AuthenticatedClient();
        var response = await guest.PostAsJsonAsync("/api/v1/branches/1/bookings", new CreateBookingDto(1, Start, Start.AddDays(2), 1, RoomIds: [1]));
        var booking = (await response.Content.ReadFromJsonAsync<GetBookingDto>())!;
        using var admin = app.AuthenticatedClient("guest", "Administrator");
        var edit = Draft(100); edit.MaxGuests = 2; edit.PerNightRate = 500; edit.IsPublished = true;
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/v1/branches/1/rooms/1", edit)).StatusCode);
        edit.IsOperational = false;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync("/api/v1/branches/1/rooms/1", edit)).StatusCode);
        var updated = await guest.PutAsJsonAsync($"/api/v1/branches/1/bookings/{booking.Id}", new UpdateBookingDto(Start, Start.AddDays(3), 1, RoomIds: [1]));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(300, (await updated.Content.ReadFromJsonAsync<GetBookingDto>())!.TotalPrice);
    }

    [Fact]
    public async Task Drafts_disabled_rooms_and_tonights_reserved_rooms_are_hidden_by_default()
    {
        using var app = new TestApplication(); await app.SeedAsync();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            var draft = TestApplication.SeedRoom(3, 1, 210); draft.IsPublished = false;
            var disabled = TestApplication.SeedRoom(4, 1, 350); disabled.IsOperational = false;
            db.Rooms.AddRange(draft, disabled);
            await db.SaveChangesAsync();
        }
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Africa/Lagos"));
        using var guest = app.AuthenticatedClient();
        Assert.Equal(HttpStatusCode.OK, (await guest.PostAsJsonAsync("/api/v1/branches/1/bookings", new CreateBookingDto(1, today, today.AddDays(1), 1, RoomIds: [1]))).StatusCode);
        var rooms = (await guest.GetFromJsonAsync<PageResult<RoomDto>>("/api/v1/branches/1/rooms"))!;
        Assert.Equal(2, Assert.Single(rooms.Data).Id);
        foreach (var id in new[] { 1, 3, 4 })
            Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/v1/branches/1/rooms/{id}")).StatusCode);
    }

    [Fact]
    public async Task Historical_unassigned_reservations_still_consume_capacity()
    {
        using var app = new TestApplication(); await app.SeedAsync();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            db.Bookings.Add(new Booking { HotelId = 1, UserId = "other", CheckIn = Start, CheckOut = Start.AddDays(2), Rooms = 2 });
            await db.SaveChangesAsync();
        }
        using var guest = app.AuthenticatedClient();
        var rooms = (await guest.GetFromJsonAsync<PageResult<RoomDto>>($"/api/v1/branches/1/rooms?{Stay}"))!;
        Assert.Empty(rooms.Data);
        Assert.Equal(HttpStatusCode.Conflict, (await guest.PostAsJsonAsync("/api/v1/branches/1/bookings", new CreateBookingDto(1, Start, Start.AddDays(2), 1, RoomIds: [1]))).StatusCode);
    }
}
