using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.api.DTO.AuthDto
{
    public class LoginUserDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
