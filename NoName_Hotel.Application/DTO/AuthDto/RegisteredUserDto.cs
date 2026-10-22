namespace NoName_Hotel.Application.DTO.AuthDto
{
    public class RegisteredUserDto
    {
        public string Id { get; set; } = string.Empty;

        public string Email {  get; set; } = string.Empty;
        public string Firstname {  get; set; } = string.Empty;
        public string Lastname { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

    }
}
