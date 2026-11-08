using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.Application.DTO.AuthDto;

public record AccountDto(string FirstName, string LastName, string Email);
public record UpdateAccountDto([Required, MaxLength(100)] string FirstName, [Required, MaxLength(100)] string LastName);
public record ChangePasswordDto([Required] string CurrentPassword, [Required] string NewPassword);
