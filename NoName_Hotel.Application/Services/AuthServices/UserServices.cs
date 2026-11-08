using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Application.DTO.AuthDto;
using NoName_Hotel.Application.Validation;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.Results;
using NoName_Hotel.Domain.Entities.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
namespace NoName_Hotel.Application.Services.AuthServices;
public class UserServices(UserManager<ApplicationUser> userManager, BrowserSessionService sessions,
    IHttpContextAccessor httpContext, ILogger<UserServices> logger) : IUserServices
{
    public async Task<Result<RegisteredUserDto>> RegisterAsync(RegisterUserDto dto)
    {
        var errors = DtoValidation.Errors(dto);

        if (errors.Length > 0) return Result<RegisteredUserDto>.BadRequest(errors);

        var user = new ApplicationUser
        {
            Email = dto.Email.Trim(), 
            UserName = dto.Email.Trim(),
            FirstName = dto.FirstName.Trim(), 
            LastName = dto.LastName.Trim()
        };


        var created = await userManager.CreateAsync(user, dto.Password);

        if (!created.Succeeded)
        {
            logger.LogError("user registeration failed for {Email}: {Errors}", dto.Email, string.Join(", ", errors));
            return RegistrationErrors(created);
        }

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

    public async Task<Result<BrowserSessionTokens>> LoginUserAsync(LoginUserDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user == null || await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Failed Login attempt for email:{Email}", dto.Email);
            return InvalidCredentials();
        }


        if (!await userManager.CheckPasswordAsync(user, dto.Password))
        {
            await userManager.AccessFailedAsync(user);
            return InvalidCredentials();
        }


        await userManager.ResetAccessFailedCountAsync(user);
        return Result<BrowserSessionTokens>.Success(await sessions.StartAsync(user));
    }

    private static Result<BrowserSessionTokens> InvalidCredentials() =>
        Result<BrowserSessionTokens>.Failure(new Error(ErrorCodes.BadRequest, "Invalid credentials."));

    public string UserId => httpContext.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? httpContext.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;

    private static readonly Error AccountNotFound = new(ErrorCodes.NotFound, "Account not found.");

    public async Task<Result<AccountDto>> GetAccountAsync()
    {
        var user = await userManager.FindByIdAsync(UserId);
        return user is null
            ? Result<AccountDto>.Failure(AccountNotFound)
            : Result<AccountDto>.Success(new(user.FirstName, user.LastName, user.Email ?? ""));
    }

    public async Task<Result> UpdateAccountAsync(UpdateAccountDto dto)
    {
        var user = await userManager.FindByIdAsync(UserId);
        if (user is null) return Result.NotFound(AccountNotFound);
        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        return AccountChangeResult(await userManager.UpdateAsync(user));
    }

    public async Task<Result> ChangePasswordAsync(ChangePasswordDto dto)
    {
        var user = await userManager.FindByIdAsync(UserId);
        if (user is null) return Result.NotFound(AccountNotFound);
        return AccountChangeResult(await userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword));
    }

    private static Result AccountChangeResult(IdentityResult result) =>
        result.Succeeded
            ? Result.Success()
            : Result.BadRequest(result.Errors.Select(error => new Error(ErrorCodes.Validation, error.Description)).ToArray());

}
