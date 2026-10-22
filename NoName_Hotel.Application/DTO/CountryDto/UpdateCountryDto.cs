using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.Application.DTO.CountryDto
{
    public class UpdateCountryDto : CreateCountryDto
    {
        [Required]
        public int Id { get; set; }
    }
}
