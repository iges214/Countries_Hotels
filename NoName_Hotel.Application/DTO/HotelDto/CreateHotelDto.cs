using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.Application.DTO.HotelDto
{
    public class CreateHotelDto
    {
        [Required]
        public string Name { get; set; }

        [MaxLength(150)]
        public required string Address { get; set; }

        [Range(1,5)]
        public double Rating { get; set; }

        [Required]
        public int CountryId { get; set; }
    }
}

