using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Application.DTO.AuthDto;
using NoName_Hotel.Application.Validation;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.JWT;
using NoName_Hotel.Common.Results;
using NoName_Hotel.Domain.Entities.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
namespace NoName_Hotel.Application.Services.AuthServices;
public class UserServices(UserManager<ApplicationUser> userManager, IOptions<JwtSettings> jwtOptions,
    IHttpContextAccessor httpContext, ILogger<UserServices> logger) : IUserServices
{
    public async Task<Result<RegisteredUserDto>> RegisterAsync(RegisterUserDto dto)
    {
        var errors = DtoValidation.Errors(dto);
        if (errors.Length > 0) return Result<RegisteredUserDto>.BadRequest(errors);
        if (dto.Role != RoleNames.User || dto.AssociatedHotelId.HasValue)
            return Result<RegisteredUserDto>.BadRequest(new Error(ErrorCodes.Validation,
                "Public registration only creates ordinary user accounts."));
        var user = new ApplicationUser
        {
            Email = dto.Email.Trim(), UserName = dto.Email.Trim(),
            FirstName = dto.FirstName.Trim(), LastName = dto.LastName.Trim()
        };
        var created = await userManager.CreateAsync(user, dto.Password);
        if (!created.Succeeded) return RegistrationErrors(created);
        var assigned = await userManager.AddToRoleAsync(user, RoleNames.User);
        if (!assigned.Succeeded)
        {
            var cleanup = await userManager.DeleteAsync(user);
            if (!cleanup.Succeeded) throw new InvalidOperationException("Failed to roll back incomplete registration.");
            return RegistrationErrors(assigned);
        }
        logger.LogInformation("Registered user {UserId}", user.Id);
        return Result<RegisteredUserDto>.Success(new RegisteredUserDto
        {
            Email = user.Email!, Firstname = user.FirstName, Lastname = user.LastName,
            Id = user.Id, Role = RoleNames.User
        });
    }
    private static Result<RegisteredUserDto> RegistrationErrors(IdentityResult result) =>
        Result<RegisteredUserDto>.BadRequest(result.Errors.Select(e =>
            new Error(ErrorCodes.Validation, e.Description)).ToArray());
    public async Task<Result<string>> LoginUserAsync(LoginUserDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user == null || await userManager.IsLockedOutAsync(user)) return InvalidCredentials();
        if (!await userManager.CheckPasswordAsync(user, dto.Password))
        {
            await userManager.AccessFailedAsync(user);
            return InvalidCredentials();
        }
        await userManager.ResetAccessFailedCountAsync(user);
        return Result<string>.Success(await GenerateToken(user));
    }
    private static Result<string> InvalidCredentials() =>
        Result<string>.Failure(new Error(ErrorCodes.BadRequest, "Invalid credentials."));
    public string UserId => httpContext.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? httpContext.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;
    private async Task<string> GenerateToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Name, user.FullName)
        };
        claims.AddRange((await userManager.GetRolesAsync(user)).Select(r => new Claim(ClaimTypes.Role, r)));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.Value.key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwtOptions.Value.Issuer, jwtOptions.Value.Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(jwtOptions.Value.DurationInMinutes)),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
