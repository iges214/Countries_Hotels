using System.ComponentModel.DataAnnotations;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Application.DTO.Rooms;

public class SaveRoomDto : IValidatableObject
{
    public int Number { get; set; }
    [Required, MaxLength(2000)] public string Description { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string BedType { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Amenities { get; set; } = string.Empty;
    [Range(1, 10)] public int MaxGuests { get; set; }
    [Range(typeof(decimal), "0.01", "999999.99")] public decimal SizeSquareMetres { get; set; }
    [Range(typeof(decimal), "0.01", "9999999999999999.99")] public decimal PerNightRate { get; set; }
    public bool IsPublished { get; set; }
    public bool IsOperational { get; set; } = true;
    public bool IsHoneymoonSuite { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (RoomNumbering.CategoryFor(Number) is null)
            yield return new ValidationResult("Room number must be 100–200, 210–300, 350–400 or 480–500.", [nameof(Number)]);
        if (RoomNumbering.IsTwinRoom(Number) &&
            (!string.Equals(BedType?.Trim(), "Twin", StringComparison.OrdinalIgnoreCase) || MaxGuests < 2))
            yield return new ValidationResult("Rooms 160–200 have two separate single beds. Set bedType to Twin and maxGuests to at least 2.", [nameof(BedType), nameof(MaxGuests)]);
        if (!RoomNumbering.IsTwinRoom(Number) && string.Equals(BedType?.Trim(), "Twin", StringComparison.OrdinalIgnoreCase))
            yield return new ValidationResult("Twin-bed rooms must be numbered 160–200 on the first floor.", [nameof(Number), nameof(BedType)]);
        if (IsHoneymoonSuite && (!RoomNumbering.CanBeHoneymoonSuite(Number) || MaxGuests < 2))
            yield return new ValidationResult("Honeymoon suites must accommodate at least two guests and be in VVIP (350–400) or Platinum (480–500).", [nameof(IsHoneymoonSuite), nameof(Number), nameof(MaxGuests)]);
    }
}

public record RoomPhotoDto(int Id, string Kind, string Url, string Caption);
public record RoomDto(int Id, int HotelId, int Number, string Category, int Floor,
    string Description, string BedType, string Amenities, int MaxGuests, decimal SizeSquareMetres,
    decimal PerNightRate, string CurrencyCode, bool IsPublished, bool IsOperational, List<RoomPhotoDto> Photos,
    bool IsTwinRoom, bool IsHoneymoonSuite);

public class RoomSearch : IValidatableObject
{
    public DateOnly? CheckIn { get; set; }
    public DateOnly? CheckOut { get; set; }
    [EnumDataType(typeof(RoomCategory))] public RoomCategory? Category { get; set; }
    [Range(1, 10)] public int Guests { get; set; } = 1;
    public bool? IsTwinRoom { get; set; }
    public bool? IsHoneymoonSuite { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (CheckIn.HasValue != CheckOut.HasValue)
        {
            yield return new ValidationResult("Supply both checkIn and checkOut.");
            yield break;
        }
        if (CheckIn.HasValue && (CheckOut <= CheckIn || CheckOut.GetValueOrDefault().DayNumber - CheckIn.Value.DayNumber > 365))
            yield return new ValidationResult("Choose a future stay of 1–365 nights.");
    }
}
