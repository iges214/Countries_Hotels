using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Application.Validation;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.QuerableXtensions.Extensions;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.Results;
using NoName_Hotel.Domain.Data;
using NoName_Hotel.Domain.Entities.Models;
namespace NoName_Hotel.Application.Services;
public class HotelService(NoName_hotel_DbContext context, ICountriesService countriesService, IMapper mapper) : IHotelService
{
    public async Task<Result<PageResult<GetHotelDto>>> GetHotelsAsync(PaginationParameters page, HotelFilterParameters f)
    {
        var query = context.Hotels.AsQueryable();
        if (f.CountryId.HasValue) query = query.Where(h => h.CountryId == f.CountryId);
        if (f.MinRating.HasValue) query = query.Where(h => h.Rating >= f.MinRating);
        if (f.MaxRating.HasValue) query = query.Where(h => h.Rating <= f.MaxRating);
        if (f.MinPrice.HasValue) query = query.Where(h => h.PerNightRate >= f.MinPrice);
        if (f.MaxPrice.HasValue) query = query.Where(h => h.PerNightRate <= f.MaxPrice);
        if (!string.IsNullOrWhiteSpace(f.Location))
        {
            var location = f.Location.Trim();
            query = query.Where(h => EF.Functions.Like(h.Address, $"%{location}%"));
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim();
            query = query.Where(h => EF.Functions.Like(h.Name, $"%{term}%") || EF.Functions.Like(h.Address, $"%{term}%"));
        }
        IOrderedQueryable<Hotel> sorted = f.SortBy?.Trim().ToLowerInvariant() switch
        {
            "rating" => f.SortDescending ? query.OrderByDescending(h => h.Rating) : query.OrderBy(h => h.Rating),
            "price" => f.SortDescending ? query.OrderByDescending(h => h.PerNightRate) : query.OrderBy(h => h.PerNightRate),
            _ => f.SortDescending ? query.OrderByDescending(h => h.Name) : query.OrderBy(h => h.Name)
        };
        return Result<PageResult<GetHotelDto>>.Success(await sorted.ThenBy(h => h.Id)
            .ProjectTo<GetHotelDto>(mapper.ConfigurationProvider).ToPageResultAsync(page));
    }
    public async Task<Result<GetHotelDto>> GetHotelAsync(int id)
    {
        var dto = await context.Hotels.Where(h => h.Id == id)
            .ProjectTo<GetHotelDto>(mapper.ConfigurationProvider).FirstOrDefaultAsync();
        return dto is null ? Result<GetHotelDto>.Failure(new Error(ErrorCodes.NotFound, "Hotel not found."))
            : Result<GetHotelDto>.Success(dto);
    }
    public async Task<Result<GetHotelDto>> CreateHotelAsync(CreateHotelDto dto)
    {
        var errors = DtoValidation.Errors(dto);
        if (errors.Length > 0) return Result<GetHotelDto>.BadRequest(errors);
        var country = await context.Countries.FirstOrDefaultAsync(c => c.CountryId == dto.CountryId);
        if (country is null) return Result<GetHotelDto>.Failure(new Error(ErrorCodes.NotFound, "Country not found."));
        if (await HotelExistsAsync(dto.Name, dto.CountryId))
            return Result<GetHotelDto>.Failure(new Error(ErrorCodes.Conflict, "Hotel name already exists in this country."));
        var hotel = mapper.Map<Hotel>(dto);
        hotel.Name = hotel.Name.Trim();
        hotel.Country = country;
        // The country was read without tracking; attach it rather than inserting it again with the hotel.
        context.Attach(country);
        context.Hotels.Add(hotel);
        await context.SaveChangesAsync();
        return Result<GetHotelDto>.Success(mapper.Map<GetHotelDto>(hotel));
    }
    public async Task<Result> UpdateHotelAsync(int id, UpdateHotelDto dto)
    {
        if (id != dto.Id) return Result.BadRequest(new Error(ErrorCodes.Validation, "Id route value does not match payload Id."));
        var errors = DtoValidation.Errors(dto);
        if (errors.Length > 0) return Result.BadRequest(errors);
        return await ReservationTransactions.RunAsync(context, async () =>
        {
            var hotel = await ReservationTransactions.LockHotelAsync(context, id);
            if (hotel is null) return Result.NotFound(new Error(ErrorCodes.NotFound, "Hotel not found."));
            if (!await countriesService.CountryExistsAsync(dto.CountryId))
                return Result.NotFound(new Error(ErrorCodes.NotFound, "Country not found."));
            var name = dto.Name.Trim().ToLower();
            if (await context.Hotels.AnyAsync(h => h.Id != id && h.CountryId == dto.CountryId && h.Name.ToLower() == name))
                return Result.Failure(new Error(ErrorCodes.Conflict, "Hotel name already exists in this country."));
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var bookings = await context.Bookings.Where(b => b.HotelId == id &&
                b.Status != NoName_Hotel.Common.Enum.BookingStatus.Cancelled && b.CheckOut > today).ToListAsync();
            var lastDay = bookings.Count == 0 ? today.AddDays(1) : bookings.Max(b => b.CheckOut);
            if (!BookingAvailability.HasCapacity(dto.TotalRooms, 0, today, lastDay, bookings))
                return Result.Failure(new Error(ErrorCodes.Conflict, "Room count cannot be reduced below existing reservations."));
            mapper.Map(dto, hotel);
            hotel.Name = hotel.Name.Trim();
            await context.SaveChangesAsync();
            return Result.Success();
        });
    }
    public Task<Result> DeleteHotelAsync(int id) => ReservationTransactions.RunAsync(context, async () =>
    {
        var hotel = await ReservationTransactions.LockHotelAsync(context, id);
        if (hotel is null) return Result.NotFound(new Error(ErrorCodes.NotFound, "Hotel not found."));
        if (await context.Bookings.AnyAsync(b => b.HotelId == id) || await context.HotelAdmins.AnyAsync(a => a.HotelId == id))
            return Result.Failure(new Error(ErrorCodes.Conflict, "A hotel with bookings or assigned administrators cannot be deleted."));
        context.Hotels.Remove(hotel);
        await context.SaveChangesAsync();
        return Result.Success();
    });
    public Task<bool> HotelExistsAsync(int id) => context.Hotels.AnyAsync(h => h.Id == id);
    public Task<bool> HotelExistsAsync(string name, int countryId)
    {
        var normalized = name.Trim().ToLower();
        return context.Hotels.AnyAsync(h => h.CountryId == countryId && h.Name.ToLower() == normalized);
    }
}
