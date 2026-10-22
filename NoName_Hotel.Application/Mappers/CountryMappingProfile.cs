using AutoMapper;
using NoName_Hotel.Application.DTO.CountryDto;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Application.Mappers
{
    public class CountryMappingProfile : Profile
    {
        public CountryMappingProfile()
        {
            CreateMap<Country, GetCountryDto>()
                .ForMember(d => d.Id, opt => opt.MapFrom(s=> s.CountryId));
            CreateMap<Country, GetCountriesDto>()
                .ForMember(d => d.Id, opt => opt.MapFrom(s => s.CountryId));
            CreateMap<CreateCountryDto, Country>();
        }
    }
}
