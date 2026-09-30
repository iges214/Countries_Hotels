using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.api.Contracts;
using NoName_Hotel.api.Data;
using NoName_Hotel.api.DTO.HotelDto;
using NoName_Hotel.api.Results;
using NoName_Hotel.api.Entities.Models;
using NoName_Hotel.api.Constants;

namespace NoName_Hotel.api.Services;

    public class HotelService(NoName_hotel_DbContext context,
        ICountriesService countriesService,
        IMapper mapper) : IHotelService
    {
        public async Task<Result<IEnumerable<GetHotelDto>>> GetHotelsAsync()
        {
            var hotels = await context.Hotels
                .ProjectTo<GetHotelDto>(mapper.ConfigurationProvider)
                .ToListAsync();

            return Result<IEnumerable<GetHotelDto>>.Success(hotels);
        }

        public async Task<Result<GetHotelDto>> GetHotelAsync(int id)
        {
            var hotel = await context.Hotels
                .Where(h => h.Id == id)
                .ProjectTo<GetHotelDto>(mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (hotel is null)
            {
                return Result<GetHotelDto>.Failure(new Error(ErrorCodes.NotFound, $"Hotel '{id}' was not found."));
            }

            return Result<GetHotelDto>.Success(hotel);
        }

        public async Task<Result<GetHotelDto>> CreateHotelAsync(CreateHotelDto hotelDto)
        {
            var countryExists = await countriesService.CountryExistsAsync(hotelDto.CountryId);
            if (!countryExists)
            {
                return Result<GetHotelDto>.Failure(new Error(ErrorCodes.NotFound, $"Country '{hotelDto.CountryId}' was not found."));
            }

            var duplicate = await HotelExistsAsync(hotelDto.Name, hotelDto.CountryId);
            if (duplicate)
            {
                return Result<GetHotelDto>.Failure(new Error(ErrorCodes.Conflict, $"Hotel '{hotelDto.Name}' already exists in the selected country."));
            }

            var hotel = mapper.Map<Hotel>(hotelDto);
            context.Hotels.Add(hotel);
            await context.SaveChangesAsync();

            var dto = await context.Hotels
                .Where(h => h.Id == hotel.Id)
                .ProjectTo<GetHotelDto>(mapper.ConfigurationProvider)
                .FirstAsync();

            return Result<GetHotelDto>.Success(dto);
        }

        public async Task<Result> UpdateHotelAsync(int id, UpdateHotelDto updateDto)
        {
            if (id != updateDto.Id)
            {
                return Result.BadRequest(new Error(ErrorCodes.Validation, "Id route value does not match payload Id."));
            }

            var hotel = await context.Hotels.FindAsync(id);
            if (hotel is null)
            {
                return Result.NotFound(new Error(ErrorCodes.NotFound, $"Hotel '{id}' was not found."));
            }

            var countryExists = await countriesService.CountryExistsAsync(updateDto.CountryId);
            if (!countryExists)
            {
                return Result.NotFound(new Error(ErrorCodes.NotFound, $"Country '{updateDto.CountryId}' was not found."));
            }

            mapper.Map(updateDto, hotel);

            context.Hotels.Update(hotel);
            await context.SaveChangesAsync();

            return Result.Success();
        }

        public async Task<Result> DeleteHotelAsync(int id)
        {
            var affected = await context.Hotels
                .Where(q => q.Id == id)
                .ExecuteDeleteAsync();

            if (affected == 0)
            {
                return Result.NotFound(new Error(ErrorCodes.NotFound, $"Hotel '{id}' was not found."));
            }

            return Result.Success();
        }

        public async Task<bool> HotelExistsAsync(int id)
        {
            return await context.Hotels.AnyAsync(e => e.Id == id);
        }

        public async Task<bool> HotelExistsAsync(string name, int countryId)
        {
            return await context.Hotels
                .AnyAsync(e => e.Name.ToLower().Trim() == name.ToLower().Trim() && e.CountryId == countryId);
        }
    }