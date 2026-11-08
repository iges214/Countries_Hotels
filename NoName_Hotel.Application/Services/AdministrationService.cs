using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.Admin;
using NoName_Hotel.Application.Querying;
using NoName_Hotel.Application.Validation;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.Enum;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.Results;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Application.Services;

public sealed class AdministrationService(IHotelRepository repo)
{
    public async Task<Result<PageResult<AdminBookingDto>>> Bookings(int hotelId, PaginationParameters page)
    {
        var hotel = await repo.Query<Hotel>().FirstOrDefaultAsync(h => h.Id == hotelId);
        if (hotel is null) return Result<PageResult<AdminBookingDto>>.Failure(new Error(ErrorCodes.NotFound, "Branch not found."));
        var checkout = await repo.Query<BranchWebsite>().Where(w => w.HotelId == hotelId).Select(w => (TimeOnly?)w.CheckoutTime).FirstOrDefaultAsync() ?? new TimeOnly(12, 0);
        var result = await repo.Query<Booking>().Where(b => b.HotelId == hotelId).Include(b => b.User)
            .Include(b => b.RoomReservations).ThenInclude(r => r.Room).OrderByDescending(b => b.CreateAt).ThenBy(b => b.Id).ToPageResultAsync(page);
        return Result<PageResult<AdminBookingDto>>.Success(new() { Metadata = result.Metadata, Data = result.Data.Select(b => new AdminBookingDto(
            b.Id, b.HotelId, b.User is null ? "Guest account unavailable" : (b.User.FirstName + " " + b.User.LastName).Trim(), b.User?.Email ?? "",
            b.CheckIn, b.CheckOut, b.Guest, b.Rooms, b.Totalprice, hotel.CurrencyCode, b.Status.ToString(), b.CreateAt, b.UpdateAt == default ? null : b.UpdateAt,
            b.ActualCheckInAt, b.ActualCheckOutAt,
            ExpectedCheckout(hotel, b.CheckOut, checkout),
            b.RoomReservations.Select(r => new AdminRoomAssignment(r.RoomId, r.Room!.Number, r.Room.Category.ToString(), r.Room.BedType, r.Room.MaxGuests, r.Guests, r.NightlyRate)).ToList())).ToList() });
    }

    private static DateTime ExpectedCheckout(Hotel hotel, DateOnly day, TimeOnly time)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(hotel.TimeZoneId);
        var local = day.ToDateTime(time);
        // A daylight-saving transition may skip the configured wall-clock time.
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    public Task<Result> CheckIn(int hotelId, int bookingId, CheckInDto dto) => repo.ExecuteSerializableAsync(async () =>
    {
        var hotel = await repo.LockBranchAsync(hotelId);
        if (hotel is null) return Result.Failure(new Error(ErrorCodes.NotFound, "Branch not found."));
        var booking = await repo.Query<Booking>(true).Include(b => b.RoomReservations).ThenInclude(r => r.Room)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.HotelId == hotelId);
        if (booking is null) return Result.Failure(new Error(ErrorCodes.NotFound, "Booking not found."));
        if (booking.Status != BookingStatus.Confirmed || booking.ActualCheckInAt.HasValue || booking.ActualCheckOutAt.HasValue)
            return Result.Failure(new Error(ErrorCodes.Conflict, "Only a confirmed reservation that has not been checked in can be checked in."));
        var today = BranchClock.Today(hotel);
        if (today < booking.CheckIn || today >= booking.CheckOut) return Result.Failure(new Error(ErrorCodes.Conflict, "Check-in is only available during the reserved stay dates."));
        var assignments = booking.RoomReservations;
        if (assignments.Count != booking.Rooms || dto.Rooms.Count != assignments.Count || dto.Rooms.Select(r => r.RoomId).Distinct().Count() != assignments.Count ||
            dto.Rooms.Sum(r => r.Guests) != booking.Guest || dto.Rooms.Any(r => r.Guests < 0 || !assignments.Any(a => a.RoomId == r.RoomId && a.Room!.IsOperational && r.Guests <= a.Room.MaxGuests)))
            return Result.BadRequest(new Error(ErrorCodes.Validation, "Assign every reserved room a guest count within its capacity. The total must equal the booking guest count."));
        var ids = assignments.Select(r => r.RoomId).ToArray();
        if (await repo.Query<BookingRoom>().AnyAsync(r => ids.Contains(r.RoomId) && r.BookingId != bookingId && r.Booking!.ActualCheckInAt != null && r.Booking.ActualCheckOutAt == null))
            return Result.Failure(new Error(ErrorCodes.Conflict, "A reserved room still has a checked-in guest. Record their checkout first."));
        foreach (var assignment in assignments) assignment.Guests = dto.Rooms.Single(r => r.RoomId == assignment.RoomId).Guests;
        booking.ActualCheckInAt = booking.UpdateAt = DateTime.UtcNow;
        await repo.SaveChangesAsync();
        return Result.Success();
    });

    public Task<Result> CheckOut(int hotelId, int bookingId) => repo.ExecuteSerializableAsync(async () =>
    {
        if (await repo.LockBranchAsync(hotelId) is null) return Result.Failure(new Error(ErrorCodes.NotFound, "Branch not found."));
        var booking = await repo.Query<Booking>(true).FirstOrDefaultAsync(b => b.Id == bookingId && b.HotelId == hotelId);
        if (booking is null) return Result.Failure(new Error(ErrorCodes.NotFound, "Booking not found."));
        if (!booking.ActualCheckInAt.HasValue || booking.ActualCheckOutAt.HasValue) return Result.Failure(new Error(ErrorCodes.Conflict, "Only a current checked-in stay can be checked out."));
        booking.ActualCheckOutAt = booking.UpdateAt = DateTime.UtcNow;
        await repo.SaveChangesAsync();
        return Result.Success();
    });

    public async Task<SaveWebsiteDto> Website(int hotelId, bool admin)
    {
        var w = await repo.Query<BranchWebsite>().FirstOrDefaultAsync(w => w.HotelId == hotelId);
        return w is null ? new() : new()
        {
            DisplayName = w.DisplayName, HeroImageUrl = w.HeroImageUrl, CheckoutTime = w.CheckoutTime,
            HeroRoomImageUrl = w.HeroRoomImageUrl, HeroKitchenImageUrl = w.HeroKitchenImageUrl,
            HeroBarImageUrl = w.HeroBarImageUrl, HeroPoolImageUrl = w.HeroPoolImageUrl,
            PublicEmail = admin || w.ContactPublished ? w.PublicEmail : "", Phone = admin || w.ContactPublished ? w.Phone : "",
            PrivacyPolicy = admin || w.PrivacyPublished ? w.PrivacyPolicy : "", BookingPolicy = admin || w.BookingPolicyPublished ? w.BookingPolicy : "",
            ContactPublished = w.ContactPublished, PrivacyPublished = w.PrivacyPublished, BookingPolicyPublished = w.BookingPolicyPublished
        };
    }

    public async Task<Result> SaveWebsite(int hotelId, SaveWebsiteDto dto)
    {
        var errors = DtoValidation.Errors(dto);
        if (errors.Length > 0) return Result.BadRequest(errors);
        return await repo.ExecuteSerializableAsync(async () =>
        {
            if (await repo.LockBranchAsync(hotelId) is null) return Result.Failure(new Error(ErrorCodes.NotFound, "Branch not found."));
            var w = await repo.Query<BranchWebsite>(true).FirstOrDefaultAsync(w => w.HotelId == hotelId);
            if (w is null) { w = new() { HotelId = hotelId }; repo.Add(w); }
            w.DisplayName = dto.DisplayName.Trim(); w.HeroImageUrl = dto.HeroImageUrl.Trim(); w.PublicEmail = dto.PublicEmail.Trim(); w.Phone = dto.Phone.Trim();
            w.HeroRoomImageUrl = dto.HeroRoomImageUrl?.Trim() ?? w.HeroRoomImageUrl;
            w.HeroKitchenImageUrl = dto.HeroKitchenImageUrl?.Trim() ?? w.HeroKitchenImageUrl;
            w.HeroBarImageUrl = dto.HeroBarImageUrl?.Trim() ?? w.HeroBarImageUrl;
            w.HeroPoolImageUrl = dto.HeroPoolImageUrl?.Trim() ?? w.HeroPoolImageUrl;
            w.PrivacyPolicy = dto.PrivacyPolicy.Trim(); w.BookingPolicy = dto.BookingPolicy.Trim(); w.ContactPublished = dto.ContactPublished;
            w.PrivacyPublished = dto.PrivacyPublished; w.BookingPolicyPublished = dto.BookingPolicyPublished; w.CheckoutTime = dto.CheckoutTime;
            await repo.SaveChangesAsync(); return Result.Success();
        });
    }
}

