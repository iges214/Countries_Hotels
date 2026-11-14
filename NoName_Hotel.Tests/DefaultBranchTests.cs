using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Infrastructure.Data;
using Xunit;

namespace NoName_Hotel.Tests;

public sealed class DefaultBranchTests
{
    [Fact]
    public async Task Default_branch_is_explicit_stable_and_branch_data_stays_scoped()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            db.Hotels.Add(new Hotel { Id = 2, Name = "Alpha Lodge", Address = "Lagos", CountryId = 1, Rating = 4, TotalRooms = 1 });
            db.BranchWebsites.AddRange(
                new BranchWebsite { HotelId = 1, HeroRoomImageUrl = "https://example.com/warri-hero.jpg" },
                new BranchWebsite { HotelId = 2, HeroRoomImageUrl = "https://example.com/alpha-hero.jpg" });
            var otherBranchRoom = TestApplication.SeedRoom(20, 2, 100);
            otherBranchRoom.PerNightRate = 220m;
            db.Rooms.Add(otherBranchRoom);
            await db.SaveChangesAsync();
        }

        var admin = app.AuthenticatedClient(role: "Administrator");
        var publicClient = app.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/v1/branches/1/default", null)).StatusCode);

        var selected = await publicClient.GetFromJsonAsync<GetBranch>("/api/v1/branches/default");
        Assert.Equal(1, selected!.Id); // Test Hotel sorts after Alpha Lodge only by explicit designation.

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            var hotel = await db.Hotels.FindAsync(1);
            hotel!.Name = "Warri";
            await db.SaveChangesAsync();
        }
        selected = await publicClient.GetFromJsonAsync<GetBranch>("/api/v1/branches/default");
        Assert.Equal(1, selected!.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/v1/branches/2/default", null)).StatusCode);
        selected = await publicClient.GetFromJsonAsync<GetBranch>("/api/v1/branches/default");
        Assert.Equal(2, selected!.Id);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            Assert.Equal(1, db.Hotels.Count(h => h.IsActive && h.IsDefaultBranch));
            var conflict = await db.Hotels.AsTracking().SingleAsync(h => h.Id == 1);
            conflict!.IsDefaultBranch = true;
            await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => db.SaveChangesAsync());
        }

        var rooms1 = await publicClient.GetFromJsonAsync<RoomPage>("/api/v1/branches/1/rooms");
        var rooms2 = await publicClient.GetFromJsonAsync<RoomPage>("/api/v1/branches/2/rooms");
        Assert.All(rooms1!.Data, room => Assert.Equal(1, room.HotelId));
        Assert.All(rooms2!.Data, room => Assert.Equal(2, room.HotelId));
        Assert.Contains(rooms2.Data, room => room.Photos.Any(photo => photo.Url.Contains("room.jpg")));
        Assert.Equal(220m, Assert.Single(rooms2.Data).PerNightRate);
        var website1 = await publicClient.GetFromJsonAsync<WebsiteDto>("/api/v1/branches/1/website");
        var website2 = await publicClient.GetFromJsonAsync<WebsiteDto>("/api/v1/branches/2/website");
        Assert.Equal("https://example.com/warri-hero.jpg", website1!.HeroRoomImageUrl);
        Assert.Equal("https://example.com/alpha-hero.jpg", website2!.HeroRoomImageUrl);
    }

    [Fact]
    public async Task Inactive_default_is_rejected_and_fallback_uses_lowest_active_id()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        var admin = app.AuthenticatedClient(role: "Administrator");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>();
            db.Hotels.Add(new Hotel { Id = 2, Name = "Alpha Lodge", Address = "Lagos", CountryId = 1, Rating = 4 });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/v1/branches/1/active", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/branches/1/default", null)).StatusCode);
        var selected = await app.CreateClient().GetFromJsonAsync<GetBranch>("/api/v1/branches/default");
        Assert.Equal(2, selected!.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/v1/branches/2/active", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.CreateClient().GetAsync("/api/v1/branches/default")).StatusCode);
    }

    private sealed record GetBranch(int Id, string Name, bool IsActive, bool IsDefaultBranch);
    private sealed record RoomPage(List<RoomDto> Data);
    private sealed record RoomDto(int HotelId, decimal PerNightRate, List<RoomPhotoDto> Photos);
    private sealed record RoomPhotoDto(string Url);
    private sealed record WebsiteDto(string HeroRoomImageUrl);
}
