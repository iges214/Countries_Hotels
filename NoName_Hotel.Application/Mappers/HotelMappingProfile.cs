using AutoMapper;
using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Domain.Entities.Models;
namespace NoName_Hotel.Application.Mappers;
public class HotelMappingProfile : Profile
{
    public HotelMappingProfile()
    {
        CreateMap<Hotel, GetHotelDto>()
            .ForCtorParam("CountryName", o => o.MapFrom(s => s.Country != null ? s.Country.Name : string.Empty));
        CreateMap<Hotel, GetHotelSlimDto>();
        CreateMap<CreateHotelDto, Hotel>(MemberList.Source);
        CreateMap<UpdateHotelDto, Hotel>(MemberList.Source);
    }
}
