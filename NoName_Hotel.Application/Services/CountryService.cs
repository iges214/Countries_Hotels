using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Domain.Data;
using NoName_Hotel.Application.DTO.CountryDto;
using NoName_Hotel.Common.Results;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Common.QuerableXtensions.Extensions;
using Microsoft.AspNetCore.JsonPatch;


namespace NoName_Hotel.Application.Services
{
    public class CountriesService(NoName_hotel_DbContext context, IMapper mapper) : ICountriesService
    {
        public async Task<Result<IEnumerable<GetCountriesDto>>> GetCountriesAsync(CountryFilterParameters countryFilterParameters)
        {
            var query = context.Countries.AsQueryable();
            if (!string.IsNullOrWhiteSpace(countryFilterParameters.Search))
            {
                var term = countryFilterParameters.Search.Trim();

                query = query.Where(a => EF.Functions.Like(a.Name, $"%{term}%") ||
                        EF.Functions.Like(a.ShortName, $"%{term}$"));
            }
            var countries = await query
                .AsNoTracking()
                .ProjectTo<GetCountriesDto>(mapper.ConfigurationProvider)
                .ToListAsync();

            return Result<IEnumerable<GetCountriesDto>>.Success(countries);
        }

        public async Task<Result<GetCountryDto>> GetCountryAsync(int id)
        {
            var country = await context.Countries
                .AsNoTracking()
                .Where(q => q.CountryId == id)
                .ProjectTo<GetCountryDto>(mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            return country is null
                ? Result<GetCountryDto>.Failure(new Error(ErrorCodes.NotFound, $"Country '{id}' was not found."))
                : Result<GetCountryDto>.Success(country);
        }

        public async Task<Result<GetCountryDto>> CreateCountryAsync(CreateCountryDto createDto)
        {
            try
            {
                var exists = await CountryExistsAsync(createDto.Name);
                if (exists)
                {
                    return Result<GetCountryDto>.Failure(new Error(ErrorCodes.Conflict, $"Country with name '{createDto.Name}' already exists."));
                }

                var country = mapper.Map<Country>(createDto);
                context.Countries.Add(country);
                await context.SaveChangesAsync();

                var dto = mapper.Map<GetCountryDto>(country);

                return Result<GetCountryDto>.Success(dto);
            }
            catch
            {
                return Result<GetCountryDto>.Failure(new Error(ErrorCodes.Failure, "An unexpected error occurred while creating the country."));
            }
        }

        public async Task<Result> UpdateCountryAsync(int id, UpdateCountryDto updateDto)
        {
            try
            {
                if (id != updateDto.Id)
                {
                    return Result.BadRequest(new Error(ErrorCodes.Validation, "Id route value does not match payload Id."));
                }

                var country = await context.Countries.FindAsync(id);
                if (country is null)
                {
                    return Result.NotFound(new Error(ErrorCodes.NotFound, $"Country '{id}' was not found."));
                }

                // Use AutoMapper to map incoming DTO onto the tracked entity
                mapper.Map(updateDto, country);
                await context.SaveChangesAsync();

                return Result.Success();
            }
            catch
            {
                return Result.Failure(new Error(ErrorCodes.Failure, "An unexpected error occurred while updating the country."));
            }
        }

        public async Task<Result> DeleteCountryAsync(int id)
        {
            try
            {
                var country = await context.Countries.FindAsync(id);
                if (country is null)
                {
                    return Result.NotFound(new Error(ErrorCodes.NotFound, $"Country '{id}' was not found."));
                }

                context.Countries.Remove(country);
                await context.SaveChangesAsync();

                return Result.Success();
            }
            catch
            {
                return Result.Failure(new Error(ErrorCodes.Failure, "An unexpected error occurred while deleting the country."));
            }
        }

        public async Task<bool> CountryExistsAsync(int id)
        {
            return await context.Countries.AsNoTracking().AnyAsync(e => e.CountryId == id);
        }

        public async Task<bool> CountryExistsAsync(string name)
        {
            return await context.Countries
                .AsNoTracking()
                .AnyAsync(c => c.Name.ToLower().Trim() == name.ToLower().Trim());
        }

        public async Task<Result<GetCountryHotelsDto>> GetCountryHotelsAsync(int countryId, PaginationParameters paginationParameters, CountryFilterParameters countryFilterParameters)
        {
            var exist = await CountryExistsAsync(countryId);

            if (!exist)
            {
                return Result<GetCountryHotelsDto>.Failure(
                    new Error(ErrorCodes.NotFound, $"Country '{countryId}' was not found"));
            }

            var countryName = await context.Countries
                .Where(a => a.CountryId == countryId)
                .Select(a => a.Name)
                .SingleAsync();

            var HotelsQuery = context.Hotels
                .Where(a => a.CountryId == countryId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(countryFilterParameters.Search))
            {
                var terms = countryFilterParameters.Search.Trim();

                HotelsQuery = HotelsQuery.Where(a => EF.Functions.Like(a.Name, $"%{terms}$"));
            }

            HotelsQuery = (countryFilterParameters.SortBy?.Trim().ToLowerInvariant()) switch
            {
                "name" => countryFilterParameters.SortDescending ?
                        HotelsQuery.OrderByDescending(h => h.Name) :
                        HotelsQuery.OrderBy(h => h.Name),
                "rating" => countryFilterParameters.SortDescending ?
                        HotelsQuery.OrderByDescending(h => h.Rating) :
                        HotelsQuery.OrderBy(h => h.Rating),
                _ => HotelsQuery.OrderBy(h => h.Name)
            };

            var pageHotel = await HotelsQuery
                      .ProjectTo<GetHotelSlimDto>(mapper.ConfigurationProvider)
                      .ToPageResultAsync(paginationParameters);

            var result = new GetCountryHotelsDto
            {
                Id = countryId,
                Name = countryName,
                Hotels = pageHotel
            };

            return Result<GetCountryHotelsDto>.Success(result);

        }

        public async Task<Result> PatchCountryAsycn(int Id, JsonPatchDocument<UpdateCountryDto> patcDoc)
        {
            var country = await context.Countries.FindAsync(Id);

            if(country is null)
            {
                return Result.BadRequest(new Error(ErrorCodes.NotFound, $"country '{Id}' was not found"));
            }

            var countryDto = mapper.Map<UpdateCountryDto>(country);
            patcDoc.ApplyTo(countryDto);


            if(countryDto.Id != Id)
            {
                Result.BadRequest(new Error(ErrorCodes.Validation, "cannot modify the Id field"));
            }

            var normalized = countryDto.Name.ToLower().Trim();
            var duplicates = await context.Countries
                .AnyAsync(a => a.Name.ToLower().Trim() == normalized && a.CountryId != Id);

            if (duplicates)
            {
                return Result.Failure(new Error(ErrorCodes.Conflict, $"country with name '{countryDto.Name}' already exist"));
            }

            mapper.Map(country, country);
            context.Entry(country).State = EntityState.Modified;
            await context.SaveChangesAsync();

            return Result.Success();
        }
    }
}