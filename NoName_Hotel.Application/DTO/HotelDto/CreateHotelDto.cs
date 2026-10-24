using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.Application.DTO.HotelDto
{
    public class CreateHotelDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        public required string Address { get; set; }

        [Range(1,5)]
        public double Rating { get; set; }

        [Range(1, int.MaxValue)]
        public int CountryId { get; set; }

        [Range(typeof(decimal), "0.01", "9999999999999999.99")]
        public decimal PerNightRate { get; set; }

        [Range(1, 100000)]
        public int TotalRooms { get; set; }
    }
}

