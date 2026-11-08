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
            .ForCtorParam("RoomIds", o => o.MapFrom(s => s.RoomReservations.Select(a => a.RoomId).ToArray()))
            .ForCtorParam("CurrencyCode", o => o.MapFrom(s => s.Hotel != null ? s.Hotel.CurrencyCode : "NGN"))
            .ForCtorParam("Guests", o => o.MapFrom(s => s.Guest))
            .ForCtorParam("Status", o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.IsCheckedIn, o => o.MapFrom(s => s.ActualCheckInAt.HasValue && !s.ActualCheckOutAt.HasValue))
            .ForMember(d => d.IsCheckedOut, o => o.MapFrom(s => s.ActualCheckOutAt.HasValue))
            .ForMember(d => d.TimeZoneId, o => o.MapFrom(s => s.Hotel != null ? s.Hotel.TimeZoneId : "Africa/Lagos"));
        CreateMap<CreateBookingDto, Booking>(MemberList.Source)
            .ForMember(d => d.Guest, o => o.MapFrom(s => s.Guests))
            .ForSourceMember(s => s.RoomIds, o => o.DoNotValidate());
        CreateMap<UpdateBookingDto, Booking>(MemberList.Source)
            .ForMember(d => d.Guest, o => o.MapFrom(s => s.Guests))
            .ForSourceMember(s => s.RoomIds, o => o.DoNotValidate());
    }
}
