using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Application.Validation;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Application.Querying;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.Results;
using NoName_Hotel.Domain.Entities.Models;
namespace NoName_Hotel.Application.Services;
public class HotelService(IHotelRepository context, ICountriesService countriesService, IMapper mapper) : IHotelService
{
    public async Task<Result<PageResult<GetHotelDto>>> GetHotelsAsync(PaginationParameters page, HotelFilterParameters f)
    {
        var query = context.Query<Hotel>().AsQueryable();
        if (f.CountryId.HasValue) query = query.Where(h => h.CountryId == f.CountryId);
        if (f.MinRating.HasValue) query = query.Where(h => h.Rating >= f.MinRating);
        if (f.MaxRating.HasValue) query = query.Where(h => h.Rating <= f.MaxRating);
        if (f.MinPrice.HasValue) query = query.Where(h => h.PerNightRate >= f.MinPrice);
        if (f.MaxPrice.HasValue) query = query.Where(h => h.PerNightRate <= f.MaxPrice);
        if (!string.IsNullOrWhiteSpace(f.Location))
        {
            var location = f.Location.Trim();
            query = query.Where(h => EF.Functions.Like(h.Address, $"%{location}%") ||
                EF.Functions.Like(h.City, $"%{location}%") || EF.Functions.Like(h.State, $"%{location}%"));
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim();
            query = query.Where(h => EF.Functions.Like(h.Name, $"%{term}%") || EF.Functions.Like(h.Address, $"%{term}%") ||
                EF.Functions.Like(h.City, $"%{term}%") || EF.Functions.Like(h.State, $"%{term}%"));
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
        var dto = await context.Query<Hotel>().Where(h => h.Id == id)
            .ProjectTo<GetHotelDto>(mapper.ConfigurationProvider).FirstOrDefaultAsync();
        return dto is null ? Result<GetHotelDto>.Failure(new Error(ErrorCodes.NotFound, "Hotel not found."))
            : Result<GetHotelDto>.Success(dto);
    }
    public async Task<Result<GetHotelDto>> GetDefaultBranchAsync()
    {
        var branch = await context.Query<Hotel>().Where(h => h.IsActive)
            .OrderByDescending(h => h.IsDefaultBranch).ThenBy(h => h.Id)
            .ProjectTo<GetHotelDto>(mapper.ConfigurationProvider).FirstOrDefaultAsync();
        return branch is null ? Result<GetHotelDto>.Failure(new Error(ErrorCodes.NotFound, "No active branches are available."))
            : Result<GetHotelDto>.Success(branch);
    }
    public Task<Result> SetDefaultBranchAsync(int id) => context.ExecuteSerializableAsync(async () =>
    {
        var target = await context.LockBranchAsync(id);
        if (target is null) return Result.NotFound(new Error(ErrorCodes.NotFound, "Branch not found."));
        if (!target.IsActive) return Result.BadRequest(new Error(ErrorCodes.Validation, "An inactive branch cannot be the default."));
        var currentDefaults = await context.Query<Hotel>(true).Where(h => h.IsDefaultBranch).ToListAsync();
        foreach (var branch in currentDefaults) branch.IsDefaultBranch = false;
        if (currentDefaults.Count > 0) await context.SaveChangesAsync();
        target.IsDefaultBranch = true;
        await context.SaveChangesAsync();
        return Result.Success();
    });
    public Task<Result> SetBranchActiveAsync(int id, bool isActive) => context.ExecuteSerializableAsync(async () =>
    {
        var branch = await context.LockBranchAsync(id);
        if (branch is null) return Result.NotFound(new Error(ErrorCodes.NotFound, "Branch not found."));
        branch.IsActive = isActive;
        if (!isActive) branch.IsDefaultBranch = false;
        await context.SaveChangesAsync();
        return Result.Success();
    });
    public async Task<Result<GetHotelDto>> CreateHotelAsync(CreateHotelDto dto)
    {
        var errors = DtoValidation.Errors(dto);
        if (errors.Length > 0) return Result<GetHotelDto>.BadRequest(errors);
        var country = await context.Query<Country>().FirstOrDefaultAsync(c => c.CountryId == dto.CountryId);
        if (country is null) return Result<GetHotelDto>.Failure(new Error(ErrorCodes.NotFound, "Country not found."));
        if (await HotelExistsAsync(dto.Name, dto.CountryId))
            return Result<GetHotelDto>.Failure(new Error(ErrorCodes.Conflict, "Hotel name already exists in this country."));
        var hotel = mapper.Map<Hotel>(dto);
        hotel.Name = hotel.Name.Trim();
        hotel.Country = country;
        // The country was read without tracking; attach it rather than inserting it again with the hotel.
        context.Attach(country);
        context.Add(hotel);
        await context.SaveChangesAsync();
        return Result<GetHotelDto>.Success(mapper.Map<GetHotelDto>(hotel));
    }
    public async Task<Result> UpdateHotelAsync(int id, UpdateHotelDto dto)
    {
        if (id != dto.Id) return Result.BadRequest(new Error(ErrorCodes.Validation, "Id route value does not match payload Id."));
        var errors = DtoValidation.Errors(dto);
        if (errors.Length > 0) return Result.BadRequest(errors);
        return await context.ExecuteSerializableAsync(async () =>
        {
            var hotel = await context.LockBranchAsync(id);
            if (hotel is null) return Result.NotFound(new Error(ErrorCodes.NotFound, "Hotel not found."));
            if (!await countriesService.CountryExistsAsync(dto.CountryId))
                return Result.NotFound(new Error(ErrorCodes.NotFound, "Country not found."));
            var name = dto.Name.Trim().ToLower();
            if (await context.Query<Hotel>().AnyAsync(h => h.Id != id && h.CountryId == dto.CountryId && h.Name.ToLower() == name))
                return Result.Failure(new Error(ErrorCodes.Conflict, "Hotel name already exists in this country."));
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (await context.Query<Booking>().AnyAsync(b => b.HotelId == id) && dto.CurrencyCode != hotel.CurrencyCode)
                return Result.Failure(new Error(ErrorCodes.Conflict, "A branch with bookings cannot change its currency."));
            var inventoryCount = await context.Query<Room>().CountAsync(r => r.HotelId == id && r.IsOperational);
            if (await context.Query<Room>().AnyAsync(r => r.HotelId == id) && dto.TotalRooms != inventoryCount)
                return Result.Failure(new Error(ErrorCodes.Validation, "TotalRooms must match the operational room inventory. Manage rooms to change capacity."));
            var bookings = await context.Query<Booking>().Where(b => b.HotelId == id &&
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
    public Task<Result> DeleteHotelAsync(int id) => context.ExecuteSerializableAsync(async () =>
    {
        var hotel = await context.LockBranchAsync(id);
        if (hotel is null) return Result.NotFound(new Error(ErrorCodes.NotFound, "Hotel not found."));
        if (await context.Query<Booking>().AnyAsync(b => b.HotelId == id) || await context.Query<HotelAdmin>().AnyAsync(a => a.HotelId == id)
            || await context.Query<Room>().AnyAsync(r => r.HotelId == id) || await context.Query<BranchOffering>().AnyAsync(o => o.HotelId == id)
            || await context.Query<HotelMessage>().AnyAsync(m => m.HotelId == id))
            return Result.Failure(new Error(ErrorCodes.Conflict, "A hotel with bookings or assigned administrators cannot be deleted."));
        context.Remove(hotel);
        await context.SaveChangesAsync();
        return Result.Success();
    });
    public Task<bool> HotelExistsAsync(int id) => context.Query<Hotel>().AnyAsync(h => h.Id == id);
    public Task<bool> HotelExistsAsync(string name, int countryId)
    {
        var normalized = name.Trim().ToLower();
        return context.Query<Hotel>().AnyAsync(h => h.CountryId == countryId && h.Name.ToLower() == normalized);
    }
}
