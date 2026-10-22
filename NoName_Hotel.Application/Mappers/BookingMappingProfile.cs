using AutoMapper;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Application.Mappers
{
    public class BookingMappingProfile : Profile
    {
        public BookingMappingProfile()
        {
            CreateMap<Booking, GetBookingDto>()
                .ForMember(d => d.HotelName, o => o.MapFrom(s => s.Hotel!.Name))
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

            CreateMap<CreateBookingDto, Booking>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.UserId, o => o.Ignore())
                .ForMember(d => d.Totalprice, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.CreateAt, o => o.Ignore())
                .ForMember(d => d.UpdateAt, o => o.Ignore())
                .ForMember(d => d.Hotel, o => o.Ignore());

            CreateMap<UpdateBookingDto, Booking>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.UserId, o => o.Ignore())
                .ForMember(d => d.Totalprice, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.CreateAt, o => o.Ignore())
                .ForMember(d => d.UpdateAt, o => o.Ignore())
                .ForMember(d => d.Hotel, o => o.Ignore());
        }
    }
}
