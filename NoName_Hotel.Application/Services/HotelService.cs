using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Domain.Data;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Common.Results;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Extensions;

namespace NoName_Hotel.Application.Services;

    public class HotelService(NoName_hotel_DbContext context,
        ICountriesService countriesService,
        IMapper mapper) : IHotelService
    {
        public async Task<Result<PageResult<GetHotelDto>>> GetHotelsAsync(
            PaginationParameters paginationParameters, HotelFilterParameters hotelFilterParameters)
        {

        var query = context.Hotels.AsQueryable();
        if (hotelFilterParameters.CountryId.HasValue)
        {
            query = query.Where(a => a.CountryId == hotelFilterParameters.CountryId);
        }

        if (hotelFilterParameters.MinRating.HasValue)
        {
            query = query.Where(a => a.Rating >= hotelFilterParameters.MinRating);
        }

        if (hotelFilterParameters.MaxRating.HasValue)
        {
            query = query.Where(a => a.Rating <= hotelFilterParameters.MaxRating);
        }

        if (hotelFilterParameters.MinPrice.HasValue)
        {
            query = query.Where(a => a.PerNightRate >= hotelFilterParameters.MinPrice);
        }

        if (hotelFilterParameters.MaxPrice.HasValue)
        {
            query = query.Where(a => a.PerNightRate <= hotelFilterParameters.MaxPrice);
        }

        if (!string.IsNullOrWhiteSpace(hotelFilterParameters.Location))
        {
            query = query.Where(a => a.Address.Contains(hotelFilterParameters.Location));
        }

        //search params
        if (!string.IsNullOrWhiteSpace(hotelFilterParameters.Location))
        {
            query = query.Where(a => a.Name.Contains(hotelFilterParameters.Search) 
            || a.Address.Contains(hotelFilterParameters.Search));
        }

        query = hotelFilterParameters.SortBy?.ToLower() switch
        {
            "name" => hotelFilterParameters.SortDescending ?
                      query.OrderByDescending(a => a.Name) :
                      query.OrderBy(b => b.Name),
            "rating" => hotelFilterParameters.SortDescending ?
                      query.OrderByDescending(a => a.Rating) :
                      query.OrderBy(b => b.Rating),
            "price" => hotelFilterParameters.SortDescending ?
                     query.OrderByDescending(a => a.PerNightRate) :
                     query.OrderBy(a => a.PerNightRate),
            _ => query.OrderBy(a => a.Name)
        };

        var hotels = await query
            .Include(q => q.Country)
            .ProjectTo<GetHotelDto>(mapper.ConfigurationProvider)
            .ToPageResultAsync(paginationParameters);

        return Result<PageResult<GetHotelDto>>.Success(hotels);
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
            if (hotel == null)
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