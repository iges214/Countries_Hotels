using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.Admin;
using NoName_Hotel.Application.DTO.AuthDto;
using NoName_Hotel.Common.Enum;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Infrastructure.Data;
using Xunit;

namespace NoName_Hotel.Tests;

public class AdministrationTests
{
    private sealed class TestMail : IHotelMailSender
    {
        public bool IsConfigured => true;
        public int Sends { get; private set; }
        public Task SendReplyAsync(string recipient, string subject, string body, string? replyTo, CancellationToken token)
        { Sends++; return Task.CompletedTask; }
    }

    [Fact]
    public async Task A_reserved_spare_room_can_have_zero_guests_without_changing_the_booking_total()
    {
        using var app = new TestApplication(); await app.SeedAsync(); var id = await SeedBooking(app, guests: 1);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            var booking = await db.Bookings.AsTracking().SingleAsync(); booking.Rooms = 2;
            db.BookingRooms.Add(new BookingRoom { BookingId = id, RoomId = 2, NightlyRate = 100 }); await db.SaveChangesAsync();
        }
        using var admin = app.AuthenticatedClient("other", "Administrator");
        var response = await admin.PostAsJsonAsync($"{Root}/bookings/{id}/check-in", new { Rooms = new[] { new { RoomId = 1, Guests = 1 }, new { RoomId = 2, Guests = 0 } } });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var check = app.Services.CreateScope(); var saved = await check.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>().Bookings.SingleAsync(); Assert.Equal(200, saved.Totalprice);
    }
    private static readonly string Root = "/api/v1/branches/1/admin";
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Africa/Lagos")));

    private static async Task<int> SeedBooking(TestApplication app, BookingStatus status = BookingStatus.Confirmed, int guests = 2)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
        var user = await db.Users.AsTracking().SingleAsync(u => u.Id == "guest"); user.FirstName = "Ada"; user.LastName = "Okoro"; user.Email = "ada@example.com";
        var booking = new Booking { HotelId = 1, UserId = "guest", CheckIn = Today, CheckOut = Today.AddDays(2), Guest = guests, Rooms = 1, Totalprice = 200, Status = status,
            RoomReservations = [new BookingRoom { RoomId = 1, NightlyRate = 100 }] };
        db.Bookings.Add(booking); await db.SaveChangesAsync(); return booking.Id;
    }

    [Fact]
    public async Task Admin_workspace_enforces_branch_access_and_revocation()
    {
        using var app = new TestApplication(); await app.SeedAsync();
        using var ordinary = app.AuthenticatedClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync("/api/v1/admin/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync(Root + "/bookings")).StatusCode);
        using var system = app.AuthenticatedClient("other", "Administrator");
        // Existing account by email; assigning never changes the global role.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            var user = await db.Users.AsTracking().SingleAsync(u => u.Id == "guest"); user.Email = "guest@example.com"; user.NormalizedEmail = "GUEST@EXAMPLE.COM";
            db.Hotels.Add(new Hotel { Id = 2, Name = "Other branch", CountryId = 1, Rating = 4, TotalRooms = 0 }); await db.SaveChangesAsync();
        }
        var assigned = await system.PostAsJsonAsync(Root + "/administrators", new { Email = "guest@example.com" });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var assignmentId = (await assigned.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await ordinary.GetAsync("/api/v1/admin/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ordinary.GetAsync(Root + "/bookings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync("/api/v1/branches/2/admin/bookings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync(Root + "/administrators", new { Email = "guest@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await system.DeleteAsync(Root + "/administrators/" + assignmentId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync(Root + "/bookings")).StatusCode);
    }

    [Fact]
    public async Task Check_in_requires_confirmation_and_exact_per_room_guest_counts_and_checkout_is_recorded()
    {
        using var app = new TestApplication(); await app.SeedAsync();
        var id = await SeedBooking(app, BookingStatus.Pending);
        using var admin = app.AuthenticatedClient("other", "Administrator");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{Root}/bookings/{id}/check-in", new { Rooms = new[] { new { RoomId = 1, Guests = 2 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/api/v1/branches/1/bookings/{id}/admin/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{Root}/bookings/{id}/check-in", new { Rooms = new[] { new { RoomId = 1, Guests = 1 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"{Root}/bookings/{id}/check-in", new { Rooms = new[] { new { RoomId = 1, Guests = 2 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{Root}/bookings/{id}/check-in", new { Rooms = new[] { new { RoomId = 1, Guests = 2 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/v1/branches/1/bookings/{id}/admin/cancel", null)).StatusCode);
        var page = await admin.GetFromJsonAsync<JsonElement>(Root + "/bookings");
        var booking = page.GetProperty("data")[0]; Assert.Equal("Ada Okoro", booking.GetProperty("guestName").GetString()); Assert.Equal(2, booking.GetProperty("assignments")[0].GetProperty("guests").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"{Root}/bookings/{id}/check-out", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"{Root}/bookings/{id}/check-out", null)).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
        var saved = await db.Bookings.SingleAsync(); Assert.NotNull(saved.ActualCheckInAt); Assert.NotNull(saved.ActualCheckOutAt);
        using var guest = app.AuthenticatedClient();
        var publicBooking = await guest.GetStringAsync("/api/v1/branches/1/bookings"); Assert.DoesNotContain("guestEmail", publicBooking); Assert.DoesNotContain("actualCheckInAt", publicBooking);
    }

    [Fact]
    public async Task Future_arrivals_and_overlapping_in_house_rooms_cannot_be_checked_in()
    {
        using var app = new TestApplication(); await app.SeedAsync(); var id = await SeedBooking(app);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
        var b = await db.Bookings.AsTracking().SingleAsync(); b.CheckIn = Today.AddDays(1); b.CheckOut = Today.AddDays(3); await db.SaveChangesAsync();
        using var admin = app.AuthenticatedClient("other", "Administrator");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{Root}/bookings/{id}/check-in", new { Rooms = new[] { new { RoomId = 1, Guests = 2 } } })).StatusCode);
        b.CheckIn = Today; db.Bookings.Update(b);
        db.Bookings.Add(new Booking { HotelId = 1, UserId = "other", CheckIn = Today.AddDays(-2), CheckOut = Today, Guest = 1, Rooms = 1, Totalprice = 200, Status = BookingStatus.Confirmed, ActualCheckInAt = DateTime.UtcNow.AddDays(-2), RoomReservations = [new BookingRoom { RoomId = 1, Guests = 1, NightlyRate = 100 }] });
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{Root}/bookings/{id}/check-in", new { Rooms = new[] { new { RoomId = 1, Guests = 2 } } })).StatusCode);
        using var guest = app.AuthenticatedClient();
        var rooms = await guest.GetStringAsync($"/api/v1/branches/1/rooms?checkIn={Today:yyyy-MM-dd}&checkOut={Today.AddDays(1):yyyy-MM-dd}");
        var list = JsonDocument.Parse(rooms).RootElement.GetProperty("data"); Assert.DoesNotContain(list.EnumerateArray(), r => r.GetProperty("id").GetInt32() == 1);
    }

    [Fact]
    public async Task Website_drafts_stay_private_until_published_and_unsafe_urls_are_rejected()
    {
        using var app = new TestApplication(); await app.SeedAsync(); using var admin = app.AuthenticatedClient("other", "Administrator");
        var settings = new SaveWebsiteDto { DisplayName = "IGES Warri", PublicEmail = "hello@example.com", Phone = "+234 800 000 0000", PrivacyPolicy = "Draft privacy policy", BookingPolicy = "Draft booking policy", HeroImageUrl = "javascript:alert(1)" };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Root + "/website", settings)).StatusCode);
        settings.HeroImageUrl = "https://example.com/hero.jpg";
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(Root + "/website", settings)).StatusCode);
        var draft = await admin.GetFromJsonAsync<SaveWebsiteDto>("/api/v1/branches/1/website"); Assert.Equal("", draft!.PublicEmail); Assert.Equal("", draft.PrivacyPolicy);
        settings.ContactPublished = settings.PrivacyPublished = settings.BookingPolicyPublished = true;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(Root + "/website", settings)).StatusCode);
        var published = await admin.GetFromJsonAsync<SaveWebsiteDto>("/api/v1/branches/1/website"); Assert.Equal("hello@example.com", published!.PublicEmail); Assert.Equal(settings.PrivacyPolicy, published.PrivacyPolicy);
    }

    [Fact]
    public async Task Website_carousel_saves_all_five_branch_photos_and_supports_legacy_updates()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var admin = app.AuthenticatedClient("other", "Administrator");
        using var guest = app.AuthenticatedClient();
        var settings = new SaveWebsiteDto
        {
            HeroImageUrl = "https://example.com/exterior.jpg",
            HeroRoomImageUrl = "/uploads/room.jpg",
            HeroKitchenImageUrl = "https://example.com/kitchen.jpg",
            HeroBarImageUrl = "https://example.com/bar.jpg",
            HeroPoolImageUrl = "javascript:alert(1)"
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Root + "/website", settings)).StatusCode);
        settings.HeroPoolImageUrl = "https://example.com/pool.jpg";
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PutAsJsonAsync(Root + "/website", settings)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(Root + "/website", settings)).StatusCode);

        const string publicPath = "/api/v1/branches/1/website";
        var saved = await guest.GetFromJsonAsync<SaveWebsiteDto>(publicPath);
        Assert.NotNull(saved);
        Assert.Equal(settings.HeroImageUrl, saved.HeroImageUrl);
        Assert.Equal(settings.HeroRoomImageUrl, saved.HeroRoomImageUrl);
        Assert.Equal(settings.HeroKitchenImageUrl, saved.HeroKitchenImageUrl);
        Assert.Equal(settings.HeroBarImageUrl, saved.HeroBarImageUrl);
        Assert.Equal(settings.HeroPoolImageUrl, saved.HeroPoolImageUrl);

        var update = new { settings.DisplayName, settings.HeroImageUrl, HeroBarImageUrl = "" };
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(Root + "/website", update)).StatusCode);
        saved = await guest.GetFromJsonAsync<SaveWebsiteDto>(publicPath);
        Assert.Equal("", saved!.HeroBarImageUrl);
        Assert.Equal(settings.HeroPoolImageUrl, saved.HeroPoolImageUrl);
    }

    [Fact]
    public async Task Website_messages_are_private_and_unconfigured_mail_never_claims_delivery()
    {
        using var app = new TestApplication(); await app.SeedAsync(); using var guest = app.AuthenticatedClient();
        Assert.Equal(HttpStatusCode.Accepted, (await guest.PostAsJsonAsync("/api/v1/branches/1/messages", new ContactMessageDto { SenderName = "Ada", SenderEmail = "ada@example.com", Subject = "Late arrival", Body = "Can I arrive at 9 pm?" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync(Root + "/messages")).StatusCode);
        using var admin = app.AuthenticatedClient("other", "Administrator");
        var page = await admin.GetFromJsonAsync<JsonElement>(Root + "/messages"); var id = page.GetProperty("data")[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"{Root}/messages/{id}", new MessageStateDto(true, true))).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await admin.PostAsJsonAsync($"{Root}/messages/{id}/replies", new ReplyMessageDto { RequestId = Guid.NewGuid(), Body = "Thank you for letting us know." })).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>(); Assert.Empty(await db.HotelMessageReplies.ToListAsync());
        var saved = await db.HotelMessages.SingleAsync(); Assert.True(saved.IsRead); Assert.True(saved.IsArchived);
    }

    [Fact]
    public async Task Repeated_reply_request_sends_once_and_records_delivery()
    {
        using var original = new TestApplication(); await original.SeedAsync();
        var sender = new TestMail();
        using var configured = original.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        { services.RemoveAll<IHotelMailSender>(); services.AddSingleton<IHotelMailSender>(sender); }));
        using var sourceClient = original.AuthenticatedClient("other", "Administrator");
        using var admin = configured.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        admin.DefaultRequestHeaders.Authorization = sourceClient.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsJsonAsync("/api/v1/branches/1/messages", new ContactMessageDto { SenderName = "Ada", SenderEmail = "ada@example.com", Subject = "Question", Body = "Can I arrive late?" })).StatusCode);
        var page = await admin.GetFromJsonAsync<JsonElement>(Root + "/messages"); var id = page.GetProperty("data")[0].GetProperty("id").GetInt32();
        var dto = new ReplyMessageDto { RequestId = Guid.NewGuid(), Body = "Yes, please let the front desk know." };
        var reply = await admin.PostAsJsonAsync($"{Root}/messages/{id}/replies", dto); Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        Assert.Equal("Sent", (await reply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deliveryStatus").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{Root}/messages/{id}/replies", dto)).StatusCode);
        Assert.Equal(1, sender.Sends);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Account_profile_and_password_changes_validate_credentials_and_revoke_the_old_session(bool administrator)
    {
        using var app = new TestApplication(); await app.SeedAsync();
        string userId;
        using (var scope = app.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "staff@example.com", Email = "staff@example.com", FirstName = "Old", LastName = "Name" };
            Assert.True((await manager.CreateAsync(user, "ValidPassword1!")).Succeeded); userId = user.Id;
            if (administrator)
            {
                var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
                db.HotelAdmins.Add(new HotelAdmin { HotelId = 1, UserId = userId });
                await db.SaveChangesAsync();
            }
        }
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Email = "staff@example.com", Password = "ValidPassword1!" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadAsStringAsync()).Trim('"'));
        var profilePath = administrator ? "/api/v1/admin/me" : "/api/v1/account";
        var updatePath = administrator ? "/api/v1/admin/account" : "/api/v1/account";
        var passwordPath = administrator ? "/api/v1/admin/password" : "/api/v1/account/password";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(updatePath, new UpdateAccountDto("New", "Name"))).StatusCode);
        Assert.Equal("New", (await client.GetFromJsonAsync<AccountDto>(profilePath))!.FirstName);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(passwordPath, new ChangePasswordDto("WrongPassword1!", "NewPassword1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(passwordPath, new ChangePasswordDto("ValidPassword1!", "NewPassword1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(profilePath)).StatusCode);
    }
}
