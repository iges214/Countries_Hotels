using Microsoft.AspNetCore.Mvc;
using NoName_Hotel.api.Constants;
using NoName_Hotel.api.Results;

namespace NoName_Hotel.api.Controllers
{
    public abstract class BaseApiController : ControllerBase
    {

        protected ActionResult<T> ToActionResult<T>(Result<T> result)
        => result.IsSuccess ? Ok(result.Value) : MapErrorsToResponse(result.Errors);

        protected ActionResult ToActionResult(Result result)
        => result.IsSuccess ? NoContent() : MapErrorsToResponse(result.Errors);

        protected ActionResult MapErrorsToResponse(Error[] errors)
        {
            if (errors is null || errors.Length == 0)
                return Problem();

            var e = errors[0];
            return e.Code switch
            {
                ErrorCodes.NotFound => NotFound(e.Description),
                ErrorCodes.BadRequest => BadRequest(e.Description),
                ErrorCodes.Validation => BadRequest(e.Description),
                ErrorCodes.Conflict => Conflict(e.Description),
                _ => Problem(detail: string.Join("; ", errors.Select(x => x.Description)), title: e.Code)
            };
        }
    }
}
