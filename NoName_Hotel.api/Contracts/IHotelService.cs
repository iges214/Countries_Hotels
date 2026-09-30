using NoName_Hotel.api.DTO.CountryDto;
using NoName_Hotel.api.DTO.HotelDto;
using NoName_Hotel.api.Results;

namespace NoName_Hotel.api.Contracts
{
    public interface IHotelService
    {

        // Keep these for quick checks elsewhere if needed
        Task<bool> HotelExistsAsync(int id);
        Task<bool> HotelExistsAsync(string name, int countryId);
        Task<Result<IEnumerable<GetHotelDto>>> GetHotelsAsync();
        Task<Result<GetHotelDto>> GetHotelAsync(int id);
        Task<Result<GetHotelDto>> CreateHotelAsync(CreateHotelDto createDto);
        Task<Result> UpdateHotelAsync(int id, UpdateHotelDto updateDto);
        Task<Result> DeleteHotelAsync(int id);
    }

        
    }