using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.api.Contracts;
using NoName_Hotel.api.DTO.HotelDto;
using NoName_Hotel.api.Entities.Models;

namespace NoName_Hotel.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HotelCountroller(IHotelService hotelService) : BaseApiController
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GetHotelDto>>> GetAllHotels()
        {
            //select * from hotels, left join countries on Hotels CountryId = countries.countryId
            var hotels = await hotelService.GetHotelsAsync();
            return ToActionResult(hotels);
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<GetHotelDto>> GetHotel(int id)
        {
            var hotel = await hotelService.GetHotelAsync(id);

            return ToActionResult(hotel);
        }

        [HttpPut("{id}")]
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
        public async Task<ActionResult<GetHotelDto>> PostHotel(CreateHotelDto createHotel)
        {
            var hotel = await hotelService.CreateHotelAsync(createHotel);

            if (!hotel.IsSuccess) return MapErrorsToResponse(hotel.Errors);

            return CreatedAtAction(nameof(GetHotel), new { id = hotel.Value!.Id }, hotel.Value);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHotel(int id)
        {
            var result = await hotelService.DeleteHotelAsync(id);
            return ToActionResult(result);
        }


    }
}
