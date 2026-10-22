using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.Enum;
using NoName_Hotel.Common.QuerableXtensions.Extensions;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.Results;
using NoName_Hotel.Domain.Data;
using NoName_Hotel.Domain.Entities.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace NoName_Hotel.Application.Services
{
    public class BookingService(NoName_hotel_DbContext dbContext, IUserServices userServices, IMapper mapper) : IBookingService
    {
        public async Task<Result<PageResult<GetBookingDto>>> GetBookingPerHotelAsync(int hotelId, PaginationParameters paginationParameters, BookingFilterParameters bookingFilterParameters)
        {
            var hotelExist = await dbContext.Hotels.AnyAsync(a => a.Id == hotelId);

            if (!hotelExist)
            {
                return Result<PageResult<GetBookingDto>>.Failure(new Error(ErrorCodes.NotFound, $"hotel '{hotelId}' was not found"));
            }

            var query = ApplyFilters(hotelId, bookingFilterParameters);

            var bookings = await query
                .ProjectTo<GetBookingDto>(mapper.ConfigurationProvider)
                .ToPageResultAsync(paginationParameters);

            return Result<PageResult<GetBookingDto>>.Success(bookings);

        }

        public async Task<Result<GetBookingDto>> CreateBookingAsync(CreateBookingDto dto)
        {
            var userId = userServices.UserId;

            bool overlaps = await IsOverlap(dto.HotelId, userId, dto.CheckIn, dto.CheckOut);

            if (overlaps)
            {
                return Result<GetBookingDto>.Failure(new Error(ErrorCodes.Conflict, $"Dat/Days selected overlap with an existing booking."));
            }

            var hotel = await dbContext.Hotels
                .Where(a => a.Id == dto.HotelId)
                .FirstOrDefaultAsync();

            if (hotel == null)
            {
                return Result<GetBookingDto>.Failure(new Error(ErrorCodes.NotFound, $"Hotel '{dto.HotelId} was not found."));
            }

            //var nights = dto.CheckOut.Day - dto.CheckOut.Day;
            var nights = dto.CheckOut.DayNumber - dto.CheckIn.DayNumber;
            var totalPrice = hotel.PerNightRate * nights;
            var booking = mapper.Map<Booking>(dto);
            booking.UserId = userId;

            dbContext.Bookings.Add(booking);
            await dbContext.SaveChangesAsync();

            var result = mapper.Map<GetBookingDto>(booking);

            return Result<GetBookingDto>.Success(result);

        }

        public async Task<Result> AdminConfirmBookingAsync(int hotelId, int bookingId)
        {
            var userId = userServices.UserId;

            var booking = await dbContext.Bookings
                .Include(a => a.Hotel)
                .FirstOrDefaultAsync(b => b.Id == bookingId
                        && b.HotelId == hotelId);

            if (booking == null)
            {
                return Result.Failure(new Error(ErrorCodes.NotFound, $"Booking '{bookingId}' was not found"));
            }

            if (booking.Status == BookingStatus.Cancelled)
            {
                return Result.Failure(new Error(ErrorCodes.Conflict, "This booking has being cancelled"));
            }

            booking.Status = BookingStatus.Confirmed;
            booking.UpdateAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            return Result.Success();
        }

        public async Task<Result> AdminCancelBookingAsync(int hotelId, int bookingId)
        {
            var userId = userServices.UserId;

            var booking = await dbContext.Bookings
                .Include(a => a.Hotel)
                .FirstOrDefaultAsync(a => a.Id == bookingId
                                && a.HotelId == hotelId);

            if (booking == null)
            {
                return Result.Failure(new Error(ErrorCodes.NotFound, $"Bookinbg '{bookingId}' was not found"));
            }

            if (booking.Status == BookingStatus.Cancelled)
            {
                return Result.Failure(new Error(ErrorCodes.Conflict, "This booking is already cancelled"));
            }

            booking.Status = BookingStatus.Cancelled;
            booking.UpdateAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            return Result.Success();

        }

        public async Task<Result> CancelBookingAsync(int hotelId, int bookingId)
        {
            var userid = userServices.UserId;

            var booking = await dbContext.Bookings
                .Include(a => a.Hotel)
                .FirstOrDefaultAsync(a => a.Id == bookingId
                                    && a.HotelId == hotelId
                                    && a.UserId == userid);

            if (booking == null)
            {
                return Result.Failure(new Error(ErrorCodes.NotFound, $"booking '{bookingId}' was not found"));
            }

            if (booking.Status == BookingStatus.Cancelled)
            {
                return Result.Failure(new Error(ErrorCodes.Conflict, "This booking has already being cancelled."));
            }

            booking.Status = BookingStatus.Cancelled;
            booking.UpdateAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            return Result.Success();
        }

        public async Task<Result<GetBookingDto>> UpdateBookingAsync(int hotelId, int bookingId, UpdateBookingDto updateBookingDto)
        {
            var userId = userServices.UserId;

            bool overlaps = await IsOverlap(hotelId, userId, updateBookingDto.CheckIn, updateBookingDto.CheckOut, bookingId);

            if (overlaps)
            {
                return Result<GetBookingDto>.Failure(new Error(ErrorCodes.Conflict, "The selected date overlap with an esisting booking"));
            }

            var booking = await dbContext.Bookings
                .Include(b => b.Hotel)
            .FirstOrDefaultAsync(b =>
                b.Id == bookingId
                && b.HotelId == hotelId
                && b.UserId == userId);

            if (booking == null)
            {
                return Result<GetBookingDto>.Failure(new Error(ErrorCodes.NotFound, $"booking '{bookingId}' was not found"));
            }

            if (booking.Status == BookingStatus.Cancelled)
            {
                return Result<GetBookingDto>.Failure(new Error(ErrorCodes.Conflict, "Cancelled bookings cannot be modified."));
            }

            mapper.Map(updateBookingDto, booking);

            var perNight = booking.Hotel!.PerNightRate;
            var nights = updateBookingDto.CheckOut.DayNumber - updateBookingDto.CheckIn.DayNumber;

            booking.Totalprice = perNight * nights;

            booking.UpdateAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            var updated = mapper.Map<GetBookingDto>(booking);

            return Result<GetBookingDto>.Success(updated);
        }

        public async Task<Result<PageResult<GetBookingDto>>> GetUserBookingsForHotelAsync(int hotelId, PaginationParameters paginationParameters, BookingFilterParameters bookingFilterParameters)
        {
            var userId = userServices.UserId;

            var hotelExist = await dbContext.Hotels.AnyAsync(a => a.Id == hotelId);

            if (!hotelExist)
            {
                return Result<PageResult<GetBookingDto>>.Failure(new Error(ErrorCodes.NotFound, $"Hotel '{hotelId}' does not exist"));
            }
            var queryResult = ApplyFilters(hotelId, bookingFilterParameters);
            var bookings = await queryResult
                .Where(b => b.UserId == userId)
                .ProjectTo<GetBookingDto>(mapper.ConfigurationProvider)
                .ToPageResultAsync(paginationParameters);

            return Result<PageResult<GetBookingDto>>.Success(bookings);

        }

        private async Task<bool> IsOverlap(int hotelId, string userId, DateOnly checkIn, DateOnly checkOut, int? bookingId = null)
        {
            var query = dbContext.Bookings
                .Where(b => b.HotelId == hotelId
                && b.Status != BookingStatus.Cancelled
                && checkIn < b.CheckOut
                && checkOut > b.CheckIn
                && b.UserId == userId)
                .AsQueryable();

            if (bookingId.HasValue)
            {
                query = query.Where(a => a.Id != bookingId.Value);
            }

            return await query.AnyAsync();
        }

        private IQueryable<Booking> ApplyFilters(int hotelId, BookingFilterParameters bookingFilterParameters)
        {
            var Queryresult = dbContext.Bookings.Where(a => a.HotelId == hotelId);

            if (bookingFilterParameters.Status.HasValue)
            {
                Queryresult = Queryresult.Where(a => a.Status == bookingFilterParameters.Status.Value);
            }

            if (bookingFilterParameters.CheckInFrom.HasValue)
            {
                Queryresult = Queryresult.Where(a => a.CheckIn >= bookingFilterParameters.CheckInFrom.Value);
            }

            if (bookingFilterParameters.CheckInTo.HasValue)
            {
                Queryresult = Queryresult.Where(a => a.CheckIn <= bookingFilterParameters.CheckInTo.Value);
            }

            if (bookingFilterParameters.MinPrice.HasValue)
            {
                Queryresult = Queryresult.Where(a => a.Totalprice >= bookingFilterParameters.MinPrice.Value);
            }

            if(bookingFilterParameters.MaxPrice.HasValue)
            {
                Queryresult = Queryresult.Where(a => a.Totalprice <= bookingFilterParameters.MaxPrice.Value);
            }

            if (bookingFilterParameters.MinGuests.HasValue)
            {
                Queryresult = Queryresult.Where(a => a.Guest >= bookingFilterParameters.MinGuests.Value);
            }

            if(bookingFilterParameters.MaxGuests.HasValue)
            {
                Queryresult = Queryresult.Where(a => a.Guest <= bookingFilterParameters.MaxGuests.Value);
            }


            Queryresult = bookingFilterParameters.SortBy?.ToLower() switch
            {
                "checkin" => bookingFilterParameters.SortDescending ?
                    Queryresult.OrderByDescending(b => b.CheckIn) : Queryresult.OrderBy(b => b.CheckIn),
                "checkout" => bookingFilterParameters.SortDescending ?
                    Queryresult.OrderByDescending(b => b.CheckOut) : Queryresult.OrderBy(b => b.CheckOut),
                "price" => bookingFilterParameters.SortDescending ?
                    Queryresult.OrderByDescending(b => b.Totalprice) : Queryresult.OrderBy(b => b.Totalprice),
                "created" => bookingFilterParameters.SortDescending ?
                    Queryresult.OrderByDescending(b => b.CreateAt) : Queryresult.OrderBy(b => b.CreateAt),
                _ => Queryresult.OrderBy(b => b.CheckIn)
            };

            return Queryresult;
        }
    }




}

