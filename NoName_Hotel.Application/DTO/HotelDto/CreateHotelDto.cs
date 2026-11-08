using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.Application.DTO.HotelDto
{
    public class CreateHotelDto : IValidatableObject
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        public required string Address { get; set; }

        [Range(1,5)]
        public double Rating { get; set; } = 4.5;

        [MaxLength(100)] public string City { get; set; } = string.Empty;
        [MaxLength(100)] public string State { get; set; } = string.Empty;
        [Required, RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "NGN";
        [Required, MaxLength(100)] public string TimeZoneId { get; set; } = "Africa/Lagos";

        [Range(1, int.MaxValue)]
        public int CountryId { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999.99")]
        public decimal PerNightRate { get; set; }

        [Range(0, 100000)]
        public int TotalRooms { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            TimeZoneInfo? zone = null;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
            if (zone is null) yield return new ValidationResult("Use a valid time zone such as Africa/Lagos.", [nameof(TimeZoneId)]);
        }
    }
}

