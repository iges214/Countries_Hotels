using AutoMapper;
using NoName_Hotel.api.DTO.HotelDto;
using NoName_Hotel.api.Entities.Models;

namespace NoName_Hotel.api.Mappers
{
    public class HotelMappingProfile : Profile
    {
        public HotelMappingProfile()
        {
            CreateMap<Hotel, GetHotelDto>()
                .ForMember(d => d.Country, cfg => cfg.MapFrom<CouyntryNameResolver>());
            CreateMap<CreateHotelDto, Hotel>();
        }
    }

    public class CouyntryNameResolver : IValueResolver<Hotel, GetHotelDto, string>
    {
        public string Resolve(Hotel sourse, GetHotelDto destination, string destMember, ResolutionContext context)
        {
            return sourse.Country?.Name ?? string.Empty;
        }
    }
}
