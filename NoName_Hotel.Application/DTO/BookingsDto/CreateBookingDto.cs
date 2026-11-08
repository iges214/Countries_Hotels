using System.ComponentModel.DataAnnotations;

namespace NoName_Hotel.Application.DTO.BookingsDto;

    public record CreateBookingDto(
     [Range(1, int.MaxValue)] int HotelId,
     DateOnly CheckIn,
     DateOnly CheckOut,
     [Range(1, 10)] int Guests,
     [Range(1, 100)] int Rooms = 1,
        int[]? RoomIds = null) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {

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
