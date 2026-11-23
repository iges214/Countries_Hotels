using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.Application.DTO.BookingsDto;

    public record CreateBookingDto(
     [Range(1, int.MaxValue)] int HotelId,
     DateOnly CheckIn,
     DateOnly CheckOut,
     [Range(1, 10)] int Guests,
     [Range(1, 100)] int Rooms = 1,
        int[]? RoomIds = null,
        [Required] Guid? RequestId = null,
        [Range(typeof(decimal), "0.01", "9999999999999999.99")] decimal? ExpectedTotal = null) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!RequestId.HasValue || RequestId == Guid.Empty)
                yield return new ValidationResult("Supply a nonempty booking request identifier.", [nameof(RequestId)]);
            if (ExpectedTotal.HasValue && decimal.Round(ExpectedTotal.Value, 2) != ExpectedTotal.Value)
                yield return new ValidationResult("Expected total must have at most two decimal places.", [nameof(ExpectedTotal)]);

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
