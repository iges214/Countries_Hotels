using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.Application.DTO.BookingsDto
{
    public record UpdateBookingDto(
        DateOnly CheckIn,
        DateOnly CheckOut,
        [Range(1, 10)] int Guests,
        [Range(1, 100)] int Rooms = 1,
        int[]? RoomIds = null,
        [Required, MinLength(8), MaxLength(8)] byte[]? RowVersion = null

    ) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (RowVersion is null || RowVersion.Length != 8)
                yield return new ValidationResult("Reload the reservation and supply its current version before saving.", [nameof(RowVersion)]);

            if (RoomIds is null || RoomIds.Length != Rooms || RoomIds.Distinct().Count() != Rooms || RoomIds.Any(id => id <= 0))
                yield return new ValidationResult("Select a distinct room ID for each requested room.", [nameof(RoomIds)]);
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
