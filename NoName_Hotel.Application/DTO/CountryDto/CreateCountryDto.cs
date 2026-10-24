using System.ComponentModel.DataAnnotations;
namespace NoName_Hotel.Application.DTO.CountryDto
{
    public class CreateCountryDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        public required string ShortName {  get; set; }
    }
}
