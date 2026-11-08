using System.ComponentModel.DataAnnotations.Schema;

namespace NoName_Hotel.Domain.Entities.Models;

public enum RoomCategory { Normal = 1, Vip = 2, Vvip = 3, Platinum = 4 }
public enum RoomPhotoKind { Room = 1, Bathroom = 2 }

public static class RoomNumbering
{
    public static bool IsTwinRoom(int number) => number is >= 160 and <= 200;
    public static bool CanBeHoneymoonSuite(int number) =>
        CategoryFor(number) is RoomCategory.Vvip or RoomCategory.Platinum;

    public static RoomCategory? CategoryFor(int number) => number switch
    {
        >= 100 and <= 200 => RoomCategory.Normal,
        >= 210 and <= 300 => RoomCategory.Vip,
        >= 350 and <= 400 => RoomCategory.Vvip,
        >= 480 and <= 500 => RoomCategory.Platinum,
        _ => null
    };
}

public class Room
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    public int Number { get; set; }
    [NotMapped] public RoomCategory Category => RoomNumbering.CategoryFor(Number)!.Value;
    [NotMapped] public int Floor => (int)Category;
    [NotMapped] public bool IsTwinRoom => RoomNumbering.IsTwinRoom(Number);
    public bool IsHoneymoonSuite { get; set; }
    public string Description { get; set; } = string.Empty;
    public string BedType { get; set; } = string.Empty;
    public string Amenities { get; set; } = string.Empty;
    public int MaxGuests { get; set; }
    public decimal SizeSquareMetres { get; set; }
    public decimal PerNightRate { get; set; }
    public bool IsPublished { get; set; }
    public bool IsOperational { get; set; } = true;
    public ICollection<RoomPhoto> Photos { get; set; } = [];
    public ICollection<BookingRoom> Reservations { get; set; } = [];
}

public class RoomPhoto
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public RoomPhotoKind Kind { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
}

public class BookingRoom
{
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public decimal NightlyRate { get; set; }
    public int? Guests { get; set; }
}
