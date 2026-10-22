using NoName_Hotel.Application.DTO.CountryDto;
using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.Results;

namespace NoName_Hotel.Application.Contracts
{
    public interface IHotelService
    {

        // Keep these for quick checks elsewhere if needed
        Task<bool> HotelExistsAsync(int id);
        Task<bool> HotelExistsAsync(string name, int countryId);
        Task<Result<PageResult<GetHotelDto>>> GetHotelsAsync(
            PaginationParameters paginationParameters, HotelFilterParameters hotelFilterParameters);
        Task<Result<GetHotelDto>> GetHotelAsync(int id);
        Task<Result<GetHotelDto>> CreateHotelAsync(CreateHotelDto createDto);
        Task<Result> UpdateHotelAsync(int id, UpdateHotelDto updateDto);
        Task<Result> DeleteHotelAsync(int id);
    }

        
    }