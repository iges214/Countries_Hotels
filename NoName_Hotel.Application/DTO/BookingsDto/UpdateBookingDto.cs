using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.Application.DTO.BookingsDto
{
    public record UpdateBookingDto(
        DateOnly CheckIn,
        DateOnly CheckOut,
        [Range(1, 10)] int Guests,
        [Range(1, 100)] int Rooms = 1

    ) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (CheckIn < DateOnly.FromDateTime(DateTime.UtcNow))
                yield return new ValidationResult("Check-in cannot be in the past.", [nameof(CheckIn)]);
            if (CheckOut <= CheckIn)
            {
                yield return new ValidationResult(
                    "Check-out must be after check-in.",
                    [nameof(CheckOut), nameof(CheckIn)]);
            }
            if (CheckOut.DayNumber - CheckIn.DayNumber > 365)
                yield return new ValidationResult("A booking cannot exceed 365 nights.", [nameof(CheckOut)]);
        }
    }
}
