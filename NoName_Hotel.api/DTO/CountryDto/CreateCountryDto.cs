using System.ComponentModel.DataAnnotations;
namespace NoName_Hotel.api.DTO.CountryDto
{
    public class CreateCountryDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(10)]
        public required string ShortName {  get; set; }
    }
}
