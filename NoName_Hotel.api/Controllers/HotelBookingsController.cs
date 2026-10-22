using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoName_Hotel.api.AuthorizationFilters;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using static NuGet.Packaging.PackagingConstants;

namespace NoName_Hotel.api.Controllers
{
    [Route("api/hotels/{hotelId:int}/bookings")]
    [ApiController]
    [Authorize]
    public class HotelBookingsController(IBookingService bookingService) : BaseApiController
    {
        [HttpGet]
        public async Task<ActionResult<PageResult<GetBookingDto>>> GetBookings(
            [FromRoute] int hotelId,
            [FromQuery] PaginationParameters paginationParameters,
            [FromQuery] BookingFilterParameters bookingFilterParameters)
        {
            var result = await bookingService.GetUserBookingsForHotelAsync(hotelId, paginationParameters, bookingFilterParameters);
            return ToActionResult(result);
        }

        [HttpPost]
        public async Task<ActionResult<GetBookingDto>> CreateBooking([FromRoute] int hotelId, [FromBody] CreateBookingDto createBooking)
        {
            var result = await bookingService.CreateBookingAsync(createBooking);
            return ToActionResult(result);
        }

        [HttpPut("{bookingId:int}")]
        public async Task<ActionResult<GetBookingDto>> UpdatingBooking(
            [FromRoute] int hotelId, [FromRoute] int bookingId, 
            [FromBody] UpdateBookingDto updateBooking)
        {
            var result = await bookingService.UpdateBookingAsync(hotelId, bookingId, updateBooking);

            return ToActionResult(result);
        }

        [HttpPost("{bookingId:int}/cancel")]
        public async Task<IActionResult> CancelBooking([FromRoute] int hotelId, [FromRoute] int bookingId)
        {
            var result = await bookingService.CancelBookingAsync(hotelId, bookingId);
            return ToActionResult(result);
        }

        [HttpGet("admin")]
        [HotelOrSystemAdmin]
        public async Task<ActionResult<PageResult<GetBookingDto>>> GetBookingsByAdmin(
            [FromRoute] int hotelId,
            [FromQuery] PaginationParameters paginationParameters,
            [FromQuery] BookingFilterParameters bookingFilterParameters)
        {
            var result = await bookingService.GetBookingPerHotelAsync(hotelId, paginationParameters, bookingFilterParameters);
            return ToActionResult(result);
        }

        [HttpPost("{bookingId:int}/admin/cancel")]
        [HotelOrSystemAdmin]
        public async Task<IActionResult> AdminCancelBooking([FromRoute] int bookingId, [FromRoute] int hotelId)
        {
            var result = await bookingService.AdminCancelBookingAsync(hotelId, bookingId);
            return ToActionResult(result);
        }

        [HttpPut("{bookingId:int}/admin/confirm")]
        [HotelOrSystemAdmin]
        public async Task<IActionResult> AdminConfrimBooking([FromRoute] int hotelId, [FromRoute] int bbookingId)
        {
            var result = await bookingService.AdminConfirmBookingAsync(hotelId, bbookingId);
            return ToActionResult(result);
        }

    }


}
