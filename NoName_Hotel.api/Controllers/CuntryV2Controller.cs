using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace NoName_Hotel.api.Controllers;

[Route("api/v{version:apiVersion}/countries")] //v2 versioning
[ApiController]
[ApiVersion("2.0", Deprecated = true)] // default is false
public class CuntriesV2Controller : Controller
{
    [HttpGet]
    public IActionResult Getcountries(
        [FromQuery] int? pageNumber = 1,
        [FromQuery] int? pageSize = 10)
    {
        //version 2 implementation
        return Ok(new
        {
            Version = "2.0",
            Message = "Enhanced country endpoint with pagination",
            PageNumber = pageNumber,
            PageSize = pageSize
        });


    }
}
