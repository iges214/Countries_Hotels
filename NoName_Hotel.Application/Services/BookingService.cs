using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Application.Validation;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.Enum;
using NoName_Hotel.Common.QuerableXtensions.Extensions;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.Results;
using NoName_Hotel.Domain.Data;
using NoName_Hotel.Domain.Entities.Models;
namespace NoName_Hotel.Application.Services;
public class BookingService(NoName_hotel_DbContext db, IUserServices users, IMapper mapper,
    ILogger<BookingService> logger) : IBookingService
{
    public async Task<Result<GetBookingDto>> CreateBookingAsync(CreateBookingDto dto)
    {
        var errors = DtoValidation.Errors(dto);
        if (errors.Length > 0) return Result<GetBookingDto>.BadRequest(errors);
        if (string.IsNullOrWhiteSpace(users.UserId))
            return Result<GetBookingDto>.Failure(new Error(ErrorCodes.Forbid, "A user account is required."));
        return await ReservationTransactions.RunAsync(db, async () =>
        {
            var hotel = await ReservationTransactions.LockHotelAsync(db, dto.HotelId);
            if (hotel is null) return BookingFailure(ErrorCodes.NotFound, "Hotel not found.");
            var conflict = await CheckAvailability(hotel, dto.CheckIn, dto.CheckOut, dto.Rooms);
            if (conflict is not null) return BookingFailure(ErrorCodes.Conflict, conflict);
            var total = hotel.PerNightRate * (dto.CheckOut.DayNumber - dto.CheckIn.DayNumber) * dto.Rooms;
            if (total <= 0 || total > 9999999999999999.99m)
                return BookingFailure(ErrorCodes.Validation, "The hotel rate or booking total is invalid.");
            var booking = mapper.Map<Booking>(dto);
            booking.UserId = users.UserId;
            booking.Hotel = hotel;
            booking.Totalprice = total;
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
            logger.LogInformation("Created booking {BookingId} for hotel {HotelId}", booking.Id, hotel.Id);
            return Result<GetBookingDto>.Success(mapper.Map<GetBookingDto>(booking));
        });
    }
    public async Task<Result<GetBookingDto>> UpdateBookingAsync(int hotelId, int bookingId, UpdateBookingDto dto)
    {
        var errors = DtoValidation.Errors(dto);
        if (errors.Length > 0) return Result<GetBookingDto>.BadRequest(errors);
        return await ReservationTransactions.RunAsync(db, async () =>
        {
            var hotel = await ReservationTransactions.LockHotelAsync(db, hotelId);
            if (hotel is null) return BookingFailure(ErrorCodes.NotFound, "Hotel not found.");
            var booking = await db.Bookings.AsTracking().FirstOrDefaultAsync(b =>
                b.Id == bookingId && b.HotelId == hotelId && b.UserId == users.UserId);
            if (booking is null) return BookingFailure(ErrorCodes.NotFound, "Booking not found.");
            if (booking.Status == BookingStatus.Cancelled)
                return BookingFailure(ErrorCodes.Conflict, "Cancelled bookings cannot be modified.");
            var conflict = await CheckAvailability(hotel, dto.CheckIn, dto.CheckOut, dto.Rooms, bookingId);
            if (conflict is not null) return BookingFailure(ErrorCodes.Conflict, conflict);
            var total = hotel.PerNightRate * (dto.CheckOut.DayNumber - dto.CheckIn.DayNumber) * dto.Rooms;
            if (total <= 0 || total > 9999999999999999.99m)
                return BookingFailure(ErrorCodes.Validation, "The hotel rate or booking total is invalid.");
            mapper.Map(dto, booking);
            booking.Hotel = hotel;
            booking.Totalprice = total;
            booking.UpdateAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            logger.LogInformation("Updated booking {BookingId}", booking.Id);
            return Result<GetBookingDto>.Success(mapper.Map<GetBookingDto>(booking));
        });
    }
    public Task<Result> AdminConfirmBookingAsync(int hotelId, int bookingId) =>
        ChangeStatus(hotelId, bookingId, BookingStatus.Confirmed, requireOwner: false);
    public Task<Result> AdminCancelBookingAsync(int hotelId, int bookingId) =>
        ChangeStatus(hotelId, bookingId, BookingStatus.Cancelled, requireOwner: false);
    public Task<Result> CancelBookingAsync(int hotelId, int bookingId) =>
        ChangeStatus(hotelId, bookingId, BookingStatus.Cancelled, requireOwner: true);

    private async Task<Result> ChangeStatus(int hotelId, int bookingId, BookingStatus status, bool requireOwner)
    {
        // Administrative callers are authorized by HotelOrSystemAdmin before this service is invoked.
        return await ReservationTransactions.RunAsync(db, async () =>
        {
            var hotel = await ReservationTransactions.LockHotelAsync(db, hotelId);
            if (hotel is null) return Result.NotFound(new Error(ErrorCodes.NotFound, "Hotel not found."));
            var query = db.Bookings.AsTracking().Where(b => b.Id == bookingId && b.HotelId == hotelId);
            if (requireOwner) query = query.Where(b => b.UserId == users.UserId);
            var booking = await query.FirstOrDefaultAsync();
            if (booking is null) return Result.NotFound(new Error(ErrorCodes.NotFound, "Booking not found."));
            if (booking.Status == BookingStatus.Cancelled)
                return Result.Failure(new Error(ErrorCodes.Conflict, "The booking is already cancelled."));
            booking.Status = status;
            booking.UpdateAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            logger.LogInformation("Changed booking {BookingId} status to {Status}", booking.Id, status);
            return Result.Success();
        });
    }
    private async Task<string?> CheckAvailability(Hotel hotel, DateOnly start, DateOnly end, int rooms, int? excluding = null)
    {
        var overlaps = await db.Bookings.Where(b => b.HotelId == hotel.Id &&
            b.Status != BookingStatus.Cancelled && b.Id != excluding &&
            b.CheckIn < end && b.CheckOut > start).ToListAsync();
        if (overlaps.Any(b => b.UserId == users.UserId))
            return "The selected dates overlap with another booking on this account.";
        return BookingAvailability.HasCapacity(hotel.TotalRooms, rooms, start, end, overlaps)
            ? null : "There are not enough rooms available for the selected dates.";
    }
    public Task<Result<PageResult<GetBookingDto>>> GetBookingPerHotelAsync(int id,
        PaginationParameters page, BookingFilterParameters filters) => GetBookings(id, page, filters, false);
    public Task<Result<PageResult<GetBookingDto>>> GetUserBookingsForHotelAsync(int id,
        PaginationParameters page, BookingFilterParameters filters) => GetBookings(id, page, filters, true);
    private async Task<Result<PageResult<GetBookingDto>>> GetBookings(int id,
        PaginationParameters page, BookingFilterParameters f, bool ownerOnly)
    {
        if (!await db.Hotels.AnyAsync(h => h.Id == id))
            return Result<PageResult<GetBookingDto>>.Failure(new Error(ErrorCodes.NotFound, "Hotel not found."));
        var query = db.Bookings.Where(b => b.HotelId == id);
        if (ownerOnly) query = query.Where(b => b.UserId == users.UserId);
        if (f.Status.HasValue) query = query.Where(b => b.Status == f.Status);
        if (f.CheckInFrom.HasValue) query = query.Where(b => b.CheckIn >= f.CheckInFrom);
        if (f.CheckInTo.HasValue) query = query.Where(b => b.CheckIn <= f.CheckInTo);
        if (f.CheckOutFrom.HasValue) query = query.Where(b => b.CheckOut >= f.CheckOutFrom);
        if (f.CheckOutTo.HasValue) query = query.Where(b => b.CheckOut <= f.CheckOutTo);
        if (f.CreatedAfter.HasValue) query = query.Where(b => b.CreateAt >= f.CreatedAfter);
        if (f.CreatedBefore.HasValue) query = query.Where(b => b.CreateAt <= f.CreatedBefore);
        if (f.MinPrice.HasValue) query = query.Where(b => b.Totalprice >= f.MinPrice);
        if (f.MaxPrice.HasValue) query = query.Where(b => b.Totalprice <= f.MaxPrice);
        if (f.MinGuests.HasValue) query = query.Where(b => b.Guest >= f.MinGuests);
        if (f.MaxGuests.HasValue) query = query.Where(b => b.Guest <= f.MaxGuests);
        IOrderedQueryable<Booking> sorted = f.SortBy?.Trim().ToLowerInvariant() switch
        {
            "checkin" => f.SortDescending ? query.OrderByDescending(b => b.CheckIn) : query.OrderBy(b => b.CheckIn),
            "checkout" => f.SortDescending ? query.OrderByDescending(b => b.CheckOut) : query.OrderBy(b => b.CheckOut),
            "price" => f.SortDescending ? query.OrderByDescending(b => b.Totalprice) : query.OrderBy(b => b.Totalprice),
            _ => f.SortDescending ? query.OrderByDescending(b => b.CreateAt) : query.OrderBy(b => b.CreateAt)
        };
        var result = await sorted.ThenBy(b => b.Id).ProjectTo<GetBookingDto>(mapper.ConfigurationProvider)
            .ToPageResultAsync(page);
        return Result<PageResult<GetBookingDto>>.Success(result);
    }
    private static Result<GetBookingDto> BookingFailure(string code, string message) =>
        Result<GetBookingDto>.Failure(new Error(code, message));
}
