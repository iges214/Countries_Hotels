using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Infrastructure.Data;
using Xunit;

namespace NoName_Hotel.Tests;

public class BookingReliabilityTests
{
    private static DateOnly Day => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
    private static CreateBookingDto Request(Guid? id = null) => new(1, Day, Day.AddDays(2), 2, RoomIds: [1], RequestId: id ?? Guid.NewGuid(), ExpectedTotal: 200m);

    [Fact]
    public async Task Retries_and_lookup_return_the_committed_booking_even_after_cancellation_and_deactivation()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var owner = app.AuthenticatedClient();
        var input = Request();
        var original = await Create(owner, input);
        using var replay = await owner.PostAsJsonAsync("/api/v1/hotels/1/bookings", input);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(original.Id, (await replay.Content.ReadFromJsonAsync<GetBookingDto>())!.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/v1/branches/1/bookings/{original.Id}/cancel", null)).StatusCode);
        using var admin = app.AuthenticatedClient(role: "Administrator");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/v1/branches/1/active", new { IsActive = false })).StatusCode);
        var cancelled = await Create(owner, input);
        Assert.Equal(original.Id, cancelled.Id);
        Assert.Equal("Cancelled", cancelled.Status);
        var found = await owner.GetFromJsonAsync<GetBookingDto>($"/api/v1/branches/1/bookings/requests/{input.RequestId}");
        Assert.Equal(original.Id, found!.Id);
        using var scope = app.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>().Bookings.CountAsync());
    }

    [Fact]
    public async Task Request_keys_bind_to_the_original_payload_and_lookup_is_owner_and_branch_scoped()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var owner = app.AuthenticatedClient();
        var input = Request();
        await Create(owner, input);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/api/v1/branches/1/bookings", input with { Guests = 1 })).StatusCode);
        using var other = app.AuthenticatedClient("other", "Administrator");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/branches/1/bookings/requests/{input.RequestId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/branches/2/bookings/requests/{input.RequestId}")).StatusCode);
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/branches/1/bookings/requests/{input.RequestId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/branches/1/bookings/requests/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Stale_edits_are_rejected_without_overwriting_newer_values()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var owner = app.AuthenticatedClient();
        var original = await Create(owner, Request());
        Assert.Equal(8, original.RowVersion.Length);
        var update = new UpdateBookingDto(Day, Day.AddDays(3), 2, RoomIds: [1], RowVersion: original.RowVersion);
        using var first = await owner.PutAsJsonAsync($"/api/v1/branches/1/bookings/{original.Id}", update);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var current = (await first.Content.ReadFromJsonAsync<GetBookingDto>())!;
        Assert.False(original.RowVersion.SequenceEqual(current.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/v1/branches/1/bookings/{original.Id}", update with { Guests = 1 })).StatusCode);
        using var scope = app.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>().Bookings.SingleAsync();
        Assert.Equal(2, saved.Guest);
        Assert.Equal(Day.AddDays(3), saved.CheckOut);
        Assert.Equal(300m, saved.Totalprice);
    }

    [Fact]
    public async Task Missing_request_identity_or_edit_version_is_rejected_and_price_changes_require_review()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var owner = app.AuthenticatedClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/branches/1/bookings", Request() with { RequestId = null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/branches/1/bookings", Request(Guid.Empty))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/api/v1/branches/1/bookings", Request() with { ExpectedTotal = 199m })).StatusCode);
        var original = await Create(owner, Request());
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/v1/branches/1/bookings/{original.Id}",
            new UpdateBookingDto(Day, Day.AddDays(3), 1, RoomIds: [1]))).StatusCode);
    }

    [Fact]
    public async Task Cancelled_operations_cannot_commit_a_booking()
    {
        using var app = new TestApplication();
        await app.SeedAsync();
        using var scope = app.Services.CreateScope();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var repository = scope.ServiceProvider.GetRequiredService<IHotelRepository>();
        var entered = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.ExecuteSerializableAsync(async () =>
        {
            entered = true;
            repository.Add(new Booking { HotelId = 1, UserId = "guest", CheckIn = Day, CheckOut = Day.AddDays(1) });
            return await repository.SaveChangesAsync(cancelled.Token);
        }, cancelled.Token));
        Assert.False(entered);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>().Bookings.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_retries_use_separate_connections_and_create_one_durable_booking()
    {
        var directory = Path.Combine(Path.GetTempPath(), "iges-idempotency-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "bookings.db");
            var input = Request();
            int bookingId;
            using (var app = new TestApplication(databasePath: path))
            {
                await app.SeedAsync();
                var clients = Enumerable.Range(0, 4).Select(_ => app.AuthenticatedClient()).ToArray();
                try
                {
                    var bookings = await Task.WhenAll(clients.Select(client => Create(client, input)));
                    bookingId = bookings[0].Id;
                    Assert.All(bookings, booking => Assert.Equal(bookingId, booking.Id));
                    using var scope = app.Services.CreateScope();
                    Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>().Bookings.CountAsync());
                }
                finally { foreach (var client in clients) client.Dispose(); }
            }
            // A new host/connection proves reconciliation does not rely on process memory.
            using var restarted = new TestApplication(databasePath: path);
            using var owner = restarted.AuthenticatedClient();
            Assert.Equal(bookingId, (await Create(owner, input)).Id);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(directory).StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test cleanup escaped its temporary directory.");
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<GetBookingDto> Create(HttpClient client, CreateBookingDto input)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/branches/1/bookings", input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GetBookingDto>())!;
    }
}
