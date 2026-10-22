using AutoMapper;
using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Application.Mappers;
    public class HotelMappingProfile : Profile
    {
        public HotelMappingProfile()
        {
            CreateMap<Hotel, GetHotelDto>()
                .ForMember(d => d.CountryName, cfg => cfg.MapFrom<CountryNameResolver>());
            CreateMap<Hotel, GetHotelSlimDto>();
            CreateMap<CreateHotelDto, Hotel>();
        }
    }