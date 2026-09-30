using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.api.DTO.CountryDto
{
    public class UpdateCountryDto : CreateCountryDto
    {
        [Required]
        public int Id { get; set; }
    }
}
