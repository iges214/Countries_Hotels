using NoName_Hotel.api.DTO.CountryDto;
using NoName_Hotel.api.Results;

namespace NoName_Hotel.api.Contracts
{
    public interface ICountriesService
    {
        Task<bool> CountryExistsAsync(int id);
        Task<bool> CountryExistsAsync(string name);
        Task<Result<GetCountryDto>> CreateCountryAsync(CreateCountryDto createDto);
        Task<Result> DeleteCountryAsync(int id);
        Task<Result<IEnumerable<GetCountriesDto>>> GetCountriesAsync();
        Task<Result<GetCountryDto>> GetCountryAsync(int id);
        Task<Result> UpdateCountryAsync(int id, UpdateCountryDto updateDto);
    }
}