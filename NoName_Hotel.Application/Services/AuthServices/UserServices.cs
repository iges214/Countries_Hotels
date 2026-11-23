using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using NoName_Hotel.Application.Contracts;
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
    IHttpContextAccessor httpContext, ILogger<UserServices> logger, IHotelMailSender mail,
    IConfiguration configuration) : IUserServices
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

    public async Task<Result> ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken cancellationToken = default)
    {
        // Always return success so callers cannot use this endpoint to enumerate accounts.
        var user = await userManager.FindByEmailAsync(dto.Email.Trim());
        if (user is null || string.IsNullOrWhiteSpace(user.Email)) return Result.Success();

        var frontendUrl = configuration["PasswordReset:FrontendUrl"]?.Trim().TrimEnd('/');
        if (!Uri.TryCreate(frontendUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("https" or "http") || !mail.IsConfigured)
        {
            logger.LogWarning("Password reset email could not be sent because frontend URL or mail delivery is not configured.");
            return Result.Success();
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = new UriBuilder(baseUri)
        {
            Path = baseUri.AbsolutePath.TrimEnd('/') + "/reset-password",
            Query = $"email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}",
            Fragment = ""
        }.Uri.AbsoluteUri;
        try
        {
            await mail.SendAsync(user.Email, "Reset your IGES Hotel password",
                $"Use this link to choose a new password. The link expires and can only be used once:\n\n{resetUrl}\n\nIf you did not request this, you can ignore this email.",
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Password reset email delivery failed for user {UserId}.", user.Id);
        }
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordDto dto)
    {
        var invalidReset = new Error(ErrorCodes.Validation,
            "The reset link is invalid or has expired, or the new password does not meet the password requirements.");
        var user = await userManager.FindByEmailAsync(dto.Email.Trim());
        if (user is null) return Result.BadRequest(invalidReset);

        var result = await userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
        if (!result.Succeeded)
            return Result.BadRequest(invalidReset);

        // Identity persists the new password and security stamp together. That stamp
        // invalidates issued reset tokens, access tokens and refresh sessions.
        return Result.Success();
    }

    private static Result AccountChangeResult(IdentityResult result) =>
        result.Succeeded
            ? Result.Success()
            : Result.BadRequest(result.Errors.Select(error => new Error(ErrorCodes.Validation, error.Description)).ToArray());

}
