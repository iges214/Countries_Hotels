using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.api.DTO.AuthDto
{
    public class RegisterUserDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required, MinLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MinLength(100)]
        public string LastName { get; set; } = string.Empty;

        public string Role { get; set; } = "User";

    }
}
