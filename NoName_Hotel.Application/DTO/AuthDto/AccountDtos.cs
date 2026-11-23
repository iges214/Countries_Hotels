using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.Application.DTO.AuthDto;

public record AccountDto(string FirstName, string LastName, string Email);
public record UpdateAccountDto([Required, MaxLength(100)] string FirstName, [Required, MaxLength(100)] string LastName);
public record ChangePasswordDto([Required] string CurrentPassword, [Required] string NewPassword);
public record ForgotPasswordDto([Required, EmailAddress] string Email);
public record ResetPasswordDto([Required, EmailAddress] string Email, [Required] string Token, [Required] string NewPassword);
