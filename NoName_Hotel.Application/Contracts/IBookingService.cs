using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.Results;


namespace NoName_Hotel.Application.Contracts
{
    public interface IBookingService
    {
        Task<Result<PageResult<GetBookingDto>>> GetBookingPerHotelAsync(int hotelId, PaginationParameters paginationParameters, BookingFilterParameters bookingFilterParameters);

        Task<Result> AdminCancelBookingAsync(int hotelId, int bookingId);
        Task<Result> CancelBookingAsync(int hotelId, int bookingId);
        Task<Result> AdminConfirmBookingAsync(int hotelId, int bookingId);
        Task<Result<GetBookingDto>> CreateBookingAsync(CreateBookingDto dto);
        Task<Result<GetBookingDto>> UpdateBookingAsync(int hotelId, int bookingId, UpdateBookingDto updateBookingDto);
        Task<Result<PageResult<GetBookingDto>>> GetUserBookingsForHotelAsync(int hotelId, PaginationParameters paginationParameters, BookingFilterParameters bookingFilterParameters);
    }
}