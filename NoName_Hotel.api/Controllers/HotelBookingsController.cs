using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NoName_Hotel.api.AuthorizationFilters;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
//using static NuGet.Packaging.PackagingConstants;

namespace NoName_Hotel.api.Controllers
{
    [Route("api/hotels/{hotelId:int}/bookings")]
    [Route("api/v1/hotels/{hotelId:int}/bookings")]
    [Route("api/v1/branches/{hotelId:int}/bookings")]
    [ApiController]
    [Authorize]
    [EnableRateLimiting("perUser")]
    public class HotelBookingsController(IBookingService bookingService) : BaseApiController
    {
        [HttpGet]
        public async Task<ActionResult<PageResult<GetBookingDto>>> GetBookings(
            [FromRoute] int hotelId,
            [FromQuery] PaginationParameters paginationParameters,
            [FromQuery] BookingFilterParameters bookingFilterParameters, CancellationToken cancellationToken)
        {
            var result = await bookingService.GetUserBookingsForHotelAsync(hotelId, paginationParameters, bookingFilterParameters, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost]
        public async Task<ActionResult<GetBookingDto>> CreateBooking([FromRoute] int hotelId, [FromBody] CreateBookingDto createBooking, CancellationToken cancellationToken)
        {
            if (hotelId != createBooking.HotelId) return BadRequest("The branch in the route must match HotelId.");
            var result = await bookingService.CreateBookingAsync(createBooking, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPut("{bookingId:int}")]
        public async Task<ActionResult<GetBookingDto>> UpdatingBooking(
            [FromRoute] int hotelId, [FromRoute] int bookingId, 
            [FromBody] UpdateBookingDto updateBooking, CancellationToken cancellationToken)
        {
            var result = await bookingService.UpdateBookingAsync(hotelId, bookingId, updateBooking, cancellationToken);

            return ToActionResult(result);
        }

        [HttpPost("{bookingId:int}/cancel")]
        public async Task<IActionResult> CancelBooking([FromRoute] int hotelId, [FromRoute] int bookingId, CancellationToken cancellationToken)
        {
            var result = await bookingService.CancelBookingAsync(hotelId, bookingId, cancellationToken);
            return ToActionResult(result);
        }

        [HttpGet("admin")]
        [HotelOrSystemAdmin]
        public async Task<ActionResult<PageResult<GetBookingDto>>> GetBookingsByAdmin(
            [FromRoute] int hotelId,
            [FromQuery] PaginationParameters paginationParameters,
            [FromQuery] BookingFilterParameters bookingFilterParameters, CancellationToken cancellationToken)
        {
            var result = await bookingService.GetBookingPerHotelAsync(hotelId, paginationParameters, bookingFilterParameters, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost("{bookingId:int}/admin/cancel")]
        [HotelOrSystemAdmin]
        public async Task<IActionResult> AdminCancelBooking([FromRoute] int bookingId, [FromRoute] int hotelId, CancellationToken cancellationToken)
        {
            var result = await bookingService.AdminCancelBookingAsync(hotelId, bookingId, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPut("{bookingId:int}/admin/confirm")]
        [HotelOrSystemAdmin]
        public async Task<IActionResult> AdminConfirmBooking([FromRoute] int hotelId, [FromRoute] int bookingId, CancellationToken cancellationToken)
        {
            var result = await bookingService.AdminConfirmBookingAsync(hotelId, bookingId, cancellationToken);
            return ToActionResult(result);
        }

        [HttpGet("requests/{requestId:guid}")]
        public async Task<ActionResult<GetBookingDto>> GetBookingRequest(int hotelId, Guid requestId, CancellationToken cancellationToken) =>
            ToActionResult(await bookingService.GetBookingRequestAsync(hotelId, requestId, cancellationToken));
    }


}
