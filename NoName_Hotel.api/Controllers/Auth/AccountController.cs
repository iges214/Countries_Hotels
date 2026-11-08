using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Application.DTO.AuthDto;
using NoName_Hotel.Application.DTO.BookingsDto;
using NoName_Hotel.Common.QuerableXtensions.Paging;

namespace NoName_Hotel.api.Controllers.Auth;

[ApiController, Authorize, Route("api/v1/account")]
public sealed class AccountController(IUserServices users, IBookingService bookings) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<AccountDto>> GetAccount()
    {
        Response.Headers.CacheControl = "no-store";
        return ToActionResult(await users.GetAccountAsync());
    }

    [HttpPut]
    public async Task<IActionResult> UpdateAccount(UpdateAccountDto dto) =>
        ToActionResult(await users.UpdateAccountAsync(dto));

    [HttpPost("password"), EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto) =>
        ToActionResult(await users.ChangePasswordAsync(dto));

    [HttpGet("bookings"), EnableRateLimiting("perUser")]
    public async Task<ActionResult<PageResult<GetBookingDto>>> GetBookings([FromQuery] PaginationParameters page)
    {
        Response.Headers.CacheControl = "no-store";
        return ToActionResult(await bookings.GetUserBookingsAsync(page));
    }
}
