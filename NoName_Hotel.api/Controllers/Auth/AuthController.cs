using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Application.DTO.AuthDto;

namespace NoName_Hotel.api.Controllers.Auth
{
    [Route("api/[controller]")]
    [Route("api/v1/auth")]
    [EnableRateLimiting("auth")]
    [ApiController]

    public class AuthController(IUserServices userServices) : BaseApiController
    {
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<RegisteredUserDto>> Register(RegisterUserDto registerUserDto)
        {
            var result = await userServices.RegisterAsync(registerUserDto);
            return ToActionResult(result);
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<string>> Login(LoginUserDto loginUserDto)
        {
            var result = await userServices.LoginUserAsync(loginUserDto);
            return ToActionResult(result);
        }
    }
}
