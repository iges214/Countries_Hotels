using AutoMapper;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Domain.Entities.Models;
namespace NoName_Hotel.Application.Mappers;
public class BookingMappingProfile : Profile
{
    public BookingMappingProfile()
    {
        CreateMap<Booking, GetBookingDto>()
            .ForCtorParam("HotelName", o => o.MapFrom(s => s.Hotel != null ? s.Hotel.Name : string.Empty))
            .ForCtorParam("Guests", o => o.MapFrom(s => s.Guest))
            .ForCtorParam("Status", o => o.MapFrom(s => s.Status.ToString()));
        CreateMap<CreateBookingDto, Booking>(MemberList.Source)
            .ForMember(d => d.Guest, o => o.MapFrom(s => s.Guests));
        CreateMap<UpdateBookingDto, Booking>(MemberList.Source)
            .ForMember(d => d.Guest, o => o.MapFrom(s => s.Guests));
    }
}
