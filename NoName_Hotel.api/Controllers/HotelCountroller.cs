using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Application.Services;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using static NuGet.Packaging.PackagingConstants;

namespace NoName_Hotel.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class HotelCountroller(IHotelService hotelService) : BaseApiController
    {
        //[HttpGet]
        //public async Task<ActionResult<IEnumerable<GetHotelDto>>> GetAllHotels()
        //{
        //    //select * from hotels, left join countries on Hotels CountryId = countries.countryId
        //    var result = await hotelService.GetHotelsAsync();
        //    return ToActionResult(result);
        //}


        // GET: api/Hotels
        [HttpGet]
        public async Task<ActionResult<PageResult<GetHotelDto>>> GetHotels(
            [FromQuery] PaginationParameters paginationParameters, 
            [FromQuery] HotelFilterParameters hotelFilterParameters)
        {
            var result = await hotelService.GetHotelsAsync(paginationParameters, hotelFilterParameters);
            return ToActionResult(result);
        }




        [HttpGet("{id}")]
        public async Task<ActionResult<GetHotelDto>> GetHotel(int id)
        {
            var hotel = await hotelService.GetHotelAsync(id);

            return ToActionResult(hotel);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = RoleNames.Administrator)]
        public async Task<IActionResult> UpdateHotel(int id, UpdateHotelDto updateHotel)
        {
            if (id != updateHotel.Id)
            {
                return BadRequest("Id route value must match payload Id.");
            }

            var result = await hotelService.UpdateHotelAsync(id, updateHotel);
            return ToActionResult(result);
        }

        [HttpPost]
        [Authorize(Roles = RoleNames.Administrator)]
        public async Task<ActionResult<GetHotelDto>> PostHotel(CreateHotelDto createHotel)
        {
            var hotel = await hotelService.CreateHotelAsync(createHotel);

            if (!hotel.IsSuccess) return MapErrorsToResponse(hotel.Errors);

            return CreatedAtAction(nameof(GetHotel), new { id = hotel.Value!.Id }, hotel.Value);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = RoleNames.Administrator)]
        public async Task<IActionResult> DeleteHotel(int id)
        {
            var result = await hotelService.DeleteHotelAsync(id);
            return ToActionResult(result);
        }


    }
}
