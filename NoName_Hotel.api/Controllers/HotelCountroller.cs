using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Application.DTO.HotelDto;
using NoName_Hotel.Application.Services;
using NoName_Hotel.api.Filters;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.api.AuthorizationFilters;
using NoName_Hotel.Domain.Entities.Models;
using Microsoft.EntityFrameworkCore;
using static NuGet.Packaging.PackagingConstants;

namespace NoName_Hotel.api.Controllers
{
    [Route("api/[controller]")]
    //[Route("api/hotels")]
    [Route("api/v1/hotels")]
    [Route("api/v1/branches")]
    [ServiceFilter(typeof(CatalogCacheInvalidationFilter))]
    [ApiController]
    [Authorize]
    public class HotelCountroller(IHotelService hotelService, IHotelRepository repository, IUserServices currentUser) : BaseApiController
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
        [AllowAnonymous]
        public async Task<ActionResult<PageResult<GetHotelDto>>> GetHotels(
            [FromQuery] PaginationParameters paginationParameters, 
            [FromQuery] HotelFilterParameters hotelFilterParameters,
            [FromQuery] bool includeInactive = false)
        {
            string? administratorId = null;
            if (includeInactive)
            {
                if (User.Identity?.IsAuthenticated != true) return Unauthorized();
                if (!User.IsInRole(RoleNames.Administrator))
                {
                    administratorId = currentUser.UserId;
                    if (!await repository.Query<HotelAdmin>().AnyAsync(a => a.UserId == administratorId)) return Forbid();
                }
            }
            var result = await hotelService.GetHotelsAsync(paginationParameters, hotelFilterParameters, includeInactive, administratorId);
            return ToActionResult(result);
        }

        [HttpGet("default")]
        [AllowAnonymous]
        public async Task<ActionResult<GetHotelDto>> GetDefaultBranch() =>
            ToActionResult(await hotelService.GetDefaultBranchAsync());




        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<ActionResult<GetHotelDto>> GetHotel(int id)
        {
            var hotel = await hotelService.GetHotelAsync(id);
            // Existing guests need branch details to view/edit their reservation after
            // deactivation. This exception never reopens its public room catalogue.
            if (!hotel.IsSuccess && hotel.Errors.Any(e => e.Code == ErrorCodes.NotFound) && User.Identity?.IsAuthenticated == true)
            {
                var userId = currentUser.UserId;
                var mayReadInactive = User.IsInRole(RoleNames.Administrator)
                    || await repository.Query<HotelAdmin>().AnyAsync(a => a.HotelId == id && a.UserId == userId)
                    || await repository.Query<Booking>().AnyAsync(b => b.HotelId == id && b.UserId == userId);
                if (mayReadInactive) hotel = await hotelService.GetHotelAsync(id, includeInactive: true);
            }

            return ToActionResult(hotel);
        }

        [HttpPost("{id:int}/default")]
        [Authorize(Roles = RoleNames.Administrator)]
        public async Task<IActionResult> SetDefaultBranch(int id) =>
            ToActionResult(await hotelService.SetDefaultBranchAsync(id));

        [HttpPut("{id:int}/active")]
        [Authorize(Roles = RoleNames.Administrator)]
        public async Task<IActionResult> SetBranchActive(int id, SetBranchActiveDto dto) =>
            ToActionResult(await hotelService.SetBranchActiveAsync(id, dto.IsActive));

        [HttpPut("{id}")]
        [HotelOrSystemAdmin]
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

    public sealed record SetBranchActiveDto(bool IsActive);
}
