using Microsoft.AspNetCore.Mvc;
using NoName_Hotel.api.Contracts;
using NoName_Hotel.api.DTO.CountryDto;
using NoName_Hotel.api.Results;

namespace NoName_Hotel.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CountriesController(ICountriesService countryService) : BaseApiController
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GetCountriesDto>>> GetCountries()
        {
            var countries = await countryService.GetCountriesAsync();

            return ToActionResult(countries);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GetCountryDto>> GetCountry(int id)
        {
            var country = await countryService.GetCountryAsync(id);


            return ToActionResult(country);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCountry(int id, UpdateCountryDto updateCountry)
        {
            var result = await countryService.UpdateCountryAsync(id, updateCountry);


            return ToActionResult(result);
        }

        [HttpPost]
        public async Task<ActionResult<GetCountryDto>> CreateCountry(CreateCountryDto createCountry)
        {
            var resultDto = await countryService.CreateCountryAsync(createCountry);
            if (!resultDto.IsSuccess) return MapErrorsToResponse(resultDto.Errors);

            return CreatedAtAction(nameof(GetCountry), new {id = resultDto.Value!.Id }, resultDto.Value);

        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCountry(int id)
        {
            var result = await countryService.DeleteCountryAsync(id);
            return ToActionResult(result);
        }



    }
}
