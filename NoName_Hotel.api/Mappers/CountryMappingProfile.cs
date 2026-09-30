using AutoMapper;
using NoName_Hotel.api.DTO.CountryDto;
using NoName_Hotel.api.Entities.Models;

namespace NoName_Hotel.api.Mappers
{
    public class CountryMappingProfile : Profile
    {
        public CountryMappingProfile()
        {
            CreateMap<Country, GetCountryDto>()
                .ForMember(d => d.Id, opt => opt.MapFrom(s=> s.CountryId));
            CreateMap<CreateCountryDto, Country>();
            CreateMap<Country, GetCountriesDto>();
        }
    }
}
