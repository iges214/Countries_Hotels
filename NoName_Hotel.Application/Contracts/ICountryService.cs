using Microsoft.AspNetCore.JsonPatch;
using NoName_Hotel.Application.DTO.CountryDto;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.Results;

namespace NoName_Hotel.Application.Contracts
{
    public interface ICountriesService
    {
        Task<bool> CountryExistsAsync(int id);
        Task<bool> CountryExistsAsync(string name);
        Task<Result<GetCountryDto>> CreateCountryAsync(CreateCountryDto createDto);
        Task<Result> DeleteCountryAsync(int id);
        Task<Result<IEnumerable<GetCountriesDto>>> GetCountriesAsync(CountryFilterParameters countryFilterParameters);
        Task<Result<GetCountryDto>> GetCountryAsync(int id);
        Task<Result> UpdateCountryAsync(int id, UpdateCountryDto updateDto);
        Task<Result<GetCountryHotelsDto>> GetCountryHotelsAsync(int countryId, PaginationParameters paginationParameters, CountryFilterParameters countryFilterParameters);
        Task<Result> PatchCountryAsycn(int Id, JsonPatchDocument<UpdateCountryDto> patcDoc);
    }



}