using AutoMapper;
using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Application.Mappers
{

    public class CountryNameResolver : IValueResolver<Hotel, GetHotelDto, string>
    {
        public string Resolve(Hotel sourse, GetHotelDto destination, string destMember, ResolutionContext context)
        {
            return sourse.Country?.Name ?? string.Empty;
        }
    }
}
