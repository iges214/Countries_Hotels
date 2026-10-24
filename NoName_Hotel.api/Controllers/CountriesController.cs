using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using NoName_Hotel.api.Filters;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.CountryDto;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.QuerableXtensions.Filtering;
using NoName_Hotel.Common.QuerableXtensions.Paging;

namespace NoName_Hotel.api.Controllers
{
    [Route("api/[controller]")]
    [Route("api/v1/countries")]
    [ApiController]
    [Authorize]
    [ServiceFilter(typeof(CatalogCacheInvalidationFilter))]
    [OutputCache(PolicyName = CacheConstants.AuthenticatedUserCachingPolicy)]
    public class CountriesController(ICountriesService countryService) : BaseApiController
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GetCountriesDto>>> GetCountries([FromQuery] CountryFilterParameters countryFilterParameters)
        {
            var countries = await countryService.GetCountriesAsync(countryFilterParameters);

            return ToActionResult(countries);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GetCountryDto>> GetCountry(int id)
        {
            var country = await countryService.GetCountryAsync(id);


            return ToActionResult(country);
        }

        // GET: api/Countries/{id}/hotels
        [HttpGet("{countryId:int}/hotels")]
        public async Task<ActionResult<GetCountryHotelsDto>> GetCountryHotels(
             [FromRoute] int countryId,
             [FromQuery] PaginationParameters paginationParameters,
             [FromQuery] CountryFilterParameters countryFilterParameters)
        {
            var result = await countryService.GetCountryHotelsAsync(countryId, paginationParameters, countryFilterParameters);
            return ToActionResult(result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = RoleNames.Administrator)]
        public async Task<IActionResult> UpdateCountry(int id, UpdateCountryDto updateCountry)
        {
            var result = await countryService.UpdateCountryAsync(id, updateCountry);


            return ToActionResult(result);
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = RoleNames.Administrator)]
        public async Task<IActionResult> PatchCountry(int id, [FromBody] JsonPatchDocument<UpdateCountryDto> patchDoc)
        {
            if(patchDoc == null)
            {
                return BadRequest("patch document is required");
            }


                
            var result = await countryService.PatchCountryAsycn(id, patchDoc);

            return ToActionResult(result);
        }

        [HttpPost]
        [Authorize(Roles = RoleNames.Administrator)]
        public async Task<ActionResult<GetCountryDto>> CreateCountry(CreateCountryDto createCountry)
        {
            var resultDto = await countryService.CreateCountryAsync(createCountry);
            if (!resultDto.IsSuccess) return MapErrorsToResponse(resultDto.Errors);

            return CreatedAtAction(nameof(GetCountry), new { id = resultDto.Value!.Id }, resultDto.Value);

        }

        [HttpDelete("{id}")]
        [Authorize(Roles = RoleNames.Administrator)]
        public async Task<IActionResult> DeleteCountry(int id)
        {
            var result = await countryService.DeleteCountryAsync(id);
            return ToActionResult(result);
        }



    }
}
