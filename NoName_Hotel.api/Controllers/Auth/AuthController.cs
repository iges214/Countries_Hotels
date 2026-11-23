using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Application.DTO.AuthDto;
using NoName_Hotel.Application.Services.AuthServices;
using System.Security.Claims;

namespace NoName_Hotel.api.Controllers.Auth
{
    [Route("api/v1/auth")]
    [ApiController]
    public class AuthController(IUserServices userServices, BrowserSessionService sessions) : BaseApiController
    {
        private const string RefreshCookie = "iges.refresh";

        [AllowAnonymous, EnableRateLimiting("auth")]
        [HttpPost("register")]
        public async Task<ActionResult<RegisteredUserDto>> Register(RegisterUserDto registerUserDto)
        {
            var result = await userServices.RegisterAsync(registerUserDto);
            return ToActionResult(result);
        }

        [AllowAnonymous, EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<ActionResult<string>> Login(LoginUserDto loginUserDto)
        {
            var result = await userServices.LoginUserAsync(loginUserDto);
            if (!result.IsSuccess) return MapErrorsToResponse(result.Errors);
            return SessionResponse(result.Value!);
        }

        [AllowAnonymous, EnableRateLimiting("auth")]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto, CancellationToken cancellationToken) =>
            ToActionResult(await userServices.ForgotPasswordAsync(dto, cancellationToken));

        [AllowAnonymous, EnableRateLimiting("auth")]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto) =>
            ToActionResult(await userServices.ResetPasswordAsync(dto));

        [AllowAnonymous]
        [HttpPost("session/{sessionId:guid}/refresh")]
        public async Task<ActionResult<string>> Refresh(Guid sessionId, RefreshRequest request)
        {
            if (!IsSessionRequest()) return StatusCode(StatusCodes.Status403Forbidden);

            var tokens = await sessions.RefreshAsync(sessionId, Request.Cookies[RefreshCookie], request.Activity);
            if (tokens is not null) return SessionResponse(tokens);
            Response.Cookies.Delete(RefreshCookie, CookieOptionsFor(sessionId));
            return Unauthorized();
        }

        [AllowAnonymous]
        [HttpPost("session/{sessionId:guid}/logout")]
        public async Task<IActionResult> Logout(Guid sessionId)
        {
            if (!IsSessionRequest()) return StatusCode(StatusCodes.Status403Forbidden);

            var owner = Guid.TryParse(User.FindFirstValue(BrowserSessionService.SessionIdClaim), out var claimedId)
                && claimedId == sessionId ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            await sessions.RevokeAsync(sessionId, Request.Cookies[RefreshCookie], owner);
            Response.Cookies.Delete(RefreshCookie, CookieOptionsFor(sessionId));
            return NoContent();
        }

        private ActionResult<string> SessionResponse(BrowserSessionTokens tokens)
        {
            Response.Headers.CacheControl = "no-store";
            var cookie = CookieOptionsFor(tokens.SessionId);
            cookie.Expires = new DateTimeOffset(DateTime.SpecifyKind(tokens.ExpiresAt, DateTimeKind.Utc));
            Response.Cookies.Append(RefreshCookie, tokens.RefreshToken, cookie);
            return Ok(tokens.AccessToken);
        }

        private bool IsSessionRequest() => Request.Headers["X-IGES-Session"] == "1"
            && Request.Headers["Sec-Fetch-Site"].ToString() is "" or "same-origin";

        private static CookieOptions CookieOptionsFor(Guid sessionId) => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = $"/api/v1/auth/session/{sessionId:D}"
        };

        public sealed record RefreshRequest(bool Activity);
    }
}
