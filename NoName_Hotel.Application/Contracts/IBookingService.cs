using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.Results;


namespace NoName_Hotel.Application.Contracts
{
    public interface IBookingService
    {
        Task<Result<PageResult<GetBookingDto>>> GetBookingPerHotelAsync(int hotelId, PaginationParameters paginationParameters, BookingFilterParameters bookingFilterParameters, CancellationToken cancellationToken = default);

        Task<Result> AdminCancelBookingAsync(int hotelId, int bookingId, CancellationToken cancellationToken = default);
        Task<Result> CancelBookingAsync(int hotelId, int bookingId, CancellationToken cancellationToken = default);
        Task<Result> AdminConfirmBookingAsync(int hotelId, int bookingId, CancellationToken cancellationToken = default);
        Task<Result<GetBookingDto>> CreateBookingAsync(CreateBookingDto dto, CancellationToken cancellationToken = default);
        Task<Result<GetBookingDto>> GetBookingRequestAsync(int hotelId, Guid requestId, CancellationToken cancellationToken = default);
        Task<Result<GetBookingDto>> UpdateBookingAsync(int hotelId, int bookingId, UpdateBookingDto updateBookingDto, CancellationToken cancellationToken = default);
        Task<Result<PageResult<GetBookingDto>>> GetUserBookingsForHotelAsync(int hotelId, PaginationParameters paginationParameters, BookingFilterParameters bookingFilterParameters, CancellationToken cancellationToken = default);
        Task<Result<PageResult<GetBookingDto>>> GetUserBookingsAsync(PaginationParameters paginationParameters, CancellationToken cancellationToken = default);
    }
}
